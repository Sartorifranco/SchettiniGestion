using Newtonsoft.Json;
using QRCoder;
using SchettiniGestion;
using System;
using System.Data;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using WinDrawing = System.Drawing;
using WinPrinting = System.Drawing.Printing;

namespace SchettiniGestion.WPF
{
    public static class PrintService
    {
        private static bool USAR_MOTOR_GRAFICO_PARA_TICKETS = true;

        #region MÉTODOS PÚBLICOS
        public static void ImprimirPresupuesto(int presupuestoID)
        {
            try
            {
                DataRow cabecera = DatabaseService.GetPresupuestoPorID(presupuestoID);
                if (cabecera == null) { MessageBox.Show("Error: No se encontró el presupuesto."); return; }
                DataTable items = DatabaseService.GetPresupuestoDetalle(presupuestoID);
                GenerarDocumentoA4ConItems("PRESUPUESTO", "PresupuestoID", cabecera, items,
                    Convert.ToDecimal(cabecera["Total"]), "Documento no válido como factura fiscal.");
            }
            catch (Exception ex) { MessageBox.Show("Error crítico al imprimir: " + ex.Message); }
        }

        public static void ImprimirRemito(int remitoID)
        {
            try
            {
                DataRow cabecera = DatabaseService.GetRemitoPorID(remitoID);
                if (cabecera == null) { MessageBox.Show("Error: No se encontró el remito."); return; }
                DataTable items = DatabaseService.GetRemitoDetalle(remitoID);
                decimal total = items.Rows.Count > 0
                    ? items.AsEnumerable().Sum(r => Convert.ToDecimal(r["Subtotal"]))
                    : 0m;
                GenerarDocumentoA4ConItems("REMITO", "RemitoID", cabecera, items, total,
                    "Comprobante de entrega. No válido como factura fiscal.");
            }
            catch (Exception ex) { MessageBox.Show("Error crítico al imprimir: " + ex.Message); }
        }

        public static void ImprimirPedido(int pedidoID)
        {
            try
            {
                DataRow cabecera = DatabaseService.GetPedidoPorID(pedidoID);
                if (cabecera == null) { MessageBox.Show("Error: No se encontró el pedido."); return; }
                DataTable items = DatabaseService.GetPedidoDetalle(pedidoID);
                string extra = cabecera["FechaEntrega"] != DBNull.Value
                    ? $"Entrega prevista: {Convert.ToDateTime(cabecera["FechaEntrega"]):dd/MM/yyyy}"
                    : null;
                GenerarDocumentoA4ConItems("PEDIDO", "PedidoID", cabecera, items,
                    Convert.ToDecimal(cabecera["Total"]), "Pedido de venta. No válido como factura fiscal.", extra);
            }
            catch (Exception ex) { MessageBox.Show("Error crítico al imprimir: " + ex.Message); }
        }

        public static void ImprimirNotaCreditoDebitoVenta(int notaID)
        {
            try
            {
                DataRow cabecera = DatabaseService.GetNotaVentaPorID(notaID);
                if (cabecera == null) { MessageBox.Show("Error: No se encontró la nota."); return; }
                string tipo = cabecera["Tipo"]?.ToString() ?? "NC";
                string titulo = tipo == "ND" ? "NOTA DE DÉBITO" : "NOTA DE CRÉDITO";
                GenerarDocumentoA4Nota(titulo, cabecera);
            }
            catch (Exception ex) { MessageBox.Show("Error crítico al imprimir: " + ex.Message); }
        }

        // --- ACTUALIZADO: PARAMETROS CAE Y VTO ---
        public static void ImprimirTicketVenta(string tipo, int nro, string cli, DateTime fec, DataTable items, decimal tot, string cond, string cae = "", string vtoCae = "")
        {
            string letra = "X";
            if (tipo != null) { if (tipo.Contains("A")) letra = "A"; if (tipo.Contains("B")) letra = "B"; if (tipo.Contains("C")) letra = "C"; }

            string tit = tipo?.ToUpper() ?? "TICKET";
            string nroStr = nro > 0 ? nro.ToString("D8") : "(Pendiente)";

            // Armar Pie Fiscal
            string pie = "";
            // Si venía en cond (versión vieja) o en parámetro nuevo
            if (cond != null && cond.Contains("CAE:"))
            {
                string[] p = cond.Split(new[] { "CAE:" }, StringSplitOptions.None);
                cond = p[0].Trim();
                if (p.Length > 1) pie = "CAE: " + p[1].Trim();
            }
            else if (!string.IsNullOrEmpty(cae))
            {
                pie = $"CAE: {cae}\nVto CAE: {vtoCae}";
            }

            if (USAR_MOTOR_GRAFICO_PARA_TICKETS)
                ImprimirTicketGrafico(tit, nroStr, cli, fec, items, tot, cond, letra, pie);
            else
                MessageBox.Show("Motor A4 no activo.");
        }

