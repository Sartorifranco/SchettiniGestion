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
// --- ¡INICIO DEL CÓDIGO NUEVO! ---
// Importamos la lógica de nuestro otro proyecto
using SchettiniGestion;
// --- ¡FIN DEL CÓDIGO NUEVO! ---

namespace SchettiniGestion.WPF
{
    /// <summary>
    /// Lógica de interacción para MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        public MainWindow()
        {
            InitializeComponent();
        }

        // --- ¡INICIO DEL CÓDIGO NUEVO! ---

        private void btnIngresar_Click(object sender, RoutedEventArgs e)
        {
            string usuario = txtUsuario.Text;
            string password = txtPassword.Password; // Se usa .Password en lugar de .Text

            // 1. Validamos campos vacíos
            if (string.IsNullOrEmpty(usuario) || string.IsNullOrEmpty(password))
            {
                MessageBox.Show("Por favor, ingrese usuario y contraseña.", "Error", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            // 2. ¡Llamamos a nuestro DatabaseService que ya funciona!
            bool esValido = DatabaseService.ValidarUsuario(usuario, password);

            if (esValido)
            {
                // ¡Login exitoso!
                MessageBox.Show("¡Login exitoso!", "Éxito", MessageBoxButton.OK, MessageBoxImage.Information);

                // (Aquí, en el futuro, abriremos la ventana principal de la app)

                // Cerramos la ventana de Login
                this.Close();
            }
            else
            {
                // Login fallido
                MessageBox.Show("Usuario o contraseña incorrectos.", "Error de Login", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void btnSalir_Click(object sender, RoutedEventArgs e)
        {
            // Cierra toda la aplicación
            Application.Current.Shutdown();
        }

        // --- ¡FIN DEL CÓDIGO NUEVO! ---
    }
}
