using System;
using System.Data;
using System.Windows;
using System.Windows.Controls;
using SchettiniGestion;

namespace SchettiniGestion.WPF
{
    public partial class MovimientosCajaControl : UserControl
    {
        public MovimientosCajaControl() { InitializeComponent(); }
        public MovimientosCajaControl(object param) : this() { }

        private void Control_Loaded(object sender, RoutedEventArgs e)
        {
            dpDesde.SelectedDate = DateTime.Today;
            dpHasta.SelectedDate = DateTime.Today;
            CargarMovimientos();
        }

        private void CargarMovimientos()
        {
            try
            {
                DateTime desde = dpDesde.SelectedDate ?? DateTime.Today;
                DateTime hasta = (dpHasta.SelectedDate ?? DateTime.Today).AddDays(1).AddSeconds(-1);
                string tipo = (cmbTipo.SelectedItem as ComboBoxItem)?.Content?.ToString() ?? "Todos";
                string filtro = txtFiltro?.Text?.Trim() ?? "";

                var dt = DatabaseService.GetMovimientosCaja(desde);

                // Solo aplicar RowFilter si la tabla tiene columnas (evita error si la tabla está vacía)
                if (dt.Columns.Count > 0)
                {
                    if (tipo != "Todos")
                        dt.DefaultView.RowFilter = $"Tipo = '{tipo}'";
                    else if (!string.IsNullOrWhiteSpace(filtro))
                        dt.DefaultView.RowFilter = $"Concepto LIKE '%{filtro.Replace("'", "''")}%'";
                }

                dgvMovimientos.ItemsSource = dt.DefaultView;

                decimal ingresos = 0, egresos = 0;
                foreach (DataRow r in dt.Rows)
                {
                    decimal m = r["Monto"] == DBNull.Value ? 0 : Convert.ToDecimal(r["Monto"]);
                    if (r["Tipo"]?.ToString() == "Ingreso") ingresos += m;
                    else egresos += m;
                }
                lblIngresos.Text = ingresos.ToString("C2");
                lblEgresos.Text = egresos.ToString("C2");
                lblSaldo.Text = (ingresos - egresos).ToString("C2");
            }
            catch (Exception ex)
            {
                ModernMessageBox.Show("Error al cargar movimientos: " + ex.Message);
            }
        }

        private void txtFiltro_TextChanged(object sender, TextChangedEventArgs e) => CargarMovimientos();
        private void btnBuscar_Click(object sender, RoutedEventArgs e) => CargarMovimientos();
    }
}
