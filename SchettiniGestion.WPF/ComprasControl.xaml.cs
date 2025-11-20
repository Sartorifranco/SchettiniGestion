using SchettiniGestion;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Data;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Threading.Tasks;

namespace SchettiniGestion.WPF
{
    public partial class ComprasControl : UserControl
    {
        private ObservableCollection<FacturaItem> CarritoDeCompra;
        private DataRow _proveedorSeleccionado;
        private DataRow _productoSeleccionado;
        private bool _ignorarPerdidaFoco = false;

        public ComprasControl()
        {
            InitializeComponent();
            CarritoDeCompra = new ObservableCollection<FacturaItem>();
            dgvCarrito.ItemsSource = CarritoDeCompra;
        }

        private void UserControl_Loaded(object sender, RoutedEventArgs e)
        {
            LimpiarFormulario();
        }

        // --- 1. LÓGICA DE BÚSQUEDA (PROVEEDOR) ---
        private void btnBuscarProveedor_Click(object sender, RoutedEventArgs e)
        {
            popupOverlay.Visibility = Visibility.Visible;
            txtBuscarProveedorPopup.Text = "";
            lstProveedores.ItemsSource = null;
            txtBuscarProveedorPopup.Focus();
        }

        private void popupOverlay_MouseUp(object sender, MouseButtonEventArgs e)
        {
            if (e.Source == popupOverlay) popupOverlay.Visibility = Visibility.Collapsed;
        }

