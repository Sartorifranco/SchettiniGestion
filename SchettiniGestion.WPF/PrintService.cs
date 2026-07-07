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

        public static void ImprimirTicketVenta(string tipo, int nro, string cli, DateTime fec, DataTable items, decimal tot, string cond, string cae = "", string vtoCae = "")
        {
            string letra = "B";
            if (tipo != null)
            {
                if (tipo.IndexOf("Factura", StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    string cuitCli = cli?.Replace("-", "").Trim() ?? "";
                    letra = (cuitCli.Length >= 11 && !cuitCli.Contains("00000000")) ? "A" : "B";
                }
                else if (tipo.IndexOf("Ticket", StringComparison.OrdinalIgnoreCase) >= 0)
                    letra = "B";
                else if (tipo.Contains("A")) letra = "A";
                else if (tipo.Contains("B")) letra = "B";
                else if (tipo.Contains("C")) letra = "C";
            }

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

        public static void ImprimirFactura(int facturaId)
        {
            try
            {
                DataRow cab = DatabaseService.GetFacturaPorID(facturaId);
                if (cab == null) { MessageBox.Show("No se encontró la factura."); return; }
                DataTable items = DatabaseService.GetFacturaDetalle(facturaId);
                string tipo = cab["TipoComprobante"]?.ToString() ?? "Ticket";
                int nro = cab["NumeroComprobanteAFIP"] != DBNull.Value && cab["NumeroComprobanteAFIP"] != null
                    ? Convert.ToInt32(cab["NumeroComprobanteAFIP"]) : facturaId;
                string cli = cab["ClienteNombre"]?.ToString() ?? "";
                DateTime fec = Convert.ToDateTime(cab["Fecha"]);
                decimal tot = Convert.ToDecimal(cab["Total"]);
                string cond = cab.Table.Columns.Contains("CondicionTicket") ? cab["CondicionTicket"]?.ToString() ?? "" : "";
                string cae = cab["CAE"]?.ToString() ?? "";
                string vto = cab["VencimientoCAE"]?.ToString() ?? "";

                if (tipo.Equals("Factura", StringComparison.OrdinalIgnoreCase))
                {
                    string extra = !string.IsNullOrEmpty(cae) ? $"CAE: {cae}  Vto: {vto}" : null;
                    GenerarDocumentoA4ConItems("FACTURA", "FacturaID", cab, items, tot,
                        "Comprobante fiscal.", extra);
                    return;
                }

                ImprimirTicketVenta(tipo, nro, cli, fec, items, tot, cond, cae, vto);
            }
            catch (Exception ex) { MessageBox.Show("Error al imprimir factura: " + ex.Message); }
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

                var (impresoraTicket, _) = DatabaseService.GetImpresoras();
                ImprimirDocumentoTicket(doc, impresoraTicket);
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
            DataRow conf = DatabaseService.GetConfiguracion();
            string nombreFantasia = conf?["NombreFantasia"]?.ToString() ?? "";
            string razonSocial    = conf?["RazonSocial"]?.ToString() ?? "";
            string cuit           = conf?["CUIT"]?.ToString() ?? "";
            string dir            = conf?["Direccion"]?.ToString() ?? "";
            string tel            = conf?["Telefono"]?.ToString() ?? "";
            string email          = conf?["Email"]?.ToString() ?? "";

            bool mostrarLogo = conf != null
                && conf.Table.Columns.Contains("LogoEnA4")
                && conf["LogoEnA4"] != DBNull.Value
                && Convert.ToBoolean(conf["LogoEnA4"]);
            string logoPath = (conf != null && conf.Table.Columns.Contains("LogoPath"))
                ? conf["LogoPath"]?.ToString() ?? "" : "";

            var contenedor = new Section();

            // ══════════════════════════════════════════════════════
            //  ENCABEZADO — tabla de 2 columnas (empresa | documento)
            // ══════════════════════════════════════════════════════
            Table tbl = new Table { CellSpacing = 0, BorderThickness = new Thickness(0), Margin = new Thickness(0, 0, 0, 0) };
            tbl.Columns.Add(new TableColumn { Width = new GridLength(2, GridUnitType.Star) });
            tbl.Columns.Add(new TableColumn { Width = new GridLength(1, GridUnitType.Star) });

            var rg  = new TableRowGroup();
            var row = new TableRow();

            // ── Celda izquierda: logo + datos de empresa ──────────
            var cellLeft = new TableCell { Padding = new Thickness(0, 0, 16, 0) };

            // Logo via MemoryStream + Freeze — única forma confiable en contexto de impresión FlowDocument
            ImageSource logoSrc = CargarImagenParaImpresion(mostrarLogo ? logoPath : null);
            if (logoSrc != null)
                cellLeft.Blocks.Add(new BlockUIContainer(
                    new Image { Source = logoSrc, Height = 72, HorizontalAlignment = HorizontalAlignment.Left, Stretch = Stretch.Uniform })
                { Margin = new Thickness(0, 0, 0, 6) });

            // Nombre fantasía
            if (!string.IsNullOrWhiteSpace(nombreFantasia))
                cellLeft.Blocks.Add(new Paragraph(new Run(nombreFantasia.ToUpper()))
                { FontSize = 20, FontWeight = FontWeights.Bold, Foreground = Brushes.Black, Margin = new Thickness(0, 0, 0, 3) });

            // Datos fiscales / contacto
            var pInfo = new Paragraph { FontSize = 10, LineHeight = 16, Foreground = Brushes.DimGray, Margin = new Thickness(0) };
            void AgregarLinea(string texto) { if (!string.IsNullOrWhiteSpace(texto)) { if (pInfo.Inlines.Count > 0) pInfo.Inlines.Add(new LineBreak()); pInfo.Inlines.Add(new Run(texto)); } }
            if (!string.IsNullOrWhiteSpace(razonSocial) && razonSocial != nombreFantasia) AgregarLinea(razonSocial);
            if (!string.IsNullOrWhiteSpace(cuit))   AgregarLinea($"CUIT: {cuit}");
            if (!string.IsNullOrWhiteSpace(dir))    AgregarLinea(dir);
            if (!string.IsNullOrWhiteSpace(tel))    AgregarLinea($"Tel: {tel}");
            if (!string.IsNullOrWhiteSpace(email))  AgregarLinea(email);
            if (pInfo.Inlines.Count > 0) cellLeft.Blocks.Add(pInfo);

            row.Cells.Add(cellLeft);

            // ── Celda derecha: tipo + número + fecha ─────────────
            //    Usamos solo Paragraph/Run (sin Border/StackPanel)
            var cellRight = new TableCell
            {
                Padding         = new Thickness(14, 8, 14, 8),
                Background      = new SolidColorBrush(Color.FromRgb(30, 58, 138)),
                BorderThickness = new Thickness(0),
                TextAlignment   = TextAlignment.Right
            };

            cellRight.Blocks.Add(new Paragraph(new Run(tituloDocumento.ToUpper()))
            { FontSize = 17, FontWeight = FontWeights.Bold, Foreground = Brushes.White, Margin = new Thickness(0), TextAlignment = TextAlignment.Right });

            cellRight.Blocks.Add(new Paragraph(new Run($"N°  {int.Parse(cabecera[idColumn].ToString()):D8}"))
            { FontSize = 13, FontWeight = FontWeights.SemiBold, Foreground = new SolidColorBrush(Color.FromRgb(186, 230, 253)), Margin = new Thickness(0, 4, 0, 0), TextAlignment = TextAlignment.Right });

            cellRight.Blocks.Add(new Paragraph(new Run($"{Convert.ToDateTime(cabecera["Fecha"]):dd/MM/yyyy}"))
            { FontSize = 11, Foreground = new SolidColorBrush(Color.FromRgb(147, 197, 253)), Margin = new Thickness(0, 3, 0, 0), TextAlignment = TextAlignment.Right });

            if (!string.IsNullOrWhiteSpace(lineaExtra))
                cellRight.Blocks.Add(new Paragraph(new Run(lineaExtra))
                { FontSize = 10, Foreground = new SolidColorBrush(Color.FromRgb(147, 197, 253)), Margin = new Thickness(0, 3, 0, 0), TextAlignment = TextAlignment.Right });

            row.Cells.Add(cellRight);
            rg.Rows.Add(row);
            tbl.RowGroups.Add(rg);
            contenedor.Blocks.Add(tbl);

            // Línea divisoria azul — usando TableCell con fondo (BorderThickness no funciona en Row)
            Table tblLinea = new Table { CellSpacing = 0 };
            tblLinea.Columns.Add(new TableColumn());
            var rgL = new TableRowGroup();
            var rowL = new TableRow();
            var cellL = new TableCell
            {
                Background = new SolidColorBrush(Color.FromRgb(30, 58, 138)),
                Padding    = new Thickness(0, 2, 0, 2)
            };
            cellL.Blocks.Add(new Paragraph() { Margin = new Thickness(0) });
            rowL.Cells.Add(cellL);
            rgL.Rows.Add(rowL);
            tblLinea.RowGroups.Add(rgL);
            contenedor.Blocks.Add(tblLinea);

            contenedor.Blocks.Add(new Paragraph() { Margin = new Thickness(0, 10, 0, 0) });
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
            var (_, impresoraA4) = DatabaseService.GetImpresoras();

            if (!string.IsNullOrWhiteSpace(impresoraA4))
            {
                var pd = new PrintDialog();
                bool encontrada = false;
                foreach (var queue in new System.Printing.LocalPrintServer().GetPrintQueues())
                {
                    if (queue.FullName == impresoraA4 || queue.Name == impresoraA4)
                    {
                        pd.PrintQueue = queue;
                        encontrada = true;
                        break;
                    }
                }
                if (!encontrada)
                {
                    MessageBox.Show(
                        $"La impresora A4 configurada no está disponible:\n{impresoraA4}\n\nSeleccione otra impresora.",
                        "Impresora no encontrada", MessageBoxButton.OK, MessageBoxImage.Warning);
                    if (pd.ShowDialog() != true) return;
                }
                doc.PageHeight = pd.PrintableAreaHeight;
                doc.PageWidth  = pd.PrintableAreaWidth;
                doc.PagePadding = new Thickness(40);
                doc.ColumnGap  = 0;
                doc.ColumnWidth = pd.PrintableAreaWidth;
                pd.PrintDocument(((IDocumentPaginatorSource)doc).DocumentPaginator, jobName);
            }
            else
            {
                // Sin impresora configurada: mostrar diálogo
                PrintDialog pd = new PrintDialog();
                if (pd.ShowDialog() == true)
                {
                    doc.PageHeight  = pd.PrintableAreaHeight;
                    doc.PageWidth   = pd.PrintableAreaWidth;
                    doc.PagePadding = new Thickness(40);
                    doc.ColumnGap   = 0;
                    doc.ColumnWidth = pd.PrintableAreaWidth;
                    pd.PrintDocument(((IDocumentPaginatorSource)doc).DocumentPaginator, jobName);
                }
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
                var (impresoraTicket, _) = DatabaseService.GetImpresoras();

                WinPrinting.PrintDocument doc = new WinPrinting.PrintDocument();
                doc.PrintController = new WinPrinting.StandardPrintController();
                doc.PrintPage += (s, e) => { DibujarTicketGDI(e.Graphics, t, n, c ?? "", f, i, tot, extra, l, pie); };

                ImprimirDocumentoTicket(doc, impresoraTicket);
            }
            catch (Exception x) { MessageBox.Show("Error Ticket: " + x.Message); }
        }

        private static void ImprimirDocumentoTicket(WinPrinting.PrintDocument doc, string impresoraTicket)
        {
            if (!string.IsNullOrWhiteSpace(impresoraTicket))
            {
                bool valida = false;
                foreach (string p in WinPrinting.PrinterSettings.InstalledPrinters)
                {
                    if (string.Equals(p, impresoraTicket, StringComparison.OrdinalIgnoreCase))
                    { valida = true; break; }
                }
                if (!valida)
                {
                    MessageBox.Show(
                        $"La impresora de tickets configurada no está disponible:\n{impresoraTicket}\n\nSeleccione otra impresora.",
                        "Impresora no encontrada", MessageBoxButton.OK, MessageBoxImage.Warning);
                    var pd = new System.Windows.Forms.PrintDialog();
                    pd.Document = doc;
                    if (pd.ShowDialog() != System.Windows.Forms.DialogResult.OK) return;
                    doc.Print();
                    return;
                }
                doc.PrinterSettings.PrinterName = impresoraTicket;
                doc.Print();
            }
            else
            {
                var pd = new System.Windows.Forms.PrintDialog();
                pd.Document = doc;
                if (pd.ShowDialog() == System.Windows.Forms.DialogResult.OK) doc.Print();
            }
        }

        public static void ImprimirPaginaDePrueba(string nombreImpresora, string tipo)
        {
            try
            {
                DataRow conf    = DatabaseService.GetConfiguracion();
                string fan      = conf?["NombreFantasia"]?.ToString() ?? "Mi Negocio";
                string dir      = conf?["Direccion"]?.ToString() ?? "";
                string tel      = conf?["Telefono"]?.ToString() ?? "";
                string cuit     = conf?["CUIT"]?.ToString() ?? "";
                bool mostrarLog = tipo == "Ticket"
                    ? (conf != null && conf.Table.Columns.Contains("LogoEnTicket") && conf["LogoEnTicket"] != DBNull.Value && Convert.ToBoolean(conf["LogoEnTicket"]))
                    : (conf != null && conf.Table.Columns.Contains("LogoEnA4")     && conf["LogoEnA4"]     != DBNull.Value && Convert.ToBoolean(conf["LogoEnA4"]));
                string logoPath = (conf != null && conf.Table.Columns.Contains("LogoPath")) ? conf["LogoPath"]?.ToString() ?? "" : "";

                WinPrinting.PrintDocument doc = new WinPrinting.PrintDocument();
                doc.PrinterSettings.PrinterName = nombreImpresora;
                doc.PrintController = new WinPrinting.StandardPrintController();
                doc.PrintPage += (s, e) =>
                {
                    var   g = e.Graphics;
                    g.SmoothingMode     = WinDrawing.Drawing2D.SmoothingMode.HighQuality;
                    g.InterpolationMode = WinDrawing.Drawing2D.InterpolationMode.HighQualityBicubic;
                    float w  = tipo == "Ticket" ? 290 : 700;
                    float y  = 14;
                    var   fT = new WinDrawing.Font("Arial", 10, WinDrawing.FontStyle.Bold);
                    var   fN = new WinDrawing.Font("Arial", 8);
                    var   fS = new WinDrawing.Font("Arial", 7);

                    // Logo con GDI+ (funciona en ambos tipos)
                    if (mostrarLog && !string.IsNullOrWhiteSpace(logoPath) && System.IO.File.Exists(logoPath))
                    {
                        try
                        {
                            byte[] logoBytes = System.IO.File.ReadAllBytes(logoPath);
                            using (var ms2 = new System.IO.MemoryStream(logoBytes))
                            using (var bmp = new WinDrawing.Bitmap(ms2))
                            {
                                float maxH = tipo == "Ticket" ? 50f : 80f;
                                float ratio = bmp.Width / (float)bmp.Height;
                                float lh = maxH, lw = lh * ratio;
                                if (lw > w - 20) { lw = w - 20; lh = lw / ratio; }
                                g.DrawImage(bmp, (w - lw) / 2f, y, lw, lh);
                                y += lh + 8f;
                            }
                        }
                        catch { }
                    }

                    DibujarTextoCentrado(g, fan.ToUpper(), fT, w, ref y);
                    if (!string.IsNullOrWhiteSpace(dir))  DibujarTextoCentrado(g, dir, fS, w, ref y);
                    if (!string.IsNullOrWhiteSpace(tel))  DibujarTextoCentrado(g, $"Tel: {tel}", fS, w, ref y);
                    if (!string.IsNullOrWhiteSpace(cuit)) DibujarTextoCentrado(g, $"CUIT: {cuit}", fS, w, ref y);
                    DibujarLinea(g, ref y, w);

                    DibujarTextoCentrado(g, "PÁGINA DE PRUEBA DE IMPRESIÓN", fT, w, ref y);
                    y += 6;
                    DibujarTextoCentrado(g, $"Impresora: {nombreImpresora}", fN, w, ref y);
                    DibujarTextoCentrado(g, $"Fecha: {DateTime.Now:dd/MM/yyyy  HH:mm}", fN, w, ref y);
                    DibujarTextoCentrado(g, "Logo: " + (mostrarLog && System.IO.File.Exists(logoPath) ? "OK" : "No configurado"), fS, w, ref y);
                    DibujarLinea(g, ref y, w);
                    DibujarTextoCentrado(g, "SCHPOS — Configuración correcta", fT, w, ref y);
                };
                doc.Print();
                MessageBox.Show($"Página de prueba enviada a:\n{nombreImpresora}", "Prueba de impresión", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Information);
            }
            catch (Exception ex) { MessageBox.Show("Error al imprimir prueba: " + ex.Message); }
        }

        private static void DibujarTicketGDI(WinDrawing.Graphics g, string tit, string nro, string cli, DateTime fec, DataTable its, decimal tot, string extra, string let, string pie)
        {
            g.SmoothingMode         = WinDrawing.Drawing2D.SmoothingMode.HighQuality;
            g.InterpolationMode     = WinDrawing.Drawing2D.InterpolationMode.HighQualityBicubic;
            float w = 290; float y = 10;

            WinDrawing.Font fT  = new WinDrawing.Font("Arial", 10, WinDrawing.FontStyle.Bold);
            WinDrawing.Font fM  = new WinDrawing.Font("Arial", 8);
            WinDrawing.Font fN  = new WinDrawing.Font("Consolas", 8);
            WinDrawing.Font fC  = new WinDrawing.Font("Consolas", 7);
            WinDrawing.Font fB  = new WinDrawing.Font("Arial", 14, WinDrawing.FontStyle.Bold);
            WinDrawing.Font fSub = new WinDrawing.Font("Arial", 7);

            DataRow conf = DatabaseService.GetConfiguracion();
            string fan   = conf?["NombreFantasia"]?.ToString() ?? "Mi Negocio";
            string dir   = conf?["Direccion"]?.ToString() ?? "";
            string tel   = conf?["Telefono"]?.ToString() ?? "";
            string cuit  = conf?["CUIT"]?.ToString() ?? "";

            bool mostrarLogo = conf != null && conf.Table.Columns.Contains("LogoEnTicket")
                               && conf["LogoEnTicket"] != DBNull.Value && Convert.ToBoolean(conf["LogoEnTicket"]);
            string logoPath  = conf != null && conf.Table.Columns.Contains("LogoPath")
                               ? conf["LogoPath"]?.ToString() ?? "" : "";

            // ── Logo centrado (si existe y está habilitado) ──
            if (mostrarLogo && !string.IsNullOrWhiteSpace(logoPath) && System.IO.File.Exists(logoPath))
            {
                try
                {
                    byte[] logoBytes = System.IO.File.ReadAllBytes(logoPath);
                    using (var ms = new System.IO.MemoryStream(logoBytes))
                    using (var bmp = new WinDrawing.Bitmap(ms))
                    {
                        float maxH  = 55f;
                        float ratio = bmp.Width / (float)bmp.Height;
                        float lh = maxH, lw = lh * ratio;
                        if (lw > w - 20) { lw = w - 20; lh = lw / ratio; }
                        g.DrawImage(bmp, (w - lw) / 2f, y, lw, lh);
                        y += lh + 6f;
                    }
                }
                catch { }
            }

            // ── Nombre fantasía ──
            DibujarTextoCentrado(g, fan.ToUpper(), fT, w, ref y);

            // ── Datos de contacto en gris pequeño ──
            if (!string.IsNullOrWhiteSpace(dir))   DibujarTextoCentrado(g, dir, fSub, w, ref y);
            if (!string.IsNullOrWhiteSpace(tel))    DibujarTextoCentrado(g, $"Tel: {tel}", fSub, w, ref y);
            if (!string.IsNullOrWhiteSpace(cuit))   DibujarTextoCentrado(g, $"CUIT: {cuit}", fSub, w, ref y);

            DibujarLinea(g, ref y, w);

            g.DrawString($"{tit}  —  Letra: {let}", fN, WinDrawing.Brushes.Black, 0, y); y += 14;
            g.DrawString($"N°: {nro}", fN, WinDrawing.Brushes.Black, 0, y); y += 14;
            g.DrawString($"Fecha: {fec:dd/MM/yyyy  HH:mm}", fN, WinDrawing.Brushes.Black, 0, y); y += 14;
            if (cli.Length > 38) cli = cli.Substring(0, 38);
            g.DrawString($"Cliente: {cli}", fN, WinDrawing.Brushes.Black, 0, y); y += 14;
            DibujarLinea(g, ref y, w);

            // Encabezado items
            g.DrawString("Cant  Descripción", fC, WinDrawing.Brushes.DimGray, 0, y);
            g.DrawString("Total", fC, WinDrawing.Brushes.DimGray, w - 45, y); y += 12;

            foreach (DataRow r in its.Rows)
            {
                string d = r.Table.Columns.Contains("Descripcion") ? r["Descripcion"].ToString() : r["Producto"].ToString();
                if (d.Length > 24) d = d.Substring(0, 24);
                g.DrawString($"{r["Cantidad"],2}x  {d}", fN, WinDrawing.Brushes.Black, 0, y);
                g.DrawString(Convert.ToDecimal(r["Subtotal"]).ToString("N2"), fN, WinDrawing.Brushes.Black, w - 50, y);
                y += 14;
            }
            DibujarLinea(g, ref y, w);

            // Total en grande
            y += 4;
            g.DrawString("TOTAL  A  PAGAR:", fT, WinDrawing.Brushes.Black, 0, y); y += 18;
            WinDrawing.SizeF sT = g.MeasureString($"${tot:N2}", fB);
            g.DrawString($"${tot:N2}", fB, WinDrawing.Brushes.Black, (w - sT.Width) / 2, y);
            y += sT.Height + 4;

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

        /// <summary>
        /// Carga una imagen desde disco usando MemoryStream y aplica Freeze().
        /// Es la única forma fiable de usar imágenes en FlowDocument para impresión WPF.
        /// </summary>
        private static ImageSource CargarImagenParaImpresion(string ruta)
        {
            if (string.IsNullOrWhiteSpace(ruta) || !File.Exists(ruta)) return null;
            try
            {
                byte[] bytes = File.ReadAllBytes(ruta);
                using (var ms = new System.IO.MemoryStream(bytes))
                {
                    var bi = new BitmapImage();
                    bi.BeginInit();
                    bi.StreamSource = ms;
                    bi.CacheOption  = BitmapCacheOption.OnLoad;
                    bi.EndInit();
                    bi.Freeze(); // imprescindible para que WPF lo use fuera del hilo de UI
                    return bi;
                }
            }
            catch { return null; }
        }
    }
}