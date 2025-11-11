using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using SchettiniGestion; // ¡Importamos la lógica Y la clase FacturaItem!
using System.Threading.Tasks;

namespace SchettiniGestion.WPF
{
    // --- ¡LA CLASE 'FacturaItem' SE MUDÓ A DatabaseService.cs! ---
    // (Esto la hace accesible globalmente)

    public partial class FacturacionControl : UserControl
    {
        private int _clienteIDSeleccionado = 1;
        private DataRow _productoSeleccionado = null;
        private List<FacturaItem> _itemsFactura = new List<FacturaItem>();
        private bool _ignorarPerdidaFoco = false;

        public FacturacionControl()
        {
            InitializeComponent();
        }

        private void FacturacionControl_Loaded(object sender, RoutedEventArgs e)
        {
            IniciarNuevaFactura();
        }

        private void IniciarNuevaFactura()
        {
            _clienteIDSeleccionado = 1;
            lblClienteSeleccionado.Text = "Consumidor Final";
            txtBuscarCliente.Clear();

            _productoSeleccionado = null;
            txtBuscarProducto.Clear();
            numCantidad.Value = 1;

            _itemsFactura.Clear();
            cmbTipoComprobante.SelectedIndex = 0;
            ActualizarGrillaYTotal();

            popupCliente.IsOpen = false;
            popupProducto.IsOpen = false;

            txtBuscarCliente.Focus();
        }

        private void ActualizarGrillaYTotal()
        {
            dgvFactura.ItemsSource = null;
            dgvFactura.ItemsSource = _itemsFactura;
            decimal total = _itemsFactura.Sum(item => item.Subtotal);
            lblTotal.Text = total.ToString("C2");
        }

        // --- BÚSQUEDA PREDICTIVA ---
        #region LogicaBusquedaPredictiva

