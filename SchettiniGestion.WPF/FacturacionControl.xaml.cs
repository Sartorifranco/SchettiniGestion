using SchettiniGestion;
using System;
using System.Collections.ObjectModel;
using System.Data;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Threading.Tasks;

namespace SchettiniGestion.WPF
{
    public partial class FacturacionControl : UserControl
    {
        private ObservableCollection<FacturaItem> CarritoDeVenta;
        private DataRow _clienteSeleccionado;
        private DataRow _productoSeleccionado;
        private bool _ignorarPerdidaFoco = false;

        // Variable para evitar recálculos innecesarios al cargar el combo
        private bool _cargandoListas = false;

        public FacturacionControl()
        {
            InitializeComponent();
            CarritoDeVenta = new ObservableCollection<FacturaItem>();
            dgvFactura.ItemsSource = CarritoDeVenta;
        }

        private void FacturacionControl_Loaded(object sender, RoutedEventArgs e)
        {
            CargarListasPrecios();
            CargarClientePorDefecto();
            LimpiarFormulario();
        }

        private void CargarListasPrecios()
        {
            try
            {
                _cargandoListas = true;
                DataTable dt = DatabaseService.GetListasPrecios();
                cmbListaPrecios.ItemsSource = dt.DefaultView;

                // Seleccionar la lista base (ID 1) por defecto si existe
                if (dt.Rows.Count > 0)
                {
                    cmbListaPrecios.SelectedValue = 1;
                }
                _cargandoListas = false;
            }
            catch { }
        }

        private void CargarClientePorDefecto()
        {
            try
            {
                _clienteSeleccionado = DatabaseService.BuscarCliente("00-00000000-0");
                if (_clienteSeleccionado != null)
                    lblClienteSeleccionado.Text = _clienteSeleccionado["RazonSocial"].ToString();
            }
            catch (Exception ex) { MessageBox.Show(ex.Message); }
        }

        // --- EVENTO DE CAMBIO DE LISTA ---
        private void cmbListaPrecios_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (_cargandoListas) return;
            RecalcularCarritoConNuevaLista();
        }

        private decimal ObtenerPorcentajeLista()
        {
            if (cmbListaPrecios.SelectedItem is DataRowView row)
            {
                return Convert.ToDecimal(row["Porcentaje"]);
            }
            return 0; // Si no hay lista, 0%
        }

        private void RecalcularCarritoConNuevaLista()
        {
            decimal porcentaje = ObtenerPorcentajeLista();

            if (CarritoDeVenta.Count > 0)
            {
                foreach (var item in CarritoDeVenta)
                {
                    // Buscamos el precio base original en la BD para no acumular porcentajes
                    // (Ej: Si paso de +10% a +20%, no quiero hacer +10%+20%, sino Base + 20%)
                    DataRow prod = DatabaseService.BuscarProducto(item.Codigo);
                    if (prod != null)
                    {
                        decimal precioBase = Convert.ToDecimal(prod["PrecioVenta"]);
                        // Aplicamos el nuevo porcentaje
                        item.PrecioUnitario = precioBase * (1 + (porcentaje / 100));
                    }
                }
                dgvFactura.Items.Refresh();
                ActualizarTotal();
            }
        }

        // --- BÚSQUEDA ---
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

        private void lstSugerenciasCliente_MouseUp(object sender, MouseButtonEventArgs e) { if (lstSugerenciasCliente.SelectedItem is DataRowView r) SeleccionarCliente(r); }
        private void lstSugerenciasProducto_MouseUp(object sender, MouseButtonEventArgs e) { if (lstSugerenciasProducto.SelectedItem is DataRowView r) SeleccionarProducto(r); }

