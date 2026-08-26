using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using QRCoder;
using SchettiniGestion;

namespace SchettiniGestion.WPF
{
    public partial class VisorClienteWindow : Window
    {
        public event Action<string> OnOpcionSeleccionada;

        private DispatcherTimer _timerVueltaBienvenida;

        private readonly List<string> _rutasPromoVertical = new List<string>();
        private readonly List<string> _rutasPromoHorizontal = new List<string>();

        private DispatcherTimer _timerPromoVertical;
        private DispatcherTimer _timerPromoHorizontal;
        private int _indicePromoVertical;
        private int _indicePromoHorizontal;
        private bool _carruselVerticalEsVideo;
        private bool _carruselHorizontalEsVideo;

        private readonly bool _modoVistaPrevia;
        private readonly string _carpetaPromoOverride;
        private readonly int? _intervaloVerticalSegundosOverride;
        private readonly int? _intervaloHorizontalSegundosOverride;
        private readonly List<string> _rutasPromoVerticalExtra = new List<string>();
        private readonly List<string> _rutasPromoHorizontalExtra = new List<string>();

        public VisorClienteWindow()
        {
            InitializeComponent();
            _modoVistaPrevia = false;
            _carpetaPromoOverride = null;
            _intervaloVerticalSegundosOverride = null;
            _intervaloHorizontalSegundosOverride = null;
            InicializarVisor(null, null);
        }

        /// <summary>Vista previa modal desde Configuración (sin segundo monitor). Cada lista de rutas extra ya sabe a qué panel pertenece.</summary>
        public VisorClienteWindow(
            bool modoVistaPrevia,
            string carpetaPromoOverride,
            int? intervaloVerticalSegundosOverride,
            int? intervaloHorizontalSegundosOverride,
            IEnumerable<string> rutasPromoVerticalExtra,
            IEnumerable<string> rutasPromoHorizontalExtra)
        {
            InitializeComponent();
            _modoVistaPrevia = modoVistaPrevia;
            _carpetaPromoOverride = carpetaPromoOverride;
            _intervaloVerticalSegundosOverride = intervaloVerticalSegundosOverride;
            _intervaloHorizontalSegundosOverride = intervaloHorizontalSegundosOverride;
            InicializarVisor(rutasPromoVerticalExtra, rutasPromoHorizontalExtra);
        }

        private void InicializarVisor(IEnumerable<string> rutasPromoVerticalExtra, IEnumerable<string> rutasPromoHorizontalExtra)
        {
            AgregarRutasExtra(_rutasPromoVerticalExtra, rutasPromoVerticalExtra);
            AgregarRutasExtra(_rutasPromoHorizontalExtra, rutasPromoHorizontalExtra);

            Closed += (_, __) => DetenerTodasPromociones();
            SizeChanged += (_, __) => AjustarLayoutPromociones();

            if (_modoVistaPrevia)
                AplicarModoVistaPrevia();

            CargarLogo();
            IniciarPromociones();
        }

        private static void AgregarRutasExtra(List<string> destino, IEnumerable<string> rutas)
        {
            if (rutas == null) return;
            foreach (string ruta in rutas)
            {
                if (!string.IsNullOrWhiteSpace(ruta) && File.Exists(ruta)
                    && !destino.Contains(ruta, StringComparer.OrdinalIgnoreCase))
                    destino.Add(ruta);
            }
        }

        private void AplicarModoVistaPrevia()
        {
            Title = "Vista previa — Pantalla del cliente";
            WindowStyle = WindowStyle.SingleBorderWindow;
            ResizeMode = ResizeMode.CanResize;
            WindowState = WindowState.Normal;
            Width = 800;
            Height = 600;
            MinWidth = 640;
            MinHeight = 480;
            WindowStartupLocation = WindowStartupLocation.CenterScreen;
            ShowInTaskbar = true;
        }

        /// <summary>Recarga carpeta, listas y reinicia el carrusel sin cerrar la ventana.</summary>
        public void RecargarPublicidades()
        {
            if (!Dispatcher.CheckAccess())
            {
                Dispatcher.Invoke(RecargarPublicidades);
                return;
            }

            CargarLogo();
            IniciarPromociones();
        }

