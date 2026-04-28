using System;
using System.Data;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using Microsoft.Win32;
using SchettiniGestion;

namespace SchettiniGestion.WPF
{
    public partial class PlanillaDiariaControl : UserControl
    {
        private DataTable _dt;

        public PlanillaDiariaControl() { InitializeComponent(); }
        public PlanillaDiariaControl(object param) : this() { }

        private void Control_Loaded(object sender, RoutedEventArgs e)
        {
            dpFecha.SelectedDate = DateTime.Today;
            CargarPlanilla();
        }

        private void btnVer_Click(object sender, RoutedEventArgs e) => CargarPlanilla();

        private void CargarPlanilla()
        {
            try
            {
                DateTime fecha = dpFecha.SelectedDate ?? DateTime.Today;
                _dt = DatabaseService.GetMovimientosCaja(fecha);
                dgvMovimientos.ItemsSource = _dt.DefaultView;

                decimal ventas = 0, compras = 0, gastos = 0, ingresosCaja = 0, egresos = 0;
                foreach (DataRow r in _dt.Rows)
                {
                    decimal m = Convert.ToDecimal(r["Monto"]);
                    string concepto = r["Concepto"]?.ToString() ?? "";
                    string tipo = r["Tipo"]?.ToString() ?? "";
                    if (tipo == "Ingreso") { ingresosCaja += m; if (concepto.Contains("Venta") || concepto.Contains("Factura")) ventas += m; }
                    else { egresos += m; if (concepto.Contains("Compra")) compras += m; else gastos += m; }
                }

                // Sumar ventas desde facturación
                var dtVentas = DatabaseService.GetFacturasPorFecha(fecha, fecha.AddDays(1).AddSeconds(-1));
                decimal totalVentas = 0;
                foreach (DataRow r in dtVentas.Rows) totalVentas += Convert.ToDecimal(r["Total"]);

                lblVentas.Text = totalVentas.ToString("C2");
                lblCompras.Text = compras.ToString("C2");
                lblGastos.Text = gastos.ToString("C2");
                lblIngresosCaja.Text = ingresosCaja.ToString("C2");
                lblNeto.Text = (ingresosCaja - egresos).ToString("C2");
            }
            catch (Exception ex)
            {
                MessageBox.Show("Error al cargar planilla: " + ex.Message);
            }
        }

        private void btnExportar_Click(object sender, RoutedEventArgs e)
        {
            if (_dt == null || _dt.Rows.Count == 0) { MessageBox.Show("No hay datos para exportar.", "Información", MessageBoxButton.OK, MessageBoxImage.Information); return; }
            var dlg = new SaveFileDialog { Filter = "CSV (*.csv)|*.csv", FileName = $"planilla_{(dpFecha.SelectedDate ?? DateTime.Today):yyyyMMdd}.csv" };
            if (dlg.ShowDialog() != true) return;
            try
            {
                var sb = new StringBuilder("Fecha;Concepto;Tipo;Monto;Usuario\n");
                foreach (DataRow r in _dt.Rows)
                    sb.AppendLine($"{r["Fecha"]};{r["Concepto"]};{r["Tipo"]};{r["Monto"]};{r["Usuario"]}");
                System.IO.File.WriteAllText(dlg.FileName, sb.ToString(), Encoding.UTF8);
                MessageBox.Show("Exportado correctamente.", "Éxito", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex) { MessageBox.Show("Error al exportar: " + ex.Message); }
        }
    }
}