        private async void txtBuscar_LostFocus(object sender, RoutedEventArgs e)
        {
            if (_ignorarPerdidaFoco) return;
            await Task.Delay(150);
            if (!lstSugerenciasCliente.IsFocused && !lstSugerenciasProducto.IsFocused) { popupCliente.IsOpen = false; popupProducto.IsOpen = false; }
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

        // --- CARRITO ---
        private void btnAgregarProducto_Click(object sender, RoutedEventArgs e)
        {
            if (_productoSeleccionado == null) { MessageBox.Show("Seleccione un producto."); return; }

            int id = Convert.ToInt32(_productoSeleccionado["ProductoID"]);
            int stock = Convert.ToInt32(_productoSeleccionado["StockActual"]);
            int cant = (int)numCantidad.Value;

            var item = CarritoDeVenta.FirstOrDefault(x => x.ProductoID == id);
            int enCarro = (item != null) ? item.Cantidad : 0;

            if ((enCarro + cant) > stock) { MessageBox.Show("Stock insuficiente."); return; }

            // ===== APLICAR LISTA DE PRECIOS AL AGREGAR =====
            decimal precioBase = Convert.ToDecimal(_productoSeleccionado["PrecioVenta"]);
            decimal porcentaje = ObtenerPorcentajeLista();
            decimal precioFinal = precioBase * (1 + (porcentaje / 100));
            // ===============================================

            if (item != null) item.Cantidad += cant;
            else
            {
                CarritoDeVenta.Add(new FacturaItem
                {
                    ProductoID = id,
                    Codigo = _productoSeleccionado["Codigo"].ToString(),
                    Descripcion = _productoSeleccionado["Descripcion"].ToString(),
                    Cantidad = cant,
                    PrecioUnitario = precioFinal // Precio con aumento/descuento aplicado
                });
            }
            dgvFactura.Items.Refresh();
            LimpiarProducto();
            ActualizarTotal();
        }

        private void btnEliminarItem_Click(object sender, RoutedEventArgs e)
        {
            if ((sender as Button)?.DataContext is FacturaItem item) { CarritoDeVenta.Remove(item); ActualizarTotal(); }
        }

        private void ActualizarTotal() { lblTotal.Text = $"{CarritoDeVenta.Sum(x => x.Subtotal):C2}"; }

        private void LimpiarProducto() { _productoSeleccionado = null; txtBuscarProducto.Text = ""; numCantidad.Value = 1; txtBuscarProducto.Focus(); }
        private void LimpiarFormulario() { CargarClientePorDefecto(); cmbTipoComprobante.SelectedIndex = 0; cmbCondicionVenta.SelectedIndex = 0; CarritoDeVenta.Clear(); ActualizarTotal(); LimpiarProducto(); txtBuscarCliente.Focus(); }

        private void btnCancelarFactura_Click(object sender, RoutedEventArgs e)
        {
            if (MessageBox.Show("¿Cancelar venta?", "Confirmar", MessageBoxButton.YesNo) == MessageBoxResult.Yes) LimpiarFormulario();
        }

        // --- GUARDAR ---
        private void btnGuardarFactura_Click(object sender, RoutedEventArgs e)
        {
            if (CarritoDeVenta.Count == 0) { MessageBox.Show("Agregue productos."); return; }
            if (_clienteSeleccionado == null) { MessageBox.Show("Seleccione cliente."); return; }

            if (MessageBox.Show("¿Confirmar venta?", "Confirmar", MessageBoxButton.YesNo) == MessageBoxResult.Yes)
            {
                try
                {
                    string condicion = (cmbCondicionVenta.SelectedItem as ComboBoxItem).Content.ToString();

                    bool exito = DatabaseService.GuardarFactura(
                        Convert.ToInt32(_clienteSeleccionado["ClienteID"]),
                        (cmbTipoComprobante.SelectedItem as ComboBoxItem).Content.ToString(),
                        CarritoDeVenta.Sum(i => i.Subtotal),
                        CarritoDeVenta.ToList(),
                        condicion
                    );

                    if (exito)
                    {
                        MessageBox.Show("Venta guardada exitosamente.", "Éxito", MessageBoxButton.OK, MessageBoxImage.Information);
                        LimpiarFormulario();
                    }
                }
                catch (Exception ex) { MessageBox.Show($"Error: {ex.Message}"); }
            }
        }
    }
}