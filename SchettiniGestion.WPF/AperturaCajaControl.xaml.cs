using System;
using System.Data;
using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using SchettiniGestion;

namespace SchettiniGestion.WPF
{
    public partial class AperturaCajaControl : UserControl
    {
        public AperturaCajaControl() { InitializeComponent(); }
        public AperturaCajaControl(object param) : this() { }

        private void Control_Loaded(object sender, RoutedEventArgs e) => ActualizarEstado();

        private void ActualizarEstado()
        {
            try
            {
                bool abierta = DatabaseService.TieneAperturaCajaHoy();
                bool cerrada = DatabaseService.TieneCierreCajaHoy();
                bool aperturaObligatoria = DatabaseService.GetUsaAperturaCajaObligatoria();
                DataRow ap = DatabaseService.GetAperturaCajaHoy();

                if (abierta && ap != null)
                {
                    panelAbrir.Visibility = Visibility.Collapsed;
                    panelAbierta.Visibility = Visibility.Visible;

                    decimal fondo = Convert.ToDecimal(ap["MontoFondoFijo"]);
                    lblFondoFijo.Text = fondo.ToString("C2");
                    lblHoraApertura.Text = Convert.ToDateTime(ap["Fecha"]).ToString("HH:mm");
                    lblUsuarioApertura.Text = ap["Usuario"]?.ToString() ?? "-";

                    borderEstado.Background = new SolidColorBrush(Color.FromRgb(0x1B, 0x3A, 0x2F));
                    borderEstado.BorderBrush = new SolidColorBrush(Color.FromRgb(0x10, 0xB9, 0x81));
                    borderEstado.BorderThickness = new Thickness(1);
                    lblEstadoTitulo.Text = "✅ Caja abierta";
                    lblEstadoTitulo.Foreground = new SolidColorBrush(Color.FromRgb(0x10, 0xB9, 0x81));
                    lblEstadoDetalle.Text = cerrada
                        ? "Hay apertura y cierre registrados hoy. Mañana deberá abrir caja nuevamente con un nuevo fondo fijo."
                        : aperturaObligatoria
                            ? "Turno en curso. Podés operar ventas y movimientos. Al finalizar, realizá el cierre de caja."
                            : "Turno en curso. La apertura de caja es opcional en la configuración del sistema.";
                }
                else
                {
                    panelAbrir.Visibility = Visibility.Visible;
                    panelAbierta.Visibility = Visibility.Collapsed;
                    txtFondoFijo.Text = "";

                    borderEstado.Background = new SolidColorBrush(Color.FromRgb(0x3A, 0x2E, 0x1B));
                    borderEstado.BorderBrush = new SolidColorBrush(Color.FromRgb(0xF5, 0x9E, 0x0B));
                    borderEstado.BorderThickness = new Thickness(1);
                    lblEstadoTitulo.Text = "⚠ Caja sin abrir";
                    lblEstadoTitulo.Foreground = new SolidColorBrush(Color.FromRgb(0xF5, 0x9E, 0x0B));
                    lblEstadoDetalle.Text = cerrada
                        ? "El cierre de hoy ya fue registrado. Para seguir operando mañana, abrí caja con el fondo fijo."
                        : aperturaObligatoria
                            ? "Antes de operar, registrá la apertura indicando el monto del fondo fijo en efectivo."
                            : "La apertura de caja está desactivada en Configuración. Podés vender sin abrir turno; registrá la apertura solo si querés controlar el fondo fijo.";
                }
            }
            catch (Exception ex)
            {
                CustomMessageBox.Show("Error al cargar apertura: " + ex.Message, "Error",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void btnAbrirCaja_Click(object sender, RoutedEventArgs e)
        {
            if (DatabaseService.TieneAperturaCajaHoy())
            {
                CustomMessageBox.Show("Ya existe una apertura registrada para hoy.", "Apertura",
                    MessageBoxButton.OK, MessageBoxImage.Information);
                ActualizarEstado();
                return;
            }

            string texto = txtFondoFijo.Text?.Trim().Replace(",", ".") ?? "";
            if (!decimal.TryParse(texto, NumberStyles.Number, CultureInfo.InvariantCulture, out decimal monto)
                && !decimal.TryParse(txtFondoFijo.Text?.Trim(), NumberStyles.Number, CultureInfo.CurrentCulture, out monto))
            {
                CustomMessageBox.Show("Ingresá un monto válido para el fondo fijo.", "Monto inválido",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            if (monto < 0)
            {
                CustomMessageBox.Show("El fondo fijo no puede ser negativo.", "Monto inválido",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            var owner = Window.GetWindow(this);
            if (CustomMessageBox.Show(
                $"¿Confirma la apertura de caja con fondo fijo de {monto:C2}?",
                "Confirmar apertura", MessageBoxButton.YesNo, MessageBoxImage.Question) != MessageBoxResult.Yes)
                return;

            if (DatabaseService.AbrirCaja(monto, txtObservaciones.Text))
            {
                CustomMessageBox.Show(
                    $"Caja abierta correctamente.\nFondo fijo: {monto:C2}",
                    "Apertura registrada", MessageBoxButton.OK, MessageBoxImage.Information);
                txtObservaciones.Clear();
                ActualizarEstado();
            }
            else
            {
                string det = !string.IsNullOrEmpty(DatabaseService.UltimoError)
                    ? "\n\n" + DatabaseService.UltimoError : "";
                CustomMessageBox.Show("No se pudo registrar la apertura." + det, "Error",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
    }
}
