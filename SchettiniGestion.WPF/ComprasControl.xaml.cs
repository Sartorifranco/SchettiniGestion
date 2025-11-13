using SchettiniGestion; // ¡Importante!
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel; // Para el Carrito
using System.Data;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Threading.Tasks; // ¡Importante para el LostFocus!

namespace SchettiniGestion.WPF
{
    public partial class ComprasControl : UserControl
    {
        private ObservableCollection<FacturaItem> CarritoDeCompra;
        private DataRow _proveedorSeleccionado;
        private DataRow _productoSeleccionado;
        private bool _ignorarPerdidaFoco = false; // ¡NUEVO!

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
            if (e.Source == popupOverlay)
            {
                popupOverlay.Visibility = Visibility.Collapsed;
            }
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
                catch (Exception ex)
                {
                    MessageBox.Show($"Error al buscar proveedores: {ex.Message}");
                }
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

        private void lstProveedores_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            SeleccionarProveedor();
        }

        private void lstProveedores_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter)
            {
                SeleccionarProveedor();
            }
        }

        // --- 2. LÓGICA DE BÚSQUEDA (PRODUCTO) ---

        // ESTA ES LA FUNCIÓN PRINCIPAL DE BÚSQUEDA
        private void BuscarProducto(string query)
        {
            if (string.IsNullOrWhiteSpace(query))
            {
                popupProductos.IsOpen = false;
                return;
            }

            try
            {
                // 1. Intentar buscar un match exacto (por Código)
                DataRow productoExacto = DatabaseService.BuscarProducto(query);

                if (productoExacto != null)
                {
                    // 2. Si se encuentra, seleccionarlo directamente
                    SeleccionarProducto(productoExacto);
                }
                else
                {
                    // 3. Si no, buscar múltiples
                    DataTable dt = DatabaseService.BuscarProductosMultiples_ParaCompra(query);
                    lstProductos.ItemsSource = dt.DefaultView;

                    if (dt.Rows.Count > 0)
                    {
                        lstProductos.SelectedIndex = 0;
                        popupProductos.IsOpen = true;
                        _ignorarPerdidaFoco = true; // Evita que LostFocus cierre el popup
                        lstProductos.Focus();
                    }
                    else
                    {
                        popupProductos.IsOpen = false;
                        _productoSeleccionado = null;
                        btnAgregar.IsEnabled = false;
                        lblProductoSeleccionado.Text = "Producto: (No encontrado)";
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error al buscar productos: {ex.Message}");
            }
        }

        private void txtBuscarProducto_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter)
            {
                BuscarProducto(txtBuscarProducto.Text);
            }
            else if (e.Key == Key.Down)
            {
                if (popupProductos.IsOpen && lstProductos.Items.Count > 0)
                {
                    lstProductos.Focus();
                }
            }
            else if (e.Key == Key.Escape)
            {
                popupProductos.IsOpen = false;
            }
        }

        // ===== INICIO DE CÓDIGO NUEVO =====
        // ESTA ES LA CORRECCIÓN CLAVE
        private async void txtBuscarProducto_LostFocus(object sender, RoutedEventArgs e)
        {
            // Espera un momento para ver si el foco se fue a la lista popup
            await Task.Delay(200);

            // Si el foco no está en la lista (es decir, el usuario hizo clic en "Precio"),
            // y el popup no está abierto, intenta una búsqueda exacta.
            if (!_ignorarPerdidaFoco && !popupProductos.IsOpen && _productoSeleccionado == null)
            {
                BuscarProducto(txtBuscarProducto.Text);
            }
            _ignorarPerdidaFoco = false; // Resetea el flag
        }
        // ===== FIN DE CÓDIGO NUEVO =====

        private void SeleccionarProducto(DataRow drv)
        {
            _productoSeleccionado = drv;
            lblProductoSeleccionado.Text = _productoSeleccionado["Descripcion"].ToString();
            numPrecioCosto.Value = Convert.ToDecimal(_productoSeleccionado["PrecioVenta"]);
            numCantidad.Value = 1;
            btnAgregar.IsEnabled = true; // ¡Habilita el botón!
            popupProductos.IsOpen = false;
            _ignorarPerdidaFoco = false;
            numPrecioCosto.Focus();
        }

        private void lstProductos_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (lstProductos.SelectedItem is DataRowView drv)
            {
                SeleccionarProducto(drv.Row);
            }
        }

        private void lstProductos_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter && lstProductos.SelectedItem is DataRowView drv)
            {
                SeleccionarProducto(drv.Row);
            }
        }


        // --- 3. LÓGICA DEL CARRITO ---
        private void btnAgregar_Click(object sender, RoutedEventArgs e)
        {
            // ===== CORRECCIÓN =====
            // Añadimos el MessageBox que faltaba
            if (_productoSeleccionado == null)
            {
                MessageBox.Show("Debe seleccionar un producto válido de la lista primero.", "Error", MessageBoxButton.OK, MessageBoxImage.Warning);
                txtBuscarProducto.Focus();
                return;
            }

            if (numPrecioCosto.Value <= 0)
            {
                MessageBox.Show("El precio de costo debe ser mayor a cero.", "Error", MessageBoxButton.OK, MessageBoxImage.Warning);
                numPrecioCosto.Focus();
                return;
            }

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
                var nuevoItem = new FacturaItem
                {
                    ProductoID = productoID,
                    Codigo = _productoSeleccionado["Codigo"].ToString(),
                    Descripcion = _productoSeleccionado["Descripcion"].ToString(),
                    Cantidad = (int)numCantidad.Value,
                    PrecioUnitario = (decimal)numPrecioCosto.Value
                };
                CarritoDeCompra.Add(nuevoItem);
            }

            LimpiarSeccionProducto();
            ActualizarTotal();
        }

        private void btnEliminarItem_Click(object sender, RoutedEventArgs e)
        {
            if ((sender as Button)?.CommandParameter is FacturaItem item)
            {
                CarritoDeCompra.Remove(item);
                ActualizarTotal();
            }
        }

        private void ActualizarTotal()
        {
            decimal total = CarritoDeCompra.Sum(item => item.Subtotal);
            lblTotal.Text = $"TOTAL: {total:C2}";
        }

        // --- 4. LÓGICA DE GUARDADO Y LIMPIEZA ---
        private void LimpiarSeccionProducto()
        {
            _productoSeleccionado = null;
            lblProductoSeleccionado.Text = "Producto:";
            txtBuscarProducto.Text = "";
            numPrecioCosto.Value = 0;
            numCantidad.Value = 1;
            btnAgregar.IsEnabled = false; // ¡Deshabilitar el botón!
            txtBuscarProducto.Focus();
        }

        private void LimpiarFormulario()
        {
            _proveedorSeleccionado = null;
            txtProveedor.Text = "Proveedor Varios";
            txtTipoComprobante.Text = "Factura A";
            CarritoDeCompra.Clear();
            ActualizarTotal();
            LimpiarSeccionProducto();
            btnBuscarProveedor.Focus();
        }

        private void btnGuardarCompra_Click(object sender, RoutedEventArgs e)
        {
            if (CarritoDeCompra.Count == 0)
            {
                MessageBox.Show("Debe agregar al menos un producto a la compra.", "Error", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }
            if (_proveedorSeleccionado == null)
            {
                MessageBox.Show("Debe seleccionar un proveedor.", "Error", MessageBoxButton.OK, MessageBoxImage.Warning);
                btnBuscarProveedor_Click(null, null);
                return;
            }

            if (MessageBox.Show("¿Está seguro de que desea guardar esta compra? Esta acción ingresará stock al sistema.", "Confirmar Compra", MessageBoxButton.YesNo, MessageBoxImage.Question) == MessageBoxResult.No)
            {
                return;
            }

            try
            {
                bool exito = DatabaseService.GuardarCompra(
                    Convert.ToInt32(_proveedorSeleccionado["ProveedorID"]),
                    txtTipoComprobante.Text,
                    CarritoDeCompra.Sum(item => item.Subtotal),
                    CarritoDeCompra.ToList()
                );

                if (exito)
                {
                    MessageBox.Show("¡Compra guardada exitosamente! El stock ha sido actualizado.", "Éxito", MessageBoxButton.OK, MessageBoxImage.Information);
                    LimpiarFormulario();
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error fatal al guardar la compra: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
    }
}