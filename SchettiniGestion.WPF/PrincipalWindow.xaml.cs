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
            // 1. Creamos una instancia de nuestro NUEVO control de clientes
            ClientesControl controlClientes = new ClientesControl();

            // 2. Lo asignamos al área de contenido
            mainContentArea.Content = controlClientes;
        }

        private void productosMenuItem_Click(object sender, RoutedEventArgs e)
        {
            // Próximo paso: Crear y mostrar el ProductosControl
            MessageBox.Show("Próximamente: ABM de Productos");
        }

        // --- CERRAR LA APLICACIÓN ---

        private void Window_Closing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            Application.Current.Shutdown();
        }
    }
}