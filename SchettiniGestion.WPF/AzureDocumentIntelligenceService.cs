using System;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;

namespace SchettiniGestion.WPF
{
    /// <summary>
    /// OCR en la nube usando el modelo "prebuilt-invoice" de Azure Document Intelligence.
    /// Cada cliente usa su propio Endpoint y Clave (gratis hasta 500 páginas/mes); SCHPOS no
    /// intermedia ni ve la clave de otros clientes.
    /// </summary>
    public static class AzureDocumentIntelligenceService
    {
        private const string ApiVersion = "2024-11-30";
        private static readonly Regex RxCuit = new Regex(@"\b(\d{2}[-.\s]?\d{8}[-.\s]?\d)\b", RegexOptions.Compiled);

        public static FacturaCompraPdfParseResult AnalizarFactura(string rutaImagen, string endpoint, string clave)
        {
            return AnalizarFacturaAsync(rutaImagen, endpoint, clave).GetAwaiter().GetResult();
        }

        private static async Task<FacturaCompraPdfParseResult> AnalizarFacturaAsync(string rutaImagen, string endpoint, string clave)
        {
            if (string.IsNullOrWhiteSpace(endpoint) || string.IsNullOrWhiteSpace(clave))
                throw new InvalidOperationException("Falta configurar el Endpoint y la Clave de Azure en Configuración > Facturas de Compra.");

            string ext = (Path.GetExtension(rutaImagen) ?? "").ToLowerInvariant();
            string contentType = ext == ".png" ? "image/png"
                : ext == ".bmp" ? "image/bmp"
                : (ext == ".tif" || ext == ".tiff") ? "image/tiff"
                : ext == ".pdf" ? "application/pdf"
                : "image/jpeg";

            byte[] bytes = File.ReadAllBytes(rutaImagen);
            string baseUrl = endpoint.Trim().TrimEnd('/');
            string url = $"{baseUrl}/documentintelligence/documentModels/prebuilt-invoice:analyze?api-version={ApiVersion}";

            using (var client = new HttpClient { Timeout = TimeSpan.FromSeconds(60) })
            {
                client.DefaultRequestHeaders.Add("Ocp-Apim-Subscription-Key", clave.Trim());
                var content = new ByteArrayContent(bytes);
                content.Headers.ContentType = new MediaTypeHeaderValue(contentType);

                HttpResponseMessage resp;
                try { resp = await client.PostAsync(url, content); }
                catch (Exception ex) { throw new InvalidOperationException("No se pudo conectar con Azure. Verifique el Endpoint y su conexión a internet.\n" + ex.Message); }

                if (resp.StatusCode != System.Net.HttpStatusCode.Accepted)
                {
                    string err = await resp.Content.ReadAsStringAsync();
                    throw new InvalidOperationException($"Azure rechazó la solicitud ({(int)resp.StatusCode}): {ResumirError(err)}");
                }

                string opUrl = resp.Headers.TryGetValues("Operation-Location", out var locs) ? locs.FirstOrDefault() : null;
                if (string.IsNullOrEmpty(opUrl))
                    throw new InvalidOperationException("Azure no devolvió la ubicación de la operación de análisis.");

                JObject resultado = null;
                for (int intento = 0; intento < 30; intento++)
                {
                    await Task.Delay(1500);
                    var pollResp = await client.GetAsync(opUrl);
                    string body = await pollResp.Content.ReadAsStringAsync();
                    if (!pollResp.IsSuccessStatusCode)
                        throw new InvalidOperationException($"Error consultando el resultado en Azure ({(int)pollResp.StatusCode}): {ResumirError(body)}");

                    var json = JObject.Parse(body);
                    string status = json.Value<string>("status");
                    if (string.Equals(status, "succeeded", StringComparison.OrdinalIgnoreCase)) { resultado = json; break; }
                    if (string.Equals(status, "failed", StringComparison.OrdinalIgnoreCase))
                        throw new InvalidOperationException("Azure no pudo procesar la imagen: " + ResumirError(body));
                }

                if (resultado == null)
                    throw new InvalidOperationException("Azure tardó demasiado en responder. Intente nuevamente.");

                return ParsearResultado(resultado);
            }
        }

