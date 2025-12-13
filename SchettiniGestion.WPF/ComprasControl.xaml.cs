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

        // --- PROVEEDOR ---
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
                catch { }
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

        // --- PRODUCTO ---
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

        // --- CARRITO ---
        private void btnAgregar_Click(object sender, RoutedEventArgs e)
        {
            if (_productoSeleccionado == null) return;
            int pid = Convert.ToInt32(_productoSeleccionado["ProductoID"]);
            var item = CarritoDeCompra.FirstOrDefault(x => x.ProductoID == pid);

            if (item != null) { item.Cantidad += (int)numCantidad.Value; item.PrecioUnitario = (decimal)numPrecioCosto.Value; }
            else
            {
                CarritoDeCompra.Add(new FacturaItem
                {
                    ProductoID = pid,
                    Codigo = _productoSeleccionado["Codigo"].ToString(),
                    Descripcion = _productoSeleccionado["Descripcion"].ToString(),
                    Cantidad = (int)numCantidad.Value,
                    PrecioUnitario = (decimal)numPrecioCosto.Value
                });
            }
            dgvCarrito.Items.Refresh();

            _productoSeleccionado = null;
            lblProductoSeleccionado.Text = "Producto:";
            txtBuscarProducto.Text = "";
            btnAgregar.IsEnabled = false;
            txtBuscarProducto.Focus();
            lblTotal.Text = $"TOTAL: {CarritoDeCompra.Sum(x => x.Subtotal):C2}";
        }

        private void btnEliminarItem_Click(object sender, RoutedEventArgs e)
        {
            if ((sender as Button)?.CommandParameter is FacturaItem item) { CarritoDeCompra.Remove(item); lblTotal.Text = $"TOTAL: {CarritoDeCompra.Sum(x => x.Subtotal):C2}"; }
        }

        // --- GUARDAR ---
        private void LimpiarFormulario()
        {
            _proveedorSeleccionado = null;
            txtProveedor.Text = "Proveedor Varios";
            txtTipoComprobante.Text = "Factura A";
            cmbCondicionCompra.SelectedIndex = 0;
            CarritoDeCompra.Clear();
            lblTotal.Text = "TOTAL: $0.00";
            btnBuscarProveedor.Focus();
        }

        private void btnGuardarCompra_Click(object sender, RoutedEventArgs e)
        {
            if (CarritoDeCompra.Count == 0) { CustomMessageBox.Show("Carrito vacío."); return; }
            if (_proveedorSeleccionado == null) { CustomMessageBox.Show("Seleccione proveedor."); return; }

            try
            {
                string cond = (cmbCondicionCompra.SelectedItem as ComboBoxItem).Content.ToString();
                bool ok = DatabaseService.GuardarCompra(Convert.ToInt32(_proveedorSeleccionado["ProveedorID"]), txtTipoComprobante.Text, CarritoDeCompra.Sum(x => x.Subtotal), CarritoDeCompra.ToList(), cond);
                if (ok) { CustomMessageBox.Show("Guardado."); LimpiarFormulario(); }
            }
            catch (Exception ex) { CustomMessageBox.Show(ex.Message); }
        }
    }
}