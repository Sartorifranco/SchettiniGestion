using SchettiniGestion;
using System;
using System.Data;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace SchettiniGestion.WPF
{
    public static class PrintService
    {
        // --- MÉTODOS PÚBLICOS ---

        public static void ImprimirPresupuesto(int presupuestoID, string clienteNombre, DateTime fecha, DataTable items, decimal total)
        {
            ImprimirDocumentoGenerico("PRESUPUESTO", presupuestoID.ToString(), clienteNombre, fecha, items, total, "Válido por 7 días");
        }

        public static void ImprimirTicketVenta(string tipoComprobante, int nroComprobante, string clienteNombre, DateTime fecha, DataTable items, decimal total, string condicionVenta)
        {
            string titulo = tipoComprobante.ToUpper();
            string extraInfo = $"CONDICIÓN: {condicionVenta.ToUpper()}";
            string nroStr = nroComprobante > 0 ? nroComprobante.ToString() : "(Pendiente)";

            ImprimirDocumentoGenerico(titulo, nroStr, clienteNombre, fecha, items, total, extraInfo);
        }

        // --- MOTOR PRINCIPAL ---

        private static void ImprimirDocumentoGenerico(string tituloDoc, string numeroDoc, string cliente, DateTime fecha, DataTable items, decimal total, string infoExtra)
        {
            try
            {
                PrintDialog pd = new PrintDialog();

                if (pd.ShowDialog() == true)
                {
                    // 1. Detectar el ancho del papel
                    double anchoPapel = pd.PrintableAreaWidth;
                    double altoPapel = pd.PrintableAreaHeight;

                    FlowDocument doc = new FlowDocument();
                    doc.PageWidth = anchoPapel;
                    doc.PageHeight = altoPapel; // Importante para paginación
                    doc.ColumnWidth = anchoPapel; // Fuerza a usar 1 sola columna del ancho total
                    doc.FontFamily = new FontFamily("Segoe UI");

                    // 2. Decidir diseño (500px es aprox 13cm, un ticket tiene 8cm)
                    if (anchoPapel > 500)
                    {
                        // Diseño A4
                        DibujarHojaA4(doc, anchoPapel, tituloDoc, numeroDoc, cliente, fecha, items, total, infoExtra);
                    }
                    else
                    {
                        // Diseño Ticket
                        doc.PagePadding = new Thickness(5);
                        DibujarTicket80mm(doc, anchoPapel, tituloDoc, numeroDoc, cliente, fecha, items, total, infoExtra);
                    }

                    // 3. Imprimir
                    IDocumentPaginatorSource idpSource = doc;
                    pd.PrintDocument(idpSource.DocumentPaginator, $"{tituloDoc} #{numeroDoc}");
                }
            }
            catch (Exception ex) { MessageBox.Show($"Error al imprimir: {ex.Message}"); }
        }

        // --- DISEÑO TICKET (Angosto) ---
        private static void DibujarTicket80mm(FlowDocument doc, double anchoDisponible, string titulo, string numero, string cliente, DateTime fecha, DataTable items, decimal total, string infoExtra)
        {
            doc.FontFamily = new FontFamily("Consolas");
            doc.FontSize = 9;
            if (anchoDisponible <= 0) anchoDisponible = 300;

            // ... (Datos de configuración)
            DataRow config = DatabaseService.GetConfiguracion();
            string empresa = "MI NEGOCIO";
            string direccion = "";
            string logoPath = "";
            if (config != null)
            {
                empresa = !string.IsNullOrEmpty(config["NombreFantasia"].ToString()) ? config["NombreFantasia"].ToString() : config["RazonSocial"].ToString();
                direccion = config["Direccion"].ToString();
                logoPath = config["LogoPath"].ToString();
            }

            Paragraph header = new Paragraph() { TextAlignment = TextAlignment.Center };
            if (!string.IsNullOrEmpty(logoPath) && System.IO.File.Exists(logoPath))
            {
                try { doc.Blocks.Add(new BlockUIContainer(new Image() { Width = 100, Stretch = Stretch.Uniform, Source = new BitmapImage(new Uri(logoPath)) })); } catch { }
            }
            header.Inlines.Add(new Run($"{empresa.ToUpper()}\n") { FontWeight = FontWeights.Bold, FontSize = 11 });
            if (!string.IsNullOrEmpty(direccion)) header.Inlines.Add(new Run($"{direccion}\n"));
            header.Inlines.Add(new Run("--------------------------------\n"));
            header.Inlines.Add(new Run($"{titulo} N° {numero}\n") { FontWeight = FontWeights.Bold });
            header.Inlines.Add(new Run($"Fecha: {fecha:dd/MM/yy HH:mm}\n"));
            header.Inlines.Add(new Run($"Cliente: {cliente}\n"));
            doc.Blocks.Add(header);

            // Tabla con anchos fijos calculados
            Table t = new Table() { CellSpacing = 0 };
            double w = anchoDisponible - 10;
            t.Columns.Add(new TableColumn() { Width = new GridLength(w * 0.55) });
            t.Columns.Add(new TableColumn() { Width = new GridLength(w * 0.15) });
            t.Columns.Add(new TableColumn() { Width = new GridLength(w * 0.30) });

            // ... (Filas del ticket, igual que antes) ...
            // (Para ahorrar espacio, uso la misma lógica de filas que ya funcionaba)
            TableRowGroup rg = new TableRowGroup();
            TableRow hRow = new TableRow();
            hRow.Cells.Add(new TableCell(new Paragraph(new Run("ART.")) { FontWeight = FontWeights.Bold }));
            hRow.Cells.Add(new TableCell(new Paragraph(new Run("CNT")) { FontWeight = FontWeights.Bold }));
            hRow.Cells.Add(new TableCell(new Paragraph(new Run("TOT")) { FontWeight = FontWeights.Bold, TextAlignment = TextAlignment.Right }));
            rg.Rows.Add(hRow);

            foreach (DataRow row in items.Rows)
            {
                TableRow tr = new TableRow();
                string desc = row["Descripcion"].ToString();
                if (desc.Length > 18) desc = desc.Substring(0, 16) + "..";
                tr.Cells.Add(new TableCell(new Paragraph(new Run(desc))));
                tr.Cells.Add(new TableCell(new Paragraph(new Run(row["Cantidad"].ToString())) { TextAlignment = TextAlignment.Center }));
                tr.Cells.Add(new TableCell(new Paragraph(new Run($"${Convert.ToDecimal(row["Subtotal"]):N0}")) { TextAlignment = TextAlignment.Right }));
                rg.Rows.Add(tr);
            }
            t.RowGroups.Add(rg);
            doc.Blocks.Add(t);

            doc.Blocks.Add(new Paragraph(new Run($"TOTAL: ${total:N2}") { FontWeight = FontWeights.Bold, FontSize = 14 }) { TextAlignment = TextAlignment.Right });
            doc.Blocks.Add(new Paragraph(new Run(infoExtra)) { TextAlignment = TextAlignment.Center, FontSize = 8 });
        }

        // --- DISEÑO A4 (CORREGIDO Y EXPANDIDO) ---
        private static void DibujarHojaA4(FlowDocument doc, double anchoDisponible, string titulo, string numero, string cliente, DateTime fecha, DataTable items, decimal total, string infoExtra)
        {
            double margen = 40;
            doc.PagePadding = new Thickness(margen);
            doc.FontSize = 11;

            // Calculamos el ancho real útil para la tabla
            double anchoUtil = anchoDisponible - (margen * 2);

            DataRow config = DatabaseService.GetConfiguracion();
            string empresa = "MI NEGOCIO";
            string dirEmpresa = "";
            string telEmpresa = "";
            string cuitEmpresa = "";
            string logoPath = "";

            if (config != null)
            {
                empresa = !string.IsNullOrEmpty(config["RazonSocial"].ToString()) ? config["RazonSocial"].ToString() : config["NombreFantasia"].ToString();
                dirEmpresa = config["Direccion"].ToString();
                telEmpresa = config["Telefono"].ToString();
                cuitEmpresa = config["CUIT"].ToString();
                logoPath = config["LogoPath"].ToString();
            }

            // 1. Cabecera
            Table headTable = new Table();
            headTable.Columns.Add(new TableColumn() { Width = new GridLength(anchoUtil * 0.5) }); // 50% Logo
            headTable.Columns.Add(new TableColumn() { Width = new GridLength(anchoUtil * 0.5) }); // 50% Datos
            TableRowGroup headGroup = new TableRowGroup();
            TableRow headRow = new TableRow();

            StackPanel pnlIzq = new StackPanel();
            if (!string.IsNullOrEmpty(logoPath) && System.IO.File.Exists(logoPath))
            {
                try { pnlIzq.Children.Add(new Image() { Height = 60, HorizontalAlignment = HorizontalAlignment.Left, Source = new BitmapImage(new Uri(logoPath)) }); } catch { }
            }
            pnlIzq.Children.Add(new TextBlock() { Text = empresa, FontWeight = FontWeights.Bold, FontSize = 18, Margin = new Thickness(0, 10, 0, 0) });
            headRow.Cells.Add(new TableCell(new BlockUIContainer(pnlIzq)));

            Border marcoDatos = new Border() { BorderBrush = Brushes.Black, BorderThickness = new Thickness(1), Padding = new Thickness(10), Background = Brushes.WhiteSmoke };
            StackPanel pnlDer = new StackPanel();
            pnlDer.Children.Add(new TextBlock() { Text = titulo, FontWeight = FontWeights.Bold, FontSize = 16, HorizontalAlignment = HorizontalAlignment.Center });
            pnlDer.Children.Add(new TextBlock() { Text = $"N° {numero}", FontWeight = FontWeights.Bold, FontSize = 14, HorizontalAlignment = HorizontalAlignment.Center });
            pnlDer.Children.Add(new TextBlock() { Text = $"Fecha: {fecha:dd/MM/yyyy}", Margin = new Thickness(0, 5, 0, 0) });
            pnlDer.Children.Add(new TextBlock() { Text = $"CUIT: {cuitEmpresa}" });
            pnlDer.Children.Add(new TextBlock() { Text = $"Dirección: {dirEmpresa}" });
            marcoDatos.Child = pnlDer;
            headRow.Cells.Add(new TableCell(new BlockUIContainer(marcoDatos)));

            headGroup.Rows.Add(headRow);
            headTable.RowGroups.Add(headGroup);
            doc.Blocks.Add(headTable);

            doc.Blocks.Add(new Paragraph(new Run(" ")) { LineHeight = 10 });

            // 2. Cliente
            Border marcoCliente = new Border() { BorderBrush = Brushes.Gray, BorderThickness = new Thickness(1), Padding = new Thickness(10) };
            StackPanel pnlCli = new StackPanel() { Orientation = Orientation.Horizontal };
            pnlCli.Children.Add(new TextBlock() { Text = $"Cliente: {cliente}", FontWeight = FontWeights.Bold, Width = 300 });
            pnlCli.Children.Add(new TextBlock() { Text = $"Condición: {infoExtra.Replace("CONDICIÓN: ", "")}" });
            marcoCliente.Child = pnlCli;
            doc.Blocks.Add(new BlockUIContainer(marcoCliente));

            doc.Blocks.Add(new Paragraph(new Run(" ")) { LineHeight = 10 });

            // 3. GRILLA (AQUÍ ESTABA EL PROBLEMA)
            // Usamos ancho Fijo en Píxeles calculado sobre 'anchoUtil'
            Table tProd = new Table() { CellSpacing = 0, BorderBrush = Brushes.Black, BorderThickness = new Thickness(0, 1, 0, 1) };

            tProd.Columns.Add(new TableColumn() { Width = new GridLength(anchoUtil * 0.15) }); // Código (15%)
            tProd.Columns.Add(new TableColumn() { Width = new GridLength(anchoUtil * 0.45) }); // Descripción (45%)
            tProd.Columns.Add(new TableColumn() { Width = new GridLength(anchoUtil * 0.10) }); // Cant (10%)
            tProd.Columns.Add(new TableColumn() { Width = new GridLength(anchoUtil * 0.15) }); // Precio (15%)
            tProd.Columns.Add(new TableColumn() { Width = new GridLength(anchoUtil * 0.15) }); // Subtotal (15%)

            TableRowGroup rgProd = new TableRowGroup();
            TableRow hProd = new TableRow() { Background = Brushes.LightGray, FontWeight = FontWeights.Bold };
            hProd.Cells.Add(new TableCell(new Paragraph(new Run("CÓDIGO"))));
            hProd.Cells.Add(new TableCell(new Paragraph(new Run("DESCRIPCIÓN"))));
            hProd.Cells.Add(new TableCell(new Paragraph(new Run("CANT")) { TextAlignment = TextAlignment.Center }));
            hProd.Cells.Add(new TableCell(new Paragraph(new Run("P. UNIT")) { TextAlignment = TextAlignment.Right }));
            hProd.Cells.Add(new TableCell(new Paragraph(new Run("SUBTOTAL")) { TextAlignment = TextAlignment.Right }));
            rgProd.Rows.Add(hProd);

            foreach (DataRow row in items.Rows)
            {
                TableRow tr = new TableRow();
                string codigo = row.Table.Columns.Contains("Codigo") ? row["Codigo"].ToString() : "-";

                tr.Cells.Add(new TableCell(new Paragraph(new Run(codigo))));
                tr.Cells.Add(new TableCell(new Paragraph(new Run(row["Descripcion"].ToString()))));
                tr.Cells.Add(new TableCell(new Paragraph(new Run(row["Cantidad"].ToString())) { TextAlignment = TextAlignment.Center }));

                decimal sub = Convert.ToDecimal(row["Subtotal"]);
                decimal cant = Convert.ToDecimal(row["Cantidad"]);
                decimal unit = cant != 0 ? sub / cant : 0;
                if (row.Table.Columns.Contains("PrecioUnitario")) unit = Convert.ToDecimal(row["PrecioUnitario"]);

                tr.Cells.Add(new TableCell(new Paragraph(new Run(unit.ToString("C2"))) { TextAlignment = TextAlignment.Right }));
                tr.Cells.Add(new TableCell(new Paragraph(new Run(sub.ToString("C2"))) { TextAlignment = TextAlignment.Right }));
                rgProd.Rows.Add(tr);
            }
            tProd.RowGroups.Add(rgProd);
            doc.Blocks.Add(tProd);

            // 4. Total
            Paragraph pTotal = new Paragraph() { Margin = new Thickness(0, 20, 0, 0), TextAlignment = TextAlignment.Right };
            pTotal.Inlines.Add(new Run($"TOTAL: {total:C2}") { FontSize = 20, FontWeight = FontWeights.Bold });
            doc.Blocks.Add(pTotal);
        }
    }
}