        private void CargarLogo()
        {
            try
            {
                var config = DatabaseService.GetConfiguracion();
                ImageSource src = null;
                string nombre = "Bienvenido";

                if (config != null)
                {
                    if (config.Table.Columns.Contains("LogoPath") && config["LogoPath"] != DBNull.Value)
                    {
                        string path = config["LogoPath"]?.ToString()?.Trim();
                        if (!string.IsNullOrEmpty(path))
                            src = SvgLogoHelper.LoadImageFromPath(path);
                    }
                    if (config.Table.Columns.Contains("NombreFantasia") && config["NombreFantasia"] != DBNull.Value)
                    {
                        string nom = config["NombreFantasia"]?.ToString()?.Trim();
                        if (!string.IsNullOrEmpty(nom)) nombre = nom;
                    }
                }

                src = src ?? SvgLogoHelper.LoadEmbeddedLogo();
                imgLogoBienvenida.Source = src;
                imgLogo.Source = src;
                txtNombreNegocio.Text = nombre;
                txtMarcaPromo.Text = nombre;
            }
            catch { }
        }

        private void IniciarPromociones()
        {
            DetenerTodasPromociones();
            _rutasPromoVertical.Clear();
            _rutasPromoHorizontal.Clear();

            foreach (string ruta in ObtenerRutasCarpetaPromociones())
            {
                if (EsPromoVertical(ruta))
                    _rutasPromoVertical.Add(ruta);
                else
                    _rutasPromoHorizontal.Add(ruta);
            }

            // Las rutas "extra" (vista previa de un archivo aún no guardado) ya saben a qué panel
            // pertenecen porque el usuario las subió desde el botón de ese panel específico.
            foreach (string ruta in _rutasPromoVerticalExtra)
                if (!_rutasPromoVertical.Contains(ruta, StringComparer.OrdinalIgnoreCase))
                    _rutasPromoVertical.Add(ruta);

            foreach (string ruta in _rutasPromoHorizontalExtra)
                if (!_rutasPromoHorizontal.Contains(ruta, StringComparer.OrdinalIgnoreCase))
                    _rutasPromoHorizontal.Add(ruta);

            AjustarLayoutPromociones();

            if (_rutasPromoVertical.Count > 0)
            {
                _indicePromoVertical = 0;
                MostrarPromoVerticalActual();
            }
            else
            {
                OcultarControlesPromoVertical();
            }

            if (_rutasPromoHorizontal.Count > 0)
            {
                _indicePromoHorizontal = 0;
                MostrarPromoHorizontalActual();
            }
            else
            {
                OcultarControlesPromoHorizontal();
            }
        }

        private IEnumerable<string> ObtenerRutasCarpetaPromociones()
        {
            var list = !string.IsNullOrWhiteSpace(_carpetaPromoOverride)
                ? DatabaseService.ListarArchivosPromoVisorCliente(_carpetaPromoOverride)
                : DatabaseService.ListarArchivosPromoVisorCliente();
            list.Sort(StringComparer.OrdinalIgnoreCase);
            return list;
        }

        private void AjustarLayoutPromociones()
        {
            if (_rutasPromoVertical.Count > 0)
            {
                // Imagen promo: proporción 1080×1920 (9:16) o 1080×1080
                double h = ActualHeight > 100 ? ActualHeight : 1080;
                double anchoIdeal = h * (1080.0 / 1920.0);
                double maxAncho = ActualWidth > 100 ? ActualWidth * 0.48 : 540;
                double ancho = Math.Min(Math.Max(anchoIdeal, 280), maxAncho);
                colPromoLateral.Width = new GridLength(ancho);
                panelPromoLateral.Visibility = Visibility.Visible;
            }
            else
            {
                colPromoLateral.Width = new GridLength(0);
                panelPromoLateral.Visibility = Visibility.Collapsed;
            }

            if (_rutasPromoHorizontal.Count > 0)
            {
                // Banner inferior: proporción recomendada 1200×300
                double w = ActualWidth > 100 ? ActualWidth : 1200;
                double altoIdeal = w * (300.0 / 1200.0);
                double alto = Math.Min(Math.Max(altoIdeal, 90), Math.Min(ActualHeight * 0.28, 300));
                rowPromoInferior.Height = new GridLength(alto);
                panelPromoInferior.Visibility = Visibility.Visible;
            }
            else
            {
                rowPromoInferior.Height = new GridLength(0);
                panelPromoInferior.Visibility = Visibility.Collapsed;
            }
        }

