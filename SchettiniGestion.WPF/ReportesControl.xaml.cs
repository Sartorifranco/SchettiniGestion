using SchettiniGestion;
using System;
using System.Data;
using System.Windows;
using System.Windows.Controls;
using System.IO;
using OfficeOpenXml; // EPPlus
using System.Linq;
using System.Collections.Generic;
using Microsoft.Win32;
using System.Diagnostics; // ¡Necesario para abrir el Excel!

namespace SchettiniGestion.WPF
{
    public partial class ReportesControl : UserControl
    {
        public ReportesControl()
        {
            InitializeComponent();
            // Licencia EPPlus (Importante)
            ExcelPackage.LicenseContext = LicenseContext.NonCommercial;
        }

        private void UserControl_Loaded(object sender, RoutedEventArgs e)
        {
            DateTime hoy = DateTime.Today;
            DateTime inicioMes = new DateTime(hoy.Year, hoy.Month, 1);

            dpDesdeRanking.SelectedDate = inicioMes;
            dpHastaRanking.SelectedDate = hoy;

            dpDesdeIVA.SelectedDate = inicioMes;
            dpHastaIVA.SelectedDate = hoy;
        }

        // --- RANKING ---
        private void btnActualizarRanking_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                DataTable dt = DatabaseService.GetRankingVentas(dpDesdeRanking.SelectedDate ?? DateTime.Now, dpHastaRanking.SelectedDate ?? DateTime.Now);
                dgvRanking.ItemsSource = dt.DefaultView;
            }
            catch (Exception ex) { CustomMessageBox.Show(ex.Message); }
        }

        private void btnExportarRanking_Click(object sender, RoutedEventArgs e)
        {
            ExportarGrillaAExcel(dgvRanking.ItemsSource as DataView, "Ranking_Ventas");
        }

        // --- LIBRO IVA ---
        private void btnActualizarIVA_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                // 1. Obtenemos los datos crudos
                DataTable rawData = DatabaseService.GetVentasParaLibroIVA(dpDesdeIVA.SelectedDate ?? DateTime.Now, dpHastaIVA.SelectedDate ?? DateTime.Now);

                // 2. Procesamos en memoria
                DataTable processedData = new DataTable();
                processedData.Columns.Add("Fecha");
                processedData.Columns.Add("TipoComprobante");
                processedData.Columns.Add("NroComprobante");
                processedData.Columns.Add("Cliente");
                processedData.Columns.Add("CUIT");
                processedData.Columns.Add("CondicionIVA");
                processedData.Columns.Add("Neto21", typeof(decimal));
                processedData.Columns.Add("IVA21", typeof(decimal));
                processedData.Columns.Add("Neto105", typeof(decimal));
                processedData.Columns.Add("IVA105", typeof(decimal));
                processedData.Columns.Add("TotalFactura", typeof(decimal));

                var facturas = rawData.AsEnumerable()
                    .GroupBy(row => row.Field<long>("NroComprobante"));

                foreach (var grupo in facturas)
                {
                    DataRow firstRow = grupo.First();
                    DataRow newRow = processedData.NewRow();

                    newRow["Fecha"] = Convert.ToDateTime(firstRow["Fecha"]).ToString("dd/MM/yyyy");
                    newRow["TipoComprobante"] = firstRow["TipoComprobante"];
                    newRow["NroComprobante"] = firstRow["NroComprobante"];
                    newRow["Cliente"] = firstRow["Cliente"];
                    newRow["CUIT"] = firstRow["CUIT"];
                    newRow["CondicionIVA"] = firstRow["CondicionIVA"];
                    newRow["TotalFactura"] = Convert.ToDecimal(firstRow["Total"]);

                    decimal neto21 = 0, iva21 = 0, neto105 = 0, iva105 = 0;

                    foreach (var item in grupo)
                    {
                        string alicuotaStr = item["AlicuotaProducto"].ToString();
                        decimal precioUnit = Convert.ToDecimal(item["PrecioUnitario"]);
                        int cantidad = Convert.ToInt32(item["Cantidad"]);
                        decimal totalItem = precioUnit * cantidad;

                        if (alicuotaStr.Contains("21"))
                        {
                            decimal neto = totalItem / 1.21m;
                            neto21 += neto;
                            iva21 += (totalItem - neto);
                        }
                        else if (alicuotaStr.Contains("10.5"))
                        {
                            decimal neto = totalItem / 1.105m;
                            neto105 += neto;
                            iva105 += (totalItem - neto);
                        }
                    }

                    newRow["Neto21"] = Math.Round(neto21, 2);
                    newRow["IVA21"] = Math.Round(iva21, 2);
                    newRow["Neto105"] = Math.Round(neto105, 2);
                    newRow["IVA105"] = Math.Round(iva105, 2);

                    processedData.Rows.Add(newRow);
                }

                dgvIVA.ItemsSource = processedData.DefaultView;
            }
            catch (Exception ex) { CustomMessageBox.Show("Error al procesar IVA: " + ex.Message); }
        }

        private void btnExportarIVA_Click(object sender, RoutedEventArgs e)
        {
            ExportarGrillaAExcel(dgvIVA.ItemsSource as DataView, "Libro_IVA_Ventas");
        }

        // --- EXPORTACIÓN GENÉRICA CORREGIDA ---
        private void ExportarGrillaAExcel(DataView view, string nombreArchivo)
        {
            if (view == null || view.Count == 0) { CustomMessageBox.Show("No hay datos para exportar."); return; }

            SaveFileDialog sfd = new SaveFileDialog
            {
                Filter = "Excel Files|*.xlsx",
                FileName = $"{nombreArchivo}_{DateTime.Now:yyyyMMdd}.xlsx"
            };

            if (sfd.ShowDialog() == true)
            {
                try
                {
                    // Borramos el archivo si ya existe para evitar conflictos
                    if (File.Exists(sfd.FileName)) File.Delete(sfd.FileName);

                    using (ExcelPackage p = new ExcelPackage(new FileInfo(sfd.FileName)))
                    {
                        ExcelWorksheet ws = p.Workbook.Worksheets.Add("Reporte");

                        // Cargar datos
                        ws.Cells["A1"].LoadFromDataTable(view.Table, true);

                        // Formato de cabecera
                        using (var range = ws.Cells[1, 1, 1, view.Table.Columns.Count])
                        {
                            range.Style.Font.Bold = true;
                            range.Style.Fill.PatternType = OfficeOpenXml.Style.ExcelFillStyle.Solid;
                            range.Style.Fill.BackgroundColor.SetColor(System.Drawing.Color.LightGray);
                        }

                        // ELIMINADO: ws.Cells.AutoFitColumns(); 
                        // (Esta línea causaba el error en la imagen image_a38c61.png)

                        p.Save();
                    }

                    // ABRIR EL ARCHIVO AUTOMÁTICAMENTE
                    if (CustomMessageBox.Show("¡Exportación exitosa!\n¿Desea abrir el archivo ahora?", "Éxito", MessageBoxButton.YesNo, MessageBoxImage.Information) == MessageBoxResult.Yes)
                    {
                        var p = new Process();
                        p.StartInfo = new ProcessStartInfo(sfd.FileName)
                        {
                            UseShellExecute = true
                        };
                        p.Start();
                    }
                }
                catch (Exception ex)
                {
                    CustomMessageBox.Show($"Error al exportar: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }
    }
}