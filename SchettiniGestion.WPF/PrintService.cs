using Newtonsoft.Json;
using QRCoder;
using SchettiniGestion;
using System;
using System.Data;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Media; // WPF Media
using System.Windows.Media.Imaging;

// Alias para evitar confusiones
using WinDrawing = System.Drawing;
using WinPrinting = System.Drawing.Printing;

namespace SchettiniGestion.WPF
{
    public static class PrintService
    {
        private static bool USAR_MOTOR_GRAFICO_PARA_TICKETS = true;

        #region Métodos Públicos

        public static void ImprimirPresupuesto(int presupuestoID, string clienteNombre, DateTime fecha, DataTable items, decimal total)
        {
            ImprimirDocumentoWPF("PRESUPUESTO", presupuestoID.ToString(), clienteNombre, fecha, items, total, "Válido por 7 días", "X", "", "", true);
        }

        public static void ImprimirTicketVenta(string tipoComprobante, int nroComprobante, string clienteNombre, DateTime fecha, DataTable items, decimal total, string condicionVenta)
        {
            string letra = "X";
            if (tipoComprobante != null)
            {
                if (tipoComprobante.Contains("Factura A")) letra = "A";
                if (tipoComprobante.Contains("Factura B")) letra = "B";
                if (tipoComprobante.Contains("Factura C")) letra = "C";
            }

            string titulo = tipoComprobante?.ToUpper() ?? "TICKET";
            string nroStr = nroComprobante > 0 ? nroComprobante.ToString("D8") : "(Pendiente)";
            string pieFiscal = "", cae = "";

            if (condicionVenta != null && condicionVenta.Contains("CAE:"))
            {
                string[] partes = condicionVenta.Split(new[] { "CAE:" }, StringSplitOptions.None);
                condicionVenta = partes[0].Trim();
                if (partes.Length > 1)
                {
                    string resto = partes[1].Trim();
                    pieFiscal = resto;
                    var datosCae = resto.Split(new[] { "VTO:" }, StringSplitOptions.None);
                    if (datosCae.Length > 0) cae = datosCae[0].Trim();
                }
            }

            if (USAR_MOTOR_GRAFICO_PARA_TICKETS)
            {
                ImprimirTicketGrafico(titulo, nroStr, clienteNombre, fecha, items, total, condicionVenta, letra, pieFiscal, cae);
            }
            else
            {
                ImprimirDocumentoWPF(titulo, nroStr, clienteNombre, fecha, items, total, condicionVenta, letra, pieFiscal, cae, false);
            }
        }

        #endregion

        #region MOTOR 1: System.Drawing (Tickets Térmicos OPTIMIZADO)

        private static void ImprimirTicketGrafico(string titulo, string numero, string cliente, DateTime fecha, DataTable items, decimal total, string extra, string letra, string pie, string cae)
        {
            try
            {
                WinPrinting.PrintDocument doc = new WinPrinting.PrintDocument();
                doc.PrintController = new WinPrinting.StandardPrintController();

                doc.PrintPage += (sender, e) =>
                {
                    DibujarTicketGDI(e.Graphics, titulo, numero, cliente, fecha, items, total, extra, letra, pie, cae);
                };

                System.Windows.Forms.PrintDialog pd = new System.Windows.Forms.PrintDialog();
                pd.Document = doc;
                doc.Print();
            }
            catch (Exception ex)
            {
                MessageBox.Show("Error al imprimir ticket: " + ex.Message);
            }
        }

