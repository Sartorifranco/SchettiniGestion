using Newtonsoft.Json;
using QRCoder;
using SchettiniGestion;
using System;
using System.Data;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Media;
using System.Windows.Media.Imaging;

using WinDrawing = System.Drawing;
using WpfMedia = System.Windows.Media;

namespace SchettiniGestion.WPF
{
    public static class PrintService
    {
        // =========================================================================
        //  CONFIGURACIÓN DE DEPURACIÓN
        //  Pon esto en TRUE para ver el diseño Ticket (80mm) en el PDF A4.
        // =========================================================================
        private static bool MODO_PRUEBA_TICKET = false;

        #region Métodos Públicos

        public static void ImprimirPresupuesto(int presupuestoID, string clienteNombre, DateTime fecha, DataTable items, decimal total)
        {
            ImprimirDocumentoGenerico("PRESUPUESTO", presupuestoID.ToString(), clienteNombre, fecha, items, total, "Válido por 7 días", "X", "", "");
        }

        public static void ImprimirTicketVenta(string tipoComprobante, int nroComprobante, string clienteNombre, DateTime fecha, DataTable items, decimal total, string condicionVenta)
        {
            string letra = "X";
            if (tipoComprobante != null)
            {
                if (tipoComprobante.Contains("Factura A")) letra = "A";
                if (tipoComprobante.Contains("Factura B")) letra = "B";
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
                    pieFiscal = "CAE: " + resto;
                    var datosCae = resto.Split(new[] { "VTO:" }, StringSplitOptions.None);
                    if (datosCae.Length > 0) cae = datosCae[0].Trim();
                }
            }

            ImprimirDocumentoGenerico(titulo, nroStr, clienteNombre, fecha, items, total, condicionVenta, letra, pieFiscal, cae);
        }

        #endregion

        #region Lógica Principal

        private static void ImprimirDocumentoGenerico(string tituloDoc, string numeroDoc, string cliente, DateTime fecha, DataTable items, decimal total, string infoExtra, string letra, string pieFiscal, string cae)
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

                    Image imgQR = null;
                    if (!string.IsNullOrEmpty(cae) && letra != "X") imgQR = IntentarGenerarQR(fecha, numeroDoc, total, letra, cae);

                    // LÓGICA DE TAMAÑO
                    if (anchoImpresora > 500 && !MODO_PRUEBA_TICKET)
                    {
                        // --- MODO A4 ---
                        double anchoA4 = 793;
                        doc.PageWidth = anchoA4;
                        doc.ColumnWidth = anchoA4;
                        doc.PagePadding = new Thickness(40);

                        DibujarFacturaA4(doc, anchoA4, tituloDoc, numeroDoc, cliente, fecha, items, total, infoExtra, letra, pieFiscal, imgQR);
                    }
                    else
                    {
                        // --- MODO TICKET ---
                        double anchoTicket = anchoImpresora;
                        if (MODO_PRUEBA_TICKET && anchoTicket > 300) anchoTicket = 280;
                        if (anchoTicket < 200) anchoTicket = 280;

                        doc.PageWidth = anchoTicket;
                        doc.ColumnWidth = anchoTicket;
                        doc.PagePadding = new Thickness(5);

                        DibujarTicket80mm(doc, anchoTicket, tituloDoc, numeroDoc, cliente, fecha, items, total, infoExtra, pieFiscal, imgQR);
                    }

