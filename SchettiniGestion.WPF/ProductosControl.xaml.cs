using SchettiniGestion; // ¡Importante!
using System;
using System.Data;
using System.Windows;
using System.Windows.Controls;

namespace SchettiniGestion.WPF
{
    public partial class ProductosControl : UserControl
    {
        private int _productoIDSeleccionado = 0;

        public ProductosControl()
        {
            InitializeComponent();
        }

        private void ProductosControl_Loaded(object sender, RoutedEventArgs e)
        {
            CargarDatos();
        }

        private void CargarDatos()
        {
            try
            {
                // GetProductos() ahora trae la columna PrecioCosto
                dgvProductos.ItemsSource = DatabaseService.GetProductos().DefaultView;
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error al cargar productos: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void LimpiarCampos()
        {
            _productoIDSeleccionado = 0;
            txtCodigo.Text = "";
            txtDescripcion.Text = "";
            numPrecioCosto.Value = 0; // ¡AÑADIDO!
            numPrecioVenta.Value = 0;
            numStock.Value = 0;

            btnGuardar.Content = "💾 Guardar";
            btnEliminar.IsEnabled = false;
            dgvProductos.SelectedIndex = -1;
            txtCodigo.Focus();
        }

        private void dgvProductos_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (dgvProductos.SelectedItem is DataRowView drv)
            {
                _productoIDSeleccionado = Convert.ToInt32(drv["ProductoID"]);
                txtCodigo.Text = drv["Codigo"].ToString();
                txtDescripcion.Text = drv["Descripcion"].ToString();
                numPrecioCosto.Value = Convert.ToDecimal(drv["PrecioCosto"]); // ¡AÑADIDO!
                numPrecioVenta.Value = Convert.ToDecimal(drv["PrecioVenta"]);
                numStock.Value = Convert.ToInt32(drv["StockActual"]);

                btnGuardar.Content = "Modificar";
                btnEliminar.IsEnabled = true;
            }
        }

        private void btnNuevo_Click(object sender, RoutedEventArgs e)
        {
            LimpiarCampos();
        }

        // ===== ¡AQUÍ ESTÁ LA CORRECCIÓN (LÍNEA 84)! =====
        private void btnGuardar_Click(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrWhiteSpace(txtCodigo.Text) || string.IsNullOrWhiteSpace(txtDescripcion.Text))
            {
                MessageBox.Show("El Código y la Descripción son obligatorios.", "Datos Incompletos", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            bool exito = DatabaseService.GuardarProducto(
                _productoIDSeleccionado,
                txtCodigo.Text,
                txtDescripcion.Text,
                numPrecioCosto.Value ?? 0, // ¡AÑADIDO!
                numPrecioVenta.Value ?? 0,
                numStock.Value ?? 0
            );

            if (exito)
            {
                MessageBox.Show("Producto guardado correctamente.", "Éxito", MessageBoxButton.OK, MessageBoxImage.Information);
                CargarDatos();
                LimpiarCampos();
            }
        }

        private void btnEliminar_Click(object sender, RoutedEventArgs e)
        {
            if (_productoIDSeleccionado == 0) return;

            if (MessageBox.Show("¿Está seguro de que desea eliminar este producto?", "Confirmar Eliminación", MessageBoxButton.YesNo, MessageBoxImage.Warning) == MessageBoxResult.Yes)
            {
                bool exito = DatabaseService.EliminarProducto(_productoIDSeleccionado);
                if (exito)
                {
                    MessageBox.Show("Producto eliminado.", "Éxito", MessageBoxButton.OK, MessageBoxImage.Information);
                    CargarDatos();
                    LimpiarCampos();
                }
            }
        }
    }
}