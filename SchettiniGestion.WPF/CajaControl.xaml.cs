using SchettiniGestion;
using System;
using System.Data;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace SchettiniGestion.WPF
{
    public partial class CajaControl : UserControl
    {
        public CajaControl()
        {
            InitializeComponent();
        }

        private void CajaControl_Loaded(object sender, RoutedEventArgs e)
        {
            ActualizarPantalla();
        }

        private void ActualizarPantalla()
        {
            try
            {
                bool usaApertura = DatabaseService.GetUsaAperturaCajaObligatoria();
                bannerSinApertura.Visibility = usaApertura && !DatabaseService.TieneAperturaCajaHoy()
                    ? Visibility.Visible
                    : Visibility.Collapsed;

                decimal saldo = DatabaseService.GetSaldoCaja();
                lblSaldo.Text = AppCulture.FormatCurrency(saldo);

                if (saldo >= 0) lblSaldo.Foreground = new SolidColorBrush(Colors.LightGreen);
                else lblSaldo.Foreground = new SolidColorBrush(Colors.Red);

                DataTable dt = DatabaseService.GetMovimientosCaja(DateTime.Now);
                dgvMovimientos.ItemsSource = dt.DefaultView;
            }
            catch (Exception ex)
            {
                CustomMessageBox.Show($"Error al actualizar caja: {ex.Message}");
            }
        }

        private void btnIngreso_Click(object sender, RoutedEventArgs e)
        {
            RegistrarMovimiento("Ingreso", "Ingresar Dinero");
        }

        private void btnEgreso_Click(object sender, RoutedEventArgs e)
        {
            RegistrarMovimiento("Egreso", "Retirar Dinero");
        }

        private void RegistrarMovimiento(string tipo, string titulo)
        {
            var owner = Window.GetWindow(this);
            var dlg = new CajaMovimientoModalWindow(titulo) { Owner = owner };
            if (dlg.ShowDialog() != true)
                return;

            decimal monto = dlg.Monto;
            string concepto = dlg.Concepto;

            if (monto <= 0)
            {
                CustomMessageBox.Show("El monto debe ser mayor a cero.", "Error", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }
            if (string.IsNullOrEmpty(concepto))
            {
                CustomMessageBox.Show("Debe ingresar un concepto o motivo.", "Error", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            if (string.Equals(concepto, DatabaseService.ConceptoFondoFijo, StringComparison.OrdinalIgnoreCase))
            {
                CustomMessageBox.Show(
                    "El fondo fijo se registra desde la pestaña «Apertura de caja», no como ingreso manual.",
                    "Usar apertura de caja", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            if (DatabaseService.RegistrarMovimientoCaja(concepto, tipo, monto))
            {
                CustomMessageBox.Show("Movimiento registrado correctamente.", "Éxito", MessageBoxButton.OK, MessageBoxImage.Information);
                ActualizarPantalla();
            }
            else
            {
                string detalle = !string.IsNullOrEmpty(DatabaseService.UltimoError)
                    ? "\n\n" + DatabaseService.UltimoError : "";
                CustomMessageBox.Show("No se pudo registrar el movimiento." + detalle, "Error",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
    }
}
