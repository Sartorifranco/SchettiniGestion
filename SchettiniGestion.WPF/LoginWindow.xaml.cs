using System;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Input;
using SchettiniGestion;

namespace SchettiniGestion.WPF
{
    public partial class LoginWindow : Window
    {
        public LoginWindow()
        {
            InitializeComponent();
            SvgLogoHelper.ApplyToImage(imgLogoLogin);
            AutomationProperties.SetAutomationId(txtUsuario, "UITest_Usuario");
            AutomationProperties.SetAutomationId(txtPassword, "UITest_Password");
            AutomationProperties.SetAutomationId(btnIngresar, "UITest_Ingresar");
            txtUsuario.Focus();
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
                ModernMessageBox.Show("Por favor, complete todos los campos.", "Aviso", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            try
            {
                // 1. Validar credenciales
                if (DatabaseService.ValidarUsuario(u, p))
                {
                    // 2. Cargar permisos en sesión
                    bool sesionCargada = DatabaseService.CargarSesionUsuario(u);
                    if (!sesionCargada)
                    {
                        ModernMessageBox.Show("Error al cargar los permisos del usuario.", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                        return;
                    }

                    // 3. Abrir Principal (solo después de cargar sesión/permisos)
                    PrincipalWindow principal = new PrincipalWindow();
                    principal.Show();
                    this.Close();
                }
                else
                {
                    string detalle = DatabaseService.UltimoErrorValidacionLogin;
                    string msg = string.IsNullOrWhiteSpace(detalle)
                        ? "Usuario o contraseña incorrectos."
                        : "Usuario o contraseña incorrectos.\n\nDetalle técnico:\n" + detalle;
                    ModernMessageBox.Show(msg, "Acceso denegado", MessageBoxButton.OK, MessageBoxImage.Error);
                    txtPassword.Clear();
                    txtPassword.Focus();
                }
            }
            catch (Exception ex)
            {
                ModernMessageBox.Show("Error de conexión: " + ex.Message, "Error Crítico", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
    }
}