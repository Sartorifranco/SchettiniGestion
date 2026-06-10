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
            try
            {
                var marca = SvgLogoHelper.LoadEmbeddedLogo();
                if (marca != null && imgLogoLogin != null)
                    imgLogoLogin.Source = marca;
            }
            catch { /* logo opcional */ }
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