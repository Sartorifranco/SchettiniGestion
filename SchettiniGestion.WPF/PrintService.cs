using SchettiniGestion;
using System;
using System.Data;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Media;

namespace SchettiniGestion.WPF
{
    public static class PrintService
    {
        /// <summary>
        /// Genera e imprime un ticket de PRESUPUESTO (No Fiscal).
        /// Ajustado para papel de 80mm.
        /// </summary>
        public static void ImprimirPresupuesto(int presupuestoID, string clienteNombre, DateTime fecha, DataTable items, decimal total)
        {
            try
            {
                PrintDialog pd = new PrintDialog();

                // Preguntamos impresora primero para obtener su ancho real si es posible,
                // si no, usamos el default de 80mm (aprox 300px).
                if (pd.ShowDialog() == true)
                {
                    FlowDocument doc = new FlowDocument();

                    // Configuración de Página (80mm ~ 280-300 unidades WPF)
                    // Quitamos márgenes grandes para aprovechar el papel
                    doc.PagePadding = new Thickness(5);
                    doc.ColumnGap = 0;

                    // Si la impresora nos da un ancho válido, lo usamos. Si no, forzamos 300 (80mm).
                    double printableWidth = pd.PrintableAreaWidth;
                    if (printableWidth <= 0 || printableWidth > 1000) printableWidth = 300;

                    doc.PageWidth = printableWidth;
                    doc.FontFamily = new FontFamily("Consolas");
                    doc.FontSize = 10;

                    // 1. Encabezado
                    Paragraph header = new Paragraph();
                    header.TextAlignment = TextAlignment.Center;
                    header.Inlines.Add(new Run("SCHETTINI GESTIÓN\n") { FontWeight = FontWeights.Bold, FontSize = 12 });
                    header.Inlines.Add(new Run("DOCUMENTO NO VÁLIDO COMO FACTURA\n"));
                    header.Inlines.Add(new Run("********************************\n"));
                    header.Inlines.Add(new Run($"PRESUPUESTO N° {presupuestoID}\n") { FontWeight = FontWeights.Bold });
                    header.Inlines.Add(new Run($"Fecha: {fecha:dd/MM/yyyy HH:mm}\n"));
                    header.Inlines.Add(new Run($"Cliente: {clienteNombre}\n"));
                    header.Inlines.Add(new Run("--------------------------------"));
                    doc.Blocks.Add(header);

                    // 2. Tabla de Productos
                    // Definimos anchos fijos proporcionales al ancho total para que no se rompa
                    Table table = new Table();
                    table.CellSpacing = 0;

                    // Calculamos anchos basados en el total disponible
                    double col1 = printableWidth * 0.55; // 55% para Nombre
                    double col2 = printableWidth * 0.15; // 15% para Cant
                    double col3 = printableWidth * 0.30; // 30% para Total

                    table.Columns.Add(new TableColumn() { Width = new GridLength(col1) });
                    table.Columns.Add(new TableColumn() { Width = new GridLength(col2) });
                    table.Columns.Add(new TableColumn() { Width = new GridLength(col3) });

                    TableRowGroup rowGroup = new TableRowGroup();

                    // Cabecera de Tabla
                    TableRow headerRow = new TableRow();
                    headerRow.Cells.Add(new TableCell(new Paragraph(new Run("PROD.")) { FontWeight = FontWeights.Bold }));
                    headerRow.Cells.Add(new TableCell(new Paragraph(new Run("CANT")) { FontWeight = FontWeights.Bold, TextAlignment = TextAlignment.Center }));
                    headerRow.Cells.Add(new TableCell(new Paragraph(new Run("TOTAL")) { FontWeight = FontWeights.Bold, TextAlignment = TextAlignment.Right }));
                    rowGroup.Rows.Add(headerRow);

                    // Filas
                    foreach (DataRow row in items.Rows)
                    {
                        TableRow tr = new TableRow();

                        // Nombre (Recortamos si es muy largo para evitar saltos de línea feos)
                        string desc = row["Descripcion"].ToString();
                        if (desc.Length > 25) desc = desc.Substring(0, 22) + "...";

                        tr.Cells.Add(new TableCell(new Paragraph(new Run(desc))));
                        tr.Cells.Add(new TableCell(new Paragraph(new Run(row["Cantidad"].ToString())) { TextAlignment = TextAlignment.Center }));

                        decimal subtotal = Convert.ToDecimal(row["Subtotal"]);
                        tr.Cells.Add(new TableCell(new Paragraph(new Run($"${subtotal:N0}")) { TextAlignment = TextAlignment.Right }));

                        rowGroup.Rows.Add(tr);
                    }
                    table.RowGroups.Add(rowGroup);
                    doc.Blocks.Add(table);

                    // 3. Totales
                    Paragraph footer = new Paragraph();
                    footer.TextAlignment = TextAlignment.Right;
                    footer.Inlines.Add(new Run("--------------------------------\n"));
                    footer.Inlines.Add(new Run($"TOTAL: ${total:N2}") { FontWeight = FontWeights.Bold, FontSize = 14 });
                    doc.Blocks.Add(footer);

                    // 4. Pie
                    Paragraph final = new Paragraph();
                    final.TextAlignment = TextAlignment.Center;
                    final.FontSize = 9;
                    final.Inlines.Add(new Run("\nValidez de la oferta: 7 días.\n"));
                    final.Inlines.Add(new Run("Gracias por su consulta."));
                    doc.Blocks.Add(final);

                    // 5. Imprimir
                    IDocumentPaginatorSource idpSource = doc;
                    pd.PrintDocument(idpSource.DocumentPaginator, $"Presupuesto #{presupuestoID}");
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error al imprimir: {ex.Message}", "Error de Impresión", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
    }
}