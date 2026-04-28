using System;
using System.Data;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using Microsoft.Win32;
using SchettiniGestion;

namespace SchettiniGestion.WPF
{
    public partial class InformesControl : UserControl
    {
        private DataTable _dtActual;

        public InformesControl() { InitializeComponent(); }
        public InformesControl(object param) : this() { }

        private void Control_Loaded(object sender, RoutedEventArgs e)
        {
            dpDesde.SelectedDate = new DateTime(DateTime.Today.Year, DateTime.Today.Month, 1);
            dpHasta.SelectedDate = DateTime.Today;
        }

        private void cmbTipoInforme_SelectionChanged(object sender, SelectionChangedEventArgs e) { }

        private void btnGenerar_Click(object sender, RoutedEventArgs e)
        {
            DateTime desde = dpDesde.SelectedDate ?? new DateTime(DateTime.Today.Year, DateTime.Today.Month, 1);
            DateTime hasta = (dpHasta.SelectedDate ?? DateTime.Today).AddDays(1).AddSeconds(-1);
            string tipo = (cmbTipoInforme.SelectedItem as ComboBoxItem)?.Content?.ToString() ?? "Ventas por Período";

            try
            {
                _dtActual = GenerarInforme(tipo, desde, hasta);
                dgvInforme.ItemsSource = _dtActual?.DefaultView;
            }
            catch (Exception ex)
            {
                MessageBox.Show("Error al generar informe: " + ex.Message);
            }
        }

        private DataTable GenerarInforme(string tipo, DateTime desde, DateTime hasta)
        {
            using (var conn = new System.Data.SqlClient.SqlConnection(DatabaseService.ConnectionString))
            {
                conn.Open();
                string sql;
                switch (tipo)
                {
                    case "Ventas por Período":
                        sql = @"SELECT f.Fecha, f.TipoComprobante, ISNULL(c.RazonSocial,'Consumidor Final') AS Cliente,
                                       f.Total, f.CAE, f.NumeroComprobanteAFIP AS NroComprobante
                                FROM Facturas f LEFT JOIN Clientes c ON f.ClienteID=c.ClienteID
                                WHERE f.Fecha >= @d AND f.Fecha <= @h ORDER BY f.Fecha DESC";
                        break;
                    case "Libro IVA Ventas":
                        sql = @"SELECT f.Fecha, f.TipoComprobante AS TipoComp, ISNULL(c.CUIT,'') AS CUIT,
                                       ISNULL(c.RazonSocial,'Consumidor Final') AS RazonSocial,
                                       ISNULL(c.CondicionIVA,'CF') AS CondIVA,
                                       ROUND(f.Total / 1.21, 2) AS Neto21,
                                       ROUND(f.Total - f.Total/1.21, 2) AS IVA21,
                                       f.Total AS Total
                                FROM Facturas f LEFT JOIN Clientes c ON f.ClienteID=c.ClienteID
                                WHERE f.Fecha >= @d AND f.Fecha <= @h ORDER BY f.Fecha";
                        break;
                    case "Libro IVA Compras":
                        sql = @"SELECT c.Fecha, c.TipoComprobante AS TipoComp, ISNULL(p.CUIT,'') AS CUIT,
                                       ISNULL(p.RazonSocial,'') AS Proveedor,
                                       ROUND(c.Total / 1.21, 2) AS Neto21,
                                       ROUND(c.Total - c.Total/1.21, 2) AS IVA21,
                                       c.Total AS Total
                                FROM Compras c LEFT JOIN Proveedores p ON c.ProveedorID=p.ProveedorID
                                WHERE c.Fecha >= @d AND c.Fecha <= @h ORDER BY c.Fecha";
                        break;
                    case "Productos Más Vendidos":
                        sql = @"SELECT ISNULL(p.Descripcion,'Desconocido') AS Producto,
                                       SUM(fd.Cantidad) AS CantidadVendida,
                                       SUM(fd.Cantidad * fd.PrecioUnitario) AS TotalVendido
                                FROM FacturaDetalle fd
                                JOIN Facturas f ON fd.FacturaID=f.FacturaID
                                LEFT JOIN Productos p ON fd.ProductoID=p.ProductoID
                                WHERE f.Fecha >= @d AND f.Fecha <= @h
                                GROUP BY fd.ProductoID, p.Descripcion
                                ORDER BY CantidadVendida DESC";
                        break;
                    case "Ranking Clientes":
                        sql = @"SELECT ISNULL(c.RazonSocial,'Consumidor Final') AS Cliente,
                                       COUNT(*) AS NroCompras, SUM(f.Total) AS TotalComprado
                                FROM Facturas f LEFT JOIN Clientes c ON f.ClienteID=c.ClienteID
                                WHERE f.Fecha >= @d AND f.Fecha <= @h
                                GROUP BY f.ClienteID, c.RazonSocial
                                ORDER BY TotalComprado DESC";
                        break;
                    default:
                        return new DataTable();
                }
                var dt = new DataTable();
                var da = new System.Data.SqlClient.SqlDataAdapter(sql, conn);
                da.SelectCommand.Parameters.AddWithValue("@d", desde);
                da.SelectCommand.Parameters.AddWithValue("@h", hasta);
                da.Fill(dt);
                return dt;
            }
        }

        private void btnExportar_Click(object sender, RoutedEventArgs e)
        {
            if (_dtActual == null || _dtActual.Rows.Count == 0)
            { MessageBox.Show("Genere un informe primero.", "Información", MessageBoxButton.OK, MessageBoxImage.Information); return; }
            var dlg = new SaveFileDialog { Filter = "CSV (*.csv)|*.csv", FileName = $"informe_{DateTime.Today:yyyyMMdd}.csv" };
            if (dlg.ShowDialog() != true) return;
            try
            {
                var sb = new StringBuilder();
                var cols = new System.Collections.Generic.List<string>();
                foreach (DataColumn col in _dtActual.Columns) cols.Add(col.ColumnName);
                sb.AppendLine(string.Join(";", cols));
                foreach (DataRow r in _dtActual.Rows)
                {
                    var vals = new System.Collections.Generic.List<string>();
                    foreach (var col in cols) vals.Add(r[col]?.ToString()?.Replace(";", ",") ?? "");
                    sb.AppendLine(string.Join(";", vals));
                }
                System.IO.File.WriteAllText(dlg.FileName, sb.ToString(), Encoding.UTF8);
                MessageBox.Show("Exportado correctamente.", "Éxito", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex) { MessageBox.Show("Error al exportar: " + ex.Message); }
        }
    }
}