        // --- NUEVO: IMPRIMIR CIERRE DE CAJA (Z) ---
        public static void ImprimirCierreZ(DateTime fecha, System.Collections.Generic.Dictionary<string, decimal> totales, decimal totalFinal)
        {
            try
            {
                WinPrinting.PrintDocument doc = new WinPrinting.PrintDocument();
                doc.PrintController = new WinPrinting.StandardPrintController();
                doc.PrintPage += (s, e) =>
                {
                    var g = e.Graphics;
                    float w = 290; float y = 0;
                    WinDrawing.Font fT = new WinDrawing.Font("Arial", 12, WinDrawing.FontStyle.Bold);
                    WinDrawing.Font fN = new WinDrawing.Font("Consolas", 9);
                    WinDrawing.Font fB = new WinDrawing.Font("Arial", 14, WinDrawing.FontStyle.Bold);

                    DibujarTextoCentrado(g, "CIERRE DE CAJA (Z)", fT, w, ref y);
                    g.DrawString($"Fecha: {fecha:dd/MM/yyyy}", fN, WinDrawing.Brushes.Black, 0, y); y += 15;
                    g.DrawString($"Impreso: {DateTime.Now:HH:mm}", fN, WinDrawing.Brushes.Black, 0, y); y += 15;
                    DibujarLinea(g, ref y, w);

                    foreach (var item in totales)
                    {
                        g.DrawString(item.Key, fN, WinDrawing.Brushes.Black, 0, y);
                        g.DrawString(item.Value.ToString("C2"), fN, WinDrawing.Brushes.Black, w - 70, y);
                        y += 20;
                    }

                    DibujarLinea(g, ref y, w);
                    y += 5;
                    g.DrawString("TOTAL RECAUDADO:", fT, WinDrawing.Brushes.Black, 0, y); y += 25;
                    DibujarTextoCentrado(g, totalFinal.ToString("C2"), fB, w, ref y);
                    y += 20;
                    DibujarTextoCentrado(g, ".", fN, w, ref y);
                };

                System.Windows.Forms.PrintDialog pd = new System.Windows.Forms.PrintDialog();
                pd.Document = doc;
                if (pd.ShowDialog() == System.Windows.Forms.DialogResult.OK) doc.Print();
            }
            catch (Exception ex) { MessageBox.Show("Error imprimiendo Z: " + ex.Message); }
        }
        // ------------------------------------------
        #endregion

        private static void GenerarDocumentoA4ConItems(string tituloDocumento, string idColumn, DataRow cabecera, DataTable items, decimal total, string pieLegal, string lineaExtra = null)
        {
            try
            {
                FlowDocument doc = CrearDocumentoBase();
                doc.Blocks.Add(CrearEncabezadoDocumento(tituloDocumento, idColumn, cabecera, lineaExtra));
                doc.Blocks.Add(CrearBloqueCliente(cabecera));
                doc.Blocks.Add(CrearTablaItems(items));
                doc.Blocks.Add(CrearBloqueTotal(total));
                doc.Blocks.Add(new Paragraph(new Run(pieLegal)) { TextAlignment = TextAlignment.Center, FontSize = 10, Foreground = Brushes.Gray, Margin = new Thickness(0, 40, 0, 0) });
                MostrarDialogoImpresion(doc, $"{tituloDocumento}_{cabecera[idColumn]}");
            }
            catch (Exception ex) { MessageBox.Show("Error generando PDF: " + ex.Message); }
        }

