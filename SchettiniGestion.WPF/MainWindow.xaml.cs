using System;
using System.Diagnostics;
using System.Windows;
using System.Windows.Input;
using SchettiniGestion;

namespace SchettiniGestion.WPF
{
    public partial class MainWindow : Window
    {
        public MainWindow()
        {
            InitializeComponent();
            try
            {
                var marca = SvgLogoHelper.LoadEmbeddedLogo();
                if (marca != null && imgMarcaLogin != null)
                    imgMarcaLogin.Source = marca;
            }
            catch { /* logo opcional */ }
            ThemeManager.ThemeChanged += ThemeManager_ThemeChanged;
            ActualizarSelectorTema();

            DatabaseService.OnDbError = (mensajeError) =>
            {
                Application.Current.Dispatcher.Invoke(() =>
                {
                    CustomMessageBox.Show(mensajeError, "Aviso del Sistema", MessageBoxButton.OK, MessageBoxImage.Error);
                });
            };
        }

        private void MainWindow_Loaded(object sender, RoutedEventArgs e)
        {
            ActualizarSelectorTema();
        }

        private void MainWindow_Closing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            ThemeManager.ThemeChanged -= ThemeManager_ThemeChanged;
        }

        private void ThemeManager_ThemeChanged(object sender, EventArgs e)
        {
            Dispatcher.BeginInvoke(new Action(ActualizarSelectorTema));
        }

        private void btnTema_Click(object sender, RoutedEventArgs e)
        {
            ThemeManager.ToggleTheme();
            ActualizarSelectorTema();
        }

        private void ActualizarSelectorTema()
        {
            try
            {
                if (txtIconoTema != null)
                    txtIconoTema.Text = ThemeManager.IsDark ? "\u263E" : "\u263C";
                if (txtEtiquetaTema != null)
                    txtEtiquetaTema.Text = ThemeManager.IsDark ? "Tema oscuro" : "Tema claro";
            }
            catch { /* controles aún no construidos */ }
        }

        private void ChromeTitleBar_MouseDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ChangedButton == MouseButton.Left && e.ButtonState == MouseButtonState.Pressed)
            {
                try { DragMove(); }
                catch { /* HWND no listo */ }
            }
        }

        private void btnIngresar_Click(object sender, RoutedEventArgs e)
        {
            string usuario = txtUsuario.Text;
            string password = txtPassword.Password;

            if (string.IsNullOrEmpty(usuario) || string.IsNullOrEmpty(password))
            {
                CustomMessageBox.Show("Por favor, ingrese usuario y contraseña.", "Datos Incompletos", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            if (!LicenseManager.ValidarLicencia())
            {
                this.Hide();
                ActivationWindow ventanaActivacion = new ActivationWindow();
                bool? resultado = ventanaActivacion.ShowDialog();
                this.Show();

                if (resultado != true)
                    return;
            }

            bool esValido = DatabaseService.ValidarUsuario(usuario, password);

            if (esValido)
            {
                bool sesionCargada = DatabaseService.CargarSesionUsuario(usuario);

                if (sesionCargada)
                {
                    PrincipalWindow ventanaPrincipal = new PrincipalWindow();
                    ventanaPrincipal.Show();
                    this.Close();
                }
                else
                {
                    CustomMessageBox.Show("Error al cargar los permisos. Contacte al administrador.", "Error de Sesión", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
            else
            {
                CustomMessageBox.Show("Usuario o contraseña incorrectos.", "Acceso Denegado", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void btnClose_Click(object sender, RoutedEventArgs e)
        {
            Application.Current.Shutdown();
        }

        private void btnTeclado_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                Process[] oskProcesses = Process.GetProcessesByName("osk");
                if (oskProcesses.Length == 0)
                {
                    string path64 = @"C:\Windows\Www64\osk.exe";
                    string path32 = @"C:\Windows\System32\osk.exe";

                    if (System.IO.File.Exists(path64))
                        Process.Start(path64);
                    else
                        Process.Start(path32);
                }
            }
            catch (Exception ex)
            {
                CustomMessageBox.Show($"No se pudo iniciar el teclado: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void Input_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter)
                btnIngresar_Click(sender, e);
        }
    }
}
