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
        private string _tipoMovimientoActual = ""; // "Ingreso" o "Egreso"

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
                // 1. Cargar Saldo Total
                decimal saldo = DatabaseService.GetSaldoCaja();
                lblSaldo.Text = saldo.ToString("C2");

                // Cambiar color si es negativo
                if (saldo >= 0) lblSaldo.Foreground = new SolidColorBrush(Colors.LightGreen);
                else lblSaldo.Foreground = new SolidColorBrush(Colors.Red);

                // 2. Cargar Movimientos del Día
                DataTable dt = DatabaseService.GetMovimientosCaja(DateTime.Now);
                dgvMovimientos.ItemsSource = dt.DefaultView;
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error al actualizar caja: {ex.Message}");
            }
        }

        // --- BOTONES PRINCIPALES ---

        private void btnIngreso_Click(object sender, RoutedEventArgs e)
        {
            AbrirPopup("Ingreso", "Ingresar Dinero");
        }

        private void btnEgreso_Click(object sender, RoutedEventArgs e)
        {
            AbrirPopup("Egreso", "Retirar Dinero");
        }

        // --- LÓGICA DEL POPUP ---

        private void AbrirPopup(string tipo, string titulo)
        {
            _tipoMovimientoActual = tipo;
            lblPopupTitulo.Text = titulo;
            numMontoManual.Value = 0;
            txtConceptoManual.Text = "";
            popupMovimiento.Visibility = Visibility.Visible;
            numMontoManual.Focus();
        }

        private void btnCancelarPopup_Click(object sender, RoutedEventArgs e)
        {
            popupMovimiento.Visibility = Visibility.Collapsed;
        }

        private void btnGuardarMovimiento_Click(object sender, RoutedEventArgs e)
        {
            decimal monto = numMontoManual.Value ?? 0;
            string concepto = txtConceptoManual.Text.Trim();

            if (monto <= 0)
            {
                MessageBox.Show("El monto debe ser mayor a cero.", "Error", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }
            if (string.IsNullOrEmpty(concepto))
            {
                MessageBox.Show("Debe ingresar un concepto o motivo.", "Error", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            // Guardar en BD
            bool exito = DatabaseService.RegistrarMovimientoCaja(concepto, _tipoMovimientoActual, monto);

            if (exito)
            {
                MessageBox.Show("Movimiento registrado correctamente.", "Éxito", MessageBoxButton.OK, MessageBoxImage.Information);
                popupMovimiento.Visibility = Visibility.Collapsed;
                ActualizarPantalla(); // Refrescar saldo y grilla
            }
        }
    }
}