        private static void GenerarDocumentoA4Nota(string tituloDocumento, DataRow cabecera)
        {
            try
            {
                FlowDocument doc = CrearDocumentoBase();
                doc.Blocks.Add(CrearEncabezadoDocumento(tituloDocumento, "NotaID", cabecera));
                doc.Blocks.Add(CrearBloqueCliente(cabecera));

                Paragraph pDetalle = new Paragraph { FontSize = 12, Margin = new Thickness(0, 10, 0, 10) };
                pDetalle.Inlines.Add(new Run("Descripción: ") { FontWeight = FontWeights.Bold });
                pDetalle.Inlines.Add(new Run(cabecera["Descripcion"]?.ToString() ?? "—"));
                if (cabecera["NumeroComprobante"] != DBNull.Value && !string.IsNullOrWhiteSpace(cabecera["NumeroComprobante"].ToString()))
                {
                    pDetalle.Inlines.Add(new LineBreak());
                    pDetalle.Inlines.Add(new Run("Comprobante asociado: ") { FontWeight = FontWeights.Bold });
                    pDetalle.Inlines.Add(new Run(cabecera["NumeroComprobante"].ToString()));
                }
                doc.Blocks.Add(pDetalle);

                doc.Blocks.Add(CrearBloqueTotal(Convert.ToDecimal(cabecera["Monto"])));
                doc.Blocks.Add(new Paragraph(new Run("Documento no válido como factura fiscal.")) { TextAlignment = TextAlignment.Center, FontSize = 10, Foreground = Brushes.Gray, Margin = new Thickness(0, 40, 0, 0) });
                MostrarDialogoImpresion(doc, $"{tituloDocumento}_{cabecera["NotaID"]}");
            }
            catch (Exception ex) { MessageBox.Show("Error generando PDF: " + ex.Message); }
        }

        private static FlowDocument CrearDocumentoBase()
        {
            return new FlowDocument
            {
                PagePadding = new Thickness(40),
                ColumnWidth = double.PositiveInfinity,
                FontFamily = new FontFamily("Arial"),
                FontSize = 11,
                PageWidth = 793
            };
        }

        private static Block CrearEncabezadoDocumento(string tituloDocumento, string idColumn, DataRow cabecera, string lineaExtra = null)
        {
            Table headerTable = new Table();
            headerTable.CellSpacing = 0;
            headerTable.Columns.Add(new TableColumn() { Width = new GridLength(450) });
            headerTable.Columns.Add(new TableColumn() { Width = new GridLength(250) });

            TableRow rowH = new TableRow();
            TableCell cellLogo = new TableCell();
            ImageSource logoSource = SvgLogoHelper.LoadEmbeddedLogo();
            if (logoSource == null)
            {
                string rutaLogo = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "logo.png");
                if (File.Exists(rutaLogo))
                {
                    try
                    {
                        BitmapImage bi = new BitmapImage();
                        bi.BeginInit();
                        bi.UriSource = new Uri(rutaLogo);
                        bi.CacheOption = BitmapCacheOption.OnLoad;
                        bi.EndInit();
                        logoSource = bi;
                    }
                    catch { }
                }
            }

            if (logoSource != null)
            {
                try
                {
                    Image img = new Image { Source = logoSource, Width = 90, HorizontalAlignment = HorizontalAlignment.Left, Stretch = Stretch.Uniform };
                    cellLogo.Blocks.Add(new BlockUIContainer(img));
                }
                catch { cellLogo.Blocks.Add(new Paragraph(new Run("SchTec"))); }
            }
            else
            {
                cellLogo.Blocks.Add(new Paragraph(new Run("SchTec")) { FontSize = 30, FontWeight = FontWeights.Bold, Foreground = Brushes.DarkBlue });
            }
            rowH.Cells.Add(cellLogo);

            Paragraph pDatos = new Paragraph { TextAlignment = TextAlignment.Right };
            pDatos.Inlines.Add(new Run(tituloDocumento) { FontSize = 18, FontWeight = FontWeights.Bold });
            pDatos.Inlines.Add(new LineBreak());
            pDatos.Inlines.Add(new Run($"Nº: {int.Parse(cabecera[idColumn].ToString()):D8}") { FontSize = 14, FontWeight = FontWeights.Bold });
            pDatos.Inlines.Add(new LineBreak());
            pDatos.Inlines.Add(new Run($"Fecha: {Convert.ToDateTime(cabecera["Fecha"]):dd/MM/yyyy}") { FontSize = 12 });
            if (!string.IsNullOrWhiteSpace(lineaExtra))
            {
                pDatos.Inlines.Add(new LineBreak());
                pDatos.Inlines.Add(new Run(lineaExtra) { FontSize = 12 });
            }
            rowH.Cells.Add(new TableCell(pDatos));

            var headerGroup = new TableRowGroup();
            headerGroup.Rows.Add(rowH);
            headerTable.RowGroups.Add(headerGroup);

            var contenedor = new Section();
            contenedor.Blocks.Add(headerTable);
            contenedor.Blocks.Add(new BlockUIContainer(new Separator { Margin = new Thickness(0, 10, 0, 10), Background = Brushes.Black, Height = 1 }));
            return contenedor;
        }

