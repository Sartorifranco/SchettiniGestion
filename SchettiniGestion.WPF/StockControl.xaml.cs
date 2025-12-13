using System;
using System.Data;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using SchettiniGestion;

namespace SchettiniGestion.WPF
{
    public partial class StockControl : UserControl
    {
        private DataRow _productoSeleccionado = null;
        private bool _ignorarPerdidaFoco = false;

        public StockControl()
        {
            InitializeComponent();
        }

        private void StockControl_Loaded(object sender, RoutedEventArgs e)
        {
            cmbTipoMovimiento.Items.Clear();
            cmbTipoMovimiento.Items.Add("Ingreso por Compra");
            cmbTipoMovimiento.Items.Add("Ajuste Manual (Suma)");
            cmbTipoMovimiento.Items.Add("Ajuste por Rotura (Resta)");
            cmbTipoMovimiento.Items.Add("Ajuste Manual (Resta)");
            cmbTipoMovimiento.SelectedIndex = 0;

            LimpiarCampos();
        }

        private void LimpiarCampos()
        {
            _productoSeleccionado = null;
            lblProductoSeleccionado.Text = "Seleccione un producto...";
            txtBuscarProducto.Clear();
            numCantidad.Value = 0;
            cmbTipoMovimiento.SelectedIndex = 0;
            popupProducto.IsOpen = false;
            txtBuscarProducto.Focus();
        }

        private void btnGuardarAjuste_Click(object sender, RoutedEventArgs e)
        {
            // 1. Validaciones
            if (_productoSeleccionado == null)
            {
                CustomMessageBox.Show("Debe seleccionar un producto.", "Error", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            int cantidad = numCantidad.Value ?? 0;
            if (cantidad == 0)
            {
                CustomMessageBox.Show("La cantidad no puede ser cero.", "Error", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            if (cmbTipoMovimiento.SelectedItem == null)
            {
                CustomMessageBox.Show("Debe seleccionar un tipo de movimiento.", "Error", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            string tipoMovimiento = cmbTipoMovimiento.SelectedItem.ToString();

            // Corrección de signo
            if ((tipoMovimiento.Contains("(Resta)") || tipoMovimiento.Contains("Rotura")) && cantidad > 0)
            {
                cantidad = cantidad * -1;
            }
            // Asegurar positivo para ingresos/sumas si el usuario puso negativo por error
            if ((tipoMovimiento.Contains("(Suma)") || tipoMovimiento.Contains("Ingreso")) && cantidad < 0)
            {
                cantidad = cantidad * -1;
            }

            // 2. Confirmación
            int productoID = Convert.ToInt32(_productoSeleccionado["ProductoID"]);
            string nombreProducto = _productoSeleccionado["Descripcion"].ToString();

            MessageBoxResult confirmacion = CustomMessageBox.Show(
                $"¿Está seguro que desea registrar el siguiente movimiento?\n\n" +
                $"Producto: {nombreProducto}\n" +
                $"Cantidad: {cantidad}\n" +
                $"Motivo: {tipoMovimiento}",
                "Confirmar Movimiento",
                MessageBoxButton.YesNo,
                MessageBoxImage.Question);

            if (confirmacion == MessageBoxResult.No) return;

            // 3. Llamar a la Base de Datos
            bool exito = DatabaseService.AjustarStock(productoID, cantidad, tipoMovimiento);

            if (exito)
            {
                CustomMessageBox.Show("¡Movimiento de stock guardado exitosamente!", "Éxito", MessageBoxButton.OK, MessageBoxImage.Information);
                LimpiarCampos();
            }
            else
            {
                CustomMessageBox.Show("No se pudo guardar el movimiento.", "Error Grave", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        // --- Lógica de Búsqueda ---
        #region LogicaBusquedaPredictiva

        private void txtBuscarProducto_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (txtBuscarProducto.Text.Length < 2) { popupProducto.IsOpen = false; _productoSeleccionado = null; lblProductoSeleccionado.Text = "Seleccione un producto..."; return; }

            // Usamos _ParaCompra porque muestra todos los productos (con o sin stock)
            DataTable productos = DatabaseService.BuscarProductosMultiples_ParaCompra(txtBuscarProducto.Text);

            if (productos.Rows.Count > 0) { lstSugerenciasProducto.ItemsSource = productos.DefaultView; popupProducto.IsOpen = true; }
            else { popupProducto.IsOpen = false; _productoSeleccionado = null; }
        }

        private void lstSugerenciasProducto_MouseUp(object sender, MouseButtonEventArgs e)
        {
            if (lstSugerenciasProducto.SelectedItem is DataRowView filaSeleccionada) { SeleccionarProducto(filaSeleccionada); }
        }

        private void SeleccionarProducto(DataRowView filaSeleccionada)
        {
            _productoSeleccionado = filaSeleccionada.Row;
            _ignorarPerdidaFoco = true;
            txtBuscarProducto.Text = filaSeleccionada["Descripcion"].ToString();
            lblProductoSeleccionado.Text = $"ID: {filaSeleccionada["ProductoID"]} | Stock Actual: {filaSeleccionada["StockActual"]}";
            _ignorarPerdidaFoco = false;
            popupProducto.IsOpen = false;
            numCantidad.Focus();
        }

        private void txtBuscar_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            if (popupProducto.IsOpen)
            {
                if (e.Key == Key.Down) { lstSugerenciasProducto.SelectedIndex = 0; lstSugerenciasProducto.Focus(); e.Handled = true; }
                else if (e.Key == Key.Escape) { popupProducto.IsOpen = false; e.Handled = true; }
            }
        }

        private void lstSugerencias_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter)
            {
                if (lstSugerenciasProducto.SelectedItem is DataRowView producto) { SeleccionarProducto(producto); }
                e.Handled = true;
            }
        }

        private async void txtBuscar_LostFocus(object sender, RoutedEventArgs e)
        {
            if (_ignorarPerdidaFoco) return;
            await System.Threading.Tasks.Task.Delay(150);
            if (!lstSugerenciasProducto.IsFocused) { popupProducto.IsOpen = false; }
        }

        #endregion
    }
}