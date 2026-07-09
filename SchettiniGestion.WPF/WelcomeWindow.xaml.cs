using System.Windows;
using SchettiniGestion;

namespace SchettiniGestion.WPF
{
    public partial class WelcomeWindow : Window
    {
        public WelcomeWindow()
        {
            InitializeComponent();
        }

        private void btnCrear_Click(object sender, RoutedEventArgs e)
        {
            string u = txtUser.Text.Trim();
            string p1 = txtPass.Password;
            string p2 = txtPassConfirm.Password;

            if (string.IsNullOrEmpty(u) || string.IsNullOrEmpty(p1) || string.IsNullOrWhiteSpace(txtNombrePersonal.Text))
            {
                MessageBox.Show("Complete usuario, nombre del personal y contraseña.");
                return;
            }

            if (p1 != p2)
            {
                MessageBox.Show("Las contraseñas no coinciden.");
                return;
            }

            // 1. Guardar Usuario Admin (Rol 1 = Admin)
            if (DatabaseService.GuardarUsuario(0, u, p1, 1, "Administrador", txtNombrePersonal.Text.Trim()))
            {
                // 2. Darle TODOS los permisos automáticamente
                var todosLosPermisos = DatabaseService.GetPermisos(); // Necesitas un método que traiga todos los IDs
                                                                      // (Opcional: Si el script SQL ya asigna permisos al Rol 1, esto no hace falta. 
                                                                      // Pero por seguridad, asignamos el rol 1).

                MessageBox.Show("¡Administrador creado con éxito!\nBienvenido al sistema.", "Éxito", MessageBoxButton.OK, MessageBoxImage.Information);

                // 3. Abrir el Login
                LoginWindow login = new LoginWindow();
                login.Show();
                this.Close();
            }
            else
            {
                MessageBox.Show("Error al crear el usuario en la base de datos.");
            }
        }

        private void Button_Click(object sender, RoutedEventArgs e)
        {
            Application.Current.Shutdown();
        }
    }
}