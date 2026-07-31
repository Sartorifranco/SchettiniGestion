using System;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Input;
using SchettiniGestion;

namespace SchettiniGestion.WPF
{
    public partial class LoginWindow : Window
    {
        private double _originalTop = double.NaN;

        public LoginWindow()
        {
            InitializeComponent();
            AutomationProperties.SetAutomationId(txtUsuario, "UITest_Usuario");
            AutomationProperties.SetAutomationId(txtPassword, "UITest_Password");
            AutomationProperties.SetAutomationId(btnIngresar, "UITest_Ingresar");

            Loaded += (s, e) =>
            {
                KeyboardService.VisibilityChanged += OnKeyboardVisibilityChanged;
                KeyboardService.EnabledChanged += ActualizarBtnTecladoLogin;
                ActualizarBtnTecladoLogin();
                if (KeyboardService.IsEnabled && KeyboardService.IsVisible)
                    OnKeyboardVisibilityChanged(true);
                if (KeyboardService.IsEnabled)
                    txtUsuario.Focus();
            };
            Unloaded += (s, e) =>
            {
                KeyboardService.VisibilityChanged -= OnKeyboardVisibilityChanged;
                KeyboardService.EnabledChanged -= ActualizarBtnTecladoLogin;
            };
        }

        private void btnTecladoLogin_Click(object sender, RoutedEventArgs e)
        {
            KeyboardService.Toggle();
            ActualizarBtnTecladoLogin();
            if (KeyboardService.IsEnabled)
                txtUsuario.Focus();
        }

        private void ActualizarBtnTecladoLogin()
        {
            Dispatcher.Invoke(() =>
            {
                bool on = KeyboardService.IsEnabled;
                if (txtLabelTecladoLogin != null)
                    txtLabelTecladoLogin.Text = on ? "Teclado ON" : "Teclado OFF";
                if (btnTecladoLogin == null) return;
                btnTecladoLogin.Opacity = on ? 1.0 : 0.7;
                btnTecladoLogin.BorderBrush = on
                    ? (System.Windows.Media.Brush)FindResource("PrimaryColor")
                    : (System.Windows.Media.Brush)FindResource("BorderColor");
                btnTecladoLogin.Foreground = on
                    ? (System.Windows.Media.Brush)FindResource("TextPrimary")
                    : (System.Windows.Media.Brush)FindResource("TextSecondary");
            });
        }

        private void OnKeyboardVisibilityChanged(bool visible)
        {
            // Teclado full-width del monitor (puede salir del login): subimos la tarjeta.
            Dispatcher.Invoke(() =>
            {
                if (visible)
                {
                    if (double.IsNaN(_originalTop)) _originalTop = Top;
                    double kbTop = KeyboardService.KeyboardTop;
                    double available = kbTop - 8;
                    double ideal = (available - ActualHeight) / 2.0;
                    Top = Math.Max(4, ideal);
                }
                else if (!double.IsNaN(_originalTop))
                {
                    Top = _originalTop;
                    _originalTop = double.NaN;
                }
            });
        }

        private void btnIngresar_Click(object sender, RoutedEventArgs e) => Ingresar();

        private void btnSalir_Click(object sender, RoutedEventArgs e)
            => Application.Current.Shutdown();

        private void txtUsuario_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter) txtPassword.Focus();
        }

        private void txtPassword_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter) Ingresar();
        }

        private void Ingresar()
        {
            string u = txtUsuario.Text.Trim();
            string p = txtPassword.Password;

            if (string.IsNullOrEmpty(u) || string.IsNullOrEmpty(p))
            {
                MessageBox.Show("Por favor, complete todos los campos.", "Aviso", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            try
            {
                if (DatabaseService.ValidarUsuario(u, p))
                {
                    if (DatabaseService.CargarSesionUsuario(u))
                    {
                        if (DatabaseService.UsandoContraseñaPorDefecto(u))
                        {
                            MessageBox.Show(
                                "⚠️  Estás usando la contraseña por defecto de instalación.\n\n" +
                                "Por seguridad, cambiá la contraseña desde:\n" +
                                "Menú → Usuarios → Editar usuario → Nueva contraseña\n\n" +
                                "Este aviso desaparecerá una vez que hayas cambiado la contraseña.",
                                "Contraseña por defecto",
                                MessageBoxButton.OK,
                                MessageBoxImage.Warning);
                        }

                        KeyboardService.Hide();
                        PrincipalWindow principal = new PrincipalWindow();
                        principal.Show();
                        this.Close();
                    }
                    else
                    {
                        MessageBox.Show("Error al cargar los permisos del usuario.", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                    }
                }
                else
                {
                    string mensaje = "Usuario o contraseña incorrectos.";
                    if (!string.IsNullOrWhiteSpace(DatabaseService.UltimoErrorValidacionLogin))
                        mensaje += "\n\nDetalle técnico:\n" + DatabaseService.UltimoErrorValidacionLogin;
                    MessageBox.Show(mensaje, "Acceso Denegado", MessageBoxButton.OK, MessageBoxImage.Error);
                    txtPassword.Clear();
                    txtPassword.Focus();
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show("Error de conexión: " + ex.Message, "Error Crítico", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
    }
}