        private static void DibujarTicketGDI(WinDrawing.Graphics g, string tit, string nro, string cli, DateTime fec, DataTable its, decimal tot, string extra, string let, string pie, string cae)
        {
            // CALIDAD
            g.InterpolationMode = WinDrawing.Drawing2D.InterpolationMode.NearestNeighbor;
            g.PixelOffsetMode = WinDrawing.Drawing2D.PixelOffsetMode.HighQuality;
            g.SmoothingMode = WinDrawing.Drawing2D.SmoothingMode.None;
            g.TextRenderingHint = WinDrawing.Text.TextRenderingHint.ClearTypeGridFit;

            // MEDIDAS
            float anchoPapel = g.VisibleClipBounds.Width;
            if (anchoPapel > 300) anchoPapel = 290;

            float y = 0;
            float margen = 2;
            float xRight = anchoPapel - margen;

            // FUENTES
            WinDrawing.Font fTitulo = new WinDrawing.Font("Arial", 10, WinDrawing.FontStyle.Bold);
            WinDrawing.Font fLetraGrande = new WinDrawing.Font("Arial", 12, WinDrawing.FontStyle.Bold);
            WinDrawing.Font fNormal = new WinDrawing.Font("Consolas", 9, WinDrawing.FontStyle.Regular);
            WinDrawing.Font fNegrita = new WinDrawing.Font("Consolas", 9, WinDrawing.FontStyle.Bold);
            WinDrawing.Font fChica = new WinDrawing.Font("Consolas", 8, WinDrawing.FontStyle.Regular);
            WinDrawing.SolidBrush brocha = new WinDrawing.SolidBrush(WinDrawing.Color.Black);

            WinDrawing.StringFormat centro = new WinDrawing.StringFormat() { Alignment = WinDrawing.StringAlignment.Center };
            WinDrawing.StringFormat derecha = new WinDrawing.StringFormat() { Alignment = WinDrawing.StringAlignment.Far };
            WinDrawing.StringFormat izquierda = new WinDrawing.StringFormat() { Alignment = WinDrawing.StringAlignment.Near };

            // 1. ENCABEZADO
            DataRow conf = DatabaseService.GetConfiguracion();
            string fantasia = conf != null ? conf["NombreFantasia"].ToString() : "MI NEGOCIO";
            string direccion = conf != null ? conf["Direccion"].ToString() : "";
            string cuitEmp = conf != null ? conf["CUIT"].ToString() : "";

            DibujarTextoCentrado(g, fantasia.ToUpper(), fTitulo, anchoPapel, ref y);
            DibujarTextoCentrado(g, direccion, fChica, anchoPapel, ref y);
            DibujarTextoCentrado(g, "CUIT: " + cuitEmp, fChica, anchoPapel, ref y);

            DibujarLinea(g, ref y, anchoPapel);

            // 2. DATOS COMPROBANTE
            if (let != "X")
            {
                g.DrawString($"[{let}]", fLetraGrande, brocha, anchoPapel / 2, y, centro);
                y += 18;
            }

            string tituloCompleto = $"{tit} N° {nro}";
            g.DrawString(tituloCompleto, fNegrita, brocha, anchoPapel / 2, y, centro);
            y += 14;

            g.DrawString("Fecha: " + fec.ToString("dd/MM/yyyy  HH:mm") + "hs", fNormal, brocha, margen, y);
            y += 14;

            DibujarLinea(g, ref y, anchoPapel);

            // 3. CLIENTE
            if (!string.IsNullOrEmpty(cli) && cli != "Consumidor Final")
            {
                g.DrawString("Cliente: " + cli, fNormal, brocha, margen, y);
                y += 14;
                if (!string.IsNullOrEmpty(extra)) { g.DrawString(extra, fChica, brocha, margen, y); y += 12; }
                DibujarLinea(g, ref y, anchoPapel);
            }

            // 4. ÍTEMS (CON CÁLCULO DE IVA + DISEÑO ADAPTATIVO)
            float wCant = 35;
            float wTot = 70;
            float wDesc = anchoPapel - wCant - wTot - (margen * 2) - 5;

            float xCant = margen;
            float xDesc = margen + wCant;
            float xTotStart = anchoPapel - margen - wTot;

            g.DrawString("CNT", fChica, brocha, xCant, y);
            g.DrawString("DESCRIPCION", fChica, brocha, xDesc, y);
            g.DrawString("TOTAL", fChica, brocha, xRight, y, derecha);
            y += 14;

            // Variables para acumular IVA
            decimal acumuladoNeto = 0;
            decimal acumuladoIva21 = 0;
            decimal acumuladoIva105 = 0;

            foreach (DataRow row in its.Rows)
            {
                decimal q = Convert.ToDecimal(row["Cantidad"]);
                string d = row.Table.Columns.Contains("Descripcion") ? row["Descripcion"].ToString() : row["Producto"].ToString();
                decimal s = Convert.ToDecimal(row["Subtotal"]);

                // --- CÁLCULO DE IVA ---
                decimal alicuota = 21m; // Default
                if (row.Table.Columns.Contains("Alicuota") && row["Alicuota"] != DBNull.Value) decimal.TryParse(row["Alicuota"].ToString(), out alicuota);
                else if (row.Table.Columns.Contains("IVA") && row["IVA"] != DBNull.Value) decimal.TryParse(row["IVA"].ToString(), out alicuota);

                decimal netoItem = s / (1 + (alicuota / 100));
                decimal ivaItem = s - netoItem;

                acumuladoNeto += netoItem;
                if (alicuota == 10.5m) acumuladoIva105 += ivaItem;
                else acumuladoIva21 += ivaItem;

                // --- DIBUJO ---
                g.DrawString(q.ToString("0.#"), fNormal, brocha, xCant, y);

                WinDrawing.RectangleF rectTotal = new WinDrawing.RectangleF(xTotStart, y, wTot, 20);
                g.DrawString(s.ToString("N2"), fNormal, brocha, rectTotal, derecha);

                WinDrawing.RectangleF rectDesc = new WinDrawing.RectangleF(xDesc, y, wDesc, 500);
                g.DrawString(d, fNormal, brocha, rectDesc);

                WinDrawing.SizeF size = g.MeasureString(d, fNormal, (int)wDesc);
                y += Math.Max(14, size.Height);
            }

            DibujarLinea(g, ref y, anchoPapel);

            // 5. TOTALES Y DISCRIMINACIÓN
            y += 5;

            if (let == "A")
            {
                // FACTURA A: Desglose ANTES del total
                g.DrawString($"Subtotal Neto: ${acumuladoNeto:N2}", fNormal, brocha, xRight, y, derecha); y += 12;
                if (acumuladoIva21 > 0) { g.DrawString($"IVA (21%): ${acumuladoIva21:N2}", fNormal, brocha, xRight, y, derecha); y += 12; }
                if (acumuladoIva105 > 0) { g.DrawString($"IVA (10.5%): ${acumuladoIva105:N2}", fNormal, brocha, xRight, y, derecha); y += 12; }

                DibujarLinea(g, ref y, anchoPapel);
                g.DrawString("TOTAL: $" + tot.ToString("N2"), fTitulo, brocha, xRight, y, derecha); y += 20;
            }
            else
            {
                // FACTURA B: Total y luego Transparencia
                g.DrawString("TOTAL: $" + tot.ToString("N2"), fTitulo, brocha, xRight, y, derecha); y += 20;

                if (let == "B")
                {
                    y += 5;
                    DibujarTextoCentrado(g, "TRANSPARENCIA FISCAL", fChica, anchoPapel, ref y);
                    if (acumuladoIva21 > 0) { g.DrawString($"IVA Contenido (21%): ${acumuladoIva21:N2}", fChica, brocha, xRight, y, derecha); y += 10; }
                    if (acumuladoIva105 > 0) { g.DrawString($"IVA Contenido (10.5%): ${acumuladoIva105:N2}", fChica, brocha, xRight, y, derecha); y += 10; }
                    g.DrawString("Otros Impuestos: $0,00", fChica, brocha, xRight, y, derecha); y += 15;
                    DibujarLinea(g, ref y, anchoPapel);
                }
            }

            // 6. PIE FISCAL / QR
            if (!string.IsNullOrEmpty(cae) && let != "X")
            {
                WinDrawing.Image qrImg = GenerarQrGDI(fec, nro, tot, let, cae, cuitEmp);
                if (qrImg != null)
                {
                    float qrSize = 110;
                    float xQr = (anchoPapel - qrSize) / 2;
                    g.DrawImage(qrImg, xQr, y, qrSize, qrSize);
                    y += qrSize + 5;
                }

                string vto = "";
                if (pie.Contains("VTO:"))
                {
                    var partes = pie.Split(new[] { "VTO:" }, StringSplitOptions.RemoveEmptyEntries);
                    if (partes.Length > 1) vto = partes[1].Trim();
                }

                DibujarTextoCentrado(g, "CAE: " + cae, fChica, anchoPapel, ref y);
                if (!string.IsNullOrEmpty(vto)) DibujarTextoCentrado(g, "VTO: " + vto, fChica, anchoPapel, ref y);
            }

            y += 5;
            DibujarTextoCentrado(g, "Gracias por su compra", fNormal, anchoPapel, ref y);
            DibujarTextoCentrado(g, "Schettini Gestión", fChica, anchoPapel, ref y);
        }

