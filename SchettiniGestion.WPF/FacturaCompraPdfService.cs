using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using SchettiniGestion;
using UglyToad.PdfPig;

namespace SchettiniGestion.WPF
{
    public class FacturaCompraPdfLinea
    {
        public string CodigoProveedor { get; set; } = "";
        public string DescripcionPdf { get; set; } = "";
        public int Cantidad { get; set; } = 1;
        public decimal CostoUnitario { get; set; }
        public decimal Subtotal { get; set; }
    }

    public class FacturaCompraPdfParseResult
    {
        public string TextoCompleto { get; set; } = "";
        public string CuitEmisor { get; set; } = "";
        public string RazonSocialEmisor { get; set; } = "";
        public string TipoComprobante { get; set; } = "Factura A";
        public string NumeroComprobante { get; set; } = "";
        public List<FacturaCompraPdfLinea> Lineas { get; set; } = new List<FacturaCompraPdfLinea>();
        public string MensajeAdvertencia { get; set; }
    }

    public class FacturaCompraPdfMatchLinea
    {
        public FacturaCompraPdfLinea LineaPdf { get; set; }
        public int ProductoID { get; set; }
        public string CodigoProducto { get; set; } = "";
        public string DescripcionProducto { get; set; } = "";
        public decimal Confianza { get; set; }
        public string OrigenMatch { get; set; } = "Sin match";
        public string Estado
        {
            get
            {
                if (ProductoID <= 0) return "Sin match";
                if (Confianza >= 0.85m || OrigenMatch == "Código" || OrigenMatch == "Alias") return "OK";
                return "Revisar";
            }
        }
    }

    public class FacturaCompraPdfImportResult
    {
        public FacturaCompraPdfParseResult Parse { get; set; }
        public int ProveedorID { get; set; }
        public string ProveedorNombre { get; set; } = "";
        public List<FacturaCompraPdfMatchLinea> Lineas { get; set; } = new List<FacturaCompraPdfMatchLinea>();
    }

    public static class FacturaCompraPdfService
    {
        private static readonly Regex RxCuit = new Regex(@"\b(\d{2}[-.\s]?\d{8}[-.\s]?\d)\b", RegexOptions.Compiled);
        private static readonly HashSet<string> StopWords = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "DE", "LA", "EL", "LOS", "LAS", "Y", "EN", "DEL", "AL", "UN", "UNA", "CON", "POR", "PARA", "X", "UNI", "UNIDAD", "U"
        };

        public static FacturaCompraPdfParseResult ExtraerYParsear(string rutaPdf)
        {
            var result = new FacturaCompraPdfParseResult();
            string texto = ExtraerTexto(rutaPdf);
            result.TextoCompleto = texto ?? "";
            if (string.IsNullOrWhiteSpace(texto))
            {
                result.MensajeAdvertencia = "No se pudo leer texto del PDF. Si es un escaneo (imagen), esta versión no soporta OCR.";
                return result;
            }

            ParsearCabecera(texto, result);
            result.Lineas = ParsearLineas(texto);
            if (result.Lineas.Count == 0)
                result.MensajeAdvertencia = "Se leyó el PDF pero no se detectaron ítems automáticamente. Revise el archivo o cargue la factura a mano.";
            return result;
        }

        public static FacturaCompraPdfImportResult ImportarConMatching(string rutaPdf)
        {
            var parse = ExtraerYParsear(rutaPdf);
            var import = new FacturaCompraPdfImportResult { Parse = parse };

            if (!string.IsNullOrWhiteSpace(parse.CuitEmisor))
            {
                var prov = DatabaseService.BuscarProveedorPorCuit(parse.CuitEmisor);
                if (prov != null)
                {
                    import.ProveedorID = Convert.ToInt32(prov["ProveedorID"]);
                    import.ProveedorNombre = prov["RazonSocial"]?.ToString() ?? "";
                }
            }

            var catalogo = DatabaseService.GetProductosCatalogoMatchCompra();
            foreach (var linea in parse.Lineas)
            {
                import.Lineas.Add(MatchearLinea(linea, import.ProveedorID, catalogo));
            }
            return import;
        }