        private static bool EsPromoVertical(string path)
        {
            string fileName = Path.GetFileName(path) ?? "";
            // Prefijos opcionales para forzar ubicación: banner_ / horizontal_ → franja; promo_ / vertical_ → lateral
            if (fileName.StartsWith("banner_", StringComparison.OrdinalIgnoreCase)
                || fileName.StartsWith("horizontal_", StringComparison.OrdinalIgnoreCase)
                || fileName.StartsWith("h_", StringComparison.OrdinalIgnoreCase))
                return false;
            if (fileName.StartsWith("promo_", StringComparison.OrdinalIgnoreCase)
                || fileName.StartsWith("vertical_", StringComparison.OrdinalIgnoreCase)
                || fileName.StartsWith("v_", StringComparison.OrdinalIgnoreCase))
                return true;

            string ext = Path.GetExtension(path);
            if (DatabaseService.EsExtensionPromoVideoCliente(ext))
            {
                // Videos: si el nombre no indica banner, van al panel vertical (1080×1920)
                return true;
            }

            if (!DatabaseService.EsExtensionPromoImagenCliente(ext))
                return true;

            try
            {
                var bi = new BitmapImage();
                bi.BeginInit();
                bi.UriSource = new Uri(path, UriKind.Absolute);
                bi.CacheOption = BitmapCacheOption.OnLoad;
                bi.EndInit();
                bi.Freeze();
                // Banner típico 1200×300 (ancho > alto * 1.6); promo 1080×1920 o cuadrado
                return bi.PixelWidth <= bi.PixelHeight * 1.6;
            }
            catch
            {
                return true;
            }
        }

        /// <summary>Segundos entre cambios del panel izquierdo (imagen promocional vertical).</summary>
        private int LeerIntervaloPromoVerticalSeg()
        {
            if (_intervaloVerticalSegundosOverride.HasValue)
                return Math.Min(Math.Max(_intervaloVerticalSegundosOverride.Value, 3), 120);

            try
            {
                var cfg = DatabaseService.GetConfiguracion();
                if (cfg != null && cfg.Table.Columns.Contains("VisorPromoIntervaloSeg") && cfg["VisorPromoIntervaloSeg"] != DBNull.Value
                    && int.TryParse(cfg["VisorPromoIntervaloSeg"].ToString(), out int s) && s >= 3)
                    return Math.Min(s, 120);
            }
            catch { }
            return 8;
        }

        /// <summary>Segundos entre cambios de la franja inferior (banner). Configurable de forma independiente al panel izquierdo.</summary>
        private int LeerIntervaloPromoHorizontalSeg()
        {
            if (_intervaloHorizontalSegundosOverride.HasValue)
                return Math.Min(Math.Max(_intervaloHorizontalSegundosOverride.Value, 3), 120);

            try
            {
                var cfg = DatabaseService.GetConfiguracion();
                if (cfg != null && cfg.Table.Columns.Contains("VisorBannerIntervaloSeg") && cfg["VisorBannerIntervaloSeg"] != DBNull.Value
                    && int.TryParse(cfg["VisorBannerIntervaloSeg"].ToString(), out int s) && s >= 3)
                    return Math.Min(s, 120);
            }
            catch { }
            return 8;
        }

        private void MostrarPromoVerticalActual()
        {
            if (_rutasPromoVertical.Count == 0) return;
            string path = _rutasPromoVertical[_indicePromoVertical % _rutasPromoVertical.Count];
            _carruselVerticalEsVideo = MostrarArchivoPromo(path, imgPromo, mediaPromo);
            if (!_carruselVerticalEsVideo)
                ReiniciarTimerPromoVertical();
            else
                DetenerTimerPromoVertical();
        }

        private void MostrarPromoHorizontalActual()
        {
            if (_rutasPromoHorizontal.Count == 0) return;
            string path = _rutasPromoHorizontal[_indicePromoHorizontal % _rutasPromoHorizontal.Count];
            _carruselHorizontalEsVideo = MostrarArchivoPromo(path, imgPromoHorizontal, mediaPromoHorizontal);
            if (!_carruselHorizontalEsVideo)
                ReiniciarTimerPromoHorizontal();
            else
                DetenerTimerPromoHorizontal();
        }

        private bool MostrarArchivoPromo(string path, Image img, MediaElement media)
        {
            string ext = Path.GetExtension(path);

            LiberarMediaPromo(media);
            LiberarImagenPromo(img);

            if (DatabaseService.EsExtensionPromoVideoCliente(ext))
            {
                try
                {
                    media.Source = new Uri(path, UriKind.Absolute);
                    media.Visibility = Visibility.Visible;
                    media.Play();
                    return true;
                }
                catch { }
            }

            if (DatabaseService.EsExtensionPromoImagenCliente(ext))
            {
                try
                {
                    var bi = new BitmapImage();
                    bi.BeginInit();
                    bi.CacheOption = BitmapCacheOption.OnLoad;
                    bi.UriSource = new Uri(path, UriKind.Absolute);
                    bi.EndInit();
                    bi.Freeze();
                    img.Source = bi;
                    img.Visibility = Visibility.Visible;
                }
                catch { }
            }

            return false;
        }

