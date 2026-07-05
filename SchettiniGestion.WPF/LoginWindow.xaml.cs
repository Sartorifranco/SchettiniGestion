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

            Loaded   += (s, e) =>
            {
                KeyboardService.VisibilityChanged += OnKeyboardVisibilityChanged;
                // Si el teclado ya está visible cuando se abre la ventana
                if (KeyboardService.IsEnabled && KeyboardService.KeyboardTop < double.MaxValue)
                    OnKeyboardVisibilityChanged(true);
                txtUsuario.Focus();
            };
            Unloaded += (s, e) => KeyboardService.VisibilityChanged -= OnKeyboardVisibilityChanged;
        }

        private void OnKeyboardVisibilityChanged(bool visible)
        {
            Dispatcher.Invoke(() =>
            {
                if (visible)
                {
                    if (double.IsNaN(_originalTop)) _originalTop = Top;

                    double kbTop         = KeyboardService.KeyboardTop;
                    double available     = kbTop - 8;
                    double ideal         = (available - ActualHeight) / 2.0;
                    Top = Math.Max(4, ideal);
                }
                else
                {
                    if (!double.IsNaN(_originalTop))
                    {
                        Top        = _originalTop;
                        _originalTop = double.NaN;
                    }
                }
            });
        }

        private void btnIngresar_Click(object sender, RoutedEventArgs e)
        {
            Ingresar();
        }

        private void btnSalir_Click(object sender, RoutedEventArgs e)
        {
            Application.Current.Shutdown();
        }

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
                // 1. Validar credenciales
                if (DatabaseService.ValidarUsuario(u, p))
                {
                    // 2. Cargar permisos en sesión
                    if (DatabaseService.CargarSesionUsuario(u))
                    {
                        // Advertir si está usando la contraseña por defecto de instalación
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

                        // 3. Abrir Principal
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