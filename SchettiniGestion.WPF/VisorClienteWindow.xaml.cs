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
        private readonly int? _intervaloSegundosOverride;
        private readonly List<string> _rutasPromoExtra = new List<string>();

        public VisorClienteWindow()
        {
            InitializeComponent();
            _modoVistaPrevia = false;
            _carpetaPromoOverride = null;
            _intervaloSegundosOverride = null;
            InicializarVisor(null);
        }

        /// <summary>Vista previa modal desde Configuración (sin segundo monitor).</summary>
        public VisorClienteWindow(bool modoVistaPrevia, string carpetaPromoOverride, int? intervaloSegundosOverride, IEnumerable<string> rutasPromoExtra)
        {
            InitializeComponent();
            _modoVistaPrevia = modoVistaPrevia;
            _carpetaPromoOverride = carpetaPromoOverride;
            _intervaloSegundosOverride = intervaloSegundosOverride;
            InicializarVisor(rutasPromoExtra);
        }

        private void InicializarVisor(IEnumerable<string> rutasPromoExtra)
        {
            if (rutasPromoExtra != null)
            {
                foreach (string ruta in rutasPromoExtra)
                {
                    if (!string.IsNullOrWhiteSpace(ruta) && File.Exists(ruta)
                        && !_rutasPromoExtra.Contains(ruta, StringComparer.OrdinalIgnoreCase))
                        _rutasPromoExtra.Add(ruta);
                }
            }

            Closed += (_, __) => DetenerTodasPromociones();
            SizeChanged += (_, __) => AjustarLayoutPromociones();

            if (_modoVistaPrevia)
                AplicarModoVistaPrevia();

            CargarLogo();
            IniciarPromociones();
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

            foreach (string ruta in ObtenerRutasPromociones())
            {
                if (EsPromoVertical(ruta))
                    _rutasPromoVertical.Add(ruta);
                else
                    _rutasPromoHorizontal.Add(ruta);
            }

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

        private IEnumerable<string> ObtenerRutasPromociones()
        {
            var list = new List<string>();
            if (!string.IsNullOrWhiteSpace(_carpetaPromoOverride))
                list.AddRange(DatabaseService.ListarArchivosPromoVisorCliente(_carpetaPromoOverride));
            else
                list.AddRange(DatabaseService.ListarArchivosPromoVisorCliente());

            foreach (string extra in _rutasPromoExtra)
            {
                if (!list.Contains(extra, StringComparer.OrdinalIgnoreCase))
                    list.Add(extra);
            }

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

        private int LeerIntervaloPromoSeg()
        {
            if (_intervaloSegundosOverride.HasValue)
                return Math.Min(Math.Max(_intervaloSegundosOverride.Value, 3), 120);

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
            int seg = LeerIntervaloPromoSeg();
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
            int seg = LeerIntervaloPromoSeg();
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
            lblTotalQR.Text = $"$ {monto:N2}";
            lblEstadoQR.Text = "Escanee el código con su celular";
            lblEstadoQR.Foreground = Brushes.White;
            GenerarQREnPantalla(dataQR);
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