        private static Block CrearBloqueCliente(DataRow cabecera)
        {
            var section = new Section();
            Paragraph pCliente = new Paragraph { FontSize = 11 };
            pCliente.Inlines.Add(new Run("CLIENTE: ") { FontWeight = FontWeights.Bold });
            pCliente.Inlines.Add(new Run(cabecera["ClienteNombre"].ToString().ToUpper()));
            pCliente.Inlines.Add(new LineBreak());
            pCliente.Inlines.Add(new Run($"CUIT: {cabecera["ClienteCUIT"]}    |    IVA: {cabecera["ClienteIVA"]}"));
            string dir = cabecera["ClienteDireccion"].ToString();
            if (dir != "-") pCliente.Inlines.Add(new Run($"    |    Dir: {dir}"));
            section.Blocks.Add(pCliente);
            section.Blocks.Add(new BlockUIContainer(new Separator { Margin = new Thickness(0, 5, 0, 15), Background = Brushes.LightGray }));
            return section;
        }

        private static Block CrearTablaItems(DataTable items)
        {
            Table table = new Table
            {
                CellSpacing = 0,
                BorderBrush = Brushes.Gray,
                BorderThickness = new Thickness(0, 1, 0, 1),
                Margin = new Thickness(0, 0, 0, 15)
            };
            table.Columns.Add(new TableColumn() { Width = new GridLength(50) });
            table.Columns.Add(new TableColumn() { Width = new GridLength(360) });
            table.Columns.Add(new TableColumn() { Width = new GridLength(100) });
            table.Columns.Add(new TableColumn() { Width = new GridLength(100) });

            TableRowGroup groupData = new TableRowGroup();
            TableRow rowTitulos = new TableRow { Background = Brushes.LightGray };
            rowTitulos.Cells.Add(CrearCelda("CANT", TextAlignment.Center, true));
            rowTitulos.Cells.Add(CrearCelda("DESCRIPCIÓN", TextAlignment.Left, true));
            rowTitulos.Cells.Add(CrearCelda("UNITARIO", TextAlignment.Right, true));
            rowTitulos.Cells.Add(CrearCelda("TOTAL", TextAlignment.Right, true));
            groupData.Rows.Add(rowTitulos);

            foreach (DataRow item in items.Rows)
            {
                TableRow r = new TableRow();
                r.Cells.Add(CrearCelda(item["Cantidad"].ToString(), TextAlignment.Center));
                r.Cells.Add(CrearCelda(item["Descripcion"].ToString(), TextAlignment.Left));
                r.Cells.Add(CrearCelda(Convert.ToDecimal(item["PrecioUnitario"]).ToString("C2"), TextAlignment.Right));
                r.Cells.Add(CrearCelda(Convert.ToDecimal(item["Subtotal"]).ToString("C2"), TextAlignment.Right));
                groupData.Rows.Add(r);
            }
            table.RowGroups.Add(groupData);
            return table;
        }

        private static Paragraph CrearBloqueTotal(decimal total)
        {
            Paragraph pTotal = new Paragraph { TextAlignment = TextAlignment.Right, Margin = new Thickness(0, 10, 0, 0) };
            pTotal.Inlines.Add(new Run("TOTAL:  ") { FontSize = 14, FontWeight = FontWeights.SemiBold });
            pTotal.Inlines.Add(new Run(total.ToString("C2")) { FontSize = 22, FontWeight = FontWeights.Bold });
            return pTotal;
        }

        private static void MostrarDialogoImpresion(FlowDocument doc, string jobName)
        {
            PrintDialog pd = new PrintDialog();
            if (pd.ShowDialog() == true)
            {
                doc.PageHeight = pd.PrintableAreaHeight;
                doc.PageWidth = pd.PrintableAreaWidth;
                doc.PagePadding = new Thickness(40);
                doc.ColumnGap = 0;
                doc.ColumnWidth = pd.PrintableAreaWidth;
                IDocumentPaginatorSource dps = doc;
                pd.PrintDocument(dps.DocumentPaginator, jobName);
            }
        }

        private static TableCell CrearCelda(string texto, TextAlignment alineacion, bool negrita = false)
        {
            var p = new Paragraph(new Run(texto)); p.TextAlignment = alineacion; if (negrita) p.FontWeight = FontWeights.Bold;
            return new TableCell(p) { Padding = new Thickness(5) };
        }

