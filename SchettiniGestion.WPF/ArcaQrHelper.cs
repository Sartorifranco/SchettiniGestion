using Newtonsoft.Json;
using QRCoder;
using SchettiniGestion;
using System;
using System.Data;
using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using WinDrawing = System.Drawing;

namespace SchettiniGestion.WPF
{
    /// <summary>
    /// Genera el QR obligatorio de comprobantes electrónicos según especificación ARCA
    /// (https://www.arca.gob.ar/fe/qr/?p={JSON Base64}).
    /// </summary>
    public static class ArcaQrHelper
    {
        public const string UrlBaseQr = "https://www.arca.gob.ar/fe/qr/";

        /// <summary>
        /// Construye la URL del QR fiscal. Devuelve null si faltan datos mínimos (CUIT emisor o CAE).
        /// </summary>
        public static string ConstruirUrl(
            DateTime fecha,
            string cuitEmisor,
            int puntoVenta,
            int tipoComprobanteAfip,
            int numeroComprobante,
            decimal importeTotal,
            string cae,
            string cuitReceptor = null,
            string moneda = "PES",
            decimal cotizacion = 1m)
        {
            long cuit = SoloDigitosLong(cuitEmisor);
            long codAut = SoloDigitosLong(cae);
            if (cuit <= 0 || codAut <= 0 || puntoVenta <= 0 || numeroComprobante <= 0 || tipoComprobanteAfip <= 0)
                return null;

            long nroDocRec = SoloDigitosLong(cuitReceptor);
            int tipoDocRec = nroDocRec >= 10000000 ? 80 : 99; // 80=CUIT, 99=Consumidor Final
            if (tipoDocRec == 99) nroDocRec = 0;

            var payload = new
            {
                ver = 1,
                fecha = fecha.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
                cuit = cuit,
                ptoVta = puntoVenta,
                tipoCmp = tipoComprobanteAfip,
                nroCmp = numeroComprobante,
                importe = Math.Round(importeTotal, 2),
                moneda = string.IsNullOrWhiteSpace(moneda) ? "PES" : moneda.Trim().ToUpperInvariant(),
                ctz = cotizacion <= 0 ? 1m : cotizacion,
                tipoDocRec = tipoDocRec,
                nroDocRec = nroDocRec,
                tipoCodAut = "E",
                codAut = codAut
            };

            // Base64 crudo sin URL-encode (requisito del lector oficial ARCA).
            string json = JsonConvert.SerializeObject(payload);
            string b64 = Convert.ToBase64String(Encoding.UTF8.GetBytes(json));
            return UrlBaseQr + "?p=" + b64;
        }

        /// <summary>Construye la URL a partir de una factura guardada y la configuración del negocio.</summary>
        public static string ConstruirUrlDesdeFactura(DataRow cab, string letra = null)
        {
            if (cab == null) return null;
            string cae = cab.Table.Columns.Contains("CAE") ? cab["CAE"]?.ToString()?.Trim() ?? "" : "";
            if (string.IsNullOrWhiteSpace(cae)) return null;

            DataRow conf = DatabaseService.GetConfiguracion();
            string cuitEmisor = conf?["CUIT"]?.ToString() ?? "";
            string condicionIva = conf != null && conf.Table.Columns.Contains("CondicionIVAEmpresa")
                ? conf["CondicionIVAEmpresa"]?.ToString() ?? "" : "";
            int.TryParse(conf?["PuntoVenta"]?.ToString()?.Trim(), out int ptoVta);

            string tipo = cab.Table.Columns.Contains("TipoComprobante") ? cab["TipoComprobante"]?.ToString() ?? "" : "";
            string clienteCuit = cab.Table.Columns.Contains("ClienteCUIT") ? cab["ClienteCUIT"]?.ToString() ?? "" : "";
            DateTime fecha = cab.Table.Columns.Contains("Fecha") && cab["Fecha"] != DBNull.Value
                ? Convert.ToDateTime(cab["Fecha"]) : DateTime.Today;
            decimal total = cab.Table.Columns.Contains("Total") && cab["Total"] != DBNull.Value
                ? Convert.ToDecimal(cab["Total"]) : 0m;

            int nro = 0;
            if (cab.Table.Columns.Contains("NumeroComprobanteAFIP") && cab["NumeroComprobanteAFIP"] != DBNull.Value)
                nro = Convert.ToInt32(cab["NumeroComprobanteAFIP"]);
            if (nro <= 0 && cab.Table.Columns.Contains("FacturaID") && cab["FacturaID"] != DBNull.Value)
                nro = Convert.ToInt32(cab["FacturaID"]);

            if (string.IsNullOrWhiteSpace(letra))
                letra = InferirLetra(tipo, clienteCuit, condicionIva);

            int tipoAfip = ResolverTipoComprobanteAfip(tipo, letra, clienteCuit, condicionIva);
            return ConstruirUrl(fecha, cuitEmisor, ptoVta, tipoAfip, nro, total, cae, clienteCuit);
        }