        private void txtBuscarProveedorPopup_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter || e.Key == Key.Down)
            {
                try
                {
                    DataTable dt = DatabaseService.BuscarProveedoresMultiples(txtBuscarProveedorPopup.Text);
                    lstProveedores.ItemsSource = dt.DefaultView;
                    if (dt.Rows.Count > 0)
                    {
                        lstProveedores.SelectedIndex = 0;
                        lstProveedores.Focus();
                    }
                }
                catch (Exception ex) { MessageBox.Show($"Error al buscar: {ex.Message}"); }
            }
        }

        private void SeleccionarProveedor()
        {
            if (lstProveedores.SelectedItem is DataRowView drv)
            {
                _proveedorSeleccionado = drv.Row;
                txtProveedor.Text = _proveedorSeleccionado["RazonSocial"].ToString();
                popupOverlay.Visibility = Visibility.Collapsed;
                txtBuscarProducto.Focus();
            }
        }

        private void lstProveedores_SelectionChanged(object sender, SelectionChangedEventArgs e) { SeleccionarProveedor(); }
        private void lstProveedores_KeyDown(object sender, KeyEventArgs e) { if (e.Key == Key.Enter) SeleccionarProveedor(); }


        // --- 2. LÓGICA DE BÚSQUEDA (PRODUCTO) ---

        private void txtBuscarProducto_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (_ignorarPerdidaFoco) return;
            if (txtBuscarProducto.Text.Length < 2) { popupProductos.IsOpen = false; _productoSeleccionado = null; return; }

            try
            {
                DataTable dt = DatabaseService.BuscarProductosMultiples_ParaCompra(txtBuscarProducto.Text);
                lstSugerenciasProducto.ItemsSource = dt.DefaultView;
                popupProductos.IsOpen = dt.Rows.Count > 0;
            }
            catch { }
        }

        private void SeleccionarProducto(DataRow drv)
        {
            _productoSeleccionado = drv;
            _ignorarPerdidaFoco = true;
            lblProductoSeleccionado.Text = _productoSeleccionado["Descripcion"].ToString();
            txtBuscarProducto.Text = _productoSeleccionado["Descripcion"].ToString();
            numPrecioCosto.Value = Convert.ToDecimal(_productoSeleccionado["PrecioCosto"]);
            numCantidad.Value = 1;
            btnAgregar.IsEnabled = true;
            popupProductos.IsOpen = false;
            _ignorarPerdidaFoco = false;
            numPrecioCosto.Focus();
        }

        // Eventos para la lista de sugerencias (Nombres corregidos)
        private void lstSugerenciasProducto_MouseUp(object sender, MouseButtonEventArgs e)
        {
            if (lstSugerenciasProducto.SelectedItem is DataRowView drv) SeleccionarProducto(drv.Row);
        }

        private void lstSugerencias_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter && lstSugerenciasProducto.SelectedItem is DataRowView drv) SeleccionarProducto(drv.Row);
        }

        private void txtBuscar_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Down && popupProductos.IsOpen)
            {
                lstSugerenciasProducto.SelectedIndex = 0;
                lstSugerenciasProducto.Focus();
            }
            else if (e.Key == Key.Escape) popupProductos.IsOpen = false;
        }

        private async void txtBuscarProducto_LostFocus(object sender, RoutedEventArgs e)
        {
            await Task.Delay(150);
            if (!lstSugerenciasProducto.IsFocused) popupProductos.IsOpen = false;
        }

        // --- 3. CARRITO ---
        private void btnAgregar_Click(object sender, RoutedEventArgs e)
        {
            if (_productoSeleccionado == null) return;

            int productoID = Convert.ToInt32(_productoSeleccionado["ProductoID"]);
            var itemExistente = CarritoDeCompra.FirstOrDefault(item => item.ProductoID == productoID);

            if (itemExistente != null)
            {
                itemExistente.Cantidad += (int)numCantidad.Value;
                itemExistente.PrecioUnitario = (decimal)numPrecioCosto.Value;
                dgvCarrito.Items.Refresh();
            }
            else
            {
                CarritoDeCompra.Add(new FacturaItem
                {
                    ProductoID = productoID,
                    Codigo = _productoSeleccionado["Codigo"].ToString(),
                    Descripcion = _productoSeleccionado["Descripcion"].ToString(),
                    Cantidad = (int)numCantidad.Value,
                    PrecioUnitario = (decimal)numPrecioCosto.Value
                });
            }

            _productoSeleccionado = null;
            lblProductoSeleccionado.Text = "Producto:";
            txtBuscarProducto.Text = "";
            btnAgregar.IsEnabled = false;
            txtBuscarProducto.Focus();
            ActualizarTotal();
        }

        private void btnEliminarItem_Click(object sender, RoutedEventArgs e)
        {
            if ((sender as Button)?.CommandParameter is FacturaItem item) { CarritoDeCompra.Remove(item); ActualizarTotal(); }
        }

        private void ActualizarTotal() { lblTotal.Text = $"TOTAL: {CarritoDeCompra.Sum(item => item.Subtotal):C2}"; }

        // --- 4. GUARDAR ---
        private void LimpiarFormulario()
        {
            _proveedorSeleccionado = null;
            txtProveedor.Text = "Proveedor Varios";
            txtTipoComprobante.Text = "Factura A";
            cmbCondicionCompra.SelectedIndex = 0; // Reset a Contado
            CarritoDeCompra.Clear();
            ActualizarTotal();
            btnBuscarProveedor.Focus();
        }

        private void btnGuardarCompra_Click(object sender, RoutedEventArgs e)
        {
            if (CarritoDeCompra.Count == 0) { MessageBox.Show("Debe agregar al menos un producto.", "Error", MessageBoxButton.OK, MessageBoxImage.Warning); return; }
            if (_proveedorSeleccionado == null) { MessageBox.Show("Seleccione un proveedor.", "Error", MessageBoxButton.OK, MessageBoxImage.Warning); return; }

            if (MessageBox.Show("¿Confirmar compra?", "Confirmar", MessageBoxButton.YesNo) == MessageBoxResult.No) return;

            try
            {
                string condicion = (cmbCondicionCompra.SelectedItem as ComboBoxItem).Content.ToString();

                bool exito = DatabaseService.GuardarCompra(
                    Convert.ToInt32(_proveedorSeleccionado["ProveedorID"]),
                    txtTipoComprobante.Text,
                    CarritoDeCompra.Sum(item => item.Subtotal),
                    CarritoDeCompra.ToList(),
                    condicion
                );

                if (exito)
                {
                    MessageBox.Show("¡Compra guardada exitosamente!", "Éxito", MessageBoxButton.OK, MessageBoxImage.Information);
                    LimpiarFormulario();
                }
            }
            catch (Exception ex) { MessageBox.Show($"Error: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error); }
        }
    }
}