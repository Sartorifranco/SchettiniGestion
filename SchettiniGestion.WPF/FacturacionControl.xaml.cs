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
        // Pago Mercado Pago QR aprobado → cobranza pre-armada para saltear CobroModalWindow
        private bool _pagoMPAprobado = false;
        private List<CobranzaItem> _parcelas_MP = null;

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

            ActualizarUiSegunTipoComprobante();
        }

        private void cmbTipoComprobante_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            ActualizarUiSegunTipoComprobante();
        }

        private string ObtenerTipoComprobanteSeleccionado()
        {
            return (cmbTipoComprobante.SelectedItem as ComboBoxItem)?.Content?.ToString() ?? "Factura";
        }

        private bool EsDocumentoSinCobro(string tipo)
        {
            return tipo == "Presupuesto" || tipo == "Remito" || tipo == "Pedido"
                || tipo == "Nota de Crédito" || tipo == "Nota de Débito";
        }

        private string ObtenerTextoBotonGuardar(string tipo)
        {
            switch (tipo)
            {
                case "Presupuesto": return "📄 GENERAR PRESUPUESTO";
                case "Remito": return "📄 GENERAR REMITO";
                case "Pedido": return "📄 GENERAR PEDIDO";
                case "Nota de Crédito": return "📄 GENERAR NOTA DE CRÉDITO";
                case "Nota de Débito": return "📄 GENERAR NOTA DE DÉBITO";
                default: return "✅ COBRAR";
            }
        }

        private void ActualizarUiSegunTipoComprobante()
        {
            if (btnGuardarFactura == null) return;

            string tipo = ObtenerTipoComprobanteSeleccionado();
            bool sinCobro = EsDocumentoSinCobro(tipo);

            btnGuardarFactura.Content = ObtenerTextoBotonGuardar(tipo);

            if (pnlCondicionPago != null)
                pnlCondicionPago.Visibility = sinCobro ? Visibility.Collapsed : Visibility.Visible;

            if (btnPagoQR != null)
                btnPagoQR.Visibility = sinCobro ? Visibility.Collapsed : Visibility.Visible;

            if (sinCobro)
            {
                if (pnlCalculoEfectivo != null)
                    pnlCalculoEfectivo.Visibility = Visibility.Collapsed;
            }
            else
            {
                cmbCondicionVenta_SelectionChanged(null, null);
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
                if (lstSugerenciasProducto.SelectedItem is DataRowView sel)
                {
                    SeleccionarProductoSugerencia(sel);
                    return;
                }
                if (lstSugerenciasProducto.Items.Count > 0 && lstSugerenciasProducto.Items[0] is DataRowView first)
                {
                    SeleccionarProductoSugerencia(first);
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
                if (CustomMessageBox.Show("¿Cancelar el cobro con QR?", "Cancelar", MessageBoxButton.YesNo) == MessageBoxResult.Yes)
                    CancelarModoQR();
                return;
            }

            if (CarritoDeVenta.Count == 0) { CustomMessageBox.Show("No hay productos."); return; }

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
                    CustomMessageBox.Show("Error MP: " + respuesta.Error);
                    CancelarModoQR();
                }
            }
            catch (Exception ex)
            {
                CustomMessageBox.Show("Error: " + ex.Message);
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
                    _esperandoPagoMP  = false;
                    _referenciaPagoMP = info.IdOperacion;

                    CustomerScreenService.ActualizarMensajeQR("¡PAGO APROBADO!", Brushes.LightGreen);
                    await Task.Delay(1500);

                    // Armar cobranza Mercado Pago pre-confirmada para saltear CobroModalWindow
                    decimal totalMP = CarritoDeVenta.Sum(x => x.Subtotal);
                    int mpMedioId   = ObtenerMedioPagoIdMercadoPago();
                    _parcelas_MP    = new List<CobranzaItem>
                    {
                        new CobranzaItem { MedioPagoID = mpMedioId, nombreMedio = "Mercado Pago QR", monto = totalMP }
                    };
                    _pagoMPAprobado = true;

                    SeleccionarCondicionMP();
                    btnPagoQR.Content = "📱 Mercado Pago QR";

                    if (CustomMessageBox.Show(
                            $"Pago QR aprobado: {totalMP:C2}\n\n¿Confirmar y registrar la venta?",
                            "Mercado Pago — confirmar venta",
                            MessageBoxButton.YesNo, MessageBoxImage.Question) == MessageBoxResult.Yes)
                    {
                        btnGuardarFactura.IsEnabled = true;
                        btnGuardarFactura_Click(sender, new RoutedEventArgs());
                    }
                    else
                    {
                        _pagoMPAprobado = false;
                        _parcelas_MP = null;
                        btnGuardarFactura.IsEnabled = true;
                        CustomMessageBox.Show(
                            "El pago ya fue acreditado en Mercado Pago.\n\n" +
                            "Si no registra la venta, deberá gestionar la devolución manualmente en MP.",
                            "Pago QR recibido", MessageBoxButton.OK, MessageBoxImage.Warning);
                        CustomerScreenService.Actualizar(CarritoDeVenta.ToList(), CarritoDeVenta.Sum(x => x.Subtotal));
                    }
                }
                else if (info.Estado == "in_process") CustomerScreenService.ActualizarMensajeQR("Procesando...", Brushes.Yellow);
                else if (info.Estado == "rejected") CustomerScreenService.ActualizarMensajeQR("Pago Rechazado.", Brushes.Red);
            }
            catch { }
        }

        /// <summary>
        /// Devuelve el MedioPagoID cuyo nombre contenga "mercado" o "mp".
        /// Si no existe, devuelve 0 (genérico).
        /// </summary>
        private int ObtenerMedioPagoIdMercadoPago()
        {
            try
            {
                var dt = DatabaseService.GetMediosPagoCompleto();
                foreach (System.Data.DataRow row in dt.Rows)
                {
                    string nombre = row["Nombre"]?.ToString();
                    if (string.IsNullOrEmpty(nombre) && row.Table.Columns.Contains("NombreMedio"))
                        nombre = row["NombreMedio"]?.ToString();
                    nombre = nombre ?? "";
                    if (nombre.IndexOf("mercado", StringComparison.OrdinalIgnoreCase) >= 0 ||
                        nombre.IndexOf(" mp", StringComparison.OrdinalIgnoreCase) >= 0)
                        return Convert.ToInt32(row["MedioID"]);
                }
            }
            catch { }
            return 0;
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
                else if (opcion == "TARJETA") CustomMessageBox.Show("Usar Posnet.", "Info");
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
            if (CarritoDeVenta.Count == 0) { CustomMessageBox.Show("Agregue productos."); return; }
            // Si no hay cliente seleccionado, usar Consumidor Final automáticamente
            if (_clienteSeleccionado == null) CargarClientePorDefecto();
            if (_clienteSeleccionado == null) { CustomMessageBox.Show("No se pudo cargar el cliente por defecto. Verificá la conexión a la base de datos."); return; }

            string tipoCompTexto = ObtenerTipoComprobanteSeleccionado();
            if (tipoCompTexto == "Presupuesto") { GuardarPresupuestoDesdePos(); return; }
            if (tipoCompTexto == "Remito") { GuardarRemitoDesdePos(); return; }
            if (tipoCompTexto == "Pedido") { GuardarPedidoDesdePos(); return; }
            if (tipoCompTexto == "Nota de Crédito") { GuardarNotaVentaDesdePos("NC", "Nota de Crédito"); return; }
            if (tipoCompTexto == "Nota de Débito") { GuardarNotaVentaDesdePos("ND", "Nota de Débito"); return; }

            foreach (var it in CarritoDeVenta)
            {
                if (string.Equals(it.Codigo, "VARIOS", StringComparison.OrdinalIgnoreCase)) continue;
                int disp = DatabaseService.GetStockActualProducto(it.ProductoID);
                if (it.Cantidad > disp)
                {
                    CustomMessageBox.Show($"Stock insuficiente: «{it.Descripcion}» (disponible {disp}, pedido {it.Cantidad}).");
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
                    CustomMessageBox.Show("Error: Para Factura A, el cliente debe tener CUIT válido.");
                    return;
                }
            }

            btnGuardarFactura.IsEnabled = false;
            bool cobroConfirmado = false;
            try
            {
                bool afipConfigurado = AfipEstaConfigurado(config);
                if (tipoCompTexto == "Factura" && !afipConfigurado)
                {
                    if (CustomMessageBox.Show(
                            "AFIP no está configurado (CUIT y certificado).\n\n" +
                            "La venta se guardará sin CAE ni numeración fiscal.\n\n¿Desea continuar?",
                            "Factura sin AFIP", MessageBoxButton.YesNo, MessageBoxImage.Warning) != MessageBoxResult.Yes)
                    {
                        btnGuardarFactura.IsEnabled = true;
                        return;
                    }
                }

                // ── PASO 1: cobro PRIMERO para que el cajero confirme antes de ir a AFIP ──
                var win = Application.Current.Windows.OfType<Window>().FirstOrDefault(w => w.IsActive) ?? Application.Current.MainWindow;
                List<CobranzaItem> cobranzasConfirmadas;

                if (_pagoMPAprobado && _parcelas_MP != null)
                {
                    // Pago QR ya aprobado → usar cobranza pre-armada, sin modal
                    cobranzasConfirmadas = _parcelas_MP;
                    _pagoMPAprobado = false;
                    _parcelas_MP    = null;
                    cobroConfirmado = true;
                }
                else if (EsCuentaCorriente((cmbCondicionVenta.SelectedItem as ComboBoxItem)?.Content?.ToString()))
                {
                    // Cuenta corriente: no cobrar en caja ahora, solo registrar deuda
                    cobranzasConfirmadas = new List<CobranzaItem>();
                }
                else
                {
                    var cobroModal = new CobroModalWindow(win, total);
                    if (cobroModal.ShowDialog() != true)
                    {
                        btnGuardarFactura.IsEnabled = true;
                        return;
                    }
                    cobranzasConfirmadas = cobroModal.Cobranzas;
                    cobroConfirmado = cobranzasConfirmadas != null && cobranzasConfirmadas.Count > 0;
                }

                // ── PASO 2: AFIP (solo si el cobro fue confirmado) ──
                string cae = null;
                string vtoCae = null;
                int nroComprobante = 0;

                // Solo llamar AFIP si está configurado (tiene CUIT y certificado)
                if (tipoAfip > 0 && afipConfigurado)
                {
                    CustomerScreenService.ActualizarMensajeQR("Facturando AFIP...", Brushes.Orange);
                    string cuitLimpio = _clienteSeleccionado["CUIT"].ToString().Replace("-", "").Trim();
                    long cuitCliente = 0;
                    long.TryParse(cuitLimpio, out cuitCliente);
                    var resultadoAfip = await AfipService.FacturarAsync(tipoAfip, puntoVentaConfig, (double)total, cuitCliente, CarritoDeVenta.ToList(),
                        _clienteSeleccionado["CondicionIVA"]?.ToString());
                    if (resultadoAfip.Exito)
                    {
                        cae = resultadoAfip.CAE;
                        vtoCae = resultadoAfip.Vencimiento;
                        nroComprobante = resultadoAfip.NumeroComprobante;
                    }
                    else
                    {
                        CustomMessageBox.Show(
                            "❌ AFIP rechazó la factura electrónica.\n\n" +
                            "Detalle: " + resultadoAfip.Error + "\n\n" +
                            "⚠️ IMPORTANTE: el cobro fue confirmado pero la venta NO quedó registrada.\n\n" +
                            "Opciones:\n" +
                            "• Intentar de nuevo (el cobro ya fue recibido, NO vuelva a cobrar).\n" +
                            "• Cambiar el tipo a 'Ticket' para guardar sin código AFIP.",
                            "Error AFIP — venta no registrada");
                        btnGuardarFactura.IsEnabled = true;
                        return;
                    }
                }

                string condicionTicket = string.Join(" + ", cobranzasConfirmadas.Select(c => $"{c.nombreMedio} {c.monto:C2}"));
                string condVent = (cmbCondicionVenta.SelectedItem as ComboBoxItem)?.Content?.ToString() ?? "Contado";
                int cliID = Convert.ToInt32(_clienteSeleccionado["ClienteID"]);
                int? listaId = cmbListaPrecios.SelectedItem is DataRowView lr ? (int?)Convert.ToInt32(lr["ListaID"]) : null;

                var parcelas = cobranzasConfirmadas.ConvertAll(ci => new FacturaCobranzaParcela
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
                    if (CustomMessageBox.Show($"{msgExito}\n¿Imprimir comprobante?", "Éxito", MessageBoxButton.YesNo) == MessageBoxResult.Yes)
                        PrintService.ImprimirFactura(fid);
                    await Task.Delay(2000);
                    LimpiarFormulario();
                }
                else
                {
                    string detalleSql = !string.IsNullOrEmpty(DatabaseService.UltimoError)
                        ? "\n\nError técnico: " + DatabaseService.UltimoError
                        : "";

                    if (!string.IsNullOrEmpty(cae))
                    {
                        // AFIP aprobó y cobro confirmado, pero el INSERT en BD falló → situación crítica
                        CustomMessageBox.Show(
                            "⛔ ERROR CRÍTICO: La venta no se guardó en el sistema.\n\n" +
                            "El cobro fue recibido y AFIP ya emitió el comprobante:\n" +
                            $"CAE: {cae}  |  Vto: {vtoCae}  |  Nro: {nroComprobante}\n\n" +
                            "⚠️ NO vuelva a cobrar al cliente.\n" +
                            "Anote el CAE y comuníquese con soporte." +
                            detalleSql,
                            "Error al guardar — cobro ya realizado",
                            MessageBoxButton.OK, MessageBoxImage.Error);
                    }
                    else
                    {
                        CustomMessageBox.Show(
                            "No se pudo guardar la factura.\n\n" +
                            "⚠️ IMPORTANTE: el cobro ya fue confirmado pero la venta NO quedó registrada.\n" +
                            "NO vuelva a cobrar al cliente. Intente guardar de nuevo." +
                            detalleSql,
                            "Error al guardar — cobro ya realizado",
                            MessageBoxButton.OK, MessageBoxImage.Error);
                    }
                }
            }
            catch (InvalidOperationException ex)
            {
                CustomMessageBox.Show(
                    ex.Message + (cobroConfirmado
                        ? "\n\n⚠️ El cobro ya fue confirmado. NO vuelva a cobrar al cliente.\nActualice el carrito e intente guardar de nuevo."
                        : ""),
                    "Error al guardar", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            catch (Exception ex)
            {
                string avisoCobro = cobroConfirmado
                    ? "\n\n⚠️ El cobro ya fue confirmado. NO vuelva a cobrar al cliente."
                    : "";
                CustomMessageBox.Show("ERROR: " + ex.Message + avisoCobro);
            }
            finally
            {
                btnGuardarFactura.IsEnabled = true;
            }
        }

        private void GuardarPresupuestoDesdePos()
        {
            GuardarDocumentoSinCobro(
                () => DatabaseService.GuardarPresupuesto(Convert.ToInt32(_clienteSeleccionado["ClienteID"]), CarritoDeVenta.Sum(x => x.Subtotal), CarritoDeVenta.ToList()),
                "Presupuesto", id => PrintService.ImprimirPresupuesto(id));
        }

        private void GuardarRemitoDesdePos()
        {
            GuardarDocumentoSinCobro(
                () => DatabaseService.GuardarRemito(Convert.ToInt32(_clienteSeleccionado["ClienteID"]), CarritoDeVenta.ToList()),
                "Remito", id => PrintService.ImprimirRemito(id));
        }

        private void GuardarPedidoDesdePos()
        {
            GuardarDocumentoSinCobro(
                () => DatabaseService.GuardarPedido(Convert.ToInt32(_clienteSeleccionado["ClienteID"]), CarritoDeVenta.Sum(x => x.Subtotal), CarritoDeVenta.ToList()),
                "Pedido", id => PrintService.ImprimirPedido(id));
        }

        private void GuardarNotaVentaDesdePos(string tipoCodigo, string tipoNombre)
        {
            decimal total = CarritoDeVenta.Sum(x => x.Subtotal);
            string descripcion = ConstruirDescripcionCarrito();
            GuardarDocumentoSinCobro(
                () => DatabaseService.GuardarNotaCreditoDebitoVenta(Convert.ToInt32(_clienteSeleccionado["ClienteID"]), tipoCodigo, total, descripcion),
                tipoNombre, id => PrintService.ImprimirNotaCreditoDebitoVenta(id));
        }

        private string ConstruirDescripcionCarrito()
        {
            var partes = CarritoDeVenta.Take(6).Select(i => $"{i.Cantidad} x {i.Descripcion}").ToList();
            string desc = string.Join("; ", partes);
            if (CarritoDeVenta.Count > 6) desc += $" (+{CarritoDeVenta.Count - 6} ítems más)";
            return desc;
        }

        private void GuardarDocumentoSinCobro(Func<int> guardar, string nombreDocumento, Action<int> imprimir)
        {
            btnGuardarFactura.IsEnabled = false;
            try
            {
                int id = guardar();
                if (id <= 0)
                {
                    CustomMessageBox.Show($"No se pudo guardar el {nombreDocumento.ToLower()}.", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                    return;
                }

                if (CustomMessageBox.Show(
                    $"{nombreDocumento} #{id:D8} guardado correctamente.\n¿Imprimir?",
                    $"{nombreDocumento} generado",
                    MessageBoxButton.YesNo,
                    MessageBoxImage.Question) == MessageBoxResult.Yes)
                {
                    imprimir(id);
                }

                LimpiarFormulario();
            }
            catch (Exception ex)
            {
                CustomMessageBox.Show($"Error al guardar {nombreDocumento.ToLower()}: " + ex.Message, "Error", MessageBoxButton.OK, MessageBoxImage.Error);
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
            ActualizarUiSegunTipoComprobante();
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

        private static bool AfipEstaConfigurado(DataRow config)
        {
            if (config == null) return false;
            string cuit = config["CUIT"]?.ToString().Replace("-", "").Trim() ?? "";
            string cert = config["CertificadoPath"]?.ToString().Trim() ?? "";
            return cuit.Length >= 10 && !string.IsNullOrEmpty(cert) && System.IO.File.Exists(cert);
        }

        private static bool EsCuentaCorriente(string condicionVenta)
        {
            if (string.IsNullOrWhiteSpace(condicionVenta)) return false;
            return condicionVenta.IndexOf("Cta", StringComparison.OrdinalIgnoreCase) >= 0
                || condicionVenta.IndexOf("Corriente", StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private void CargarClientePorDefecto()
        {
            _clienteSeleccionado = DatabaseService.BuscarCliente("00-00000000-0");
            if (_clienteSeleccionado == null)
            {
                // Si no existe, lo creamos y lo buscamos de nuevo
                DatabaseService.AsegurarConsumidorFinal();
                _clienteSeleccionado = DatabaseService.BuscarCliente("00-00000000-0");
            }
            if (_clienteSeleccionado != null)
            {
                lblClienteSeleccionado.Text = _clienteSeleccionado["RazonSocial"].ToString();
                txtBuscarCliente.Text = "";
            }
        }
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
        private void txtBuscarCliente_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (txtBuscarCliente.Text.Length < 2) { popupCliente.IsOpen = false; return; }
            try
            {
                DataTable dt = DatabaseService.BuscarClientesMultiples(txtBuscarCliente.Text);
                lstSugerenciasCliente.ItemsSource = dt.DefaultView;
                popupCliente.IsOpen = dt.Rows.Count > 0;
                AutocompleteListHelper.ReiniciarSeleccion(lstSugerenciasCliente);
            }
            catch { }
        }

        private void txtBuscarProducto_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (_ignorarPerdidaFoco) return;
            if (txtBuscarProducto.Text.Length < 1) { popupProducto.IsOpen = false; _productoSeleccionado = null; return; }
            try
            {
                DataTable dt = DatabaseService.BuscarProductosMultiples_ParaVenta(txtBuscarProducto.Text);
                lstSugerenciasProducto.ItemsSource = dt.DefaultView;
                popupProducto.IsOpen = dt.Rows.Count > 0;
                AutocompleteListHelper.ReiniciarSeleccion(lstSugerenciasProducto);
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

        private void SeleccionarProductoSugerencia(DataRowView row)
        {
            _productoSeleccionado = row.Row;
            _ignorarPerdidaFoco = true;
            txtBuscarProducto.Text = _productoSeleccionado["Descripcion"].ToString();
            _ignorarPerdidaFoco = false;
            popupProducto.IsOpen = false;
            txtCantidad.Focus();
            txtCantidad.SelectAll();
        }

        private void lstSugerenciasCliente_MouseUp(object sender, MouseButtonEventArgs e)
        {
            if (lstSugerenciasCliente.SelectedItem is DataRowView r) SeleccionarCliente(r);
        }

        private void lstSugerenciasProducto_MouseUp(object sender, MouseButtonEventArgs e)
        {
            if (lstSugerenciasProducto.SelectedItem is DataRowView r) SeleccionarProductoSugerencia(r);
        }

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
                if (sender == txtBuscarCliente && popupCliente.IsOpen && lstSugerenciasCliente.Items.Count > 0)
                {
                    AutocompleteListHelper.MoverSeleccion(lstSugerenciasCliente, 1);
                    e.Handled = true;
                }
                else if (sender == txtBuscarProducto && popupProducto.IsOpen && lstSugerenciasProducto.Items.Count > 0)
                {
                    AutocompleteListHelper.MoverSeleccion(lstSugerenciasProducto, 1);
                    e.Handled = true;
                }
            }
            else if (e.Key == Key.Up)
            {
                if (sender == txtBuscarCliente && popupCliente.IsOpen && lstSugerenciasCliente.Items.Count > 0)
                {
                    AutocompleteListHelper.MoverSeleccion(lstSugerenciasCliente, -1);
                    e.Handled = true;
                }
                else if (sender == txtBuscarProducto && popupProducto.IsOpen && lstSugerenciasProducto.Items.Count > 0)
                {
                    AutocompleteListHelper.MoverSeleccion(lstSugerenciasProducto, -1);
                    e.Handled = true;
                }
            }
            else if (e.Key == Key.Escape)
            {
                popupCliente.IsOpen = false;
                popupProducto.IsOpen = false;
                e.Handled = true;
            }
            else if (e.Key == Key.Enter)
            {
                if (sender == txtBuscarCliente && popupCliente.IsOpen && lstSugerenciasCliente.Items.Count > 0)
                {
                    if (lstSugerenciasCliente.SelectedItem is DataRowView cv)
                        SeleccionarCliente(cv);
                    else if (lstSugerenciasCliente.Items[0] is DataRowView cv0)
                        SeleccionarCliente(cv0);
                    e.Handled = true;
                }
                else if (sender == txtBuscarProducto)
                {
                    if (popupProducto.IsOpen && lstSugerenciasProducto.Items.Count > 0)
                    {
                        if (lstSugerenciasProducto.SelectedItem is DataRowView pv)
                            SeleccionarProductoSugerencia(pv);
                        else if (lstSugerenciasProducto.Items[0] is DataRowView pv0)
                            SeleccionarProductoSugerencia(pv0);
                        e.Handled = true;
                    }
                    else
                    {
                        e.Handled = true;
                        txtBuscarProductoEnterAgregarSiCorresponde();
                    }
                }
            }
        }

        private void lstSugerencias_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter)
            {
                if (sender == lstSugerenciasCliente && lstSugerenciasCliente.SelectedItem is DataRowView c)
                    SeleccionarCliente(c);
                else if (sender == lstSugerenciasProducto && lstSugerenciasProducto.SelectedItem is DataRowView p)
                    SeleccionarProductoSugerencia(p);
                e.Handled = true;
            }
            else if (e.Key == Key.Escape)
            {
                popupCliente.IsOpen = false;
                popupProducto.IsOpen = false;
                if (sender == lstSugerenciasCliente) txtBuscarCliente.Focus();
                else txtBuscarProducto.Focus();
                e.Handled = true;
            }
        }
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
            if (string.IsNullOrEmpty(texto)) { CustomMessageBox.Show("Escriba el nombre o razón social del cliente."); return; }
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
                var win = new InputWindow("Descuento", "Porcentaje de descuento:", item.DescuentoPorcentaje.ToString("N0"));
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
                var win = new InputWindow("Recargo", "Porcentaje de recargo:", item.RecargoPorcentaje.ToString("N0"));
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
                var winCant = new InputWindow("Editar cantidad", "Nueva cantidad:", item.Cantidad.ToString());
                if (winCant.ShowDialog() == true && int.TryParse(winCant.ResponseText, out int cant) && cant > 0) { item.Cantidad = cant; ActualizarTotal(); return; }
                var winPrecio = new InputWindow("Editar precio", "Nuevo precio unitario:", item.PrecioUnitario.ToString("N2"));
                if (winPrecio.ShowDialog() == true && decimal.TryParse(winPrecio.ResponseText?.Replace(",", "."), System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out decimal prec) && prec >= 0) { item.PrecioUnitario = prec; ActualizarTotal(); }
            }
        }

        // --- AYUDA ATAJOS ---
        private void btnAyudaAtajos_Click(object sender, RoutedEventArgs e) { new AyudaAtajosWindow().ShowDialog(); }
    }
}