using SchettiniGestion;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
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
            public decimal StockActual { get; set; }
            public bool SinStock { get; set; }
            public string StockTexto { get; set; }
            public string ImagenPath { get; set; }
            public bool EnPromo { get; set; }
            public string PromoTooltip { get; set; }
        }

        private ObservableCollection<FacturaItem> CarritoDeVenta;
        private readonly ObservableCollection<FacturaItem> _carritoVisible = new ObservableCollection<FacturaItem>();
        private bool _suspendCarritoSync;
        private ObservableCollection<PosProductoVm> CatalogoProductos;
        private List<PosProductoVm> _catalogoCompleto = new List<PosProductoVm>();
        private enum ModoBusquedaPos { Todo, CodigoBarras, Descripcion }
        private ModoBusquedaPos _modoBusqueda = ModoBusquedaPos.Todo;
        private DataRow _clienteSeleccionado;
        private DataRow _productoSeleccionado;
        private bool _ignorarPerdidaFoco = false;
        private bool _cargandoListas = false;
        private DispatcherTimer _timerVerificacionMP;
        private string _referenciaPagoMP = "";
        private string _ordenIdMP = "";
        private bool _esperandoPagoMP = false;
        // Pago Mercado Pago QR aprobado → cobranza pre-armada para saltear CobroModalWindow
        private bool _pagoMPAprobado = false;
        private List<CobranzaItem> _parcelas_MP = null;
        private bool _pagoPointAprobado = false;
        private List<CobranzaItem> _parcelasPoint = null;
        private int _ultimoDocumentoId;
        private string _ultimoTipoDocumento = "Factura";
        private FacturaItem _itemCarritoSeleccionado;
        private DateTime _ultimoEnterProductoUtc = DateTime.MinValue;
        private bool _guardandoVenta;
        private DispatcherTimer _timerLectorBarras;
        private string _textoPendienteLector;

        /// <summary>Si se asigna (ej. "Remito", "Pedido"), preselecciona ese tipo al cargar. Usado cuando se abre desde Nuevo Remito/Pedido.</summary>
        public string TipoComprobanteInicial { get; set; }

        public FacturacionControl()
        {
            InitializeComponent();
            CarritoDeVenta = new ObservableCollection<FacturaItem>();
            CarritoDeVenta.CollectionChanged += CarritoDeVenta_CollectionChanged;
            CatalogoProductos = new ObservableCollection<PosProductoVm>();
            dgvFactura.ItemsSource = CarritoDeVenta;
            icCardsFactura.ItemsSource = CarritoDeVenta;
            icCarrito.ItemsSource = _carritoVisible;
            dgvCatalogo.ItemsSource = CatalogoProductos;
            icCatalogo.ItemsSource = CatalogoProductos;

            CustomerScreenService.OnClienteEligioPago += ProcesarPagoCliente;
            this.Unloaded += FacturacionControl_Unloaded;
            this.IsVisibleChanged += FacturacionControl_IsVisibleChanged;
            this.PreviewKeyDown += FacturacionControl_PreviewKeyDown;
            Focusable = true;
        }

        private void FacturacionControl_IsVisibleChanged(object sender, DependencyPropertyChangedEventArgs e)
        {
            if (IsVisible)
            {
                ActualizarBloqueoAperturaCaja();
                ActualizarPromosPos();
            }
        }

        private void ActualizarBloqueoAperturaCaja()
        {
            if (bdrBloqueoApertura == null) return;
            bool bloqueado = !DatabaseService.PuedeRegistrarVentasPos();
            bdrBloqueoApertura.Visibility = bloqueado ? Visibility.Visible : Visibility.Collapsed;
            if (bloqueado && txtBloqueoApertura != null)
                txtBloqueoApertura.Text = DatabaseService.MensajeBloqueoVentasPos();
        }

        private bool ValidarPuedeVenderEnPos()
        {
            if (DatabaseService.PuedeRegistrarVentasPos())
                return true;
            CustomMessageBox.Show(
                DatabaseService.MensajeBloqueoVentasPos(),
                "Caja no disponible",
                MessageBoxButton.OK,
                MessageBoxImage.Warning);
            ActualizarBloqueoAperturaCaja();
            return false;
        }

        private void FacturacionControl_Loaded(object sender, RoutedEventArgs e)
        {
            CargarListasPrecios();
            AplicarConfigPredeterminadaPos();
            CargarCatalogo();
            CargarClientePorDefecto();
            SincronizarCarritoVisible();
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
            AplicarLayoutResponsivo();
            ActualizarBloqueoAperturaCaja();
            ActualizarIndicadorModoBusqueda();
        }

        private void FacturacionControl_SizeChanged(object sender, SizeChangedEventArgs e)
        {
            AplicarLayoutResponsivo();
        }

        private void AplicarLayoutResponsivo()
        {
            if (!IsLoaded || bdrResumenVenta == null)
                return;

            double ancho = ActualWidth;
            double altoPanel = bdrResumenVenta.ActualHeight;
            bool compacto = UiScaleHelper.IsCompactWidth(ancho) || UiScaleHelper.IsCompactHeight(altoPanel);
            bool muyCompacto = UiScaleHelper.IsVeryCompactWidth(ancho) || altoPanel < 520;

            if (gridRoot != null)
                gridRoot.Margin = UiScaleHelper.ContentMargin(compacto);

            if (txtAyudaResumen != null)
                txtAyudaResumen.Visibility = muyCompacto ? Visibility.Collapsed : Visibility.Visible;

            if (viewboxTotal != null)
                viewboxTotal.MaxHeight = muyCompacto ? 30 : (compacto ? 36 : 44);

            if (btnAyudaAtajos != null)
                btnAyudaAtajos.Visibility = muyCompacto ? Visibility.Collapsed : Visibility.Visible;
        }

        private void CargarCatalogo()
        {
            _catalogoCompleto.Clear();
            var dt = DatabaseService.GetProductos("");
            if (dt != null)
            {
                int? listaId = ObtenerListaIdSeleccionada();
                foreach (DataRow r in dt.Rows)
                {
                    if (r["Codigo"]?.ToString() == "VAR") continue;
                    _catalogoCompleto.Add(CrearVmCatalogo(r, listaId));
                }
            }
            ActualizarPromosPos();
            FiltrarCatalogo();
        }

        /// <summary>Banner «Promos activas hoy» + badge EN PROMO en el catálogo.</summary>
        private void ActualizarPromosPos()
        {
            var promos = DatabaseService.GetPromocionesVigentesHoy();
            if (bdrPromosHoy != null && txtPromosHoy != null)
            {
                if (promos == null || promos.Count == 0)
                {
                    bdrPromosHoy.Visibility = Visibility.Collapsed;
                    txtPromosHoy.Text = "";
                }
                else
                {
                    var partes = new List<string>();
                    foreach (var p in promos)
                    {
                        string alcance = string.IsNullOrWhiteSpace(p.AlcanceTexto) ? "" : " (" + p.AlcanceTexto + ")";
                        partes.Add($"{p.Nombre} -{p.Porcentaje:0.##}%{alcance}");
                    }
                    txtPromosHoy.Text = string.Join(" · ", partes);
                    bdrPromosHoy.Visibility = Visibility.Visible;
                }
            }

            if (_catalogoCompleto == null || _catalogoCompleto.Count == 0)
                return;

            foreach (var p in _catalogoCompleto)
            {
                string cat = p.Row != null && p.Row.Table.Columns.Contains("Categoria")
                    ? (p.Row["Categoria"]?.ToString() ?? "")
                    : "";
                var promo = DatabaseService.ResolverMejorPromo(promos, p.ProductoID, cat);
                p.EnPromo = promo != null && promo.Porcentaje > 0;
                p.PromoTooltip = p.EnPromo
                    ? $"🎯 {promo.Nombre} · -{promo.Porcentaje:0.##}%"
                    : null;
            }

            dgvCatalogo?.Items.Refresh();
            icCatalogo?.Items.Refresh();
        }

        private int? ObtenerListaIdSeleccionada()
        {
            if (cmbListaPrecios?.SelectedValue != null && cmbListaPrecios.SelectedValue != DBNull.Value)
            {
                try { return Convert.ToInt32(cmbListaPrecios.SelectedValue); }
                catch { }
            }
            if (cmbListaPrecios?.SelectedItem is DataRowView rv)
                return Convert.ToInt32(rv["ListaID"]);
            return null;
        }

        private static PosProductoVm CrearVmCatalogo(DataRow r, int? listaId)
        {
            bool esStockeable = !r.Table.Columns.Contains("EsStockeable") || r["EsStockeable"] == DBNull.Value || Convert.ToBoolean(r["EsStockeable"]);
            var politica = DatabaseService.ProductoStockPolitica.DesdeFila(r);
            decimal stock = r["StockActual"] != DBNull.Value ? Convert.ToDecimal(r["StockActual"]) : 0m;
            bool sinStock = politica.ExigeStockSuficiente && stock <= 0m;
            int productoId = Convert.ToInt32(r["ProductoID"]);
            decimal precioLista = listaId.HasValue
                ? DatabaseService.CalcularPrecioListaPorIds(productoId, listaId.Value)
                : (r["PrecioVenta"] != DBNull.Value ? Convert.ToDecimal(r["PrecioVenta"]) : 0m);
            return new PosProductoVm
            {
                Row = r,
                ProductoID = productoId,
                Codigo = r["Codigo"]?.ToString() ?? "",
                CodigoBarra = r.Table.Columns.Contains("CodigoBarra") ? r["CodigoBarra"]?.ToString() ?? "" : "",
                Descripcion = r["Descripcion"]?.ToString() ?? "",
                PrecioLista = precioLista,
                StockActual = stock,
                SinStock = sinStock,
                StockTexto = esStockeable
                    ? (sinStock ? "Sin stock" : politica.AceptaStockNegativo && stock <= 0m ? "0 (permite neg.)" : FormatearStockCatalogo(stock))
                    : "—",
                ImagenPath = r.Table.Columns.Contains("ImagenPath") ? r["ImagenPath"]?.ToString() : null
            };
        }

        private static string FormatearStockCatalogo(decimal stock)
        {
            return stock.ToString("0.######", CultureInfo.CurrentCulture);
        }

        private void RefrescarPreciosCatalogo()
        {
            int? listaId = ObtenerListaIdSeleccionada();
            foreach (var p in _catalogoCompleto)
            {
                p.PrecioLista = listaId.HasValue
                    ? DatabaseService.CalcularPrecioListaPorIds(p.ProductoID, listaId.Value)
                    : (p.Row["PrecioVenta"] != DBNull.Value ? Convert.ToDecimal(p.Row["PrecioVenta"]) : 0m);
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
                bool coincide = string.IsNullOrEmpty(q);
                if (!coincide)
                {
                    switch (_modoBusqueda)
                    {
                        case ModoBusquedaPos.CodigoBarras:
                            coincide = (p.Codigo ?? "").ToUpperInvariant().Contains(q)
                                || (p.CodigoBarra ?? "").ToUpperInvariant().Contains(q);
                            break;
                        case ModoBusquedaPos.Descripcion:
                            coincide = (p.Descripcion ?? "").ToUpperInvariant().Contains(q);
                            break;
                        default:
                            coincide = (p.Codigo ?? "").ToUpperInvariant().Contains(q)
                                || (p.Descripcion ?? "").ToUpperInvariant().Contains(q)
                                || (p.CodigoBarra ?? "").ToUpperInvariant().Contains(q);
                            break;
                    }
                }
                if (coincide)
                    CatalogoProductos.Add(p);
            }
            if (lblCantidadCatalogo != null)
                lblCantidadCatalogo.Text = $"{CatalogoProductos.Count} producto(s) mostrados";
        }

        private void ActualizarIndicadorModoBusqueda()
        {
            if (lblModoBusqueda == null) return;
            switch (_modoBusqueda)
            {
                case ModoBusquedaPos.CodigoBarras:
                    lblModoBusqueda.Text = "Modo: código / barras (Alt+Q cambiar)";
                    break;
                case ModoBusquedaPos.Descripcion:
                    lblModoBusqueda.Text = "Modo: descripción (Alt+Q cambiar)";
                    break;
                default:
                    lblModoBusqueda.Text = "Modo: todo (Alt+Q cambiar)";
                    break;
            }
        }

        private void CiclarModoBusqueda()
        {
            _modoBusqueda = _modoBusqueda == ModoBusquedaPos.Todo
                ? ModoBusquedaPos.CodigoBarras
                : _modoBusqueda == ModoBusquedaPos.CodigoBarras
                    ? ModoBusquedaPos.Descripcion
                    : ModoBusquedaPos.Todo;
            ActualizarIndicadorModoBusqueda();
            FiltrarCatalogo();
            txtBuscarProducto?.Focus();
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

        private static bool ValidarStockCarrito(IEnumerable<FacturaItem> items, out string mensaje)
        {
            mensaje = null;
            if (items == null) return true;
            foreach (var it in items)
            {
                if (it == null) continue;
                if (string.Equals(it.Codigo, "VARIOS", StringComparison.OrdinalIgnoreCase)) continue;
                if (it.ProductoID <= 0) continue;
                if (!DatabaseService.ProductoExigeStockSuficiente(it.ProductoID)) continue;

                int disp = DatabaseService.GetStockActualProducto(it.ProductoID);
                if (it.Cantidad > disp)
                {
                    mensaje = $"Stock insuficiente: «{it.Descripcion}» (disponible {disp}, pedido {it.Cantidad}).\n\n" +
                              "Si el producto admite stock negativo, activá «Acepta stock negativo» en su ficha.";
                    return false;
                }
            }
            return true;
        }

        private bool ValidarStockCarrito(out string mensaje) => ValidarStockCarrito(CarritoDeVenta, out mensaje);

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
                expConfigVenta.IsExpanded = false;
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
                ConfigExpandida = false
            };
        }

        private void btnToggleConfigVenta_Click(object sender, RoutedEventArgs e)
        {
            if (expConfigVenta == null) return;
            expConfigVenta.IsExpanded = !expConfigVenta.IsExpanded;
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
                MarcarItemCarrito(item);
                item.Cantidad++;
                ActualizarTotal();
            }
        }

        private void btnCarritoRestar_Click(object sender, RoutedEventArgs e)
        {
            if ((sender as Button)?.Tag is FacturaItem item)
            {
                MarcarItemCarrito(item);
                if (item.Cantidad > 1) item.Cantidad--;
                else CarritoDeVenta.Remove(item);
                PurgeCarritoInvalido();
                ActualizarTotal();
            }
        }

        private void CarritoDeVenta_CollectionChanged(object sender, NotifyCollectionChangedEventArgs e)
        {
            if (!_suspendCarritoSync)
                SincronizarCarritoVisible();
        }

        private void SincronizarCarritoVisible()
        {
            _suspendCarritoSync = true;
            try
            {
                PurgeCarritoInvalido();
                var validos = CarritoDeVenta.Where(i => i != null && i.EsValido).ToList();

                for (int i = _carritoVisible.Count - 1; i >= 0; i--)
                {
                    if (!validos.Contains(_carritoVisible[i]))
                        _carritoVisible.RemoveAt(i);
                }

                foreach (var it in validos)
                {
                    if (!_carritoVisible.Contains(it))
                        _carritoVisible.Add(it);
                }
            }
            finally
            {
                _suspendCarritoSync = false;
            }
        }

        private void PurgeCarritoInvalido()
        {
            for (int i = CarritoDeVenta.Count - 1; i >= 0; i--)
            {
                var it = CarritoDeVenta[i];
                if (it == null || !it.EsValido)
                    CarritoDeVenta.RemoveAt(i);
            }
        }

        private void RefrescarVistaCarrito()
        {
            SincronizarCarritoVisible();
            dgvFactura.Items.Refresh();
            icCardsFactura.Items.Refresh();
        }

        private void ExpanderCarrito_Expanded(object sender, RoutedEventArgs e)
        {
            if (sender is Expander expandido)
            {
                foreach (var hijo in FindVisualChildren<Expander>(icCarrito))
                {
                    if (!ReferenceEquals(hijo, expandido))
                        hijo.IsExpanded = false;
                }
            }
        }

        private static System.Collections.Generic.IEnumerable<T> FindVisualChildren<T>(DependencyObject parent) where T : DependencyObject
        {
            if (parent == null) yield break;
            for (int i = 0; i < System.Windows.Media.VisualTreeHelper.GetChildrenCount(parent); i++)
            {
                var child = System.Windows.Media.VisualTreeHelper.GetChild(parent, i);
                if (child is T match)
                    yield return match;
                foreach (var nested in FindVisualChildren<T>(child))
                    yield return nested;
            }
        }

        private void cmbTipoComprobante_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (cmbTipoComprobante == null) return;

            string tipo = ObtenerTipoComprobanteSeleccionado();
            if (tipo == "Factura" && !LicenseManager.TieneAfip())
            {
                for (int i = 0; i < cmbTipoComprobante.Items.Count; i++)
                {
                    if ((cmbTipoComprobante.Items[i] as ComboBoxItem)?.Content?.ToString() == "Ticket")
                    {
                        cmbTipoComprobante.SelectedIndex = i;
                        CustomMessageBox.Show(
                            "La factura electrónica ARCA no está incluida en su licencia.\n\n" +
                            "Solicite el extra «ARCA / Factura electrónica» o use «Ticket» para comprobante interno.",
                            "Extra no habilitado", MessageBoxButton.OK, MessageBoxImage.Information);
                        break;
                    }
                }
            }

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
            this.PreviewKeyDown -= FacturacionControl_PreviewKeyDown;
            CancelarModoQR();
        }

        private void Ventana_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            if (!IsVisible || !EnPestañaPos()) return;
            if (e.Key == Key.F1)
            {
                MostrarAyudaAtajos();
                e.Handled = true;
            }
        }

        private void FacturacionControl_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            if (!IsVisible || !EnPestañaPos()) return;

            Key tecla = ObtenerTecla(e);

            if (tecla == Key.F1)
            {
                MostrarAyudaAtajos();
                e.Handled = true;
                return;
            }

            if (tecla == Key.Escape)
            {
                if ((e.KeyboardDevice.Modifiers & ModifierKeys.Shift) == ModifierKeys.Shift)
                {
                    if (ProcesarEscape())
                    {
                        e.Handled = true;
                        return;
                    }
                    txtBuscarProducto?.Focus();
                    e.Handled = true;
                    return;
                }
                if (ProcesarEscape())
                    e.Handled = true;
                return;
            }

            if (tecla == Key.Delete && CarritoDeVenta.Count > 0)
            {
                EliminarItemCarritoSeleccionado();
                e.Handled = true;
                return;
            }

            if (!EsSoloAlt(e)) return;

            switch (tecla)
            {
                case Key.E:
                    txtBuscarProducto?.Focus();
                    txtBuscarProducto?.SelectAll();
                    e.Handled = true;
                    break;
                case Key.C:
                    txtBuscarCliente?.Focus();
                    txtBuscarCliente?.SelectAll();
                    e.Handled = true;
                    break;
                case Key.F:
                    if (txtBuscarProducto != null)
                    {
                        txtBuscarProducto.Text = "";
                        _productoSeleccionado = null;
                        popupProducto.IsOpen = false;
                        txtBuscarProducto.Focus();
                    }
                    e.Handled = true;
                    break;
                case Key.O:
                    btnToggleConfigVenta_Click(null, null);
                    e.Handled = true;
                    break;
                case Key.L:
                    if (CarritoDeVenta.Count == 0 || CustomMessageBox.Show(
                        "¿Limpiar la venta actual y empezar un comprobante nuevo?",
                        "Nueva venta", MessageBoxButton.YesNo, MessageBoxImage.Question) == MessageBoxResult.Yes)
                        LimpiarFormulario();
                    e.Handled = true;
                    break;
                case Key.P:
                    ImprimirUltimoComprobante();
                    e.Handled = true;
                    break;
                case Key.V:
                    if (btnGuardarFactura?.IsEnabled == true)
                        btnGuardarFactura_Click(btnGuardarFactura, new RoutedEventArgs());
                    e.Handled = true;
                    break;
                case Key.D:
                    btnDescuentoGlobal_Click(null, null);
                    e.Handled = true;
                    break;
                case Key.Q:
                    CiclarModoBusqueda();
                    e.Handled = true;
                    break;
            }
        }

        private bool EnPestañaPos()
        {
            return tabFacturacion == null || tabFacturacion.SelectedIndex == 0;
        }

        private static Key ObtenerTecla(KeyEventArgs e) => e.Key == Key.System ? e.SystemKey : e.Key;

        private static bool EsSoloAlt(KeyEventArgs e)
        {
            ModifierKeys m = e.KeyboardDevice.Modifiers;
            return (m & ModifierKeys.Alt) == ModifierKeys.Alt
                && (m & ModifierKeys.Control) == 0
                && (m & ModifierKeys.Shift) == 0;
        }

        private void MostrarAyudaAtajos()
        {
            var win = new AyudaAtajosWindow { Owner = Window.GetWindow(this) };
            win.ShowDialog();
        }

        private bool ProcesarEscape()
        {
            if (popupCliente.IsOpen)
            {
                popupCliente.IsOpen = false;
                txtBuscarCliente?.Focus();
                return true;
            }
            if (popupProducto.IsOpen)
            {
                popupProducto.IsOpen = false;
                txtBuscarProducto?.Focus();
                return true;
            }
            if (expConfigVenta?.IsExpanded == true)
            {
                expConfigVenta.IsExpanded = false;
                return true;
            }
            if (txtBuscarProducto != null && !string.IsNullOrWhiteSpace(txtBuscarProducto.Text))
            {
                LimpiarProducto();
                return true;
            }
            if (txtBuscarCliente != null && !string.IsNullOrWhiteSpace(txtBuscarCliente.Text))
            {
                CargarClientePorDefecto();
                txtBuscarCliente.Text = "";
                popupCliente.IsOpen = false;
                txtBuscarProducto?.Focus();
                return true;
            }
            return false;
        }

        private void RegistrarUltimoComprobante(int id, string tipo)
        {
            if (id <= 0) return;
            _ultimoDocumentoId = id;
            _ultimoTipoDocumento = tipo ?? "Factura";
        }

        private void ImprimirUltimoComprobante()
        {
            if (_ultimoDocumentoId <= 0)
            {
                CustomMessageBox.Show("No hay un comprobante reciente para imprimir.", "Imprimir", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }
            try
            {
                switch (_ultimoTipoDocumento)
                {
                    case "Presupuesto": PrintService.ImprimirPresupuesto(_ultimoDocumentoId); break;
                    case "Remito": PrintService.ImprimirRemito(_ultimoDocumentoId); break;
                    case "Pedido": PrintService.ImprimirPedido(_ultimoDocumentoId); break;
                    case "Nota de Crédito":
                    case "Nota de Débito":
                        PrintService.ImprimirNotaCreditoDebitoVenta(_ultimoDocumentoId); break;
                    default: PrintService.ImprimirFactura(_ultimoDocumentoId); break;
                }
            }
            catch (Exception ex)
            {
                CustomMessageBox.Show("Error al imprimir: " + ex.Message, "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void MarcarItemCarrito(FacturaItem item)
        {
            if (item != null && item.EsValido)
                _itemCarritoSeleccionado = item;
        }

        private void EliminarItemCarritoSeleccionado()
        {
            FacturaItem item = _itemCarritoSeleccionado;
            if (item == null || !CarritoDeVenta.Contains(item))
                item = CarritoDeVenta.LastOrDefault(i => i.EsValido);
            if (item == null) return;
            CarritoDeVenta.Remove(item);
            _itemCarritoSeleccionado = null;
            PurgeCarritoInvalido();
            ActualizarTotal();
        }

        private void RegistrarDobleEnterProducto()
        {
            if (CarritoDeVenta.Count == 0 || btnGuardarFactura?.IsEnabled != true) return;
            var ahora = DateTime.UtcNow;
            if ((ahora - _ultimoEnterProductoUtc).TotalMilliseconds < 700)
            {
                _ultimoEnterProductoUtc = DateTime.MinValue;
                btnGuardarFactura_Click(btnGuardarFactura, new RoutedEventArgs());
            }
            else
            {
                _ultimoEnterProductoUtc = ahora;
            }
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
            if (!ValidarPuedeVenderEnPos()) return;
            if (_productoSeleccionado == null) return;

            int id = Convert.ToInt32(_productoSeleccionado["ProductoID"]);
            int cant = 1;
            int.TryParse(txtCantidad.Text, out cant);
            if (cant < 1) cant = 1;

            int? listaId = ObtenerListaIdSeleccionada();
            decimal precioFinal = listaId.HasValue
                ? DatabaseService.CalcularPrecioListaPorIds(id, listaId.Value)
                : Convert.ToDecimal(_productoSeleccionado["PrecioVenta"]);
            string imgPath = _productoSeleccionado.Table.Columns.Contains("ImagenPath") ? _productoSeleccionado["ImagenPath"].ToString() : null;

            string categoria = _productoSeleccionado.Table.Columns.Contains("Categoria")
                ? (_productoSeleccionado["Categoria"]?.ToString() ?? "")
                : "";
            var promo = DatabaseService.ObtenerPromoVigenteParaProducto(id, categoria);

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
                    DescuentoPorcentaje = promo != null && promo.Porcentaje > 0 ? promo.Porcentaje : 0,
                    PromoNombre = promo != null && promo.Porcentaje > 0 ? promo.Nombre : null,
                    AlicuotaIvaPct = alicuota,
                    ImagenPath = imgPath,
                    PermiteModificarPrecioVenta = LeerPermiteModificarPrecioVenta(_productoSeleccionado)
                });
            }
            RefrescarVistaCarrito();
            popupProducto.IsOpen = false;
            LimpiarProducto();
            ActualizarTotal();
            MarcarItemCarrito(CarritoDeVenta.LastOrDefault(i => i.EsValido));
            if (promo != null && promo.Porcentaje > 0 && item == null)
                MostrarAvisoPromo(promo.Nombre, promo.Porcentaje);
        }

        private DispatcherTimer _timerAvisoPromo;

        private void MostrarAvisoPromo(string nombrePromo, decimal porcentaje)
        {
            if (bdrAvisoPromo == null || txtAvisoPromo == null) return;
            string nombre = string.IsNullOrWhiteSpace(nombrePromo) ? "Promoción" : nombrePromo.Trim();
            txtAvisoPromo.Text = $"🎯 Se aplicó «{nombre}»: -{porcentaje:N0}% en este producto.";
            bdrAvisoPromo.Visibility = Visibility.Visible;

            if (_timerAvisoPromo == null)
            {
                _timerAvisoPromo = new DispatcherTimer { Interval = TimeSpan.FromSeconds(4) };
                _timerAvisoPromo.Tick += (_, __) =>
                {
                    _timerAvisoPromo.Stop();
                    if (bdrAvisoPromo != null)
                        bdrAvisoPromo.Visibility = Visibility.Collapsed;
                };
            }
            _timerAvisoPromo.Stop();
            _timerAvisoPromo.Start();
        }

        /// <summary>
        /// Enter / lector: código o barras exacto → suma al carrito.
        /// Una sola sugerencia o ítem elegido en el listado → también suma (no pide el "+").
        /// </summary>
        private void txtBuscarProductoEnterAgregarSiCorresponde()
        {
            CancelarTimerLectorBarras();
            string texto = (txtBuscarProducto.Text ?? "").Trim();
            if (string.IsNullOrEmpty(texto)) return;

            DataRow exacto = DatabaseService.BuscarProductoExactoCodigoOCodigoBarra(texto);
            if (exacto != null)
            {
                _productoSeleccionado = exacto;
                AgregarProductoSeleccionadoAlCarrito();
                return;
            }

            // Coincidencia exacta de código/barras dentro de las sugerencias visibles
            if (popupProducto.IsOpen && lstSugerenciasProducto.Items.Count > 0)
            {
                DataRow exactoEnLista = BuscarExactoEnSugerencias(texto);
                if (exactoEnLista != null)
                {
                    _productoSeleccionado = exactoEnLista;
                    AgregarProductoSeleccionadoAlCarrito();
                    return;
                }

                if (lstSugerenciasProducto.Items.Count == 1 && lstSugerenciasProducto.Items[0] is DataRowView drvUnico)
                {
                    _productoSeleccionado = drvUnico.Row;
                    AgregarProductoSeleccionadoAlCarrito();
                    return;
                }

                if (lstSugerenciasProducto.SelectedItem is DataRowView sel)
                {
                    _productoSeleccionado = sel.Row;
                    AgregarProductoSeleccionadoAlCarrito();
                    return;
                }

                // Búsqueda por descripción con varios resultados: dejar el primero listo, sin sumar a ciegas
                if (!PareceCodigoBarras(texto) && lstSugerenciasProducto.Items[0] is DataRowView first)
                {
                    SeleccionarProductoSugerencia(first);
                    return;
                }

                if (lstSugerenciasProducto.Items[0] is DataRowView firstBarra)
                {
                    _productoSeleccionado = firstBarra.Row;
                    AgregarProductoSeleccionadoAlCarrito();
                    return;
                }
            }

            if (_productoSeleccionado != null)
            {
                AgregarProductoSeleccionadoAlCarrito();
                return;
            }

            if (!popupProducto.IsOpen)
                AbrirVentanaVarios();
        }

        private DataRow BuscarExactoEnSugerencias(string texto)
        {
            if (lstSugerenciasProducto?.Items == null) return null;
            string q = texto.Trim();
            foreach (var item in lstSugerenciasProducto.Items)
            {
                if (item is DataRowView drv)
                {
                    string cod = drv.Row.Table.Columns.Contains("Codigo") ? drv.Row["Codigo"]?.ToString() ?? "" : "";
                    string bar = drv.Row.Table.Columns.Contains("CodigoBarra") ? drv.Row["CodigoBarra"]?.ToString() ?? "" : "";
                    if (string.Equals(cod, q, StringComparison.OrdinalIgnoreCase)
                        || string.Equals(bar, q, StringComparison.OrdinalIgnoreCase))
                        return drv.Row;
                }
            }
            return null;
        }

        private static bool PareceCodigoBarras(string texto)
        {
            if (string.IsNullOrWhiteSpace(texto) || texto.Length < 4) return false;
            foreach (char c in texto)
                if (!char.IsDigit(c)) return false;
            return true;
        }

        private void ProgramarAutoAgregarLector(string texto)
        {
            _textoPendienteLector = texto;
            if (_timerLectorBarras == null)
            {
                _timerLectorBarras = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(120) };
                _timerLectorBarras.Tick += (s, e) =>
                {
                    CancelarTimerLectorBarras();
                    string t = (_textoPendienteLector ?? "").Trim();
                    if (string.IsNullOrEmpty(t)) return;
                    // Solo auto-suma si es código/barras exacto (flujo típico del lector sin Enter)
                    DataRow exacto = DatabaseService.BuscarProductoExactoCodigoOCodigoBarra(t);
                    if (exacto == null) return;
                    if (!string.Equals((txtBuscarProducto?.Text ?? "").Trim(), t, StringComparison.Ordinal))
                        return;
                    _productoSeleccionado = exacto;
                    AgregarProductoSeleccionadoAlCarrito();
                };
            }
            _timerLectorBarras.Stop();
            _timerLectorBarras.Start();
        }

        private void CancelarTimerLectorBarras()
        {
            if (_timerLectorBarras != null)
                _timerLectorBarras.Stop();
            _textoPendienteLector = null;
        }

        // --- MERCADO PAGO QR ---
        private async void btnPagoQR_Click(object sender, RoutedEventArgs e)
        {
            if (!LicenseManager.TieneMercadoPagoQr())
            {
                CustomMessageBox.Show(
                    "Mercado Pago QR no está incluido en su licencia.\n\n" +
                    "Solicite el abono «Mercado Pago QR» para cobrar con código QR.",
                    "Abono no habilitado", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

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
                    _ordenIdMP = respuesta.OrdenId;
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
                var info = await Task.Run(() => MercadoPagoService.VerificarEstadoPago(_ordenIdMP));

                if (info.Estado == "approved")
                {
                    _timerVerificacionMP.Stop();
                    _esperandoPagoMP  = false;
                    _referenciaPagoMP = info.IdOperacion;
                    _ordenIdMP = "";

                    CustomerScreenService.PantallaGracias();
                    await Task.Delay(1800);

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
                else if (info.Estado == "rejected")
                {
                    // La orden quedó cancelada, vencida o falló: dejamos de consultar
                    CustomerScreenService.ActualizarMensajeQR("Pago Rechazado.", Brushes.Red);
                    await Task.Delay(2000);
                    _ordenIdMP = "";
                    CancelarModoQR();
                }
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

        private int ObtenerMedioPagoIdTarjeta()
        {
            try
            {
                var dt = DatabaseService.GetMediosPagoCompleto();
                foreach (DataRow row in dt.Rows)
                {
                    string tipo = row.Table.Columns.Contains("Tipo") ? row["Tipo"]?.ToString() ?? "" : "";
                    string nombre = row["Nombre"]?.ToString() ?? "";
                    if (tipo.IndexOf("tarjeta", StringComparison.OrdinalIgnoreCase) >= 0
                        || nombre.IndexOf("tarjeta", StringComparison.OrdinalIgnoreCase) >= 0
                        || nombre.IndexOf("crédito", StringComparison.OrdinalIgnoreCase) >= 0
                        || nombre.IndexOf("credito", StringComparison.OrdinalIgnoreCase) >= 0
                        || nombre.IndexOf("débito", StringComparison.OrdinalIgnoreCase) >= 0
                        || nombre.IndexOf("debito", StringComparison.OrdinalIgnoreCase) >= 0)
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

            if (!string.IsNullOrEmpty(_ordenIdMP))
            {
                string ordenACancelar = _ordenIdMP;
                _ordenIdMP = "";
                _ = Task.Run(() => MercadoPagoService.CancelarOrden(ordenACancelar));
            }

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
            if (_guardandoVenta) return;
            if (CarritoDeVenta.Count == 0) { CustomMessageBox.Show("Agregue productos."); return; }
            if (!ValidarPuedeVenderEnPos()) return;
            // Si no hay cliente seleccionado, usar Consumidor Final automáticamente
            if (_clienteSeleccionado == null) CargarClientePorDefecto();
            if (_clienteSeleccionado == null) { CustomMessageBox.Show("No se pudo cargar el cliente por defecto. Verificá la conexión a la base de datos."); return; }

            string tipoCompTexto = ObtenerTipoComprobanteSeleccionado();
            if (tipoCompTexto == "Presupuesto") { GuardarPresupuestoDesdePos(); return; }
            if (tipoCompTexto == "Remito") { GuardarRemitoDesdePos(); return; }
            if (tipoCompTexto == "Pedido") { GuardarPedidoDesdePos(); return; }
            if (tipoCompTexto == "Nota de Crédito") { GuardarNotaVentaDesdePos("NC", "Nota de Crédito"); return; }
            if (tipoCompTexto == "Nota de Débito") { GuardarNotaVentaDesdePos("ND", "Nota de Débito"); return; }

            if (!ValidarStockCarrito(out string errorStock))
            {
                CustomMessageBox.Show(errorStock);
                return;
            }

            // 1. Obtener Configuración
            DataRow config = DatabaseService.GetConfiguracion();
            int puntoVentaConfig = 0;
            if (config != null && config["PuntoVenta"] != DBNull.Value)
            {
                int pvGuardado = Convert.ToInt32(config["PuntoVenta"]);
                if (pvGuardado > 0) puntoVentaConfig = pvGuardado;
            }

            // 2. Determinar Tipo ARCA
            decimal total = CarritoDeVenta.Sum(x => x.Subtotal);

            // Un emisor monotributista siempre emite Factura C (tipo 11), nunca A ni B.
            bool emisorMonotributo = config != null
                && config.Table.Columns.Contains("CondicionIVAEmpresa")
                && (config["CondicionIVAEmpresa"]?.ToString() ?? "")
                    .IndexOf("monotrib", StringComparison.OrdinalIgnoreCase) >= 0;

            int tipoAfip = 0;
            if (tipoCompTexto == "Factura")
            {
                if (emisorMonotributo) tipoAfip = 11;
                else
                {
                    string cuitStr = _clienteSeleccionado["CUIT"].ToString();
                    if (cuitStr.Length >= 11 && !cuitStr.Contains("00-00000000")) tipoAfip = 1;
                    else tipoAfip = 6;
                }
            }
            else if (tipoCompTexto == "Ticket") tipoAfip = emisorMonotributo ? 11 : 6;

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
            _guardandoVenta = true;
            bool cobroConfirmado = false;
            try
            {
                bool afipLicenciado = LicenseManager.TieneAfip();
                bool afipConfigurado = AfipEstaConfigurado(config) && afipLicenciado;
                if (tipoCompTexto == "Factura" && !afipLicenciado)
                {
                    CustomMessageBox.Show(
                        "La factura electrónica ARCA no está incluida en su licencia.\n\n" +
                        "Use «Ticket» para comprobante interno o solicite el extra ARCA.",
                        "Extra no habilitado", MessageBoxButton.OK, MessageBoxImage.Warning);
                    btnGuardarFactura.IsEnabled = true;
                    return;
                }
                if (tipoAfip > 0 && afipConfigurado && puntoVentaConfig <= 0)
                {
                    CustomMessageBox.Show(
                        "No hay un punto de venta ARCA configurado.\n\n" +
                        "Ingrese el número real asignado por ARCA antes de emitir comprobantes fiscales.",
                        "Punto de venta requerido", MessageBoxButton.OK, MessageBoxImage.Warning);
                    btnGuardarFactura.IsEnabled = true;
                    return;
                }

                // ── PASO 1: cobro primero (flujo ágil) ──
                var win = Application.Current.Windows.OfType<Window>().FirstOrDefault(w => w.IsActive) ?? Application.Current.MainWindow;
                List<CobranzaItem> cobranzasConfirmadas;

                if (_pagoPointAprobado && _parcelasPoint != null)
                {
                    cobranzasConfirmadas = _parcelasPoint;
                    cobroConfirmado = true;
                }
                else if (_pagoMPAprobado && _parcelas_MP != null)
                {
                    // Pago QR ya aprobado → usar cobranza pre-armada, sin modal
                    cobranzasConfirmadas = _parcelas_MP;
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
                    if (cobroModal.SolicitoMercadoPagoQR)
                    {
                        btnGuardarFactura.IsEnabled = true;
                        btnPagoQR_Click(sender, e);
                        return;
                    }
                    if (cobroModal.SolicitoMercadoPagoPoint)
                    {
                        var pointModal = new PointCobroWindow(win, total);
                        bool? resultadoPoint = pointModal.ShowDialog();
                        if (resultadoPoint != true || pointModal.PagoAprobado == null)
                        {
                            btnGuardarFactura.IsEnabled = true;
                            CustomerScreenService.Actualizar(CarritoDeVenta.ToList(), total);
                            return;
                        }

                        EstadoPagoPoint pago = pointModal.PagoAprobado;
                        _parcelasPoint = new List<CobranzaItem>
                        {
                            new CobranzaItem
                            {
                                MedioPagoID = ObtenerMedioPagoIdTarjeta(),
                                nombreMedio = "Tarjeta — Mercado Pago Point",
                                monto = total,
                                NroCuotas = pago.Cuotas,
                                UltimosDigitosTarjeta = pago.UltimosDigitos,
                                MarcaTarjeta = pago.MarcaTarjeta,
                                OperacionExternaID = pago.OperacionId
                            }
                        };
                        _pagoPointAprobado = true;
                        btnGuardarFactura.IsEnabled = true;
                        btnGuardarFactura_Click(sender, e);
                        return;
                    }
                    cobranzasConfirmadas = cobroModal.Cobranzas;
                    cobroConfirmado = cobranzasConfirmadas != null && cobranzasConfirmadas.Count > 0;
                }

                // Aviso ARCA después del cobro (no interrumpe el flujo antes de cobrar)
                if (tipoCompTexto == "Factura" && !afipConfigurado)
                {
                    if (CustomMessageBox.Show(
                            "ARCA no está configurado (CUIT y certificado).\n\n" +
                            "La venta se guardará sin CAE ni numeración fiscal.\n\n¿Desea continuar?",
                            "Factura sin ARCA", MessageBoxButton.YesNo, MessageBoxImage.Warning) != MessageBoxResult.Yes)
                    {
                        btnGuardarFactura.IsEnabled = true;
                        return;
                    }
                }

                // ── PASO 2: ARCA (solo si el cobro fue confirmado) ──
                string cae = null;
                string vtoCae = null;
                int nroComprobante = 0;

                // Solo llamar ARCA si está configurado (tiene CUIT y certificado)
                if (tipoAfip > 0 && afipConfigurado)
                {
                    CustomerScreenService.ActualizarMensajeQR("Facturando ARCA...", Brushes.Orange);
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
                            "❌ ARCA rechazó la factura electrónica.\n\n" +
                            "Detalle: " + resultadoAfip.Error + "\n\n" +
                            "⚠️ IMPORTANTE: el cobro fue confirmado pero la venta NO quedó registrada.\n\n" +
                            "Opciones:\n" +
                            "• Intentar de nuevo (el cobro ya fue recibido, NO vuelva a cobrar).\n" +
                            "• Cambiar el tipo a 'Ticket' para guardar sin código ARCA.",
                            "Error ARCA — venta no registrada");
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
                    Monto = ci.monto,
                    NroCuotas = ci.NroCuotas,
                    UltimosDigitosTarjeta = ci.UltimosDigitosTarjeta,
                    MarcaTarjeta = ci.MarcaTarjeta,
                    OperacionExternaID = ci.OperacionExternaID
                });

                int fid = DatabaseService.GuardarFactura(cliID, tipoCompTexto, total, CarritoDeVenta.ToList(),
                    condVent, condicionTicket, cae, vtoCae, nroComprobante, listaId, parcelas);

                if (fid > 0)
                {
                    // El cobro electrónico se consume recién cuando la venta quedó persistida.
                    // Si ARCA o SQL fallan, el reintento no debe volver a cobrar al cliente.
                    _pagoPointAprobado = false;
                    _parcelasPoint = null;
                    _pagoMPAprobado = false;
                    _parcelas_MP = null;
                    RegistrarUltimoComprobante(fid, tipoCompTexto);
                    CustomerScreenService.PantallaGracias();
                    string msgExito = "Venta Guardada.";
                    if (!string.IsNullOrEmpty(cae)) msgExito += "\n¡Factura Electrónica Aprobada!";
                    OfrecerImprimirComprobante(fid, PrintService.ImprimirFactura, msgExito, tipoCompTexto);
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
                        // ARCA aprobó y cobro confirmado, pero el INSERT en BD falló → situación crítica
                        CustomMessageBox.Show(
                            "⛔ ERROR CRÍTICO: La venta no se guardó en el sistema.\n\n" +
                            "El cobro fue recibido y ARCA ya emitió el comprobante:\n" +
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
                _guardandoVenta = false;
                btnGuardarFactura.IsEnabled = true;
            }
        }

        private static void OfrecerImprimirComprobante(int id, Action<int> imprimir, string mensajeExito, string tipoComprobante)
        {
            bool hayImpresora;
            bool esVentaPos = string.Equals(tipoComprobante, "Ticket", StringComparison.OrdinalIgnoreCase)
                || string.Equals(tipoComprobante, "Factura", StringComparison.OrdinalIgnoreCase);
            if (esVentaPos)
            {
                string destino = DatabaseService.GetDestinoImpresionVenta();
                hayImpresora = destino == "Preguntar" || destino == "Archivo" || DatabaseService.PuedeEmitirComprobante(destino);
            }
            else
                hayImpresora = DatabaseService.TieneImpresoraA4Configurada();

            if (DatabaseService.GetPreguntarAntesImprimir())
            {
                if (CustomMessageBox.Show($"{mensajeExito}\n¿Imprimir comprobante?", "Éxito", MessageBoxButton.YesNo) != MessageBoxResult.Yes)
                    return;
            }
            else if (!hayImpresora)
                return;

            imprimir(id);
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

                string tipoImpresion = nombreDocumento == "Ticket" || nombreDocumento == "Factura"
                    ? nombreDocumento
                    : "Factura";
                OfrecerImprimirComprobante(id, imprimir,
                    $"{nombreDocumento} #{id:D8} guardado correctamente.",
                    tipoImpresion);

                RegistrarUltimoComprobante(id, nombreDocumento);
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
            PurgeCarritoInvalido();
            decimal subtotal = 0m, descuentos = 0m, recargos = 0m;
            foreach (var it in CarritoDeVenta)
            {
                decimal bruto = it.Cantidad * it.PrecioUnitario;
                decimal linea = it.Subtotal;
                subtotal += bruto;
                if (linea < bruto) descuentos += bruto - linea;
                else if (linea > bruto) recargos += linea - bruto;
            }
            decimal totalConDescRec = CarritoDeVenta.Sum(x => x.Subtotal);
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
            lblDescuentos.Text = descuentos > 0m ? descuentos.ToString("C2") : "$ 0,00";
            if (lblRecargos != null)
                lblRecargos.Text = recargos > 0m ? recargos.ToString("C2") : "$ 0,00";
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
            MarcarItemCarrito(item);
            if (item != null)
            {
                CarritoDeVenta.Remove(item);
                PurgeCarritoInvalido();
                ActualizarTotal();
            }
        }

        private void btnCancelarFactura_Click(object sender, RoutedEventArgs e) { LimpiarFormulario(); }

        private void AbrirVentanaVarios()
        {
            if (!ValidarPuedeVenderEnPos()) return;
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
                    ImagenPath = null,
                    PermiteModificarPrecioVenta = true
                });
                RefrescarVistaCarrito();
                ActualizarTotal();
                LimpiarProducto();
                MarcarItemCarrito(CarritoDeVenta.LastOrDefault(i => i.EsValido));
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
        private void RecalcularCarritoConNuevaLista()
        {
            int? listaId = ObtenerListaIdSeleccionada();
            foreach (var item in CarritoDeVenta)
            {
                if (item.Codigo == "VAR") continue;
                DataRow prod = DatabaseService.BuscarProducto(item.Codigo);
                if (prod != null)
                {
                    int pid = Convert.ToInt32(prod["ProductoID"]);
                    item.PrecioUnitario = listaId.HasValue
                        ? DatabaseService.CalcularPrecioListaPorIds(pid, listaId.Value)
                        : Convert.ToDecimal(prod["PrecioVenta"]);
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
            if (txtBuscarProducto.Text.Length < 1)
            {
                CancelarTimerLectorBarras();
                popupProducto.IsOpen = false;
                _productoSeleccionado = null;
                return;
            }
            try
            {
                string texto = txtBuscarProducto.Text.Trim();
                DataTable dt = DatabaseService.BuscarProductosMultiples_ParaVenta(texto);
                if (dt != null && _modoBusqueda != ModoBusquedaPos.Todo)
                {
                    string q = texto.ToUpperInvariant();
                    var filas = dt.AsEnumerable().Where(r =>
                    {
                        if (_modoBusqueda == ModoBusquedaPos.CodigoBarras)
                        {
                            string cod = r.Table.Columns.Contains("Codigo") ? r["Codigo"]?.ToString() ?? "" : "";
                            string bar = r.Table.Columns.Contains("CodigoBarra") ? r["CodigoBarra"]?.ToString() ?? "" : "";
                            return cod.ToUpperInvariant().Contains(q) || bar.ToUpperInvariant().Contains(q);
                        }
                        string desc = r.Table.Columns.Contains("Descripcion") ? r["Descripcion"]?.ToString() ?? "" : "";
                        return desc.ToUpperInvariant().Contains(q);
                    }).ToArray();
                    dt = filas.Length > 0 ? filas.CopyToDataTable() : dt.Clone();
                }
                lstSugerenciasProducto.ItemsSource = dt.DefaultView;
                popupProducto.IsOpen = dt.Rows.Count > 0;
                AutocompleteListHelper.ReiniciarSeleccion(lstSugerenciasProducto);

                // Lector de barras: al detectar código exacto, sumar solo (sin pedir "+")
                if (PareceCodigoBarras(texto) || DatabaseService.BuscarProductoExactoCodigoOCodigoBarra(texto) != null)
                    ProgramarAutoAgregarLector(texto);
                else
                    CancelarTimerLectorBarras();
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
            if (lstSugerenciasProducto.SelectedItem is DataRowView r)
            {
                _productoSeleccionado = r.Row;
                AgregarProductoSeleccionadoAlCarrito();
            }
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
                    // Enter / sufijo del lector: siempre intentar sumar al carrito
                    e.Handled = true;
                    txtBuscarProductoEnterAgregarSiCorresponde();
                    RegistrarDobleEnterProducto();
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
                {
                    _productoSeleccionado = p.Row;
                    AgregarProductoSeleccionadoAlCarrito();
                }
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
            MarcarItemCarrito(item);
            var win = CrearInputModal("Descuento del ítem", "Porcentaje de descuento solo para este producto:", item.DescuentoPorcentaje.ToString("N0"), soloNumeros: true);
            if (win.ShowDialog() == true && decimal.TryParse(win.ResponseText?.Replace(",", "."), NumberStyles.Any, CultureInfo.InvariantCulture, out decimal pct) && pct >= 0 && pct <= 100)
            {
                item.DescuentoPorcentaje = pct;
                item.PromoNombre = null; // descuento manual: ya no es la promo automática
                if (pct > 0) item.RecargoPorcentaje = 0;
                RefrescarVistaCarrito();
                ActualizarTotal();
            }
        }
        private void btnRecargoItem_Click(object sender, RoutedEventArgs e)
        {
            var item = ObtenerItemCarrito(sender);
            if (item == null) return;
            MarcarItemCarrito(item);
            var win = CrearInputModal("Recargo del ítem", "Porcentaje de recargo solo para este producto:", item.RecargoPorcentaje.ToString("N0"), soloNumeros: true);
            if (win.ShowDialog() == true && decimal.TryParse(win.ResponseText?.Replace(",", "."), NumberStyles.Any, CultureInfo.InvariantCulture, out decimal pct) && pct >= 0 && pct <= 1000)
            {
                item.RecargoPorcentaje = pct;
                if (pct > 0)
                {
                    item.DescuentoPorcentaje = 0;
                    item.PromoNombre = null;
                }
                RefrescarVistaCarrito();
                ActualizarTotal();
            }
        }
        private void btnEditarItem_Click(object sender, RoutedEventArgs e)
        {
            var item = ObtenerItemCarrito(sender);
            if (item == null) return;
            if (!item.PermiteModificarPrecioVenta)
            {
                CustomMessageBox.Show(
                    "Este producto no permite modificar el precio en venta.\nActive la opción en la ficha del producto si desea habilitarlo.",
                    "Precio bloqueado", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }
            MarcarItemCarrito(item);
            var winPrecio = CrearInputModal("Editar precio", "Nuevo precio unitario de este producto:", item.PrecioUnitario.ToString("N2"), soloNumeros: true);
            if (winPrecio.ShowDialog() == true && decimal.TryParse(winPrecio.ResponseText?.Replace(",", "."), NumberStyles.Any, CultureInfo.InvariantCulture, out decimal prec) && prec >= 0)
            {
                item.PrecioUnitario = prec;
                ActualizarTotal();
            }
        }

        // --- AYUDA ATAJOS ---
        private void btnAyudaAtajos_Click(object sender, RoutedEventArgs e) => MostrarAyudaAtajos();

        private static FacturaItem ObtenerItemCarrito(object sender)
        {
            DependencyObject dep = sender as DependencyObject;
            while (dep != null)
            {
                if (dep is FrameworkElement fe && fe.DataContext is FacturaItem item)
                    return item;
                dep = LogicalTreeHelper.GetParent(dep);
            }
            return null;
        }

        private static bool LeerPermiteModificarPrecioVenta(DataRow producto)
        {
            return producto != null
                && producto.Table.Columns.Contains("PermiteModificarPrecioVenta")
                && producto["PermiteModificarPrecioVenta"] != DBNull.Value
                && Convert.ToBoolean(producto["PermiteModificarPrecioVenta"]);
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