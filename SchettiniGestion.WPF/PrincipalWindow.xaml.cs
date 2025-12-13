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
// --- ¡BORRAMOS LOS USINGS VIEJOS! ---
// (Ya no necesitamos System.Diagnostics ni System.IO aquí)

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

        // --- ¡INICIO DE LA MODIFICACIÓN (LA BUENA)! ---
        private void btnTeclado_Click(object sender, RoutedEventArgs e)
        {
            // ¡Llamamos a nuestro nuevo ayudante!
            KeyboardHelper.ShowOnScreenKeyboard();
        }
        // --- ¡FIN DE LA MODIFICACIÓN (LA BUENA)! ---
    }
}