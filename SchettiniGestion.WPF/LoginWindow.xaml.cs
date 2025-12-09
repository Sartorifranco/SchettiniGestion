using System.Windows;
using SchettiniGestion; // Para acceder a DatabaseService

namespace SchettiniGestion.WPF
{
    public partial class LoginWindow : Window
    {
        public LoginWindow()
        {
            InitializeComponent();
            txtPass.Focus();
        }

        private void btnIngresar_Click(object sender, RoutedEventArgs e)
        {
            string u = txtUser.Text.Trim();
            string p = txtPass.Password;

            if (DatabaseService.ValidarUsuario(u, p))
            {
                // Cargar permisos y licencia antes de entrar
                if (DatabaseService.CargarSesionUsuario(u))
                {
                    PrincipalWindow principal = new PrincipalWindow();
                    principal.Show();
                    this.Close();
                }
                else
                {
                    MessageBox.Show("Error al cargar perfil de usuario.");
                }
            }
            else
            {
                MessageBox.Show("Usuario o contraseña incorrectos.", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void btnSalir_Click(object sender, RoutedEventArgs e)
        {
            Application.Current.Shutdown();
        }
    }
}