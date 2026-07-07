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
        public class PosProductoVm
        {
            public DataRow Row { get; set; }
            public int ProductoID { get; set; }
            public string Codigo { get; set; }
            public string CodigoBarra { get; set; }
            public string Descripcion { get; set; }
            public decimal PrecioLista { get; set; }
            public int StockActual { get; set; }
            public bool SinStock { get; set; }
            public string StockTexto { get; set; }
            public string ImagenPath { get; set; }
        }

        private ObservableCollection<FacturaItem> CarritoDeVenta;
        private ObservableCollection<PosProductoVm> CatalogoProductos;
        private List<PosProductoVm> _catalogoCompleto = new List<PosProductoVm>();
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
            CatalogoProductos = new ObservableCollection<PosProductoVm>();
            dgvFactura.ItemsSource = CarritoDeVenta;
            icCardsFactura.ItemsSource = CarritoDeVenta;
            icCarrito.ItemsSource = CarritoDeVenta;
            dgvCatalogo.ItemsSource = CatalogoProductos;
            icCatalogo.ItemsSource = CatalogoProductos;

            CustomerScreenService.OnClienteEligioPago += ProcesarPagoCliente;
            this.Unloaded += FacturacionControl_Unloaded;
        }

        private void FacturacionControl_Loaded(object sender, RoutedEventArgs e)
        {
            CargarListasPrecios();
            AplicarConfigPredeterminadaPos();
            CargarCatalogo();
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
            btnVistaCatalogoLista_Click(null, null);
        }

        private void CargarCatalogo()
        {
            _catalogoCompleto.Clear();
            var dt = DatabaseService.GetProductos("");
            if (dt != null)
            {
                decimal pctLista = ObtenerPorcentajeLista();
                foreach (DataRow r in dt.Rows)
                {
                    if (r["Codigo"]?.ToString() == "VAR") continue;
                    _catalogoCompleto.Add(CrearVmCatalogo(r, pctLista));
                }
            }
            FiltrarCatalogo();
        }

        private static PosProductoVm CrearVmCatalogo(DataRow r, decimal pctLista)
        {
            bool esStockeable = !r.Table.Columns.Contains("EsStockeable") || r["EsStockeable"] == DBNull.Value || Convert.ToBoolean(r["EsStockeable"]);
            int stock = r["StockActual"] != DBNull.Value ? Convert.ToInt32(r["StockActual"]) : 0;
            bool sinStock = esStockeable && stock <= 0;
            decimal precioBase = r["PrecioVenta"] != DBNull.Value ? Convert.ToDecimal(r["PrecioVenta"]) : 0;
            return new PosProductoVm
            {
                Row = r,
                ProductoID = Convert.ToInt32(r["ProductoID"]),
                Codigo = r["Codigo"]?.ToString() ?? "",
                CodigoBarra = r.Table.Columns.Contains("CodigoBarra") ? r["CodigoBarra"]?.ToString() ?? "" : "",
                Descripcion = r["Descripcion"]?.ToString() ?? "",
                PrecioLista = Math.Round(precioBase * (1 + pctLista / 100m), 2),
                StockActual = stock,
                SinStock = sinStock,
                StockTexto = esStockeable ? (sinStock ? "Sin stock" : stock.ToString()) : "—",
                ImagenPath = r.Table.Columns.Contains("ImagenPath") ? r["ImagenPath"]?.ToString() : null
            };
        }

        private void RefrescarPreciosCatalogo()
        {
            decimal pctLista = ObtenerPorcentajeLista();
            foreach (var p in _catalogoCompleto)
            {
                decimal precioBase = p.Row["PrecioVenta"] != DBNull.Value ? Convert.ToDecimal(p.Row["PrecioVenta"]) : 0;
                p.PrecioLista = Math.Round(precioBase * (1 + pctLista / 100m), 2);
            }
            dgvCatalogo.Items.Refresh();
            icCatalogo.Items.Refresh();
        }

        private void FiltrarCatalogo()
        {
            string q = (txtBuscarProducto?.Text ?? "").Trim().ToUpperInvariant();
            CatalogoProductos.Clear();
            foreach (var p in _catalogoCompleto)
            {
                if (string.IsNullOrEmpty(q)
                    || (p.Codigo ?? "").ToUpperInvariant().Contains(q)
                    || (p.Descripcion ?? "").ToUpperInvariant().Contains(q)
                    || (p.CodigoBarra ?? "").ToUpperInvariant().Contains(q))
                    CatalogoProductos.Add(p);
            }
            if (lblCantidadCatalogo != null)
                lblCantidadCatalogo.Text = $"{CatalogoProductos.Count} producto(s) mostrados";
        }

        private void AgregarProductoAlCarritoDesdeRow(DataRow row)
        {
            if (row == null) return;
            _productoSeleccionado = row;
            AgregarProductoSeleccionadoAlCarrito();
        }

        private void AgregarProductoAlCarritoDesdeVm(PosProductoVm vm)
        {
            if (vm?.Row == null) return;
            AgregarProductoAlCarritoDesdeRow(vm.Row);
        }

        private void CatalogoCard_Click(object sender, MouseButtonEventArgs e)
        {
            if ((sender as FrameworkElement)?.DataContext is PosProductoVm vm)
                AgregarProductoAlCarritoDesdeVm(vm);
        }

        private void dgvCatalogo_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        {
            if (dgvCatalogo.SelectedItem is PosProductoVm vm)
                AgregarProductoAlCarritoDesdeVm(vm);
        }

        private void btnVistaCatalogoLista_Click(object sender, RoutedEventArgs e)
        {
            if (dgvCatalogo == null) return;
            dgvCatalogo.Visibility = Visibility.Visible;
            if (svCatalogoCards != null) svCatalogoCards.Visibility = Visibility.Collapsed;
            if (btnVistaCatalogoLista != null) btnVistaCatalogoLista.Style = (Style)FindResource("ButtonStyle");
            if (btnVistaCatalogoCards != null) btnVistaCatalogoCards.Style = (Style)FindResource("SecondaryButtonStyle");
        }

        private void btnVistaCatalogoCards_Click(object sender, RoutedEventArgs e)
        {
            if (dgvCatalogo == null) return;
            dgvCatalogo.Visibility = Visibility.Collapsed;
            if (svCatalogoCards != null) svCatalogoCards.Visibility = Visibility.Visible;
            if (btnVistaCatalogoCards != null) btnVistaCatalogoCards.Style = (Style)FindResource("ButtonStyle");
            if (btnVistaCatalogoLista != null) btnVistaCatalogoLista.Style = (Style)FindResource("SecondaryButtonStyle");
        }

        private void btnDescuentoGlobal_Click(object sender, RoutedEventArgs e)
        {
            if (CarritoDeVenta.Count == 0)
            {
                CustomMessageBox.Show("No hay productos en el pedido.", "Aviso", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }
            var win = CrearInputModal("Descuento total", "Porcentaje de descuento para TODA la venta:", "0", soloNumeros: true);
            if (win.ShowDialog() == true
                && decimal.TryParse(win.ResponseText?.Replace(",", "."), NumberStyles.Any, CultureInfo.InvariantCulture, out decimal pct)
                && pct >= 0 && pct <= 100)
            {
                foreach (var item in CarritoDeVenta)
                    item.DescuentoPorcentaje = pct;
                ActualizarTotal();
            }
        }

        private void btnBorrarVenta_Click(object sender, RoutedEventArgs e)
        {
            if (CarritoDeVenta.Count == 0) return;
            if (CustomMessageBox.Show("¿Borrar todos los productos del pedido?", "Confirmar", MessageBoxButton.YesNo, MessageBoxImage.Question) == MessageBoxResult.Yes)
                LimpiarFormulario();
        }

        private void AplicarConfigPredeterminadaPos()
        {
            var cfg = DatabaseService.ObtenerConfigPosPredeterminada();
            if (expConfigVenta != null)
                expConfigVenta.IsExpanded = cfg.ConfigExpandida;
            if (!string.IsNullOrWhiteSpace(cfg.TipoComprobante))
                SeleccionarComboPorTexto(cmbTipoComprobante, cfg.TipoComprobante);
            if (!string.IsNullOrWhiteSpace(cfg.CondicionVenta))
                SeleccionarComboPorTexto(cmbCondicionVenta, cfg.CondicionVenta);
            if (cfg.ListaPrecioID.HasValue && cmbListaPrecios != null)
            {
                _cargandoListas = true;
                try { cmbListaPrecios.SelectedValue = cfg.ListaPrecioID.Value; }
                catch { }
                finally { _cargandoListas = false; }
            }
        }

        private static void SeleccionarComboPorTexto(ComboBox cb, string texto)
        {
            if (cb == null || string.IsNullOrWhiteSpace(texto)) return;
            for (int i = 0; i < cb.Items.Count; i++)
            {
                if ((cb.Items[i] as ComboBoxItem)?.Content?.ToString() == texto)
                {
                    cb.SelectedIndex = i;
                    return;
                }
            }
        }

        private PosConfigPredeterminada RecolectarConfigPosActual()
        {
            int? listaId = null;
            if (cmbListaPrecios?.SelectedValue != null && cmbListaPrecios.SelectedValue != DBNull.Value)
            {
                try { listaId = Convert.ToInt32(cmbListaPrecios.SelectedValue); }
                catch { }
            }
            else if (cmbListaPrecios?.SelectedItem is DataRowView rv)
                listaId = Convert.ToInt32(rv["ListaID"]);

            return new PosConfigPredeterminada
            {
                ListaPrecioID = listaId,
                TipoComprobante = (cmbTipoComprobante?.SelectedItem as ComboBoxItem)?.Content?.ToString(),
                CondicionVenta = (cmbCondicionVenta?.SelectedItem as ComboBoxItem)?.Content?.ToString(),
                ConfigExpandida = expConfigVenta?.IsExpanded != false
            };
        }

        private void btnGuardarConfigPredeterminada_Click(object sender, RoutedEventArgs e)
        {
            if (DatabaseService.GuardarConfigPosPredeterminada(RecolectarConfigPosActual()))
                CustomMessageBox.Show("Configuración del POS guardada como predeterminada.\nSe aplicará en cada nueva venta.", "Guardado", MessageBoxButton.OK, MessageBoxImage.Information);
            else
                CustomMessageBox.Show("No se pudo guardar la configuración.", "Error", MessageBoxButton.OK, MessageBoxImage.Warning);
        }
        private void btnCarritoSumar_Click(object sender, RoutedEventArgs e)
        {
            if ((sender as Button)?.Tag is FacturaItem item)
            {
                item.Cantidad++;
                ActualizarTotal();
            }
        }

        private void btnCarritoRestar_Click(object sender, RoutedEventArgs e)
        {
            if ((sender as Button)?.Tag is FacturaItem item)
            {
                if (item.Cantidad > 1) item.Cantidad--;
                else CarritoDeVenta.Remove(item);
                ActualizarTotal();
            }
        }

        private void RefrescarVistaCarrito()
        {
            dgvFactura.Items.Refresh();
            icCardsFactura.Items.Refresh();
            icCarrito.Items.Refresh();
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
                decimal alicuota = DatabaseService.ObtenerAlicuotaIvaVentaProducto(_productoSeleccionado);
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
            RefrescarVistaCarrito();
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
            if (lblCantidadItems != null)
            {
                int unidades = CarritoDeVenta.Sum(x => x.Cantidad);
                lblCantidadItems.Text = CarritoDeVenta.Count == 0
                    ? "0 productos en el pedido"
                    : $"{CarritoDeVenta.Count} producto(s) · {unidades} unidad(es)";
            }
            CustomerScreenService.Actualizar(CarritoDeVenta.ToList(), totalConDescRec);

            RefrescarVistaCarrito();
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
            var item = ObtenerItemCarrito(sender);
            if (item != null) { CarritoDeVenta.Remove(item); ActualizarTotal(); }
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
                RefrescarVistaCarrito();
                ActualizarTotal();
                LimpiarProducto();
            }
        }

        private void CargarListasPrecios()
        {
            try
            {
                _cargandoListas = true;
                cmbListaPrecios.ItemsSource = DatabaseService.GetListasPrecios().DefaultView;
                if (cmbListaPrecios.Items.Count > 0 && cmbListaPrecios.SelectedIndex < 0)
                    cmbListaPrecios.SelectedIndex = 0;
            }
            catch { }
            finally { _cargandoListas = false; }
        }

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
                    item.AlicuotaIvaPct = DatabaseService.ObtenerAlicuotaIvaVentaProducto(prod);
                }
            }
            RefrescarPreciosCatalogo();
            RefrescarVistaCarrito();
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
            FiltrarCatalogo();
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
            var item = ObtenerItemCarrito(sender);
            if (item == null) return;
            var win = CrearInputModal("Descuento del ítem", "Porcentaje de descuento solo para este producto:", item.DescuentoPorcentaje.ToString("N0"), soloNumeros: true);
            if (win.ShowDialog() == true && decimal.TryParse(win.ResponseText?.Replace(",", "."), NumberStyles.Any, CultureInfo.InvariantCulture, out decimal pct) && pct >= 0 && pct <= 100)
            {
                item.DescuentoPorcentaje = pct;
                ActualizarTotal();
            }
        }
        private void btnRecargoItem_Click(object sender, RoutedEventArgs e)
        {
            var item = ObtenerItemCarrito(sender);
            if (item == null) return;
            var win = CrearInputModal("Recargo del ítem", "Porcentaje de recargo solo para este producto:", item.RecargoPorcentaje.ToString("N0"), soloNumeros: true);
            if (win.ShowDialog() == true && decimal.TryParse(win.ResponseText?.Replace(",", "."), NumberStyles.Any, CultureInfo.InvariantCulture, out decimal pct) && pct >= 0 && pct <= 100)
            {
                item.RecargoPorcentaje = pct;
                ActualizarTotal();
            }
        }
        private void btnEditarItem_Click(object sender, RoutedEventArgs e)
        {
            var item = ObtenerItemCarrito(sender);
            if (item == null) return;
            var winPrecio = CrearInputModal("Editar precio", "Nuevo precio unitario de este producto:", item.PrecioUnitario.ToString("N2"), soloNumeros: true);
            if (winPrecio.ShowDialog() == true && decimal.TryParse(winPrecio.ResponseText?.Replace(",", "."), NumberStyles.Any, CultureInfo.InvariantCulture, out decimal prec) && prec >= 0)
            {
                item.PrecioUnitario = prec;
                ActualizarTotal();
            }
        }

        // --- AYUDA ATAJOS ---
        private void btnAyudaAtajos_Click(object sender, RoutedEventArgs e) { new AyudaAtajosWindow().ShowDialog(); }

        private static FacturaItem ObtenerItemCarrito(object sender)
        {
            if (!(sender is Button btn)) return null;
            if (btn.Tag is FacturaItem desdeTag) return desdeTag;
            if (btn.DataContext is FacturaItem desdeCtx) return desdeCtx;
            return null;
        }

        private ModernInputWindow CrearInputModal(string titulo, string etiqueta, string valorInicial, bool soloNumeros = false)
        {
            return new ModernInputWindow(titulo, etiqueta, valorInicial)
            {
                Owner = Window.GetWindow(this),
                SoloNumeros = soloNumeros
            };
        }
    }
}