        public static FacturaCompraPdfMatchLinea MatchearLinea(FacturaCompraPdfLinea linea, int proveedorId, List<ProductoMatchCatalogo> catalogo)
        {
            var match = new FacturaCompraPdfMatchLinea { LineaPdf = linea };
            if (linea == null) return match;

            string codigo = (linea.CodigoProveedor ?? "").Trim();
            if (!string.IsNullOrEmpty(codigo) && catalogo != null)
            {
                var porCodigo = catalogo.FirstOrDefault(p =>
                    EqualsIgn(p.Codigo, codigo) || EqualsIgn(p.CodigoBarra, codigo) || EqualsIgn(p.CodigoExterno, codigo));
                if (porCodigo != null)
                {
                    Asignar(match, porCodigo, 1m, "Código");
                    return match;
                }
            }

            if (proveedorId > 0)
            {
                int? aliasId = DatabaseService.BuscarAliasProductoProveedor(proveedorId, linea.DescripcionPdf, codigo);
                if (aliasId.HasValue && catalogo != null)
                {
                    var prod = catalogo.FirstOrDefault(p => p.ProductoID == aliasId.Value);
                    if (prod != null)
                    {
                        Asignar(match, prod, 0.98m, "Alias");
                        return match;
                    }
                }
            }

            if (catalogo == null || catalogo.Count == 0 || string.IsNullOrWhiteSpace(linea.DescripcionPdf))
                return match;

            string normPdf = DatabaseService.NormalizarDescripcionProveedor(linea.DescripcionPdf);
            var tokensPdf = Tokenizar(normPdf);
            if (tokensPdf.Count == 0) return match;

            ProductoMatchCatalogo mejor = null;
            decimal mejorScore = 0;
            foreach (var p in catalogo)
            {
                string normProd = DatabaseService.NormalizarDescripcionProveedor(p.Descripcion);
                if (string.IsNullOrEmpty(normProd)) continue;

                if (normPdf == normProd)
                {
                    mejor = p;
                    mejorScore = 1m;
                    break;
                }

                var tokensProd = Tokenizar(normProd);
                if (tokensProd.Count == 0) continue;

                int comunes = tokensPdf.Count(t => tokensProd.Contains(t));
                if (comunes == 0) continue;

                // Contención: la descripción del sistema está incluida en la del PDF (caso Remera negra ⊂ Remera negra cuello...)
                bool contencion = tokensProd.All(t => tokensPdf.Contains(t)) && tokensProd.Count >= 2;
                decimal jaccard = (decimal)comunes / Math.Max(tokensPdf.Count, tokensProd.Count);
                decimal coberturaProd = (decimal)comunes / tokensProd.Count;
                decimal score = contencion ? Math.Max(0.82m, coberturaProd * 0.95m) : Math.Max(jaccard, coberturaProd * 0.75m);

                if (score > mejorScore)
                {
                    mejorScore = score;
                    mejor = p;
                }
            }

            if (mejor != null && mejorScore >= 0.45m)
                Asignar(match, mejor, Math.Min(0.95m, mejorScore), "Fuzzy");

            return match;
        }

        private static void Asignar(FacturaCompraPdfMatchLinea match, ProductoMatchCatalogo p, decimal conf, string origen)
        {
            match.ProductoID = p.ProductoID;
            match.CodigoProducto = p.Codigo ?? "";
            match.DescripcionProducto = p.Descripcion ?? "";
            match.Confianza = conf;
            match.OrigenMatch = origen;
        }

        private static bool EqualsIgn(string a, string b)
        {
            return !string.IsNullOrWhiteSpace(a) && !string.IsNullOrWhiteSpace(b)
                && string.Equals(a.Trim(), b.Trim(), StringComparison.OrdinalIgnoreCase);
        }

