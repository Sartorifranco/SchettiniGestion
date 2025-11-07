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
// Importamos la lógica de nuestro otro proyecto
using SchettiniGestion;
// --- ¡INICIO DEL CÓDIGO NUEVO! ---
using System.Diagnostics; // Para el teclado
using System.IO; // Para el teclado
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
                // --- ¡INICIO DEL CAMBIO! ---
                // ¡Login exitoso!
                // Ya no mostramos un MessageBox, abrimos la ventana principal.

                PrincipalWindow ventanaPrincipal = new PrincipalWindow();
                ventanaPrincipal.Show(); // Muestra la ventana del menú

                // Cerramos esta ventana de Login
                this.Close();
                // --- ¡FIN DEL CAMBIO! ---
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

        // --- ¡INICIO DEL CÓDIGO NUEVO! ---
        // Lógica simplificada para el Teclado
        private void btnTeclado_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                // Verificamos si ya está abierto
                Process[] oskProcesses = Process.GetProcessesByName("osk");
                if (oskProcesses.Length == 0)
                {
                    // Si no está abierto, lo iniciamos.
                    // Esta es la ruta directa que debe funcionar en todos los Windows.
                    Process.Start(@"C:\Windows\System32\osk.exe");
                }
            }
            catch (Exception ex)
            {
                // Si falla (ej: archivo no encontrado en esa ruta)
                MessageBox.Show($"No se pudo iniciar el teclado en pantalla: {ex.Message}", "Error de teclado", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
        // --- ¡FIN DEL CÓDIGO NUEVO! ---
    }
}