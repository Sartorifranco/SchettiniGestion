using System.Globalization;
using System.Windows;

namespace SchettiniGestion.WPF
{
    public partial class LineaFacturaCompraManualWindow : Window
    {
        public string Codigo { get; private set; } = "";
        public string Descripcion { get; private set; } = "";
        public int Cantidad { get; private set; }
        public decimal CostoUnitario { get; private set; }

        public LineaFacturaCompraManualWindow(Window owner)
        {
            InitializeComponent();
            Owner = owner;
        }

        private void btnAgregar_Click(object sender, RoutedEventArgs e)
        {
            string descripcion = txtDescripcion.Text?.Trim() ?? "";
            if (descripcion.Length < 2)
            {
                ModernMessageBox.Show("Ingresá la descripción del producto tal como aparece en la factura.",
                    "Falta la descripción", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }
            if (!int.TryParse(txtCantidad.Text?.Trim(), out int cantidad) || cantidad <= 0)
            {
                ModernMessageBox.Show("La cantidad debe ser un número entero mayor que cero.",
                    "Cantidad inválida", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }
            if (!TryParseDecimal(txtCosto.Text, out decimal costo) || costo < 0)
            {
                ModernMessageBox.Show("Ingresá un costo unitario válido.",
                    "Costo inválido", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            Codigo = txtCodigo.Text?.Trim() ?? "";
            Descripcion = descripcion;
            Cantidad = cantidad;
            CostoUnitario = costo;
            DialogResult = true;
            Close();
        }

        private static bool TryParseDecimal(string texto, out decimal valor)
        {
            string t = (texto ?? "").Trim();
            if (decimal.TryParse(t, NumberStyles.Number, CultureInfo.GetCultureInfo("es-AR"), out valor))
                return true;
            return decimal.TryParse(t.Replace(",", "."), NumberStyles.Number,
                CultureInfo.InvariantCulture, out valor);
        }

        private void btnCancelar_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }
    }
}
