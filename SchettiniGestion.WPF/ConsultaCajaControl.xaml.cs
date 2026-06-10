using System;
using System.Data;
using System.Windows;
using System.Windows.Controls;
using SchettiniGestion;

namespace SchettiniGestion.WPF
{
    public partial class ConsultaCajaControl : UserControl
    {
        public ConsultaCajaControl() { InitializeComponent(); }
        public ConsultaCajaControl(object param) : this() { }

        private void Control_Loaded(object sender, RoutedEventArgs e) => Actualizar();
        private void btnActualizar_Click(object sender, RoutedEventArgs e) => Actualizar();

        private void Actualizar()
        {
            try
            {
                decimal saldo = DatabaseService.GetSaldoCaja();
                lblSaldoActual.Text = saldo.ToString("C2");
                lblUltimaAct.Text = DateTime.Now.ToString("dd/MM/yyyy HH:mm");

                var dt = DatabaseService.GetMovimientosCaja(DateTime.Today);
                dgvUltimos.ItemsSource = dt.DefaultView;

                decimal ing = 0, egr = 0;
                foreach (DataRow r in dt.Rows)
                {
                    decimal m = Convert.ToDecimal(r["Monto"]);
                    if (r["Tipo"]?.ToString() == "Ingreso") ing += m;
                    else egr += m;
                }
                lblIngresosHoy.Text = ing.ToString("C2");
                lblEgresosHoy.Text = egr.ToString("C2");
            }
            catch (Exception ex)
            {
                ModernMessageBox.Show("Error: " + ex.Message);
            }
        }
    }
}