                    IDocumentPaginatorSource dps = doc;
                    pd.PrintDocument(dps.DocumentPaginator, $"Impresion_{tituloDoc}");
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error impresión: {ex.Message}");
            }
        }

        #endregion

        #region Diseño Ticket (80mm)
        private static void DibujarTicket80mm(FlowDocument doc, double ancho, string tit, string nro, string cli, DateTime fec, DataTable its, decimal tot, string extra, string pie, Image qr)
        {
            doc.FontFamily = new FontFamily("Consolas");
            doc.FontSize = 9;

            Paragraph h = new Paragraph { TextAlignment = TextAlignment.Center, Margin = new Thickness(0, 0, 0, 5) };
            Image logo = ObtenerImagenLogo();
            if (logo != null) { logo.Width = 100; h.Inlines.Add(new InlineUIContainer(logo)); }

            DataRow c = DatabaseService.GetConfiguracion();
            string emp = c != null ? GetValor(c, "NombreFantasia", "MI NEGOCIO") : "MI NEGOCIO";

            h.Inlines.Add(new Run(emp.ToUpper() + "\n") { FontWeight = FontWeights.Bold, FontSize = 11 });
            h.Inlines.Add(new Run("--------------------------------\n"));
            h.Inlines.Add(new Run($"{tit} N° {nro}\n") { FontWeight = FontWeights.Bold });
            h.Inlines.Add(new Run($"{fec:dd/MM/yyyy HH:mm}\n"));
            doc.Blocks.Add(h);

            foreach (DataRow r in its.Rows)
            {
                Paragraph p = new Paragraph { Margin = new Thickness(0) };
                decimal cant = GetValorDecimal(r, "Cantidad");
                decimal sub = GetValorDecimal(r, "Subtotal");
                decimal unit = cant != 0 ? sub / cant : 0;
                string desc = GetValor(r, "Descripcion", "Item");

                p.Inlines.Add(new Run(desc + "\n"));
                string nums = $"{cant:0.##} x {unit:N2}";
                string totS = $"{sub:N2}";
                int pad = 32 - nums.Length - totS.Length;
                if (pad < 1) pad = 1;
                p.Inlines.Add(new Run(nums + new string(' ', pad) + totS));
                doc.Blocks.Add(p);
            }

            Paragraph f = new Paragraph { TextAlignment = TextAlignment.Right, Margin = new Thickness(0, 5, 0, 0) };
            f.Inlines.Add(new Run("--------------------------------\n"));
            f.Inlines.Add(new Run($"TOTAL: ${tot:N2}\n") { FontWeight = FontWeights.Bold, FontSize = 14 });
            doc.Blocks.Add(f);

            if (qr != null)
            {
                Paragraph qp = new Paragraph { TextAlignment = TextAlignment.Center };
                qr.Width = 80;
                qp.Inlines.Add(new InlineUIContainer(qr));
                if (!string.IsNullOrEmpty(pie)) qp.Inlines.Add(new Run("\n" + pie) { FontSize = 8 });
                doc.Blocks.Add(qp);
            }
        }
        #endregion

        #region Diseño Factura A4

        private static void DibujarFacturaA4(FlowDocument doc, double anchoPagina, string tit, string nro, string cli, DateTime fec, DataTable its, decimal tot, string extra, string let, string pie, Image qr)
        {
            doc.FontFamily = new FontFamily("Arial");
            doc.FontSize = 10;
            double anchoUtil = anchoPagina - 80;

            DataRow conf = DatabaseService.GetConfiguracion();
            string rz = conf != null ? GetValor(conf, "RazonSocial") : "EMPRESA";
            string dir = conf != null ? GetValor(conf, "Direccion") : "";
            string cuit = conf != null ? GetValor(conf, "CUIT") : "";

            // Header
            Grid gHead = new Grid();
            gHead.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            gHead.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(60) });
            gHead.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

            StackPanel sIzq = new StackPanel();
            Image logo = ObtenerImagenLogo();
            if (logo != null) { logo.Width = 150; logo.HorizontalAlignment = HorizontalAlignment.Left; sIzq.Children.Add(logo); }
            sIzq.Children.Add(new TextBlock { Text = rz, FontWeight = FontWeights.Bold, FontSize = 16, Margin = new Thickness(0, 5, 0, 0) });
            sIzq.Children.Add(new TextBlock { Text = dir });
            Grid.SetColumn(sIzq, 0);

            Border bLet = new Border { BorderBrush = Brushes.Black, BorderThickness = new Thickness(1), Background = Brushes.WhiteSmoke, Height = 50, VerticalAlignment = VerticalAlignment.Top };
            StackPanel sLet = new StackPanel { VerticalAlignment = VerticalAlignment.Center };
            sLet.Children.Add(new TextBlock { Text = let, FontSize = 28, FontWeight = FontWeights.Bold, HorizontalAlignment = HorizontalAlignment.Center });
            sLet.Children.Add(new TextBlock { Text = "COD " + GetCodigoComprobante(let), FontSize = 8, HorizontalAlignment = HorizontalAlignment.Center });
            bLet.Child = sLet;
            Grid.SetColumn(bLet, 1);

            StackPanel sDer = new StackPanel { HorizontalAlignment = HorizontalAlignment.Right };
            sDer.Children.Add(new TextBlock { Text = tit, FontSize = 20, FontWeight = FontWeights.Bold, HorizontalAlignment = HorizontalAlignment.Right });
            sDer.Children.Add(new TextBlock { Text = $"N° {nro}", FontSize = 14, FontWeight = FontWeights.Bold, HorizontalAlignment = HorizontalAlignment.Right });
            sDer.Children.Add(new TextBlock { Text = $"FECHA: {fec:dd/MM/yyyy}", HorizontalAlignment = HorizontalAlignment.Right, Margin = new Thickness(0, 10, 0, 0) });
            sDer.Children.Add(new TextBlock { Text = $"CUIT: {cuit}", HorizontalAlignment = HorizontalAlignment.Right });
            Grid.SetColumn(sDer, 2);

            gHead.Children.Add(sIzq); gHead.Children.Add(bLet); gHead.Children.Add(sDer);
            doc.Blocks.Add(new BlockUIContainer(gHead));

            // Cliente
            Border bCli = new Border { BorderBrush = Brushes.Gray, BorderThickness = new Thickness(1), Margin = new Thickness(0, 15, 0, 15), Padding = new Thickness(5) };
            StackPanel sCli = new StackPanel();
            sCli.Children.Add(new TextBlock { Text = $"CLIENTE: {cli}", FontWeight = FontWeights.Bold });
            sCli.Children.Add(new TextBlock { Text = $"CONDICIÓN: {extra}" });
            bCli.Child = sCli;
            doc.Blocks.Add(new BlockUIContainer(bCli));

            // Tabla
            Table t = new Table { CellSpacing = 0, BorderBrush = Brushes.Black, BorderThickness = new Thickness(0, 1, 0, 1) };
            t.Columns.Add(new TableColumn { Width = new GridLength(anchoUtil * 0.15) });
            t.Columns.Add(new TableColumn { Width = new GridLength(anchoUtil * 0.45) });
            t.Columns.Add(new TableColumn { Width = new GridLength(anchoUtil * 0.10) });
            t.Columns.Add(new TableColumn { Width = new GridLength(anchoUtil * 0.15) });
            t.Columns.Add(new TableColumn { Width = new GridLength(anchoUtil * 0.15) });

            TableRowGroup rg = new TableRowGroup();
            TableRow rh = new TableRow { Background = Brushes.LightGray, FontWeight = FontWeights.Bold };
            rh.Cells.Add(Celda("CÓDIGO"));
            rh.Cells.Add(Celda("DESCRIPCIÓN"));
            rh.Cells.Add(Celda("CANT", TextAlignment.Center));
            rh.Cells.Add(Celda("UNITARIO", TextAlignment.Right));
            rh.Cells.Add(Celda("SUBTOTAL", TextAlignment.Right));
            rg.Rows.Add(rh);

            foreach (DataRow r in its.Rows)
            {
                decimal cant = GetValorDecimal(r, "Cantidad");
                decimal sub = GetValorDecimal(r, "Subtotal");
                decimal unit = cant != 0 ? sub / cant : 0;

                TableRow row = new TableRow();
                row.Cells.Add(Celda(GetValor(r, "Codigo", "-")));
                row.Cells.Add(Celda(GetValor(r, "Descripcion", "Producto")));
                row.Cells.Add(Celda(cant.ToString("0.##"), TextAlignment.Center));
                row.Cells.Add(Celda(unit.ToString("N2"), TextAlignment.Right));
                row.Cells.Add(Celda(sub.ToString("N2"), TextAlignment.Right));
                rg.Rows.Add(row);
            }

            if (its.Rows.Count < 5)
            {
                for (int i = 0; i < (5 - its.Rows.Count); i++)
                {
                    TableRow rVacia = new TableRow();
                    rVacia.Cells.Add(Celda(" ")); rVacia.Cells.Add(Celda(" ")); rVacia.Cells.Add(Celda(" ")); rVacia.Cells.Add(Celda(" ")); rVacia.Cells.Add(Celda(" "));
                    rg.Rows.Add(rVacia);
                }
            }

            t.RowGroups.Add(rg);
            doc.Blocks.Add(t);

            // Pie
            Grid gPie = new Grid { Margin = new Thickness(0, 20, 0, 0) };
            gPie.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(2, GridUnitType.Star) });
            gPie.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

            StackPanel sQr = new StackPanel();
            if (qr != null) { qr.Width = 100; qr.HorizontalAlignment = HorizontalAlignment.Left; sQr.Children.Add(qr); }
            if (!string.IsNullOrEmpty(pie)) sQr.Children.Add(new TextBlock { Text = pie, FontWeight = FontWeights.Bold, Margin = new Thickness(0, 5, 0, 0) });
            Grid.SetColumn(sQr, 0);

            StackPanel sTot = new StackPanel();
            sTot.Children.Add(new TextBlock { Text = $"SUBTOTAL: {tot:C2}", HorizontalAlignment = HorizontalAlignment.Right });
            sTot.Children.Add(new TextBlock { Text = $"TOTAL: {tot:C2}", FontWeight = FontWeights.Bold, FontSize = 20, HorizontalAlignment = HorizontalAlignment.Right, Margin = new Thickness(0, 10, 0, 0) });
            Grid.SetColumn(sTot, 1);

            gPie.Children.Add(sQr); gPie.Children.Add(sTot);
            doc.Blocks.Add(new BlockUIContainer(gPie));
        }
        #endregion

        #region Helpers
        private static TableCell Celda(string t, TextAlignment a = TextAlignment.Left)
        {
            return new TableCell(new Paragraph(new Run(t)) { TextAlignment = a }) { BorderBrush = Brushes.Gray, BorderThickness = new Thickness(0, 0, 0, 0.5), Padding = new Thickness(2, 5, 2, 5) };
        }

        // CORREGIDO: Eliminado "columna:" que causaba el error
        private static string GetValor(DataRow r, string c, string def = "")
        {
            if (r.Table.Columns.Contains(c) && r[c] != DBNull.Value) return r[c].ToString();
            if (c == "Descripcion" && r.Table.Columns.Contains("Producto")) return r["Producto"].ToString();
            return def;
        }

        private static decimal GetValorDecimal(DataRow r, string c)
        {
            if (r.Table.Columns.Contains(c) && r[c] != DBNull.Value && decimal.TryParse(r[c].ToString(), out decimal d)) return d;
            return 0;
        }
        private static string GetCodigoComprobante(string l) { return l == "A" ? "001" : (l == "B" ? "006" : "000"); }

        private static Image IntentarGenerarQR(DateTime fecha, string numeroDoc, decimal total, string letra, string cae)
        {
            try
            {
                DataRow conf = DatabaseService.GetConfiguracion();
                if (conf != null)
                {
                    long cuit = long.Parse(GetValor(conf, "CUIT").Replace("-", "").Replace(" ", ""));
                    int pto = Convert.ToInt32(GetValor(conf, "PuntoVenta", "1"));
                    int tipo = letra == "A" ? 1 : 6;
                    int nro = int.TryParse(numeroDoc, out int n) ? n : 0;

                    var datos = new { ver = 1, fecha = fecha.ToString("yyyy-MM-dd"), cuit = cuit, ptoVta = pto, tipoCmp = tipo, nroCmp = nro, importe = total, moneda = "PES", ctz = 1, tipoDocRec = 99, nroDocRec = 0, tipoCodAut = "E", codAut = long.Parse(cae) };
                    string json = JsonConvert.SerializeObject(datos);
                    string base64 = Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes(json));
                    string url = $"https://www.afip.gob.ar/fe/qr/?p={base64}";

                    QRCodeGenerator qrGen = new QRCodeGenerator();
                    QRCodeData qrData = qrGen.CreateQrCode(url, QRCodeGenerator.ECCLevel.M);
                    QRCode qrCode = new QRCode(qrData);
                    WinDrawing.Bitmap qrBmp = qrCode.GetGraphic(5);

                    using (MemoryStream ms = new MemoryStream())
                    {
                        qrBmp.Save(ms, System.Drawing.Imaging.ImageFormat.Png);
                        ms.Position = 0;
                        BitmapImage bi = new BitmapImage();
                        bi.BeginInit();
                        bi.StreamSource = ms;
                        bi.CacheOption = BitmapCacheOption.OnLoad;
                        bi.EndInit();
                        RenderOptions.SetBitmapScalingMode(bi, BitmapScalingMode.NearestNeighbor);
                        return new Image { Source = bi, Stretch = Stretch.Uniform };
                    }
                }
            }
            catch { }
            return null;
        }

        private static Image ObtenerImagenLogo()
        {
            try
            {
                DataRow c = DatabaseService.GetConfiguracion();
                if (c != null)
                {
                    string p = GetValor(c, "LogoPath");
                    if (!string.IsNullOrEmpty(p) && File.Exists(p))
                    {
                        BitmapImage bi = new BitmapImage();
                        bi.BeginInit(); bi.UriSource = new Uri(p); bi.CacheOption = BitmapCacheOption.OnLoad; bi.EndInit();
                        return new Image { Source = bi, Stretch = Stretch.Uniform };
                    }
                }
            }
            catch { }
            return null;
        }
        #endregion
    }
}