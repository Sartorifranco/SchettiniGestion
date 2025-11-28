using SchettiniGestion;
using System;
using System.Collections.ObjectModel;
using System.Data;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Threading.Tasks;
using System.Collections.Generic; // Necesario para List<>

namespace SchettiniGestion.WPF
{
    public partial class FacturacionControl : UserControl
    {
        private ObservableCollection<FacturaItem> CarritoDeVenta;
        private DataRow _clienteSeleccionado;
        private DataRow _productoSeleccionado;
        private bool _ignorarPerdidaFoco = false;
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

        // --- CARGAS INICIALES ---
        private void CargarListasPrecios()
        {
            try
            {
                _cargandoListas = true;
                DataTable dt = DatabaseService.GetListasPrecios();
                if (this.FindName("cmbListaPrecios") != null)
                {
                    cmbListaPrecios.ItemsSource = dt.DefaultView;
                    if (dt.Rows.Count > 0) cmbListaPrecios.SelectedValue = 1;
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
            catch { }
        }

        // --- LISTAS DE PRECIOS ---
        private void cmbListaPrecios_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (_cargandoListas) return;
            RecalcularCarritoConNuevaLista();
        }

        private decimal ObtenerPorcentajeLista()
        {
            if (this.FindName("cmbListaPrecios") != null && cmbListaPrecios.SelectedItem is DataRowView row)
                return Convert.ToDecimal(row["Porcentaje"]);
            return 0;
        }

        private void RecalcularCarritoConNuevaLista()
        {
            decimal porcentaje = ObtenerPorcentajeLista();
            if (CarritoDeVenta.Count > 0)
            {
                foreach (var item in CarritoDeVenta)
                {
                    DataRow prod = DatabaseService.BuscarProducto(item.Codigo);
                    if (prod != null)
                    {
                        decimal precioBase = Convert.ToDecimal(prod["PrecioVenta"]);
                        item.PrecioUnitario = precioBase * (1 + (porcentaje / 100));
                    }
                }
                dgvFactura.Items.Refresh();
                ActualizarTotal();
            }
        }

        // --- BUSCADOR CLIENTE ---
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

        // --- BUSCADOR PRODUCTO ---
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

        // --- EVENTOS UI ---
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
            if (_productoSeleccionado == null) { CustomMessageBox.Show("Seleccione producto."); return; }

            int id = Convert.ToInt32(_productoSeleccionado["ProductoID"]);
            int stock = Convert.ToInt32(_productoSeleccionado["StockActual"]);
            int cant = (int)numCantidad.Value;

            var item = CarritoDeVenta.FirstOrDefault(x => x.ProductoID == id);
            int enCarro = (item != null) ? item.Cantidad : 0;

            if ((enCarro + cant) > stock) { CustomMessageBox.Show("Stock insuficiente."); return; }

            decimal precioBase = Convert.ToDecimal(_productoSeleccionado["PrecioVenta"]);
            decimal porcentaje = ObtenerPorcentajeLista();
            decimal precioFinal = precioBase * (1 + (porcentaje / 100));

            if (item != null) item.Cantidad += cant;
            else
            {
                CarritoDeVenta.Add(new FacturaItem
                {
                    ProductoID = id,
                    Codigo = _productoSeleccionado["Codigo"].ToString(),
                    Descripcion = _productoSeleccionado["Descripcion"].ToString(),
                    Cantidad = cant,
                    PrecioUnitario = precioFinal
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

        private void LimpiarFormulario()
        {
            CargarClientePorDefecto();
            cmbTipoComprobante.SelectedIndex = 0;
            cmbCondicionVenta.SelectedIndex = 0;
            if (this.FindName("cmbListaPrecios") != null && cmbListaPrecios.Items.Count > 0) cmbListaPrecios.SelectedValue = 1;
            CarritoDeVenta.Clear();
            ActualizarTotal();
            LimpiarProducto();
            txtBuscarCliente.Focus();
        }

        private void btnCancelarFactura_Click(object sender, RoutedEventArgs e)
        {
            if (CustomMessageBox.Show("¿Cancelar venta?", "Confirmar", MessageBoxButton.YesNo) == MessageBoxResult.Yes) LimpiarFormulario();
        }

        // --- GUARDAR Y FACTURAR (LA PARTE IMPORTANTE) ---
        private async void btnGuardarFactura_Click(object sender, RoutedEventArgs e)
        {
            if (CarritoDeVenta.Count == 0) { CustomMessageBox.Show("Agregue productos."); return; }
            if (_clienteSeleccionado == null) { CustomMessageBox.Show("Seleccione cliente."); return; }

            // 1. Recopilar datos
            string condicion = (cmbCondicionVenta.SelectedItem as ComboBoxItem).Content.ToString();
            string tipoCompStr = (cmbTipoComprobante.SelectedItem as ComboBoxItem).Content.ToString();
            decimal totalVenta = CarritoDeVenta.Sum(i => i.Subtotal);
            int clienteID = Convert.ToInt32(_clienteSeleccionado["ClienteID"]);
            string clienteNombre = _clienteSeleccionado["RazonSocial"].ToString();

            // Variables Fiscales
            string caeObtenido = "";
            string vtoCae = "";
            int nroComprobante = 0;

            // 2. Lógica Fiscal (AFIP)
            if (tipoCompStr.Contains("Factura"))
            {
                if (CustomMessageBox.Show("¿Confirmar FACTURA ELECTRÓNICA con AFIP?", "Atención Fiscal", MessageBoxButton.YesNo, MessageBoxImage.Warning) == MessageBoxResult.Yes)
                {
                    DataRow config = DatabaseService.GetConfiguracion();
                    int ptoVenta = config != null && config["PuntoVenta"] != DBNull.Value ? Convert.ToInt32(config["PuntoVenta"]) : 1;
                    int tipoAfip = tipoCompStr == "Factura A" ? 1 : 6;

                    // Limpieza de CUIT
                    string cuitLimpio = _clienteSeleccionado["CUIT"].ToString().Replace("-", "").Replace(" ", "");
                    long cuitCliente = 0;
                    long.TryParse(cuitLimpio, out cuitCliente);

                    // Llamada Asíncrona (Esto ahora coincidirá con el archivo AfipService actualizado)
                    var resultadoAfip = await AfipService.FacturarAsync(tipoAfip, ptoVenta, (double)totalVenta, cuitCliente, CarritoDeVenta.ToList());

                    if (!resultadoAfip.Exito)
                    {
                        CustomMessageBox.Show($"ERROR AFIP:\n{resultadoAfip.Error}", "Fallo", MessageBoxButton.OK, MessageBoxImage.Error);
                        return; // Cancelar si falla
                    }

                    caeObtenido = resultadoAfip.CAE;
                    vtoCae = resultadoAfip.Vencimiento;
                    nroComprobante = resultadoAfip.NumeroComprobante;

                    CustomMessageBox.Show("¡Factura Autorizada por AFIP!", "Éxito", MessageBoxButton.OK, MessageBoxImage.Information);
                }
            }

            // 3. Guardar en BD
            bool exito = DatabaseService.GuardarFactura(
                clienteID,
                tipoCompStr,
                totalVenta,
                CarritoDeVenta.ToList(),
                condicion
            );

            if (exito)
            {
                if (CustomMessageBox.Show("Venta registrada. ¿Imprimir comprobante?", "Imprimir", MessageBoxButton.YesNo, MessageBoxImage.Question) == MessageBoxResult.Yes)
                {
                    DataTable dtItems = new DataTable();
                    dtItems.Columns.Add("Descripcion");
                    dtItems.Columns.Add("Cantidad");
                    dtItems.Columns.Add("Subtotal");
                    foreach (var item in CarritoDeVenta) dtItems.Rows.Add(item.Descripcion, item.Cantidad, item.Subtotal);

                    string infoExtra = $"CONDICIÓN: {condicion.ToUpper()}";
                    if (!string.IsNullOrEmpty(caeObtenido))
                    {
                        infoExtra += $"\n\nCAE: {caeObtenido}\nVTO: {vtoCae}";
                    }

                    // Si obtuvimos número de AFIP, lo usamos. Si no, 0 (Pendiente)
                    PrintService.ImprimirTicketVenta(tipoCompStr, nroComprobante, clienteNombre, DateTime.Now, dtItems, totalVenta, infoExtra);
                }
                LimpiarFormulario();
            }
        }
    }
}