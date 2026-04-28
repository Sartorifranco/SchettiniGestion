using System;
using System.Windows;
using System.Windows.Media;
using SchettiniGestion;

namespace SchettiniGestion.WPF
{
    public partial class ActivationWindow : Window
    {
        public ActivationWindow()
        {
            InitializeComponent();
            Loaded += OnLoaded;
        }

        private void OnLoaded(object sender, RoutedEventArgs e)
        {
            txtMachineId.Text = ObtenerMachineId();
        }

        private static string ObtenerMachineId()
        {
            try
            {
                string raw = $"{Environment.MachineName}-{Environment.UserDomainName}-{Environment.ProcessorCount}";
                int hash = 0;
                foreach (char c in raw) hash = hash * 31 + c;
                return Math.Abs(hash).ToString("X8");
            }
            catch
            {
                return Environment.MachineName.ToUpper();
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

            if (!DatabaseService.GuardarNuevaLicencia(key))
            {
                MostrarError("No se pudo guardar la licencia. Verifique la conexión a la base de datos.");
                return;
            }

            if (!LicenseManager.ValidarLicencia())
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
