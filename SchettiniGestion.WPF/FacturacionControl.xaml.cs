using SchettiniGestion; // Importante
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel; // Para el Carrito
using System.Data;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Threading.Tasks; // Para el LostFocus

namespace SchettiniGestion.WPF
{
    public partial class FacturacionControl : UserControl
    {
        private ObservableCollection<FacturaItem> CarritoDeVenta;
        private DataRow _clienteSeleccionado;
        private DataRow _productoSeleccionado;
        private bool _ignorarPerdidaFoco = false; // Para los popups

        public FacturacionControl()
        {
            InitializeComponent();
            CarritoDeVenta = new ObservableCollection<FacturaItem>();
            dgvFactura.ItemsSource = CarritoDeVenta; // Sincronizado con tu XAML
        }

        private void FacturacionControl_Loaded(object sender, RoutedEventArgs e) // Sincronizado
        {
            CargarClientePorDefecto();
            LimpiarFormulario();
            cmbTipoComprobante.SelectedIndex = 0; // Sincronizado
        }

        private void CargarClientePorDefecto()
        {
            try
            {
                _clienteSeleccionado = DatabaseService.BuscarCliente("00-00000000-0");
                if (_clienteSeleccionado != null)
                {
                    lblClienteSeleccionado.Text = _clienteSeleccionado["RazonSocial"].ToString(); // Sincronizado
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error al cargar cliente por defecto: {ex.Message}");
            }
        }

        // --- 1. LÓGICA DE BÚSQUEDA (CLIENTE Y PRODUCTOS) ---

        // Este método ahora se dispara con TextChanged, no con un botón
        private void txtBuscarCliente_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (txtBuscarCliente.Text.Length < 2)
            {
                popupCliente.IsOpen = false;
                return;
            }

            try
            {
                DataTable dt = DatabaseService.BuscarClientesMultiples(txtBuscarCliente.Text);
                lstSugerenciasCliente.ItemsSource = dt.DefaultView;
                popupCliente.IsOpen = dt.Rows.Count > 0;
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error al buscar clientes: {ex.Message}");
            }
        }

        private void SeleccionarCliente(DataRowView filaSeleccionada)
        {
            _clienteSeleccionado = filaSeleccionada.Row;
            _ignorarPerdidaFoco = true;
            txtBuscarCliente.Text = filaSeleccionada["RazonSocial"].ToString();
            lblClienteSeleccionado.Text = filaSeleccionada["RazonSocial"].ToString();
            _ignorarPerdidaFoco = false;
            popupCliente.IsOpen = false;
            txtBuscarProducto.Focus();
        }

        private void lstSugerenciasCliente_MouseUp(object sender, MouseButtonEventArgs e)
        {
            if (lstSugerenciasCliente.SelectedItem is DataRowView drv)
            {
                SeleccionarCliente(drv);
            }
        }

        // --- Búsqueda de Productos ---

        private void txtBuscarProducto_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (_ignorarPerdidaFoco) return;
            if (txtBuscarProducto.Text.Length < 2)
            {
                popupProducto.IsOpen = false;
                _productoSeleccionado = null;
                return;
            }

            try
            {
                // ¡Llamada corregida al método _ParaVenta!
                DataTable dt = DatabaseService.BuscarProductosMultiples_ParaVenta(txtBuscarProducto.Text);
                lstSugerenciasProducto.ItemsSource = dt.DefaultView;
                popupProducto.IsOpen = dt.Rows.Count > 0;
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error al buscar productos: {ex.Message}");
            }
        }

        private void SeleccionarProducto(DataRowView filaSeleccionada)
        {
            _productoSeleccionado = filaSeleccionada.Row;
            _ignorarPerdidaFoco = true;
            txtBuscarProducto.Text = filaSeleccionada["Descripcion"].ToString();
            _ignorarPerdidaFoco = false;
            popupProducto.IsOpen = false;
            numCantidad.Focus();
        }

        private void lstSugerenciasProducto_MouseUp(object sender, MouseButtonEventArgs e)
        {
            if (lstSugerenciasProducto.SelectedItem is DataRowView drv)
            {
                SeleccionarProducto(drv);
            }
        }

        // --- Manejo de Foco y Teclas en Popups ---

        private async void txtBuscar_LostFocus(object sender, RoutedEventArgs e)
        {
            if (_ignorarPerdidaFoco) return;
            // Espera un momento para permitir que el clic en la lista se registre primero
            await Task.Delay(150);
            if (!lstSugerenciasCliente.IsFocused && !lstSugerenciasProducto.IsFocused)
            {
                popupCliente.IsOpen = false;
                popupProducto.IsOpen = false;
            }
        }

