using SchettiniGestion;
using System;
using System.Collections.ObjectModel;
using System.Data;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Threading.Tasks;
using System.Collections.Generic;

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

            CustomerScreenService.OnClienteEligioPago += ProcesarPagoCliente;
            this.Unloaded += FacturacionControl_Unloaded;
        }

        private void FacturacionControl_Loaded(object sender, RoutedEventArgs e)
        {
            CargarListasPrecios();
            CargarClientePorDefecto();
            CustomerScreenService.Iniciar();
            CustomerScreenService.Resetear();
            LimpiarFormulario();
        }

        private void FacturacionControl_Unloaded(object sender, RoutedEventArgs e)
        {
            CustomerScreenService.OnClienteEligioPago -= ProcesarPagoCliente;
        }

        // --- LÓGICA DE PAGO (QR / TACTIL) ---

        // 1. Si el EMPLEADO hace clic en el botón QR
        private void btnPagoQR_Click(object sender, RoutedEventArgs e)
        {
            if (CarritoDeVenta.Count == 0) { CustomMessageBox.Show("No hay productos para cobrar."); return; }

            // Calculamos total y mostramos QR atrás
            decimal total = CarritoDeVenta.Sum(x => x.Subtotal);
            CustomerScreenService.PantallaQR(total);

            // Seleccionamos "Mercado Pago" en el combo del empleado para ahorrar tiempo
            foreach (ComboBoxItem item in cmbCondicionVenta.Items)
            {
                if (item.Content.ToString() == "Mercado Pago")
                {
                    cmbCondicionVenta.SelectedItem = item;
                    break;
                }
            }

            CustomMessageBox.Show("El Código QR se está mostrando en la pantalla del cliente.\nEspere la confirmación del pago.", "Esperando Pago", MessageBoxButton.OK, MessageBoxImage.Information);
        }

        // 2. Si el CLIENTE toca la pantalla
        private void ProcesarPagoCliente(string opcion)
        {
            Dispatcher.Invoke(() =>
            {
                if (opcion == "MERCADOPAGO")
                {
                    // Hacemos lo mismo que si el empleado hubiera apretado el botón
                    btnPagoQR_Click(null, null);
                }
                else if (opcion == "TARJETA")
                {
                    CustomMessageBox.Show("El cliente eligió Tarjeta. (Se puede aplicar recargo aquí).", "Atención");
                }
                else
                {
                    CustomMessageBox.Show($"El cliente eligió: {opcion}", "Atención");
                }
            });
        }
        // ------------------------------------

        private void CargarListasPrecios() { try { _cargandoListas = true; DataTable dt = DatabaseService.GetListasPrecios(); if (this.FindName("cmbListaPrecios") != null) { cmbListaPrecios.ItemsSource = dt.DefaultView; if (dt.Rows.Count > 0) cmbListaPrecios.SelectedValue = 1; } _cargandoListas = false; } catch { } }
        private void CargarClientePorDefecto() { try { _clienteSeleccionado = DatabaseService.BuscarCliente("00-00000000-0"); if (_clienteSeleccionado != null) lblClienteSeleccionado.Text = _clienteSeleccionado["RazonSocial"].ToString(); } catch { } }
        private void cmbListaPrecios_SelectionChanged(object sender, SelectionChangedEventArgs e) { if (_cargandoListas) return; RecalcularCarritoConNuevaLista(); }
        private decimal ObtenerPorcentajeLista() { if (this.FindName("cmbListaPrecios") != null && cmbListaPrecios.SelectedItem is DataRowView row) return Convert.ToDecimal(row["Porcentaje"]); return 0; }

        private void RecalcularCarritoConNuevaLista()
        {
            decimal porcentaje = ObtenerPorcentajeLista();
            foreach (var item in CarritoDeVenta)
            {
                if (item.Codigo == "VAR") continue;
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

        private void txtBuscarCliente_TextChanged(object sender, TextChangedEventArgs e) { if (txtBuscarCliente.Text.Length < 2) { popupCliente.IsOpen = false; return; } try { DataTable dt = DatabaseService.BuscarClientesMultiples(txtBuscarCliente.Text); lstSugerenciasCliente.ItemsSource = dt.DefaultView; popupCliente.IsOpen = dt.Rows.Count > 0; } catch { } }
        private void SeleccionarCliente(DataRowView row) { _clienteSeleccionado = row.Row; _ignorarPerdidaFoco = true; txtBuscarCliente.Text = _clienteSeleccionado["RazonSocial"].ToString(); lblClienteSeleccionado.Text = _clienteSeleccionado["RazonSocial"].ToString(); _ignorarPerdidaFoco = false; popupCliente.IsOpen = false; txtBuscarProducto.Focus(); }
        private void txtBuscarProducto_TextChanged(object sender, TextChangedEventArgs e) { if (_ignorarPerdidaFoco) return; if (txtBuscarProducto.Text.Length < 2) { popupProducto.IsOpen = false; _productoSeleccionado = null; return; } try { DataTable dt = DatabaseService.BuscarProductosMultiples_ParaVenta(txtBuscarProducto.Text); lstSugerenciasProducto.ItemsSource = dt.DefaultView; popupProducto.IsOpen = dt.Rows.Count > 0; } catch { } }
        private void SeleccionarProducto(DataRowView row) { _productoSeleccionado = row.Row; _ignorarPerdidaFoco = true; txtBuscarProducto.Text = _productoSeleccionado["Descripcion"].ToString(); _ignorarPerdidaFoco = false; popupProducto.IsOpen = false; numCantidad.Focus(); }
        private void lstSugerenciasCliente_MouseUp(object sender, MouseButtonEventArgs e) { if (lstSugerenciasCliente.SelectedItem is DataRowView r) SeleccionarCliente(r); }
        private void lstSugerenciasProducto_MouseUp(object sender, MouseButtonEventArgs e) { if (lstSugerenciasProducto.SelectedItem is DataRowView r) SeleccionarProducto(r); }
        private async void txtBuscar_LostFocus(object sender, RoutedEventArgs e) { if (_ignorarPerdidaFoco) return; await Task.Delay(150); if (!lstSugerenciasCliente.IsFocused && !lstSugerenciasProducto.IsFocused) { popupCliente.IsOpen = false; popupProducto.IsOpen = false; } }
        private void txtBuscar_PreviewKeyDown(object sender, KeyEventArgs e) { if (e.Key == Key.Down) { if (popupCliente.IsOpen) { lstSugerenciasCliente.SelectedIndex = 0; lstSugerenciasCliente.Focus(); } else if (popupProducto.IsOpen) { lstSugerenciasProducto.SelectedIndex = 0; lstSugerenciasProducto.Focus(); } } else if (e.Key == Key.Escape) { popupCliente.IsOpen = false; popupProducto.IsOpen = false; } else if (e.Key == Key.Enter && sender == txtBuscarProducto) { if (!popupProducto.IsOpen) { AbrirVentanaVarios(); e.Handled = true; } } }
        private void lstSugerencias_PreviewKeyDown(object sender, KeyEventArgs e) { if (e.Key == Key.Enter) { if (sender == lstSugerenciasCliente && lstSugerenciasCliente.SelectedItem is DataRowView c) SeleccionarCliente(c); else if (sender == lstSugerenciasProducto && lstSugerenciasProducto.SelectedItem is DataRowView p) SeleccionarProducto(p); } }

        private void btnAgregarProducto_Click(object sender, RoutedEventArgs e)
        {
            if (_productoSeleccionado == null) { AbrirVentanaVarios(); return; }

            int id = Convert.ToInt32(_productoSeleccionado["ProductoID"]);
            int cant = (int)numCantidad.Value;
            decimal precioBase = Convert.ToDecimal(_productoSeleccionado["PrecioVenta"]);
            decimal porcentaje = ObtenerPorcentajeLista();
            decimal precioFinal = precioBase * (1 + (porcentaje / 100));
            string imgPath = _productoSeleccionado.Table.Columns.Contains("ImagenPath") ? _productoSeleccionado["ImagenPath"].ToString() : null;

            var item = CarritoDeVenta.FirstOrDefault(x => x.ProductoID == id);
            if (item != null) item.Cantidad += cant;
            else
            {
                CarritoDeVenta.Add(new FacturaItem
                {
                    ProductoID = id,
                    Codigo = _productoSeleccionado["Codigo"].ToString(),
                    Descripcion = _productoSeleccionado["Descripcion"].ToString(),
                    Cantidad = cant,
                    PrecioUnitario = precioFinal,
                    ImagenPath = imgPath
                });
            }
            dgvFactura.Items.Refresh();
            LimpiarProducto();
            ActualizarTotal();
        }

        private void AbrirVentanaVarios()
        {
            var ventanaVarios = new ProductoVarioWindow();
            if (Application.Current.MainWindow != null) Application.Current.MainWindow.Opacity = 0.8;
            ventanaVarios.ShowDialog();
            if (Application.Current.MainWindow != null) Application.Current.MainWindow.Opacity = 1;

            if (ventanaVarios.Confirmado)
            {
                CarritoDeVenta.Add(new FacturaItem
                {
                    ProductoID = DatabaseService.ObtenerIDProductoVarios(),
                    Codigo = "VAR",
                    Descripcion = ventanaVarios.Descripcion,
                    Cantidad = 1,
                    PrecioUnitario = ventanaVarios.Precio,
                    ImagenPath = null
                });
                dgvFactura.Items.Refresh();
                ActualizarTotal();
                LimpiarProducto();
            }
        }

        private void btnEliminarItem_Click(object sender, RoutedEventArgs e)
        {
            if ((sender as Button)?.DataContext is FacturaItem item) { CarritoDeVenta.Remove(item); ActualizarTotal(); }
        }

        private void ActualizarTotal()
        {
            decimal total = CarritoDeVenta.Sum(x => x.Subtotal);
            lblTotal.Text = $"{total:C2}";

            if (CarritoDeVenta.Count > 0)
                CustomerScreenService.Actualizar(CarritoDeVenta.ToList(), total);
            else
                CustomerScreenService.Resetear();
        }

        private void LimpiarProducto() { _productoSeleccionado = null; txtBuscarProducto.Text = ""; numCantidad.Value = 1; txtBuscarProducto.Focus(); }
        private void LimpiarFormulario() { CargarClientePorDefecto(); cmbTipoComprobante.SelectedIndex = 0; cmbCondicionVenta.SelectedIndex = 0; if (this.FindName("cmbListaPrecios") != null && cmbListaPrecios.Items.Count > 0) cmbListaPrecios.SelectedValue = 1; CarritoDeVenta.Clear(); ActualizarTotal(); LimpiarProducto(); txtBuscarCliente.Focus(); }
        private void btnCancelarFactura_Click(object sender, RoutedEventArgs e) { if (CustomMessageBox.Show("¿Cancelar venta?", "Confirmar", MessageBoxButton.YesNo) == MessageBoxResult.Yes) LimpiarFormulario(); }

        private async void btnGuardarFactura_Click(object sender, RoutedEventArgs e)
        {
            if (CarritoDeVenta.Count == 0) { CustomMessageBox.Show("Agregue productos."); return; }
            if (_clienteSeleccionado == null) { CustomMessageBox.Show("Seleccione cliente."); return; }

            // Si el cliente no eligió método de pago en pantalla táctil, 
            // podemos forzar la selección aquí llamando a: CustomerScreenService.PantallaPagos();

            if (CustomMessageBox.Show("¿Confirmar venta?", "Confirmar", MessageBoxButton.YesNo) == MessageBoxResult.Yes)
            {
                try
                {
                    // Si elegimos "Mercado Pago" en el combo, usamos eso. Si no, Contado.
                    string condicion = cmbCondicionVenta.Text; // Usamos .Text por si seleccionamos desde código
                    if (string.IsNullOrEmpty(condicion)) condicion = "Contado";

                    string tipoComp = (cmbTipoComprobante.SelectedItem as ComboBoxItem).Content.ToString();
                    decimal totalVenta = CarritoDeVenta.Sum(i => i.Subtotal);
                    int clienteID = Convert.ToInt32(_clienteSeleccionado["ClienteID"]);
                    string clienteNombre = _clienteSeleccionado["RazonSocial"].ToString();
                    string caeObtenido = "", vtoCae = "";
                    int nroComprobante = 0;

                    if (tipoComp.Contains("Factura"))
                    {
                        if (CustomMessageBox.Show("¿Confirmar FACTURA ELECTRÓNICA?", "AFIP", MessageBoxButton.YesNo, MessageBoxImage.Warning) == MessageBoxResult.Yes)
                        {
                            DataRow config = DatabaseService.GetConfiguracion();
                            int ptoVenta = config != null ? Convert.ToInt32(config["PuntoVenta"]) : 1;
                            int tipoAfip = tipoComp == "Factura A" ? 1 : 6;
                            long cuitCliente = 0;
                            long.TryParse(_clienteSeleccionado["CUIT"].ToString().Replace("-", ""), out cuitCliente);
                            var res = await AfipService.FacturarAsync(tipoAfip, ptoVenta, (double)totalVenta, cuitCliente, CarritoDeVenta.ToList());
                            if (!res.Exito) { CustomMessageBox.Show("Error AFIP: " + res.Error); return; }
                            caeObtenido = res.CAE; vtoCae = res.Vencimiento; nroComprobante = res.NumeroComprobante;
                        }
                    }

                    bool exito = DatabaseService.GuardarFactura(clienteID, tipoComp, totalVenta, CarritoDeVenta.ToList(), condicion);

                    if (exito)
                    {
                        CustomerScreenService.PantallaGracias(); // Mostrar agradecimiento

                        if (CustomMessageBox.Show("¿Imprimir?", "Éxito", MessageBoxButton.YesNo) == MessageBoxResult.Yes)
                        {
                            DataTable dt = new DataTable();
                            dt.Columns.Add("Codigo"); dt.Columns.Add("Descripcion"); dt.Columns.Add("Cantidad"); dt.Columns.Add("Subtotal");
                            foreach (var i in CarritoDeVenta) dt.Rows.Add(i.Codigo, i.Descripcion, i.Cantidad, i.Subtotal);
                            string infoExtra = condicion;
                            if (!string.IsNullOrEmpty(caeObtenido)) infoExtra += $" CAE:{caeObtenido} VTO:{vtoCae}";
                            PrintService.ImprimirTicketVenta(tipoComp, nroComprobante, clienteNombre, DateTime.Now, dt, totalVenta, infoExtra);
                        }

                        await Task.Delay(3000); // Dar tiempo al cliente de ver el Gracias
                        LimpiarFormulario();
                    }
                }
                catch (Exception ex) { CustomMessageBox.Show(ex.Message); }
            }
        }
    }
}