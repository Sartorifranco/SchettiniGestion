using System;
using System.IO;
using System.Windows;
using System.Windows.Media;
using Microsoft.Win32;
using SchettiniGestion;

namespace SchettiniGestion.WPF
{
    public partial class ActivationWindow : Window
    {
        private double _originalTop = double.NaN;

        public ActivationWindow()
        {
            InitializeComponent();
            Loaded   += OnLoaded;
            Unloaded += (s, e) => KeyboardService.VisibilityChanged -= OnKeyboardVisibilityChanged;
        }

        private void OnLoaded(object sender, RoutedEventArgs e)
        {
            txtMachineId.Text = LicenseManager.ObtenerHardwareId();
            KeyboardService.VisibilityChanged += OnKeyboardVisibilityChanged;
            if (KeyboardService.IsEnabled && KeyboardService.KeyboardTop < double.MaxValue)
                OnKeyboardVisibilityChanged(true);
        }

        private void OnKeyboardVisibilityChanged(bool visible)
        {
            Dispatcher.Invoke(() =>
            {
                if (visible)
                {
                    if (double.IsNaN(_originalTop)) _originalTop = Top;
                    double kbTop = KeyboardService.KeyboardTop;
                    Top = Math.Max(4, (kbTop - ActualHeight) / 2.0);
                }
                else if (!double.IsNaN(_originalTop))
                {
                    Top        = _originalTop;
                    _originalTop = double.NaN;
                }
            });
        }

        private void btnCargarArchivoLicencia_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var dlg = new OpenFileDialog
                {
                    Title = "Seleccionar archivo de licencia",
                    Filter = "Licencia (*.key)|*.key|Todos los archivos|*.*",
                    FileName = "licencia.key"
                };
                if (dlg.ShowDialog() != true)
                    return;

                string contenido = File.ReadAllText(dlg.FileName).Trim();
                if (string.IsNullOrWhiteSpace(contenido))
                {
                    MostrarError("El archivo está vacío.");
                    return;
                }

                txtLicenciaKey.Text = contenido;
                lblError.Visibility = Visibility.Collapsed;
            }
            catch (Exception ex)
            {
                MostrarError("No se pudo leer el archivo: " + ex.Message);
            }
        }

        private void btnCopiarMachineId_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                Clipboard.SetText(txtMachineId.Text);
                MessageBox.Show("ID de máquina copiado al portapapeles.", "Copiado", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch { }
        }

        private void btnActivar_Click(object sender, RoutedEventArgs e)
        {
            string key = txtLicenciaKey.Text.Trim();
            if (string.IsNullOrWhiteSpace(key))
            {
                MostrarError("Por favor ingrese la clave de licencia.");
                return;
            }

            try
            {
                DatabaseService.GuardarNuevaLicencia(key);
            }
            catch (Exception exGuardar)
            {
                MostrarError("ERROR AL GUARDAR LICENCIA:\n" + exGuardar.Message);
                return;
            }

            if (!LicenseManager.ValidarLicencia(key))
            {
                MostrarError(LicenseManager.UltimoMensajeError ?? "Clave de licencia no válida o expirada.");
                return;
            }

            var ok = Application.Current?.TryFindResource("SuccessColor") as Brush ?? Brushes.LimeGreen;
            var surface = Application.Current?.TryFindResource("SurfaceDark") as Brush ?? Brushes.DimGray;
            borderStatus.Background = surface;
            iconStatus.Text = "✔";
            iconStatus.Foreground = ok;
            lblStatusTitle.Text = "¡Licencia activada correctamente!";
            lblStatusTitle.Foreground = ok;
            lblStatusDesc.Text = $"Vence: {LicenseManager.ObtenerFechaVencimiento()}";
            lblError.Visibility = Visibility.Collapsed;

            MessageBox.Show("¡Sistema activado correctamente!\nPuede ingresar ahora.", "Activación Exitosa", MessageBoxButton.OK, MessageBoxImage.Information);
            DialogResult = true;
            Close();
        }

        private void MostrarError(string msg)
        {
            lblError.Text = msg;
            lblError.Visibility = Visibility.Visible;
        }

        private void btnCancelar_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }

        private void btnCerrar_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }

        private void Header_MouseLeftButtonDown(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            if (e.ButtonState == System.Windows.Input.MouseButtonState.Pressed)
                DragMove();
        }
    }
}