        public static WinDrawing.Bitmap GenerarBitmap(string urlQr, int pixelsPorModulo = 4)
        {
            if (string.IsNullOrWhiteSpace(urlQr)) return null;
            var gen = new QRCodeGenerator();
            using (QRCodeData data = gen.CreateQrCode(urlQr, QRCodeGenerator.ECCLevel.Q))
            {
                var qr = new QRCode(data);
                return qr.GetGraphic(Math.Max(2, pixelsPorModulo), WinDrawing.Color.Black, WinDrawing.Color.White, drawQuietZones: true);
            }
        }

        public static byte[] GenerarPngBytes(string urlQr, int pixelsPorModulo = 4)
        {
            using (var bmp = GenerarBitmap(urlQr, pixelsPorModulo))
            {
                if (bmp == null) return null;
                using (var ms = new System.IO.MemoryStream())
                {
                    bmp.Save(ms, WinDrawing.Imaging.ImageFormat.Png);
                    return ms.ToArray();
                }
            }
        }

        /// <summary>Mapea letra/tipo de venta al código AFIP/ARCA del comprobante.</summary>
        public static int ResolverTipoComprobanteAfip(string tipoComprobante, string letra, string cuitCliente, string condicionIvaEmisor)
        {
            string tipo = (tipoComprobante ?? "").Trim();
            string let = (letra ?? "").Trim().ToUpperInvariant();
            bool monotributo = !string.IsNullOrWhiteSpace(condicionIvaEmisor)
                && condicionIvaEmisor.IndexOf("Monotribut", StringComparison.OrdinalIgnoreCase) >= 0;

            if (tipo.IndexOf("Nota de Crédito", StringComparison.OrdinalIgnoreCase) >= 0
                || tipo.IndexOf("Nota Credito", StringComparison.OrdinalIgnoreCase) >= 0
                || tipo.Equals("NC", StringComparison.OrdinalIgnoreCase))
            {
                if (let == "A" || (!monotributo && EsCuitValido(cuitCliente))) return 3;
                if (let == "C" || monotributo) return 13;
                return 8;
            }

            if (tipo.IndexOf("Nota de Débito", StringComparison.OrdinalIgnoreCase) >= 0
                || tipo.IndexOf("Nota Debito", StringComparison.OrdinalIgnoreCase) >= 0
                || tipo.Equals("ND", StringComparison.OrdinalIgnoreCase))
            {
                if (let == "A" || (!monotributo && EsCuitValido(cuitCliente))) return 2;
                if (let == "C" || monotributo) return 12;
                return 7;
            }

            if (let == "A") return 1;
            if (let == "C") return 11;
            if (let == "B") return 6;

            if (monotributo) return 11;
            if (EsCuitValido(cuitCliente)) return 1;
            return 6;
        }

        public static string InferirLetra(string tipoComprobante, string cuitCliente, string condicionIvaEmisor)
        {
            string tipo = tipoComprobante ?? "";
            if (tipo.IndexOf("Ticket", StringComparison.OrdinalIgnoreCase) >= 0) return "X";
            bool monotributo = !string.IsNullOrWhiteSpace(condicionIvaEmisor)
                && condicionIvaEmisor.IndexOf("Monotribut", StringComparison.OrdinalIgnoreCase) >= 0;
            if (monotributo) return "C";
            if (EsCuitValido(cuitCliente)) return "A";
            return "B";
        }

        public static long SoloDigitosLong(string valor)
        {
            if (string.IsNullOrWhiteSpace(valor)) return 0;
            string digits = Regex.Replace(valor, @"\D", "");
            if (string.IsNullOrEmpty(digits)) return 0;
            return long.TryParse(digits, NumberStyles.Integer, CultureInfo.InvariantCulture, out long n) ? n : 0;
        }

        private static bool EsCuitValido(string cuit)
        {
            string d = Regex.Replace(cuit ?? "", @"\D", "");
            return d.Length >= 11 && !d.Contains("00000000");
        }
    }
}