        private static void DibujarLinea(WinDrawing.Graphics g, ref float y, float ancho)
        {
            y += 3;
            WinDrawing.Pen p = new WinDrawing.Pen(WinDrawing.Color.Black, 1);
            p.DashStyle = WinDrawing.Drawing2D.DashStyle.Dash;
            g.DrawLine(p, 2, y, ancho - 2, y);
            y += 5;
        }

        private static void DibujarTextoCentrado(WinDrawing.Graphics g, string texto, WinDrawing.Font fuente, float anchoPapel, ref float y)
        {
            if (string.IsNullOrEmpty(texto)) return;
            WinDrawing.SizeF size = g.MeasureString(texto, fuente);
            float x = (anchoPapel - size.Width) / 2;
            g.DrawString(texto, fuente, new WinDrawing.SolidBrush(WinDrawing.Color.Black), x, y);
            y += size.Height;
        }

        private static WinDrawing.Image GenerarQrGDI(DateTime fecha, string nroComprobante, decimal total, string letra, string cae, string cuitEmp)
        {
            try
            {
                long cuit = long.Parse(cuitEmp.Replace("-", "").Replace(" ", ""));
                int pto = 1;
                try { DataRow c = DatabaseService.GetConfiguracion(); pto = int.Parse(c["PuntoVenta"].ToString()); } catch { }

                int tipo = 0;
                if (letra == "A") tipo = 1; else if (letra == "B") tipo = 6; else if (letra == "C") tipo = 11;
                int nro = int.TryParse(nroComprobante, out int n) ? n : 0;

                var datos = new { ver = 1, fecha = fecha.ToString("yyyy-MM-dd"), cuit = cuit, ptoVta = pto, tipoCmp = tipo, nroCmp = nro, importe = total, moneda = "PES", ctz = 1, tipoDocRec = 99, nroDocRec = 0, tipoCodAut = "E", codAut = long.Parse(cae) };

                string json = JsonConvert.SerializeObject(datos);
                string base64 = Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes(json));
                string url = $"https://www.afip.gob.ar/fe/qr/?p={base64}";

                QRCodeGenerator qrGen = new QRCodeGenerator();
                QRCodeData qrData = qrGen.CreateQrCode(url, QRCodeGenerator.ECCLevel.M);
                QRCode qrCode = new QRCode(qrData);
                return qrCode.GetGraphic(20, WinDrawing.Color.Black, WinDrawing.Color.White, true);
            }
            catch { return null; }
        }

