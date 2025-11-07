using SchettiniGestion; // <-- ¡Nuestro Cerebro!
using System;
using System.Data; // <-- Para usar DataTable
using System.Windows;
using System.Windows.Controls;
// ¡Importamos el Toolkit!
using Xceed.Wpf.Toolkit;

namespace SchettiniGestion.WPF
{
    /// <summary>
    /// Lógica de interacción para ProductosControl.xaml
    /// </summary>
    public partial class ProductosControl : UserControl
    {
        private int _productoIDSeleccionado = 0;

        public ProductosControl()
        {
            InitializeComponent();
        }

        // --- 1. MÉTODOS DE CARGA ---

        private void ProductosControl_Loaded(object sender, RoutedEventArgs e)
        {
            ConfigurarControlesNumericos();
            CargarProductos();
            LimpiarCampos();
        }

        private void ConfigurarControlesNumericos()
        {
            // --- ¡ESTA ES LA CORRECCIÓN! ---
            // Borramos las líneas que daban error (DecimalPlaces y Minimum)
            // 'FormatString' ya hace el trabajo de formatear.

            // Configuración para el Precio de Venta
            numPrecioVenta.FormatString = "C2"; // "C2" = Formato Moneda (ej: $1,250.50)
            numPrecioVenta.AllowSpin = true; // Permite usar las flechitas

            // Configuración para el Stock
            numStockActual.FormatString = "N0"; // "N0" = Número sin decimales
            numStockActual.AllowSpin = true;
        }

        private void CargarProductos()
        {
            try
            {
                DataTable dt = DatabaseService.GetProductos();
                dvgProductos.ItemsSource = dt.DefaultView;
            }
            catch (Exception ex)
            {
                // Especificamos 'System.Windows' para evitar la ambigüedad
                System.Windows.MessageBox.Show($"Error al cargar productos: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        // --- 2. LÓGICA DE LOS BOTONES ---

        private void btnNuevo_Click(object sender, RoutedEventArgs e)
        {
            LimpiarCampos();
        }

        private void btnGuardar_Click(object sender, RoutedEventArgs e)
        {
            // 1. Validar que la descripción no esté vacía
            if (string.IsNullOrWhiteSpace(txtDescripcion.Text))
            {
                System.Windows.MessageBox.Show("La descripción del producto no puede estar vacía.", "Error", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            // 2. Obtener los valores de los campos
            string codigo = txtCodigo.Text.Trim();
            string descripcion = txtDescripcion.Text.Trim();
            decimal precio = numPrecioVenta.Value ?? 0;
            int stock = numStockActual.Value ?? 0;

            // 3. Llamar al servicio de base de datos para guardar
            bool exito = DatabaseService.GuardarProducto(_productoIDSeleccionado, codigo, descripcion, precio, stock);

            if (exito)
            {
                System.Windows.MessageBox.Show("Producto guardado exitosamente.", "Éxito", MessageBoxButton.OK, MessageBoxImage.Information);

                CargarProductos();
                LimpiarCampos();
            }
        }

        private void btnEliminar_Click(object sender, RoutedEventArgs e)
        {
            // 1. Validar que haya un producto seleccionado
            if (_productoIDSeleccionado == 0)
            {
                System.Windows.MessageBox.Show("Por favor, seleccione un producto de la grilla para eliminar.", "Error", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            // 2. Pedir confirmación
            MessageBoxResult confirmacion = System.Windows.MessageBox.Show($"¿Está seguro de que desea eliminar el producto '{txtDescripcion.Text}'?",
                                                      "Confirmar eliminación",
                                                      MessageBoxButton.YesNo,
                                                      MessageBoxImage.Warning);

            if (confirmacion == MessageBoxResult.Yes)
            {
                // 3. Llamar al servicio para eliminar
                bool exito = DatabaseService.EliminarProducto(_productoIDSeleccionado);

                if (exito)
                {
                    System.Windows.MessageBox.Show("Producto eliminado exitosamente.", "Éxito", MessageBoxButton.OK, MessageBoxImage.Information);

                    CargarProductos();
                    LimpiarCampos();
                }
            }
        }

        // --- 3. MÉTODOS AYUDANTES ---

        private void LimpiarCampos()
        {
            _productoIDSeleccionado = 0;
            txtCodigo.Text = "";
            txtDescripcion.Text = "";
            numPrecioVenta.Value = 0;
            numStockActual.Value = 0;

            dvgProductos.UnselectAll();
        }

        private void dvgProductos_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (dvgProductos.SelectedItem is DataRowView filaSeleccionada)
            {
                _productoIDSeleccionado = Convert.ToInt32(filaSeleccionada["ProductoID"]);
                txtCodigo.Text = filaSeleccionada["Codigo"].ToString();
                txtDescripcion.Text = filaSeleccionada["Descripcion"].ToString();
                numPrecioVenta.Value = Convert.ToDecimal(filaSeleccionada["PrecioVenta"]);
                numStockActual.Value = Convert.ToInt32(filaSeleccionada["StockActual"]);
            }
        }
    }
}