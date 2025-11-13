using System;
using System.Collections.Generic;
using System.Data;
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
using SchettiniGestion; // ¡Nuestro Cerebro!

namespace SchettiniGestion.WPF
{
    /// <summary>
    /// Lógica de interacción para StockControl.xaml
    /// </summary>
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
            // Cargamos los motivos de ajuste
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
                MessageBox.Show("Debe seleccionar un producto.", "Error", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            int cantidad = numCantidad.Value ?? 0;
            if (cantidad == 0)
            {
                MessageBox.Show("La cantidad no puede ser cero.", "Error", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            if (cmbTipoMovimiento.SelectedItem == null)
            {
                MessageBox.Show("Debe seleccionar un tipo de movimiento.", "Error", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            string tipoMovimiento = cmbTipoMovimiento.SelectedItem.ToString();

            // ===== ¡CORRECCIÓN DE LÓGICA! =====
            // Si el movimiento es de resta, la cantidad debe ser negativa
            if (tipoMovimiento.Contains("(Resta)") && cantidad > 0)
            {
                cantidad = cantidad * -1;
            }
            // Si el movimiento es de suma, la cantidad debe ser positiva
            if (tipoMovimiento.Contains("(Suma)") && cantidad < 0)
            {
                cantidad = cantidad * -1;
            }
            if (tipoMovimiento.Contains("Ingreso por Compra") && cantidad < 0)
            {
                cantidad = cantidad * -1;
            }
            // ===== FIN DE CORRECCIÓN =====


            // 2. Confirmación
            int productoID = Convert.ToInt32(_productoSeleccionado["ProductoID"]);
            string nombreProducto = _productoSeleccionado["Descripcion"].ToString();

            MessageBoxResult confirmacion = MessageBox.Show(
                $"¿Está seguro que desea registrar el siguiente movimiento?\n\n" +
                $"Producto: {nombreProducto}\n" +
                $"Cantidad: {cantidad}\n" + // Mostrará la cantidad ya con el signo
                $"Motivo: {tipoMovimiento}",
                "Confirmar Movimiento",
                MessageBoxButton.YesNo,
                MessageBoxImage.Question);

            if (confirmacion == MessageBoxResult.No)
            {
                return;
            }

            // 3. Llamar a la Base de Datos
            bool exito = DatabaseService.AjustarStock(productoID, cantidad, tipoMovimiento);

            if (exito)
            {
                MessageBox.Show("¡Movimiento de stock guardado exitosamente!", "Éxito", MessageBoxButton.OK, MessageBoxImage.Information);
                LimpiarCampos();
            }
            else
            {
                MessageBox.Show("No se pudo guardar el movimiento.", "Error Grave", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        // --- Lógica de Búsqueda Predictiva (copiada de FacturacionControl) ---
        #region LogicaBusquedaPredictiva

        private void txtBuscarProducto_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (txtBuscarProducto.Text.Length < 2) { popupProducto.IsOpen = false; _productoSeleccionado = null; lblProductoSeleccionado.Text = "Seleccione un producto..."; return; }

            // ===== ¡AQUÍ ESTÁ LA CORRECCIÓN! =====
            // Usamos el método _ParaCompra que no filtra por stock
            DataTable productos = DatabaseService.BuscarProductosMultiples_ParaCompra(txtBuscarProducto.Text);
            // ===== FIN DE LA CORRECCIÓN =====

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
            await Task.Delay(150);
            if (!lstSugerenciasProducto.IsFocused) { popupProducto.IsOpen = false; }
        }

        #endregion
    }
}