        #endregion

        #region MOTOR 2: WPF (Para A4)
        private static void ImprimirDocumentoWPF(string tituloDoc, string numeroDoc, string cliente, DateTime fecha, DataTable items, decimal total, string infoExtra, string letra, string pieFiscal, string cae, bool forzarA4)
        {
            try
            {
                PrintDialog pd = new PrintDialog();
                if (pd.ShowDialog() == true)
                {
                    double anchoImpresora = pd.PrintableAreaWidth;
                    FlowDocument doc = new FlowDocument();
                    doc.FontFamily = new FontFamily("Arial");
                    doc.TextAlignment = TextAlignment.Left;

                    if (anchoImpresora > 500 || forzarA4)
                    {
                        double anchoA4 = 793;
                        doc.PageWidth = anchoA4;
                        doc.ColumnWidth = anchoA4;
                        doc.PagePadding = new Thickness(40);
                        Image imgQR = null;
                        DibujarFacturaA4(doc, anchoA4, tituloDoc, numeroDoc, cliente, fecha, items, total, infoExtra, letra, pieFiscal, imgQR);
                    }
                    else
                    {
                        doc.Blocks.Add(new Paragraph(new Run("Para Tickets use el Motor Gráfico.")));
                    }
                    IDocumentPaginatorSource dps = doc;
                    pd.PrintDocument(dps.DocumentPaginator, $"Impresion_{tituloDoc}");
                }
            }
            catch (Exception ex) { MessageBox.Show(ex.Message); }
        }

        private static void DibujarFacturaA4(FlowDocument doc, double anchoPagina, string tit, string nro, string cli, DateTime fec, DataTable its, decimal tot, string extra, string let, string pie, Image qr)
        {
            doc.FontFamily = new FontFamily("Arial"); doc.FontSize = 10;
            Paragraph p = new Paragraph(new Run($"DOCUMENTO: {tit} {nro}\nCLIENTE: {cli}\nTOTAL: {tot:C2}"));
            doc.Blocks.Add(p);
        }

        private static string GetValor(DataRow r, string c, string def = "") { if (r.Table.Columns.Contains(c) && r[c] != DBNull.Value) return r[c].ToString(); return def; }
        private static decimal GetValorDecimal(DataRow r, string c) { if (r.Table.Columns.Contains(c) && r[c] != DBNull.Value && decimal.TryParse(r[c].ToString(), out decimal d)) return d; return 0; }
        private static string GetCodigoComprobante(string l) { return l == "A" ? "001" : (l == "B" ? "006" : "000"); }
        private static Image ObtenerImagenLogo() { return null; }
        private static Image IntentarGenerarQRWPF(DateTime f, string n, decimal t, string l, string c) { return null; }
        #endregion
    }
}