        private static void LiberarImagenPromo(Image img)
        {
            if (img == null) return;
            img.Visibility = Visibility.Collapsed;
            img.Source = null;
        }

        private static void LiberarMediaPromo(MediaElement media)
        {
            if (media == null) return;
            try { media.Stop(); } catch { }
            media.Source = null;
            media.Visibility = Visibility.Collapsed;
        }

        private void OcultarControlesPromoVertical()
        {
            LiberarImagenPromo(imgPromo);
            LiberarMediaPromo(mediaPromo);
        }

        private void OcultarControlesPromoHorizontal()
        {
            LiberarImagenPromo(imgPromoHorizontal);
            LiberarMediaPromo(mediaPromoHorizontal);
        }

        private void AvanzarPromoVertical()
        {
            if (_rutasPromoVertical.Count == 0) return;
            if (_rutasPromoVertical.Count > 1)
                _indicePromoVertical = (_indicePromoVertical + 1) % _rutasPromoVertical.Count;
            MostrarPromoVerticalActual();
        }

        private void AvanzarPromoHorizontal()
        {
            if (_rutasPromoHorizontal.Count == 0) return;
            if (_rutasPromoHorizontal.Count > 1)
                _indicePromoHorizontal = (_indicePromoHorizontal + 1) % _rutasPromoHorizontal.Count;
            MostrarPromoHorizontalActual();
        }

        private void ReiniciarTimerPromoVertical()
        {
            DetenerTimerPromoVertical();
            int seg = LeerIntervaloPromoVerticalSeg();
            _timerPromoVertical = new DispatcherTimer { Interval = TimeSpan.FromSeconds(seg) };
            _timerPromoVertical.Tick += (_, __) =>
            {
                if (!_carruselVerticalEsVideo)
                    AvanzarPromoVertical();
            };
            _timerPromoVertical.Start();
        }

        private void ReiniciarTimerPromoHorizontal()
        {
            DetenerTimerPromoHorizontal();
            int seg = LeerIntervaloPromoHorizontalSeg();
            _timerPromoHorizontal = new DispatcherTimer { Interval = TimeSpan.FromSeconds(seg) };
            _timerPromoHorizontal.Tick += (_, __) =>
            {
                if (!_carruselHorizontalEsVideo)
                    AvanzarPromoHorizontal();
            };
            _timerPromoHorizontal.Start();
        }

        private void DetenerTimerPromoVertical()
        {
            if (_timerPromoVertical != null)
            {
                _timerPromoVertical.Stop();
                _timerPromoVertical = null;
            }
        }

        private void DetenerTimerPromoHorizontal()
        {
            if (_timerPromoHorizontal != null)
            {
                _timerPromoHorizontal.Stop();
                _timerPromoHorizontal = null;
            }
        }

        private void DetenerTodasPromociones()
        {
            DetenerTimerPromoVertical();
            DetenerTimerPromoHorizontal();
            _carruselVerticalEsVideo = false;
            _carruselHorizontalEsVideo = false;
            OcultarControlesPromoVertical();
            OcultarControlesPromoHorizontal();
        }

        private void MediaPromo_MediaEnded(object sender, RoutedEventArgs e)
        {
            _carruselVerticalEsVideo = false;
            AvanzarPromoVertical();
        }

        private void MediaPromo_MediaFailed(object sender, ExceptionRoutedEventArgs e)
        {
            _carruselVerticalEsVideo = false;
            AvanzarPromoVertical();
        }

        private void MediaPromoHorizontal_MediaEnded(object sender, RoutedEventArgs e)
        {
            _carruselHorizontalEsVideo = false;
            AvanzarPromoHorizontal();
        }

        private void MediaPromoHorizontal_MediaFailed(object sender, ExceptionRoutedEventArgs e)
        {
            _carruselHorizontalEsVideo = false;
            AvanzarPromoHorizontal();
        }

        public void ActualizarGrilla(List<FacturaItem> items, decimal total)
        {
            OcultarPanelesContenido();
            GridVenta.Visibility = Visibility.Visible;
            dgvDetalleCliente.ItemsSource = null;
            dgvDetalleCliente.ItemsSource = items;
            if (items.Count > 0)
                dgvDetalleCliente.ScrollIntoView(items[items.Count - 1]);
            lblTotal.Text = $"$ {total:N2}";
        }