        private void txtBuscar_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Down)
            {
                if (popupCliente.IsOpen)
                {
                    lstSugerenciasCliente.SelectedIndex = 0;
                    lstSugerenciasCliente.Focus();
                    e.Handled = true;
                }
                else if (popupProducto.IsOpen)
                {
                    lstSugerenciasProducto.SelectedIndex = 0;
                    lstSugerenciasProducto.Focus();
                    e.Handled = true;
                }
            }
            else if (e.Key == Key.Escape)
            {
                popupCliente.IsOpen = false;
                popupProducto.IsOpen = false;
                e.Handled = true;
            }
        }

        private void lstSugerencias_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter)
            {
                if (sender == lstSugerenciasCliente && lstSugerenciasCliente.SelectedItem is DataRowView cliente)
                {
                    SeleccionarCliente(cliente);
                    e.Handled = true;
                }
                else if (sender == lstSugerenciasProducto && lstSugerenciasProducto.SelectedItem is DataRowView producto)
                {
                    SeleccionarProducto(producto);
                    e.Handled = true;
                }
            }
        }

        // --- 2. LÓGICA DEL CARRITO ---

        private void btnAgregarProducto_Click(object sender, RoutedEventArgs e) // Sincronizado
        {
            if (_productoSeleccionado == null)
            {
                MessageBox.Show("Debe seleccionar un producto de la lista primero.", "Error", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            int productoID = Convert.ToInt32(_productoSeleccionado["ProductoID"]);
            int stockDisponible = Convert.ToInt32(_productoSeleccionado["StockActual"]);
            int cantidadDeseada = (int)numCantidad.Value;

            var itemExistente = CarritoDeVenta.FirstOrDefault(item => item.ProductoID == productoID);
            int cantidadEnCarrito = (itemExistente != null) ? itemExistente.Cantidad : 0;

            // Validar Stock
            if ((cantidadEnCarrito + cantidadDeseada) > stockDisponible)
            {
                MessageBox.Show($"Stock insuficiente. Stock disponible: {stockDisponible} unidades.", "Error de Stock", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            if (itemExistente != null)
            {
                itemExistente.Cantidad += cantidadDeseada;
                dgvFactura.Items.Refresh(); // Sincronizado
            }
            else
            {
                var nuevoItem = new FacturaItem
                {
                    ProductoID = productoID,
                    Codigo = _productoSeleccionado["Codigo"].ToString(),
                    Descripcion = _productoSeleccionado["Descripcion"].ToString(),
                    Cantidad = cantidadDeseada,
                    PrecioUnitario = Convert.ToDecimal(_productoSeleccionado["PrecioVenta"])
                };
                CarritoDeVenta.Add(nuevoItem);
            }

            LimpiarSeccionProducto();
            ActualizarTotal();
        }

        private void btnEliminarItem_Click(object sender, RoutedEventArgs e)
        {
            // El CommandParameter no es necesario, podemos obtener la fila del DataGrid
            if (dgvFactura.SelectedItem is FacturaItem item)
            {
                CarritoDeVenta.Remove(item);
                ActualizarTotal();
            }
            else if (sender is Button btn && btn.DataContext is FacturaItem itemDesdeBoton)
            {
                CarritoDeVenta.Remove(itemDesdeBoton);
                ActualizarTotal();
            }
        }

        private void ActualizarTotal()
        {
            decimal total = CarritoDeVenta.Sum(item => item.Subtotal);
            lblTotal.Text = $"{total:C2}"; // Sincronizado (Formato C2)
        }

        // --- 3. LÓGICA DE GUARDADO Y LIMPIEZA ---

        private void LimpiarSeccionProducto()
        {
            _productoSeleccionado = null;
            txtBuscarProducto.Text = "";
            numCantidad.Value = 1;
            txtBuscarProducto.Focus();
        }

        private void LimpiarFormulario()
        {
            CargarClientePorDefecto();
            cmbTipoComprobante.SelectedIndex = 0;
            CarritoDeVenta.Clear();
            ActualizarTotal();
            LimpiarSeccionProducto();
            txtBuscarCliente.Focus();
        }

        private void btnCancelarFactura_Click(object sender, RoutedEventArgs e) // Sincronizado
        {
            if (MessageBox.Show("¿Está seguro de que desea cancelar la venta actual? Se borrarán todos los ítems.", "Confirmar Cancelación", MessageBoxButton.YesNo, MessageBoxImage.Warning) == MessageBoxResult.Yes)
            {
                LimpiarFormulario();
            }
        }

        private void btnGuardarFactura_Click(object sender, RoutedEventArgs e) // Sincronizado
        {
            if (CarritoDeVenta.Count == 0)
            {
                MessageBox.Show("Debe agregar al menos un producto a la venta.", "Error", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }
            if (_clienteSeleccionado == null)
            {
                MessageBox.Show("Debe seleccionar un cliente.", "Error", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            if (MessageBox.Show("¿Está seguro de que desea guardar esta venta? Esta acción descontará stock.", "Confirmar Venta", MessageBoxButton.YesNo, MessageBoxImage.Question) == MessageBoxResult.No)
            {
                return;
            }

            try
            {
                bool exito = DatabaseService.GuardarFactura(
                    Convert.ToInt32(_clienteSeleccionado["ClienteID"]),
                    (cmbTipoComprobante.SelectedItem as ComboBoxItem).Content.ToString(), // Sincronizado
                    CarritoDeVenta.Sum(item => item.Subtotal),
                    CarritoDeVenta.ToList()
                );

                if (exito)
                {
                    MessageBox.Show("¡Venta guardada exitosamente! El stock ha sido actualizado.", "Éxito", MessageBoxButton.OK, MessageBoxImage.Information);
                    LimpiarFormulario();
                    // Aquí iría la lógica de impresión fiscal
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error fatal al guardar la venta: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
    }
}