        private void txtBuscarCliente_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (txtBuscarCliente.Text.Length < 2) { popupCliente.IsOpen = false; return; }
            DataTable clientes = DatabaseService.BuscarClientesMultiples(txtBuscarCliente.Text);
            if (clientes.Rows.Count > 0) { lstSugerenciasCliente.ItemsSource = clientes.DefaultView; popupCliente.IsOpen = true; }
            else { popupCliente.IsOpen = false; }
        }
        private void lstSugerenciasCliente_MouseUp(object sender, MouseButtonEventArgs e)
        {
            if (lstSugerenciasCliente.SelectedItem is DataRowView filaSeleccionada) { SeleccionarCliente(filaSeleccionada); }
        }
        private void SeleccionarCliente(DataRowView filaSeleccionada)
        {
            _clienteIDSeleccionado = Convert.ToInt32(filaSeleccionada["ClienteID"]);
            lblClienteSeleccionado.Text = filaSeleccionada["RazonSocial"].ToString();
            _ignorarPerdidaFoco = true;
            txtBuscarCliente.Text = filaSeleccionada["RazonSocial"].ToString();
            _ignorarPerdidaFoco = false;
            popupCliente.IsOpen = false;
            txtBuscarProducto.Focus();
        }
        private void txtBuscarProducto_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (txtBuscarProducto.Text.Length < 2) { popupProducto.IsOpen = false; _productoSeleccionado = null; return; }
            DataTable productos = DatabaseService.BuscarProductosMultiples(txtBuscarProducto.Text);
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
            _ignorarPerdidaFoco = false;
            popupProducto.IsOpen = false;
            numCantidad.Focus();
        }
        private void txtBuscar_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            ListBox popupList = null;
            if (sender == txtBuscarCliente && popupCliente.IsOpen) popupList = lstSugerenciasCliente;
            else if (sender == txtBuscarProducto && popupProducto.IsOpen) popupList = lstSugerenciasProducto;

            if (popupList != null)
            {
                if (e.Key == Key.Down) { popupList.SelectedIndex = 0; popupList.Focus(); e.Handled = true; }
                else if (e.Key == Key.Escape) { popupCliente.IsOpen = false; popupProducto.IsOpen = false; e.Handled = true; }
            }
            if (e.Key == Key.Enter)
            {
                if (sender == txtBuscarCliente) txtBuscarCliente_Enter();
                if (sender == txtBuscarProducto) txtBuscarProducto_Enter();
            }
        }
        private void lstSugerencias_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter)
            {
                if (sender == lstSugerenciasCliente && lstSugerenciasCliente.SelectedItem is DataRowView cliente) SeleccionarCliente(cliente);
                else if (sender == lstSugerenciasProducto && lstSugerenciasProducto.SelectedItem is DataRowView producto) SeleccionarProducto(producto);
                e.Handled = true;
            }
        }
        private async void txtBuscar_LostFocus(object sender, RoutedEventArgs e)
        {
            if (_ignorarPerdidaFoco) return;
            await Task.Delay(150);
            if (sender == txtBuscarCliente && !lstSugerenciasCliente.IsFocused) popupCliente.IsOpen = false;
            if (sender == txtBuscarProducto && !lstSugerenciasProducto.IsFocused) popupProducto.IsOpen = false;
        }
        private void txtBuscarCliente_Enter()
        {
            if (string.IsNullOrWhiteSpace(txtBuscarCliente.Text)) { _clienteIDSeleccionado = 1; lblClienteSeleccionado.Text = "Consumidor Final"; txtBuscarProducto.Focus(); return; }
            DataRow cliente = DatabaseService.BuscarCliente(txtBuscarCliente.Text);
            if (cliente != null) { _clienteIDSeleccionado = Convert.ToInt32(cliente["ClienteID"]); lblClienteSeleccionado.Text = cliente["RazonSocial"].ToString(); txtBuscarProducto.Focus(); }
            else { System.Windows.MessageBox.Show("Cliente no encontrado.", "Aviso"); _clienteIDSeleccionado = 1; lblClienteSeleccionado.Text = "Consumidor Final"; }
        }
        private void txtBuscarProducto_Enter()
        {
            if (string.IsNullOrWhiteSpace(txtBuscarProducto.Text)) return;
            DataRow producto = DatabaseService.BuscarProducto(txtBuscarProducto.Text);
            if (producto != null) { _productoSeleccionado = producto; numCantidad.Focus(); }
            else { System.Windows.MessageBox.Show("Producto no encontrado.", "Error"); _productoSeleccionado = null; }
        }

        #endregion

        // --- BOTONES DE ACCIÓN ---

        private void btnAgregarProducto_Click(object sender, RoutedEventArgs e)
        {
            if (_productoSeleccionado == null)
            {
                System.Windows.MessageBox.Show("Primero debe buscar y seleccionar un producto.", "Error", MessageBoxButton.OK, MessageBoxImage.Warning);
                txtBuscarProducto.Focus();
                return;
            }

            int stockDisponible = Convert.ToInt32(_productoSeleccionado["StockActual"]);
            int cantidadAgregar = (int)(numCantidad.Value ?? 1);

            FacturaItem itemExistente = _itemsFactura.FirstOrDefault(item => item.ProductoID == Convert.ToInt32(_productoSeleccionado["ProductoID"]));
            int cantidadEnCarrito = (itemExistente != null) ? itemExistente.Cantidad : 0;

            if (cantidadAgregar + cantidadEnCarrito > stockDisponible)
            {
                System.Windows.MessageBox.Show($"Stock insuficiente. Stock disponible: {stockDisponible} (ya tiene {cantidadEnCarrito} en el carrito).", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            if (itemExistente != null)
            {
                itemExistente.Cantidad += cantidadAgregar;
            }
            else
            {
                FacturaItem nuevoItem = new FacturaItem
                {
                    ProductoID = Convert.ToInt32(_productoSeleccionado["ProductoID"]),
                    Codigo = _productoSeleccionado["Codigo"].ToString(),
                    Descripcion = _productoSeleccionado["Descripcion"].ToString(),
                    PrecioUnitario = Convert.ToDecimal(_productoSeleccionado["PrecioVenta"]),
                    Cantidad = cantidadAgregar
                };
                _itemsFactura.Add(nuevoItem);
            }

            ActualizarGrillaYTotal();
            _productoSeleccionado = null;
            txtBuscarProducto.Clear();
            numCantidad.Value = 1;
            txtBuscarProducto.Focus();
        }

        private void btnEliminarItem_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button botonEliminar && botonEliminar.DataContext is FacturaItem itemParaBorrar)
            {
                _itemsFactura.Remove(itemParaBorrar);
                ActualizarGrillaYTotal();
            }
        }

        private void btnGuardarFactura_Click(object sender, RoutedEventArgs e)
        {
            // 1. Validar que el carrito no esté vacío
            if (_itemsFactura.Count == 0)
            {
                System.Windows.MessageBox.Show("Debe agregar al menos un producto a la factura.", "Error", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            // 2. Pedir confirmación
            decimal total = _itemsFactura.Sum(item => item.Subtotal);
            MessageBoxResult confirmacion = System.Windows.MessageBox.Show($"El total de la factura es: {total.ToString("C2")}\n\n¿Desea confirmar la venta?",
                                                      "Confirmar Venta",
                                                      MessageBoxButton.YesNo,
                                                      MessageBoxImage.Question);

            if (confirmacion == MessageBoxResult.No)
            {
                return;
            }

            // 3. Recolectar datos para la DB
            int clienteID = _clienteIDSeleccionado;
            string tipoComprobante = (cmbTipoComprobante.SelectedItem as ComboBoxItem)?.Content.ToString() ?? "Ticket";

            // 4. Llamar al servicio de base de datos
            bool exito = DatabaseService.GuardarFactura(clienteID, tipoComprobante, total, _itemsFactura);

            // 5. Feedback y Reset
            if (exito)
            {
                System.Windows.MessageBox.Show("¡Venta guardada exitosamente!", "Éxito", MessageBoxButton.OK, MessageBoxImage.Information);
                // (Aquí iría la lógica para imprimir el ticket/factura)
                IniciarNuevaFactura(); // Limpiamos la pantalla para la próxima venta
            }
            else
            {
                // El DatabaseService ya mostró un error detallado
                System.Windows.MessageBox.Show("No se pudo guardar la factura. El stock no fue descontado.", "Error Grave", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void btnCancelarFactura_Click(object sender, RoutedEventArgs e)
        {
            IniciarNuevaFactura();
        }
    }
}