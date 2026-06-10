using SchettiniGestion;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Data;
using System.Globalization;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;

namespace SchettiniGestion.WPF
{
    public partial class FacturacionControl : UserControl
    {
        private ObservableCollection<FacturaItem> CarritoDeVenta;
        private DataRow _clienteSeleccionado;
        private DataRow _productoSeleccionado;
        private bool _ignorarPerdidaFoco = false;
        private bool _cargandoListas = false;
        private DispatcherTimer _timerVerificacionMP;
        private string _referenciaPagoMP = "";
        private bool _esperandoPagoMP = false;

        /// <summary>Si se asigna (ej. "Remito", "Pedido"), preselecciona ese tipo al cargar. Usado cuando se abre desde Nuevo Remito/Pedido.</summary>
        public string TipoComprobanteInicial { get; set; }

        public FacturacionControl()
        {
            InitializeComponent();
            CarritoDeVenta = new ObservableCollection<FacturaItem>();
            dgvFactura.ItemsSource = CarritoDeVenta;
            icCardsFactura.ItemsSource = CarritoDeVenta;

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
            cmbCondicionVenta_SelectionChanged(null, null);
            var w = Window.GetWindow(this);
            if (w != null) w.PreviewKeyDown += Ventana_PreviewKeyDown;

            if (!string.IsNullOrEmpty(TipoComprobanteInicial))
            {
                for (int i = 0; i < cmbTipoComprobante.Items.Count; i++)
                {
                    if ((cmbTipoComprobante.Items[i] as System.Windows.Controls.ComboBoxItem)?.Content?.ToString() == TipoComprobanteInicial)
                    {
                        cmbTipoComprobante.SelectedIndex = i;
                        break;
                    }
                }
                if (tabFacturacion != null) tabFacturacion.SelectedIndex = 0;
            }
        }

        private void FacturacionControl_Unloaded(object sender, RoutedEventArgs e)
        {
            CustomerScreenService.OnClienteEligioPago -= ProcesarPagoCliente;
            var w = Window.GetWindow(this);
            if (w != null) w.PreviewKeyDown -= Ventana_PreviewKeyDown;
            CancelarModoQR();
        }

