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
using System.Windows.Shapes;
using System.Diagnostics; // Para poder llamar a procesos (el teclado)
using System.IO; // Para manejar rutas de archivos


namespace SchettiniGestion.WPF
{
    /// <summary>
    /// Lógica de interacción para PrincipalWindow.xaml
    /// </summary>
    public partial class PrincipalWindow : Window
    {
        public PrincipalWindow()
        {
            InitializeComponent();
        }

        // --- LÓGICA DEL MENÚ ---

        private void salirMenuItem_Click(object sender, RoutedEventArgs e)
        {
            this.Close();
        }

        private void usuariosMenuItem_Click(object sender, RoutedEventArgs e)
        {
            UsuariosControl controlUsuarios = new UsuariosControl();
            mainContentArea.Content = controlUsuarios;
        }

        private void clientesMenuItem_Click(object sender, RoutedEventArgs e)
        {
            ClientesControl controlClientes = new ClientesControl();
            mainContentArea.Content = controlClientes;
        }

        private void productosMenuItem_Click(object sender, RoutedEventArgs e)
        {
            ProductosControl controlProductos = new ProductosControl();
            mainContentArea.Content = controlProductos;
        }

        // --- CERRAR LA APLICACIÓN ---

        private void Window_Closing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            Application.Current.Shutdown();
        }

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
    }
}