        private static string ResumirError(string body)
        {
            try
            {
                var j = JObject.Parse(body);
                string msg = j.SelectToken("error.message")?.ToString() ?? j.SelectToken("error.innererror.message")?.ToString();
                return string.IsNullOrWhiteSpace(msg) ? body : msg;
            }
            catch { return body; }
        }

        private static FacturaCompraPdfParseResult ParsearResultado(JObject json)
        {
            var result = new FacturaCompraPdfParseResult { TipoComprobante = "Factura A" };
            result.TextoCompleto = json.SelectToken("analyzeResult.content")?.ToString() ?? "";

            var doc = json.SelectToken("analyzeResult.documents[0]");
            if (doc == null)
            {
                result.MensajeAdvertencia = "Azure no detectó una factura en la imagen. Pruebe con mejor luz/encuadre o cargue los datos a mano.";
                return result;
            }

            var fields = doc["fields"];
            string vendorName = TxtField(fields, "VendorName");
            string vendorTaxId = TxtField(fields, "VendorTaxId");
            string invoiceId = TxtField(fields, "InvoiceId");

            result.RazonSocialEmisor = vendorName ?? "";
            result.NumeroComprobante = invoiceId ?? "";
            result.CuitEmisor = BuscarCuitEnTexto(vendorTaxId);
            if (string.IsNullOrEmpty(result.CuitEmisor))
                result.CuitEmisor = BuscarCuitEnTexto(result.TextoCompleto);

            var mTipo = Regex.Match(result.TextoCompleto, @"FACTURA\s*([ABC])\b", RegexOptions.IgnoreCase);
            if (mTipo.Success)
                result.TipoComprobante = "Factura " + mTipo.Groups[1].Value.ToUpperInvariant();

            var items = fields?["Items"]?["valueArray"] as JArray;
            if (items != null)
            {
                foreach (var it in items)
                {
                    var obj = it["valueObject"];
                    if (obj == null) continue;

                    string desc = TxtField(obj, "Description");
                    if (string.IsNullOrWhiteSpace(desc)) continue;

                    string codigo = TxtField(obj, "ProductCode");
                    decimal cantidad = NumField(obj, "Quantity") ?? 1m;
                    decimal costoUnit = CurrencyField(obj, "UnitPrice") ?? 0m;
                    decimal importe = CurrencyField(obj, "Amount") ?? 0m;
                    if (cantidad <= 0) cantidad = 1m;
                    if (costoUnit <= 0 && importe > 0) costoUnit = Math.Round(importe / cantidad, 2);

                    result.Lineas.Add(new FacturaCompraPdfLinea
                    {
                        CodigoProveedor = codigo ?? "",
                        DescripcionPdf = desc.Trim(),
                        Cantidad = (int)Math.Max(1, Math.Round(cantidad, MidpointRounding.AwayFromZero)),
                        CostoUnitario = costoUnit,
                        Subtotal = importe > 0 ? importe : Math.Round(cantidad * costoUnit, 2)
                    });
                }
            }

            if (result.Lineas.Count == 0)
                result.MensajeAdvertencia = "Azure detectó la factura pero no pudo identificar ítems individuales. Revise el archivo o cargue la factura a mano.";

            return result;
        }

        private static string TxtField(JToken fields, string name)
        {
            var f = fields?[name];
            if (f == null) return null;
            return f.Value<string>("valueString") ?? f.Value<string>("content");
        }

        private static decimal? NumField(JToken fields, string name)
        {
            var v = fields?[name]?["valueNumber"];
            return v != null ? (decimal?)v.Value<decimal>() : null;
        }

        private static decimal? CurrencyField(JToken fields, string name)
        {
            var f = fields?[name];
            if (f == null) return null;
            var cur = f["valueCurrency"];
            if (cur != null && cur["amount"] != null) return cur.Value<decimal>("amount");
            var num = f["valueNumber"];
            return num != null ? (decimal?)num.Value<decimal>() : null;
        }

        private static string BuscarCuitEnTexto(string texto)
        {
            if (string.IsNullOrWhiteSpace(texto)) return "";
            var m = RxCuit.Match(texto);
            if (!m.Success) return "";
            string d = Regex.Replace(m.Groups[1].Value, @"[^\d]", "");
            if (d.Length != 11) return "";
            return $"{d.Substring(0, 2)}-{d.Substring(2, 8)}-{d.Substring(10, 1)}";
        }
    }
}