        private static List<string> Tokenizar(string norm)
        {
            return (norm ?? "")
                .Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries)
                .Where(t => t.Length > 1 && !StopWords.Contains(t))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();
        }

        private static string ExtraerTexto(string rutaPdf)
        {
            var sb = new StringBuilder();
            using (var doc = PdfDocument.Open(rutaPdf))
            {
                foreach (var page in doc.GetPages())
                {
                    string t = page.Text;
                    if (!string.IsNullOrWhiteSpace(t))
                    {
                        sb.AppendLine(t);
                        continue;
                    }
                    // Fallback: palabras ordenadas por posición
                    var words = page.GetWords()?.OrderByDescending(w => w.BoundingBox.Top).ThenBy(w => w.BoundingBox.Left);
                    if (words == null) continue;
                    double? lastTop = null;
                    foreach (var w in words)
                    {
                        if (lastTop.HasValue && Math.Abs(lastTop.Value - w.BoundingBox.Top) > 3)
                            sb.AppendLine();
                        else if (sb.Length > 0 && sb[sb.Length - 1] != '\n')
                            sb.Append(' ');
                        sb.Append(w.Text);
                        lastTop = w.BoundingBox.Top;
                    }
                    sb.AppendLine();
                }
            }
            return sb.ToString();
        }

        private static void ParsearCabecera(string texto, FacturaCompraPdfParseResult result)
        {
            var cuits = RxCuit.Matches(texto);
            if (cuits.Count > 0)
            {
                // Suele aparecer primero el CUIT del emisor en facturas AR
                result.CuitEmisor = NormalizarCuit(cuits[0].Groups[1].Value);
                if (cuits.Count > 1 && string.IsNullOrEmpty(result.CuitEmisor))
                    result.CuitEmisor = NormalizarCuit(cuits[1].Groups[1].Value);
            }

            var mTipo = Regex.Match(texto, @"FACTURA\s*([ABC])\b", RegexOptions.IgnoreCase);
            if (mTipo.Success)
                result.TipoComprobante = "Factura " + mTipo.Groups[1].Value.ToUpperInvariant();
            else if (Regex.IsMatch(texto, @"\bCOD\.?\s*0?01\b", RegexOptions.IgnoreCase))
                result.TipoComprobante = "Factura A";
            else if (Regex.IsMatch(texto, @"\bCOD\.?\s*0?06\b", RegexOptions.IgnoreCase))
                result.TipoComprobante = "Factura B";
            else if (Regex.IsMatch(texto, @"\bCOD\.?\s*0?11\b", RegexOptions.IgnoreCase))
                result.TipoComprobante = "Factura C";

            var mNro = Regex.Match(texto, @"(?:N[ºo°\.]*|Comp\.?\s*N[ºo°\.]*)\s*(\d{4,5}\s*[-/]\s*\d{7,8}|\d{4,5}-\d{7,8})", RegexOptions.IgnoreCase);
            if (mNro.Success)
                result.NumeroComprobante = Regex.Replace(mNro.Groups[1].Value, @"\s+", "");

            // Razón social: línea cercana a "Razón Social" o primera línea sustancial
            var mRs = Regex.Match(texto, @"Raz[oó]n\s*Social\s*[:\-]?\s*(.+)", RegexOptions.IgnoreCase);
            if (mRs.Success)
            {
                string rs = mRs.Groups[1].Value.Trim();
                int corte = rs.IndexOfAny(new[] { '\r', '\n' });
                if (corte > 0) rs = rs.Substring(0, corte);
                result.RazonSocialEmisor = rs.Trim();
            }
        }

        private static string NormalizarCuit(string cuit)
        {
            string d = Regex.Replace(cuit ?? "", @"[^\d]", "");
            if (d.Length != 11) return cuit?.Trim() ?? "";
            return $"{d.Substring(0, 2)}-{d.Substring(2, 8)}-{d.Substring(10, 1)}";
        }

        private static List<FacturaCompraPdfLinea> ParsearLineas(string texto)
        {
            var lineas = new List<FacturaCompraPdfLinea>();
            var rawLines = texto.Split(new[] { "\r\n", "\n", "\r" }, StringSplitOptions.RemoveEmptyEntries)
                .Select(l => Regex.Replace(l.Trim(), @"\s+", " "))
                .Where(l => l.Length >= 6)
                .ToList();

            // Patrón típico: [codigo] descripcion cantidad precio [bonif] importe
            var rx = new Regex(
                @"^(?:(?<cod>[A-Z0-9\.\-/]{2,24})\s+)?(?<desc>.+?)\s+(?<cant>\d+(?:[.,]\d{1,3})?)\s+(?<pu>\d{1,3}(?:\.\d{3})*(?:,\d{2})|\d+(?:[.,]\d{2}))(?:\s+(?<imp>\d{1,3}(?:\.\d{3})*(?:,\d{2})|\d+(?:[.,]\d{2})))?\s*$",
                RegexOptions.IgnoreCase);

            foreach (string line in rawLines)
            {
                if (EsRuido(line)) continue;
                var m = rx.Match(line);
                if (!m.Success) continue;

                string desc = m.Groups["desc"].Value.Trim();
                if (desc.Length < 3) continue;
                if (!TryParseDecimalAr(m.Groups["cant"].Value, out decimal cantDec) || cantDec <= 0) continue;
                if (!TryParseDecimalAr(m.Groups["pu"].Value, out decimal pu) || pu < 0) continue;

                decimal imp = 0;
                if (m.Groups["imp"].Success)
                    TryParseDecimalAr(m.Groups["imp"].Value, out imp);

                int cant = (int)Math.Round(cantDec, MidpointRounding.AwayFromZero);
                if (cant <= 0) cant = 1;

                // Si el "código" parece parte de la descripción (palabras), moverlo
                string cod = m.Groups["cod"].Success ? m.Groups["cod"].Value.Trim() : "";
                if (!string.IsNullOrEmpty(cod) && Regex.IsMatch(cod, @"^[A-Za-z]{3,}$") && !Regex.IsMatch(cod, @"\d"))
                {
                    desc = (cod + " " + desc).Trim();
                    cod = "";
                }

                lineas.Add(new FacturaCompraPdfLinea
                {
                    CodigoProveedor = cod,
                    DescripcionPdf = desc,
                    Cantidad = cant,
                    CostoUnitario = pu,
                    Subtotal = imp > 0 ? imp : Math.Round(cant * pu, 2)
                });
            }

            // Deduplicar líneas idénticas consecutivas
            var dedup = new List<FacturaCompraPdfLinea>();
            foreach (var l in lineas)
            {
                if (dedup.Count > 0)
                {
                    var prev = dedup[dedup.Count - 1];
                    if (prev.DescripcionPdf == l.DescripcionPdf && prev.Cantidad == l.Cantidad && prev.CostoUnitario == l.CostoUnitario)
                        continue;
                }
                dedup.Add(l);
            }
            return dedup;
        }

        private static bool EsRuido(string line)
        {
            string u = line.ToUpperInvariant();
            if (u.Contains("CUIT") && u.Length < 40) return true;
            if (u.Contains("IVA") && (u.Contains("ALICUOTA") || u.Contains("ALÍCUOTA"))) return true;
            if (u.StartsWith("TOTAL") || u.StartsWith("SUBTOTAL") || u.StartsWith("IMPORTE TOTAL")) return true;
            if (u.Contains("CAE") || u.Contains("VENCIMIENTO")) return true;
            if (u.Contains("PÁGINA") || u.Contains("PAGINA")) return true;
            if (Regex.IsMatch(u, @"^(CODIGO|CÓDIGO|DESCRIPCION|DESCRIPCIÓN|CANT|PRECIO|IMPORTE)")) return true;
            return false;
        }

        private static bool TryParseDecimalAr(string s, out decimal value)
        {
            value = 0;
            if (string.IsNullOrWhiteSpace(s)) return false;
            string t = s.Trim();
            // 1.234,56 → AR
            if (t.Contains(",") && t.Contains("."))
                t = t.Replace(".", "").Replace(",", ".");
            else if (t.Contains(","))
                t = t.Replace(",", ".");
            return decimal.TryParse(t, NumberStyles.Number, CultureInfo.InvariantCulture, out value);
        }
    }
}