        private static void ImprimirTicketGrafico(string t, string n, string c, DateTime f, DataTable i, decimal tot, string extra, string l, string pie)
        {
            try
            {
                WinPrinting.PrintDocument doc = new WinPrinting.PrintDocument();
                doc.PrintController = new WinPrinting.StandardPrintController();
                doc.PrintPage += (s, e) => { DibujarTicketGDI(e.Graphics, t, n, c, f, i, tot, extra, l, pie); };
                System.Windows.Forms.PrintDialog pd = new System.Windows.Forms.PrintDialog();
                pd.Document = doc;
                if (pd.ShowDialog() == System.Windows.Forms.DialogResult.OK) doc.Print();
            }
            catch (Exception x) { MessageBox.Show("Error Ticket: " + x.Message); }
        }

        private static void DibujarTicketGDI(WinDrawing.Graphics g, string tit, string nro, string cli, DateTime fec, DataTable its, decimal tot, string extra, string let, string pie)
        {
            g.InterpolationMode = WinDrawing.Drawing2D.InterpolationMode.NearestNeighbor;
            float w = 290; float y = 0;
            WinDrawing.Font fT = new WinDrawing.Font("Arial", 10, WinDrawing.FontStyle.Bold);
            WinDrawing.Font fN = new WinDrawing.Font("Consolas", 8);
            WinDrawing.Font fC = new WinDrawing.Font("Consolas", 7);
            WinDrawing.Font fB = new WinDrawing.Font("Arial", 14, WinDrawing.FontStyle.Bold);

            DataRow conf = DatabaseService.GetConfiguracion();
            string fan = conf != null ? conf["NombreFantasia"].ToString() : "SchTec";
            DibujarTextoCentrado(g, fan.ToUpper(), fT, w, ref y);
            DibujarLinea(g, ref y, w);

            g.DrawString($"{tit} Letra: {let}", fN, WinDrawing.Brushes.Black, 0, y); y += 15;
            g.DrawString($"Nro: {nro}", fN, WinDrawing.Brushes.Black, 0, y); y += 15;
            g.DrawString($"Fecha: {fec:dd/MM/yyyy HH:mm}", fN, WinDrawing.Brushes.Black, 0, y); y += 15;
            if (cli.Length > 35) cli = cli.Substring(0, 35);
            g.DrawString($"Cli: {cli}", fN, WinDrawing.Brushes.Black, 0, y); y += 15;
            DibujarLinea(g, ref y, w);

            foreach (DataRow r in its.Rows)
            {
                string d = r.Table.Columns.Contains("Descripcion") ? r["Descripcion"].ToString() : r["Producto"].ToString();
                if (d.Length > 22) d = d.Substring(0, 22);
                g.DrawString($"{r["Cantidad"]} x {d}", fN, WinDrawing.Brushes.Black, 0, y);
                g.DrawString(Convert.ToDecimal(r["Subtotal"]).ToString("N2"), fN, WinDrawing.Brushes.Black, w - 50, y);
                y += 15;
            }
            DibujarLinea(g, ref y, w);

            y += 5; g.DrawString("TOTAL:", fT, WinDrawing.Brushes.Black, 10, y);
            g.DrawString($"${tot:N2}", fB, WinDrawing.Brushes.Black, 130, y - 2); y += 30;

            if (!string.IsNullOrEmpty(extra))
            {
                if (extra.Length > 35) { g.DrawString("Pago: " + extra.Substring(0, 35), fC, WinDrawing.Brushes.Black, 0, y); y += 12; g.DrawString(extra.Substring(35), fC, WinDrawing.Brushes.Black, 0, y); y += 12; }
                else { g.DrawString("Pago: " + extra, fC, WinDrawing.Brushes.Black, 0, y); y += 15; }
            }

            if (!string.IsNullOrEmpty(pie)) { DibujarLinea(g, ref y, w); DibujarTextoCentrado(g, pie, fC, w, ref y); }

            y += 10;
            DibujarTextoCentrado(g, "Gracias por su compra", fC, w, ref y);
            DibujarTextoCentrado(g, ".", fC, w, ref y);
        }

        private static void DibujarLinea(WinDrawing.Graphics g, ref float y, float w) { y += 3; g.DrawLine(new WinDrawing.Pen(WinDrawing.Color.Black) { DashStyle = WinDrawing.Drawing2D.DashStyle.Dash }, 2, y, w - 2, y); y += 5; }
        private static void DibujarTextoCentrado(WinDrawing.Graphics g, string t, WinDrawing.Font f, float w, ref float y) { WinDrawing.SizeF s = g.MeasureString(t, f); g.DrawString(t, f, WinDrawing.Brushes.Black, (w - s.Width) / 2, y); y += s.Height; }
    }
}