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
using System.Diagnostics;
using System.IO;

namespace SchettiniGestion.WPF
{
    public partial class PrincipalWindow : Window
    {
        public PrincipalWindow()
        {
            InitializeComponent();
            btnInicio_Click(null, null); // Cargamos la pantalla de "Inicio" por defecto
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

        private void btnInicio_Click(object sender, RoutedEventArgs e)
        {
            mainContentArea.Content = new TextBlock
            {
                Text = "¡Bienvenido a SchettiniGestion!",
                FontSize = 48,
                FontWeight = FontWeights.Bold,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center,
                Foreground = new SolidColorBrush(Color.FromRgb(119, 119, 119)) // #777
            };
        }

        private void btnFacturacion_Click(object sender, RoutedEventArgs e)
        {
            FacturacionControl controlFacturacion = new FacturacionControl();
            mainContentArea.Content = controlFacturacion;
        }

        private void btnVentas_Click(object sender, RoutedEventArgs e)
        {
            VentasControl controlVentas = new VentasControl();
            mainContentArea.Content = controlVentas;
        }

        
        private void btnStock_Click(object sender, RoutedEventArgs e)
        {
            // ¡Ahora carga el control de Ajuste de Stock!
            StockControl controlStock = new StockControl();
            mainContentArea.Content = controlStock;
        }
        

        
        private void Window_Closing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            Application.Current.Shutdown();
        }

        private void btnTeclado_Click(object sender, RoutedEventArgs e)
        {
            KeyboardHelper.ShowOnScreenKeyboard();
        }
    }
}