        public void Reiniciar()
        {
            CancelarTimerVueltaBienvenida();
            OcultarPanelesContenido();
            GridBienvenida.Visibility = Visibility.Visible;
            dgvDetalleCliente.ItemsSource = null;
            lblTotal.Text = "$ 0.00";
        }

        public void MostrarSeleccionPago()
        {
            OcultarPanelesContenido();
            GridPago.Visibility = Visibility.Visible;
        }

        public void MostrarQR(string dataQR, decimal monto)
        {
            OcultarPanelesContenido();
            GridQR.Visibility = Visibility.Visible;
            lblTituloQR.Text = "Escaneá el QR para pagar";
            bordeQR.Visibility = Visibility.Visible;
            lblTotalQR.Text = $"$ {monto:N2}";
            lblEstadoQR.Text = "Escanee el código con su celular";
            lblEstadoQR.Foreground = Brushes.White;
            GenerarQREnPantalla(dataQR);
        }

        public void MostrarQREstatico(decimal monto)
        {
            OcultarPanelesContenido();
            GridQR.Visibility = Visibility.Visible;
            lblTituloQR.Text = "Escaneá el QR de la caja";
            bordeQR.Visibility = Visibility.Collapsed;
            imgQR.Source = null;
            lblTotalQR.Text = $"$ {monto:N2}";
            lblEstadoQR.Text = "El monto ya está listo en el código impreso";
            lblEstadoQR.Foreground = Brushes.White;
        }

        public void ActualizarEstadoQR(string mensaje, Brush color)
        {
            lblEstadoQR.Text = mensaje;
            lblEstadoQR.Foreground = color;
        }

        public void MostrarPoint(decimal monto, string mensaje)
        {
            OcultarPanelesContenido();
            GridPoint.Visibility = Visibility.Visible;
            lblTotalPoint.Text = $"$ {monto:N2}";
            lblEstadoPoint.Text = mensaje;
            lblEstadoPoint.Foreground = new SolidColorBrush(Color.FromRgb(85, 85, 85));
        }

        public void ActualizarEstadoPoint(string mensaje, Brush color)
        {
            lblEstadoPoint.Text = mensaje;
            lblEstadoPoint.Foreground = color;
        }

        public void MostrarGracias()
        {
            OcultarPanelesContenido();
            GridGracias.Visibility = Visibility.Visible;
            GridGracias.Opacity = 0;

            if (GridGracias.Resources["AnimacionPagoAprobado"] is Storyboard animacion)
            {
                animacion.Remove(GridGracias);
                animacion.Begin(GridGracias, true);
            }

            ProgramarVueltaABienvenida();
        }

        private void ProgramarVueltaABienvenida()
        {
            CancelarTimerVueltaBienvenida();
            _timerVueltaBienvenida = new DispatcherTimer { Interval = TimeSpan.FromSeconds(4) };
            _timerVueltaBienvenida.Tick += (s, e) =>
            {
                CancelarTimerVueltaBienvenida();
                Reiniciar();
            };
            _timerVueltaBienvenida.Start();
        }

        private void CancelarTimerVueltaBienvenida()
        {
            _timerVueltaBienvenida?.Stop();
            _timerVueltaBienvenida = null;
        }

        /// <summary>Oculta solo los paneles de contenido; las publicidades siguen activas.</summary>
        private void OcultarPanelesContenido()
        {
            GridBienvenida.Visibility = Visibility.Collapsed;
            GridVenta.Visibility = Visibility.Collapsed;
            GridPago.Visibility = Visibility.Collapsed;
            GridQR.Visibility = Visibility.Collapsed;
            GridPoint.Visibility = Visibility.Collapsed;
            GridGracias.Visibility = Visibility.Collapsed;
        }

        private void BtnPago_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button btn && btn.Tag != null)
                OnOpcionSeleccionada?.Invoke(btn.Tag.ToString());
        }

        private void GenerarQREnPantalla(string data)
        {
            try
            {
                var qrGen = new QRCodeGenerator();
                QRCodeData qrData = qrGen.CreateQrCode(data, QRCodeGenerator.ECCLevel.Q);
                var qrCode = new QRCode(qrData);
                using (var qrBmp = qrCode.GetGraphic(20))
                using (var ms = new MemoryStream())
                {
                    qrBmp.Save(ms, System.Drawing.Imaging.ImageFormat.Png);
                    ms.Position = 0;
                    var bi = new BitmapImage();
                    bi.BeginInit();
                    bi.StreamSource = ms;
                    bi.CacheOption = BitmapCacheOption.OnLoad;
                    bi.EndInit();
                    imgQR.Source = bi;
                }
            }
            catch { }
        }
    }
}
