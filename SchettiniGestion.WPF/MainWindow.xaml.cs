using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using SchettiniGestion; // Importamos la lógica
using System.Diagnostics; // <-- ¡AÑADIMOS ESTO!
using System.IO;

namespace SchettiniGestion.WPF
{
    public partial class MainWindow : Window
    {
        public MainWindow()
        {
            InitializeComponent();
        }

        private void btnIngresar_Click(object sender, RoutedEventArgs e)
        {
            string usuario = txtUsuario.Text;
            string password = txtPassword.Password;

            if (string.IsNullOrEmpty(usuario) || string.IsNullOrEmpty(password))
            {
                MessageBox.Show("Por favor, ingrese usuario y contraseña.", "Error", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            bool esValido = DatabaseService.ValidarUsuario(usuario, password);

            if (esValido)
            {
                PrincipalWindow ventanaPrincipal = new PrincipalWindow();
                ventanaPrincipal.Show();
                this.Close();
            }
            else
            {
                MessageBox.Show("Usuario o contraseña incorrectos.", "Error de Login", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void btnClose_Click(object sender, RoutedEventArgs e)
        {
            Application.Current.Shutdown();
        }

        // --- ¡ESTA ES LA LÓGICA CORRECTA Y SIMPLE! ---
        private void btnTeclado_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                // Verificamos si ya está abierto
                Process[] oskProcesses = Process.GetProcessesByName("osk");
                if (oskProcesses.Length == 0)
                {
                    // Como tu app es x64, esto SÍ funciona
                    Process.Start("osk");
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"No se pudo iniciar el teclado en pantalla: {ex.Message}", "Error de teclado", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void Input_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter)
            {
                btnIngresar_Click(sender, e);
            }
        }

        private void Window_MouseDown(object sender, MouseButtonEventArgs e)
        {
            if (e.LeftButton == MouseButtonState.Pressed)
            {
                DragMove();
            }
        }
    }
}