        private void Ventana_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.F1) { new AyudaAtajosWindow().ShowDialog(); e.Handled = true; }
        }

        // --- LÓGICA DE CANTIDAD (+ / -) ---
        private void btnRestar_Click(object sender, RoutedEventArgs e)
        {
            int cant = 1;
            int.TryParse(txtCantidad.Text, out cant);
            if (cant > 1) cant--;
            txtCantidad.Text = cant.ToString();
        }

        private void btnSumar_Click(object sender, RoutedEventArgs e)
        {
            int cant = 1;
            int.TryParse(txtCantidad.Text, out cant);
            cant++;
            txtCantidad.Text = cant.ToString();
        }

        // --- LÓGICA AGREGAR PRODUCTO ---
        private void btnAgregarProducto_Click(object sender, RoutedEventArgs e)
        {
            if (_productoSeleccionado == null) { AbrirVentanaVarios(); return; }
            AgregarProductoSeleccionadoAlCarrito();
        }

        /// <summary>Agrega al carrito el producto en <see cref="_productoSeleccionado"/> usando la cantidad del campo y la lista de precios.</summary>
        private void AgregarProductoSeleccionadoAlCarrito()
        {
            if (_productoSeleccionado == null) return;

            int id = Convert.ToInt32(_productoSeleccionado["ProductoID"]);
            int cant = 1;
            int.TryParse(txtCantidad.Text, out cant);
            if (cant < 1) cant = 1;

            decimal precioBase = Convert.ToDecimal(_productoSeleccionado["PrecioVenta"]);
            decimal porcentaje = ObtenerPorcentajeLista();
            decimal precioFinal = precioBase * (1 + (porcentaje / 100));
            string imgPath = _productoSeleccionado.Table.Columns.Contains("ImagenPath") ? _productoSeleccionado["ImagenPath"].ToString() : null;

            var item = CarritoDeVenta.FirstOrDefault(x => x.ProductoID == id);
            if (item != null) item.Cantidad += cant;
            else
            {
                decimal alicuota = DatabaseService.ObtenerPctIvaPorTipoProducto(
                    _productoSeleccionado.Table.Columns.Contains("TipoIVA") ? _productoSeleccionado["TipoIVA"] : null);
                CarritoDeVenta.Add(new FacturaItem
                {
                    ProductoID = id,
                    Codigo = _productoSeleccionado["Codigo"].ToString(),
                    Descripcion = _productoSeleccionado["Descripcion"].ToString(),
                    Cantidad = cant,
                    PrecioUnitario = precioFinal,
                    AlicuotaIvaPct = alicuota,
                    ImagenPath = imgPath
                });
            }
            dgvFactura.Items.Refresh();
            popupProducto.IsOpen = false;
            LimpiarProducto();
            ActualizarTotal();
        }

        /// <summary>Enter en buscador: código/barras exacto o una sola sugerencia agrega al carrito; varias sugerencias baja al listado; si no hay coincidencias abre Varios.</summary>
        private void txtBuscarProductoEnterAgregarSiCorresponde()
        {
            string texto = (txtBuscarProducto.Text ?? "").Trim();
            if (string.IsNullOrEmpty(texto)) return;

            DataRow exacto = DatabaseService.BuscarProductoExactoCodigoOCodigoBarra(texto);
            if (exacto != null)
            {
                _productoSeleccionado = exacto;
                AgregarProductoSeleccionadoAlCarrito();
                return;
            }

            if (popupProducto.IsOpen)
            {
                if (lstSugerenciasProducto.Items.Count == 1 && lstSugerenciasProducto.Items[0] is DataRowView drvUnico)
                {
                    _productoSeleccionado = drvUnico.Row;
                    AgregarProductoSeleccionadoAlCarrito();
                    return;
                }
                if (lstSugerenciasProducto.Items.Count > 1)
                {
                    lstSugerenciasProducto.SelectedIndex = 0;
                    lstSugerenciasProducto.Focus();
                    return;
                }
            }

            if (!popupProducto.IsOpen)
                AbrirVentanaVarios();
        }

        // --- MERCADO PAGO QR ---
        private async void btnPagoQR_Click(object sender, RoutedEventArgs e)
        {
            if (_esperandoPagoMP)
            {
                if (ModernMessageBox.Show("¿Cancelar el cobro con QR?", "Cancelar", MessageBoxButton.YesNo) == MessageBoxResult.Yes)
                    CancelarModoQR();
                return;
            }

            if (CarritoDeVenta.Count == 0) { ModernMessageBox.Show("No hay productos."); return; }

            decimal total = CarritoDeVenta.Sum(x => x.Subtotal);

            btnPagoQR.Content = "⏳ Cancelar QR";
            btnGuardarFactura.IsEnabled = false;
            _esperandoPagoMP = true;
            _referenciaPagoMP = "SchTec_" + DateTime.Now.Ticks.ToString();

            try
            {
                var respuesta = await Task.Run(() => MercadoPagoService.CrearOrdenQR(total, "Compra Local", _referenciaPagoMP));

                if (respuesta.Exito)
                {
                    CustomerScreenService.PantallaQR(respuesta.QRData, total);

                    if (_timerVerificacionMP == null)
                    {
                        _timerVerificacionMP = new DispatcherTimer();
                        _timerVerificacionMP.Interval = TimeSpan.FromSeconds(3);
                        _timerVerificacionMP.Tick += TimerVerificacionMP_Tick;
                    }
                    _timerVerificacionMP.Start();
                }
                else
                {
                    ModernMessageBox.Show("Error MP: " + respuesta.Error);
                    CancelarModoQR();
                }
            }
            catch (Exception ex)
            {
                ModernMessageBox.Show("Error: " + ex.Message);
                CancelarModoQR();
            }
        }

        private async void TimerVerificacionMP_Tick(object sender, EventArgs e)
        {
            try
            {
                var info = await Task.Run(() => MercadoPagoService.VerificarEstadoPago(_referenciaPagoMP));

                if (info.Estado == "approved")
                {
                    _timerVerificacionMP.Stop();
                    _esperandoPagoMP = false;
                    _referenciaPagoMP = info.IdOperacion;

                    CustomerScreenService.ActualizarMensajeQR("¡PAGO APROBADO!", Brushes.LightGreen);
                    await Task.Delay(1500);

                    SeleccionarCondicionMP();
                    btnGuardarFactura.IsEnabled = true;
                    btnGuardarFactura_Click(sender, new RoutedEventArgs());

                    btnPagoQR.Content = "📱 Mercado Pago QR";
                }
                else if (info.Estado == "in_process") CustomerScreenService.ActualizarMensajeQR("Procesando...", Brushes.Yellow);
                else if (info.Estado == "rejected") CustomerScreenService.ActualizarMensajeQR("Pago Rechazado.", Brushes.Red);
            }
            catch { }
        }

        private void CancelarModoQR()
        {
            _esperandoPagoMP = false;
            if (_timerVerificacionMP != null) _timerVerificacionMP.Stop();

            btnPagoQR.Content = "📱 Mercado Pago QR";
            btnGuardarFactura.IsEnabled = true;

            decimal total = CarritoDeVenta.Sum(x => x.Subtotal);
            CustomerScreenService.Actualizar(CarritoDeVenta.ToList(), total);
        }

        // --- MÉTODOS AUXILIARES ---
        private void ProcesarPagoCliente(string opcion)
        {
            Dispatcher.Invoke(() =>
            {
                SeleccionarCondicion(opcion);
                if (opcion == "MERCADOPAGO") btnPagoQR_Click(null, null);
                else if (opcion == "TARJETA") ModernMessageBox.Show("Usar Posnet.", "Info");
                else txtMontoPagado.Focus();
            });
        }

        private void SeleccionarCondicionMP() { SeleccionarCondicion("MERCADOPAGO"); }

        private void SeleccionarCondicion(string tag)
        {
            foreach (ComboBoxItem item in cmbCondicionVenta.Items)
            {
                if (item.Content.ToString().Replace(" ", "").ToUpper().Contains(tag))
                {
                    cmbCondicionVenta.SelectedItem = item;
                    break;
                }
            }
        }

        private void cmbCondicionVenta_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (pnlCalculoEfectivo == null) return;
            string cond = (cmbCondicionVenta.SelectedItem as ComboBoxItem)?.Content.ToString();

            if (cond == "Contado")
            {
                pnlCalculoEfectivo.Visibility = Visibility.Visible;
                txtMontoPagado.Focus();
            }
            else
            {
                pnlCalculoEfectivo.Visibility = Visibility.Collapsed;
                txtMontoPagado.Text = "";
                lblVuelto.Text = "$ 0,00";
            }
        }

        private async void btnGuardarFactura_Click(object sender, RoutedEventArgs e)
        {
            if (CarritoDeVenta.Count == 0) { ModernMessageBox.Show("Agregue productos."); return; }
            if (_clienteSeleccionado == null) { ModernMessageBox.Show("Seleccione cliente."); return; }

            foreach (var it in CarritoDeVenta)
            {
                if (string.Equals(it.Codigo, "VARIOS", StringComparison.OrdinalIgnoreCase)) continue;
                int disp = DatabaseService.GetStockActualProducto(it.ProductoID);
                if (it.Cantidad > disp)
                {
                    ModernMessageBox.Show($"Stock insuficiente: «{it.Descripcion}» (disponible {disp}, pedido {it.Cantidad}).");
                    return;
                }
            }

            // 1. Obtener Configuración
            DataRow config = DatabaseService.GetConfiguracion();
            int puntoVentaConfig = 1;
            if (config != null && config["PuntoVenta"] != DBNull.Value)
            {
                puntoVentaConfig = Convert.ToInt32(config["PuntoVenta"]);
            }

            // 2. Determinar Tipo AFIP
            decimal total = CarritoDeVenta.Sum(x => x.Subtotal);
            string tipoCompTexto = (cmbTipoComprobante.SelectedItem as ComboBoxItem).Content.ToString();

            int tipoAfip = 0;
            if (tipoCompTexto == "Factura")
            {
                string cuitStr = _clienteSeleccionado["CUIT"].ToString();
                if (cuitStr.Length >= 11 && !cuitStr.Contains("00-00000000")) tipoAfip = 1;
                else tipoAfip = 6;
            }
            else if (tipoCompTexto == "Ticket") tipoAfip = 6;

            // Validación Factura A
            if (tipoAfip == 1)
            {
                string cuitStr = _clienteSeleccionado["CUIT"].ToString();
                if (cuitStr.Length < 11 || cuitStr.Contains("00-00000000"))
                {
                    ModernMessageBox.Show("Error: Para Factura A, el cliente debe tener CUIT válido.");
                    return;
                }
            }

            btnGuardarFactura.IsEnabled = false;
            try
            {
                string cae = null;
                string vtoCae = null;
                int nroComprobante = 0;

                if (tipoAfip > 0)
                {
                    CustomerScreenService.ActualizarMensajeQR("Facturando AFIP...", Brushes.Orange);
                    string cuitLimpio = _clienteSeleccionado["CUIT"].ToString().Replace("-", "").Trim();
                    long cuitCliente = 0;
                    long.TryParse(cuitLimpio, out cuitCliente);
                    var resultadoAfip = await AfipService.FacturarAsync(tipoAfip, puntoVentaConfig, (double)total, cuitCliente, CarritoDeVenta.ToList());
                    if (resultadoAfip.Exito)
                    {
                        cae = resultadoAfip.CAE;
                        vtoCae = resultadoAfip.Vencimiento;
                        nroComprobante = resultadoAfip.NumeroComprobante;
                    }
                    else
                    {
                        ModernMessageBox.Show("❌ ERROR AFIP: " + resultadoAfip.Error);
                        btnGuardarFactura.IsEnabled = true;
                        return;
                    }
                }

                var win = Application.Current.Windows.OfType<Window>().FirstOrDefault(w => w.IsActive) ?? Application.Current.MainWindow;
                var cobroModal = new CobroModalWindow(win, total);
                if (cobroModal.ShowDialog() != true)
                {
                    btnGuardarFactura.IsEnabled = true;
                    return;
                }

                string condicionTicket = string.Join(" + ", cobroModal.Cobranzas.Select(c => $"{c.nombreMedio} {c.monto:C2}"));
                string condVent = (cmbCondicionVenta.SelectedItem as ComboBoxItem)?.Content?.ToString() ?? "Contado";
                int cliID = Convert.ToInt32(_clienteSeleccionado["ClienteID"]);
                int? listaId = cmbListaPrecios.SelectedItem is DataRowView lr ? (int?)Convert.ToInt32(lr["ListaID"]) : null;

                var parcelas = cobroModal.Cobranzas.ConvertAll(ci => new FacturaCobranzaParcela
                {
                    MedioPagoID = ci.MedioPagoID,
                    NombreMedio = ci.nombreMedio ?? "",
                    Monto = ci.monto
                });

                int fid = DatabaseService.GuardarFactura(cliID, tipoCompTexto, total, CarritoDeVenta.ToList(),
                    condVent, condicionTicket, cae, vtoCae, nroComprobante, listaId, parcelas);

                if (fid > 0)
                {
                    CustomerScreenService.PantallaGracias();
                    string msgExito = "Venta Guardada.";
                    if (!string.IsNullOrEmpty(cae)) msgExito += "\n¡Factura Electrónica Aprobada!";
                    if (ModernMessageBox.Show($"{msgExito}\n¿Imprimir comprobante?", "Éxito", MessageBoxButton.YesNo) == MessageBoxResult.Yes)
                    {
                        DataTable dt = new DataTable();
                        dt.Columns.Add("Codigo"); dt.Columns.Add("Descripcion"); dt.Columns.Add("Cantidad"); dt.Columns.Add("Subtotal");
                        foreach (var item in CarritoDeVenta) dt.Rows.Add(item.Codigo, item.Descripcion, item.Cantidad, item.Subtotal);
                        PrintService.ImprimirTicketVenta(tipoCompTexto, nroComprobante, _clienteSeleccionado["RazonSocial"].ToString(), DateTime.Now, dt, total, condicionTicket, cae, vtoCae);
                    }
                    await Task.Delay(2000);
                    LimpiarFormulario();
                }
                else
                    ModernMessageBox.Show("No se pudo guardar la factura.");
            }
            catch (Exception ex)
            {
                ModernMessageBox.Show("ERROR: " + ex.Message);
            }
            finally
            {
                btnGuardarFactura.IsEnabled = true;
            }
        }

        private void LimpiarFormulario()
        {
            CarritoDeVenta.Clear();
            ActualizarTotal();
            CargarClientePorDefecto();
            _referenciaPagoMP = "";
            CancelarModoQR();
            LimpiarProducto();
        }

        private void LimpiarProducto()
        {
            _productoSeleccionado = null;
            txtBuscarProducto.Text = "";
            txtCantidad.Text = "1";
            txtBuscarProducto.Focus();
        }

        private void ActualizarTotal()
        {
            decimal subtotal = CarritoDeVenta.Sum(x => x.Cantidad * x.PrecioUnitario);
            decimal totalConDescRec = CarritoDeVenta.Sum(x => x.Subtotal);
            decimal dtoRec = subtotal - totalConDescRec;
            decimal neto = 0, ivaTotal = 0;
            foreach (var it in CarritoDeVenta)
            {
                decimal line = it.Subtotal;
                decimal al = it.AlicuotaIvaPct;
                if (al <= 0.01m)
                {
                    neto += line;
                    continue;
                }
                decimal denominador = 1 + al / 100m;
                decimal nl = Math.Round(line / denominador, 2);
                decimal il = Math.Round(line - nl, 2);
                neto += nl;
                ivaTotal += il;
            }

            lblSubtotal.Text = subtotal.ToString("C2");
            lblDescuentos.Text = dtoRec != 0 ? dtoRec.ToString("C2") + (dtoRec > 0 ? " (Dto)" : " (Rec)") : "$ 0,00";
            lblNeto.Text = neto.ToString("C2");
            lblIVA.Text = ivaTotal.ToString("C2");
            lblTotal.Text = totalConDescRec.ToString("C2");
            CustomerScreenService.Actualizar(CarritoDeVenta.ToList(), totalConDescRec);

            dgvFactura.Items.Refresh();
            icCardsFactura.Items.Refresh();
        }

        // --- SOPORTE UI ---
        private void txtMontoPagado_TextChanged(object sender, TextChangedEventArgs e)
        {
            decimal total = CarritoDeVenta.Sum(x => x.Subtotal);
            decimal pagado = 0;
            decimal.TryParse(txtMontoPagado.Text, out pagado);
            decimal vuelto = pagado - total;
            lblVuelto.Text = vuelto >= 0 ? vuelto.ToString("C2") : "Falta";
            lblVuelto.Foreground = vuelto >= 0 ? Brushes.Yellow : Brushes.Red;
        }

        private void btnEliminarItem_Click(object sender, RoutedEventArgs e)
        {
            if ((sender as Button)?.DataContext is FacturaItem item) { CarritoDeVenta.Remove(item); ActualizarTotal(); }
        }

        private void btnCancelarFactura_Click(object sender, RoutedEventArgs e) { LimpiarFormulario(); }

        private void AbrirVentanaVarios()
        {
            var ventanaVarios = new ProductoVarioWindow();
            ventanaVarios.ShowDialog();
            if (ventanaVarios.Confirmado)
            {
                CarritoDeVenta.Add(new FacturaItem
                {
                    ProductoID = DatabaseService.ObtenerIDProductoVarios(),
                    Codigo = "VAR",
                    Descripcion = ventanaVarios.Descripcion,
                    Cantidad = 1,
                    PrecioUnitario = ventanaVarios.Precio,
                    AlicuotaIvaPct = 21m,
                    ImagenPath = null
                });
                dgvFactura.Items.Refresh();
                ActualizarTotal();
                LimpiarProducto();
            }
        }

        private void CargarListasPrecios() { try { cmbListaPrecios.ItemsSource = DatabaseService.GetListasPrecios().DefaultView; cmbListaPrecios.SelectedIndex = 0; } catch { } }
        private void CargarClientePorDefecto() { _clienteSeleccionado = DatabaseService.BuscarCliente("00-00000000-0"); if (_clienteSeleccionado != null) lblClienteSeleccionado.Text = _clienteSeleccionado["RazonSocial"].ToString(); }
        private decimal ObtenerPorcentajeLista() { if (cmbListaPrecios.SelectedItem is DataRowView row) return Convert.ToDecimal(row["Porcentaje"]); return 0; }
        private void RecalcularCarritoConNuevaLista()
        {
            decimal porcentaje = ObtenerPorcentajeLista();
            foreach (var item in CarritoDeVenta)
            {
                if (item.Codigo == "VAR") continue;
                DataRow prod = DatabaseService.BuscarProducto(item.Codigo);
                if (prod != null)
                {
                    item.PrecioUnitario = Convert.ToDecimal(prod["PrecioVenta"]) * (1 + (porcentaje / 100));
                    item.AlicuotaIvaPct = DatabaseService.ObtenerPctIvaPorTipoProducto(prod.Table.Columns.Contains("TipoIVA") ? prod["TipoIVA"] : null);
                }
            }
            dgvFactura.Items.Refresh();
            ActualizarTotal();
        }
        private void cmbListaPrecios_SelectionChanged(object sender, SelectionChangedEventArgs e) { if (!_cargandoListas) RecalcularCarritoConNuevaLista(); }
        private void txtBuscarCliente_TextChanged(object sender, TextChangedEventArgs e) { if (txtBuscarCliente.Text.Length < 2) { popupCliente.IsOpen = false; return; } try { DataTable dt = DatabaseService.BuscarClientesMultiples(txtBuscarCliente.Text); lstSugerenciasCliente.ItemsSource = dt.DefaultView; popupCliente.IsOpen = true; } catch { } }
        private void txtBuscarProducto_TextChanged(object sender, TextChangedEventArgs e) { if (_ignorarPerdidaFoco) return; if (txtBuscarProducto.Text.Length < 1) { popupProducto.IsOpen = false; _productoSeleccionado = null; return; } try { DataTable dt = DatabaseService.BuscarProductosMultiples_ParaVenta(txtBuscarProducto.Text); lstSugerenciasProducto.ItemsSource = dt.DefaultView; popupProducto.IsOpen = dt.Rows.Count > 0; } catch { } }
        private void SeleccionarCliente(DataRowView row) { _clienteSeleccionado = row.Row; _ignorarPerdidaFoco = true; txtBuscarCliente.Text = _clienteSeleccionado["RazonSocial"].ToString(); lblClienteSeleccionado.Text = _clienteSeleccionado["RazonSocial"].ToString(); _ignorarPerdidaFoco = false; popupCliente.IsOpen = false; txtBuscarProducto.Focus(); }
        private void lstSugerenciasCliente_MouseUp(object sender, MouseButtonEventArgs e) { if (lstSugerenciasCliente.SelectedItem is DataRowView r) SeleccionarCliente(r); }
        private void lstSugerenciasProducto_MouseUp(object sender, MouseButtonEventArgs e) { if (lstSugerenciasProducto.SelectedItem is DataRowView r) { _productoSeleccionado = r.Row; _ignorarPerdidaFoco = true; txtBuscarProducto.Text = _productoSeleccionado["Descripcion"].ToString(); _ignorarPerdidaFoco = false; popupProducto.IsOpen = false; txtCantidad.Focus(); txtCantidad.SelectAll(); } }
        private async void txtBuscar_LostFocus(object sender, RoutedEventArgs e) { if (_ignorarPerdidaFoco) return; await Task.Delay(150); if (!lstSugerenciasCliente.IsFocused && !lstSugerenciasProducto.IsFocused) { popupCliente.IsOpen = false; popupProducto.IsOpen = false; } }
        private void txtBuscar_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Down)
            {
                if (popupCliente.IsOpen) { lstSugerenciasCliente.SelectedIndex = 0; lstSugerenciasCliente.Focus(); }
                else if (popupProducto.IsOpen) { lstSugerenciasProducto.SelectedIndex = 0; lstSugerenciasProducto.Focus(); }
            }
            else if (e.Key == Key.Escape)
            {
                popupCliente.IsOpen = false;
                popupProducto.IsOpen = false;
            }
            else if (e.Key == Key.Enter && sender == txtBuscarProducto)
            {
                e.Handled = true;
                txtBuscarProductoEnterAgregarSiCorresponde();
            }
        }
        private void lstSugerencias_PreviewKeyDown(object sender, KeyEventArgs e) { if (e.Key == Key.Enter) { if (sender == lstSugerenciasCliente && lstSugerenciasCliente.SelectedItem is DataRowView c) SeleccionarCliente(c); else if (sender == lstSugerenciasProducto && lstSugerenciasProducto.SelectedItem is DataRowView p) { _productoSeleccionado = p.Row; _ignorarPerdidaFoco = true; txtBuscarProducto.Text = _productoSeleccionado["Descripcion"].ToString(); _ignorarPerdidaFoco = false; popupProducto.IsOpen = false; txtCantidad.Focus(); txtCantidad.SelectAll(); } } }
        private void NumberValidationTextBox(object sender, TextCompositionEventArgs e) { e.Handled = new Regex("[^0-9,.]+").IsMatch(e.Text); }

        // --- Vista Cards/Lista ---
        private void btnVistaLista_Click(object sender, RoutedEventArgs e)
        {
            dgvFactura.Visibility = Visibility.Visible;
            svCardsFactura.Visibility = Visibility.Collapsed;
            btnVistaLista.Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#007ACC"));
            btnVistaCards.Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#444"));
        }
        private void btnVistaCards_Click(object sender, RoutedEventArgs e)
        {
            dgvFactura.Visibility = Visibility.Collapsed;
            svCardsFactura.Visibility = Visibility.Visible;
            btnVistaCards.Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#007ACC"));
            btnVistaLista.Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#444"));
        }

        // --- Cliente: Crear ---
        private void btnCrearCliente_Click(object sender, RoutedEventArgs e)
        {
            string texto = txtBuscarCliente.Text.Trim();
            if (string.IsNullOrEmpty(texto)) { ModernMessageBox.Show("Escriba el nombre o razón social del cliente."); return; }
            var modal = new ClienteRapidoModalWindow(texto);
            if (modal.ShowDialog() == true && modal.ClienteID > 0)
            {
                _clienteSeleccionado = DatabaseService.BuscarClientePorID(modal.ClienteID);
                if (_clienteSeleccionado != null)
                {
                    lblClienteSeleccionado.Text = _clienteSeleccionado["RazonSocial"].ToString();
                    txtBuscarCliente.Text = _clienteSeleccionado["RazonSocial"].ToString();
                    popupCliente.IsOpen = false;
                    txtBuscarProducto.Focus();
                }
            }
        }

        // --- Mini botones: Descuento, Recargo, Editar, Eliminar ---
        private void btnDescuentoItem_Click(object sender, RoutedEventArgs e)
        {
            if ((sender as Button)?.DataContext is FacturaItem item)
            {
                var win = new ModernInputWindow("Descuento", "Porcentaje de descuento:", item.DescuentoPorcentaje.ToString("N0"));
                if (win.ShowDialog() == true && decimal.TryParse(win.ResponseText?.Replace(",", "."), System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out decimal pct) && pct >= 0 && pct <= 100)
                {
                    item.DescuentoPorcentaje = pct;
                    ActualizarTotal();
                }
            }
        }
        private void btnRecargoItem_Click(object sender, RoutedEventArgs e)
        {
            if ((sender as Button)?.DataContext is FacturaItem item)
            {
                var win = new ModernInputWindow("Recargo", "Porcentaje de recargo:", item.RecargoPorcentaje.ToString("N0"));
                if (win.ShowDialog() == true && decimal.TryParse(win.ResponseText?.Replace(",", "."), System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out decimal pct) && pct >= 0 && pct <= 100)
                {
                    item.RecargoPorcentaje = pct;
                    ActualizarTotal();
                }
            }
        }
        private void btnEditarItem_Click(object sender, RoutedEventArgs e)
        {
            if ((sender as Button)?.DataContext is FacturaItem item)
            {
                var winCant = new ModernInputWindow("Editar cantidad", "Nueva cantidad:", item.Cantidad.ToString());
                if (winCant.ShowDialog() == true && int.TryParse(winCant.ResponseText, out int cant) && cant > 0) { item.Cantidad = cant; ActualizarTotal(); return; }
                var winPrecio = new ModernInputWindow("Editar precio", "Nuevo precio unitario:", item.PrecioUnitario.ToString("N2"));
                if (winPrecio.ShowDialog() == true && decimal.TryParse(winPrecio.ResponseText?.Replace(",", "."), System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out decimal prec) && prec >= 0) { item.PrecioUnitario = prec; ActualizarTotal(); }
            }
        }

        // --- AYUDA ATAJOS ---
        private void btnAyudaAtajos_Click(object sender, RoutedEventArgs e) { new AyudaAtajosWindow().ShowDialog(); }
    }
}