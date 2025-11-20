using SchettiniGestion;
using System;
using System.Collections.ObjectModel;
using System.Data;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace SchettiniGestion.WPF
{
    public partial class PresupuestosControl : UserControl
    {
        private ObservableCollection<FacturaItem> Carrito;
        private DataRow _clienteSeleccionado;
        private DataRow _productoSeleccionado;
        private bool _ignorarPerdidaFoco = false;

        public PresupuestosControl()
        {
            InitializeComponent();
            Carrito = new ObservableCollection<FacturaItem>();
            dgvPresupuesto.ItemsSource = Carrito;
        }

        private void PresupuestosControl_Loaded(object sender, RoutedEventArgs e)
        {
            LimpiarFormulario();
            CargarClientePorDefecto();
        }

        private void CargarClientePorDefecto()
        {
            try
            {
                _clienteSeleccionado = DatabaseService.BuscarCliente("00-00000000-0");
                if (_clienteSeleccionado != null)
                    lblClienteSeleccionado.Text = _clienteSeleccionado["RazonSocial"].ToString();
            }
            catch { }
        }

        // --- BÚSQUEDA DE CLIENTES ---
        private void txtBuscarCliente_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (txtBuscarCliente.Text.Length < 2) { popupCliente.IsOpen = false; return; }
            try
            {
                DataTable dt = DatabaseService.BuscarClientesMultiples(txtBuscarCliente.Text);
                lstSugerenciasCliente.ItemsSource = dt.DefaultView;
                popupCliente.IsOpen = dt.Rows.Count > 0;
            }
            catch { }
        }

        private void SeleccionarCliente(DataRowView row)
        {
            _clienteSeleccionado = row.Row;
            _ignorarPerdidaFoco = true;
            txtBuscarCliente.Text = _clienteSeleccionado["RazonSocial"].ToString();
            lblClienteSeleccionado.Text = _clienteSeleccionado["RazonSocial"].ToString();
            _ignorarPerdidaFoco = false;
            popupCliente.IsOpen = false;
            txtBuscarProducto.Focus();
        }

        // --- BÚSQUEDA DE PRODUCTOS ---
        private void txtBuscarProducto_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (_ignorarPerdidaFoco) return;
            if (txtBuscarProducto.Text.Length < 2) { popupProducto.IsOpen = false; _productoSeleccionado = null; return; }

            try
            {
                DataTable dt = DatabaseService.BuscarProductosMultiples_ParaVenta(txtBuscarProducto.Text);
                lstSugerenciasProducto.ItemsSource = dt.DefaultView;
                popupProducto.IsOpen = dt.Rows.Count > 0;
            }
            catch { }
        }

        private void SeleccionarProducto(DataRowView row)
        {
            _productoSeleccionado = row.Row;
            _ignorarPerdidaFoco = true;
            txtBuscarProducto.Text = _productoSeleccionado["Descripcion"].ToString();
            _ignorarPerdidaFoco = false;
            popupProducto.IsOpen = false;
            numCantidad.Focus();
        }

        // --- EVENTOS COMPARTIDOS ---
        private void lstSugerenciasCliente_MouseUp(object sender, MouseButtonEventArgs e) { if (lstSugerenciasCliente.SelectedItem is DataRowView drv) SeleccionarCliente(drv); }
        private void lstSugerenciasProducto_MouseUp(object sender, MouseButtonEventArgs e) { if (lstSugerenciasProducto.SelectedItem is DataRowView drv) SeleccionarProducto(drv); }

        private async void txtBuscar_LostFocus(object sender, RoutedEventArgs e)
        {
            if (_ignorarPerdidaFoco) return;
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
                if (popupCliente.IsOpen) { lstSugerenciasCliente.SelectedIndex = 0; lstSugerenciasCliente.Focus(); }
                else if (popupProducto.IsOpen) { lstSugerenciasProducto.SelectedIndex = 0; lstSugerenciasProducto.Focus(); }
            }
            else if (e.Key == Key.Escape) { popupCliente.IsOpen = false; popupProducto.IsOpen = false; }
        }

        private void lstSugerencias_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter)
            {
                if (sender == lstSugerenciasCliente && lstSugerenciasCliente.SelectedItem is DataRowView c) SeleccionarCliente(c);
                else if (sender == lstSugerenciasProducto && lstSugerenciasProducto.SelectedItem is DataRowView p) SeleccionarProducto(p);
            }
        }

        // --- CARRITO Y GUARDADO ---
        private void btnAgregarProducto_Click(object sender, RoutedEventArgs e)
        {
            if (_productoSeleccionado == null) { MessageBox.Show("Seleccione un producto."); return; }

            if (!int.TryParse(numCantidad.Text, out int cant) || cant <= 0) cant = 1;

            int id = Convert.ToInt32(_productoSeleccionado["ProductoID"]);
            var item = Carrito.FirstOrDefault(x => x.ProductoID == id);

            if (item != null) item.Cantidad += cant;
            else
            {
                Carrito.Add(new FacturaItem
                {
                    ProductoID = id,
                    Codigo = _productoSeleccionado["Codigo"].ToString(),
                    Descripcion = _productoSeleccionado["Descripcion"].ToString(),
                    Cantidad = cant,
                    PrecioUnitario = Convert.ToDecimal(_productoSeleccionado["PrecioVenta"])
                });
            }
            dgvPresupuesto.Items.Refresh();
            ActualizarTotal();
            LimpiarProducto();
        }

        private void btnEliminarItem_Click(object sender, RoutedEventArgs e)
        {
            if ((sender as Button)?.DataContext is FacturaItem item)
            {
                Carrito.Remove(item);
                ActualizarTotal();
            }
        }

        private void ActualizarTotal()
        {
            lblTotal.Text = $"{Carrito.Sum(x => x.Subtotal):C2}";
        }

        private void LimpiarProducto()
        {
            _productoSeleccionado = null;
            txtBuscarProducto.Text = "";
            numCantidad.Text = "1";
            txtBuscarProducto.Focus();
        }

        private void LimpiarFormulario()
        {
            Carrito.Clear();
            ActualizarTotal();
            LimpiarProducto();
            CargarClientePorDefecto();
        }

        private void btnCancelar_Click(object sender, RoutedEventArgs e)
        {
            if (MessageBox.Show("¿Borrar presupuesto?", "Confirmar", MessageBoxButton.YesNo) == MessageBoxResult.Yes)
                LimpiarFormulario();
        }

        private void btnGuardarPresupuesto_Click(object sender, RoutedEventArgs e)
        {
            if (Carrito.Count == 0) { MessageBox.Show("Agregue productos."); return; }
            if (_clienteSeleccionado == null) { MessageBox.Show("Seleccione cliente."); return; }

            try
            {
                bool exito = DatabaseService.GuardarPresupuesto(
                    Convert.ToInt32(_clienteSeleccionado["ClienteID"]),
                    Carrito.Sum(x => x.Subtotal),
                    Carrito.ToList()
                );

                if (exito)
                {
                    MessageBox.Show("Presupuesto guardado correctamente.", "Éxito", MessageBoxButton.OK, MessageBoxImage.Information);
                    LimpiarFormulario();
                    // Aquí iría la lógica de impresión (impresora NO fiscal)
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error: {ex.Message}");
            }
        }
    }
}