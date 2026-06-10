using System;
using System.Windows;
using System.Windows.Controls; // Necesario para ComboBoxItem
using System.Windows.Input;
using System.Text.RegularExpressions;

namespace SchettiniGestion.WPF
{
    public partial class ProductoVarioWindow : Window
    {
        public string Descripcion { get; private set; }
        public decimal Precio { get; private set; }
        public string IVA { get; private set; }
        public bool Confirmado { get; private set; } = false;

        public ProductoVarioWindow()
        {
            InitializeComponent();
        }

        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            txtDescripcion.Focus();
        }

        private void btnAgregar_Click(object sender, RoutedEventArgs e)
        {
            // 1. Validaciones básicas
            if (string.IsNullOrWhiteSpace(txtDescripcion.Text))
            {
                ModernMessageBox.Show("Ingrese una descripción.", "Falta dato", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            if (!decimal.TryParse(txtPrecio.Text.Replace(".", ","), out decimal precioIngresado))
            {
                ModernMessageBox.Show("Precio inválido.", "Error", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            // 2. Obtener la alícuota de IVA seleccionada
            decimal alicuotaIVA = 0;
            if (cmbIVA.SelectedItem is ComboBoxItem item)
            {
                // Convertimos el texto "21.0" a número decimal 21
                decimal.TryParse(item.Content.ToString().Replace(".", ","), out alicuotaIVA);
            }

            // 3. CALCULAR EL PRECIO FINAL CON IVA
            // Fórmula: PrecioIngresado * (1 + (21 / 100)) = PrecioIngresado * 1.21
            decimal precioFinal = precioIngresado * (1 + (alicuotaIVA / 100));

            // 4. Guardar los datos para que los lea la pantalla de facturación
            Descripcion = txtDescripcion.Text.ToUpper();
            Precio = Math.Round(precioFinal, 2); // Redondeamos a 2 decimales para prolijidad
            IVA = alicuotaIVA.ToString("0.0");

            Confirmado = true;
            this.Close();
        }

        private void btnCancelar_Click(object sender, RoutedEventArgs e)
        {
            this.Close();
        }

        private void SoloNumeros_PreviewTextInput(object sender, TextCompositionEventArgs e)
        {
            // Regex para permitir solo números y una coma/punto
            Regex regex = new Regex("[^0-9,.]");
            e.Handled = regex.IsMatch(e.Text);
        }
    }
}