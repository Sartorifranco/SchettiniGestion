using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
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

        private static readonly string[] _extPromoImagen = { ".jpg", ".jpeg", ".png", ".bmp" };
        private static readonly string[] _extPromoMedia = { ".gif", ".mp4", ".wmv", ".mpeg", ".mpg", ".avi" };

        private readonly List<string> _rutasPromoVertical = new List<string>();
        private readonly List<string> _rutasPromoHorizontal = new List<string>();

        private DispatcherTimer _timerPromoVertical;
        private DispatcherTimer _timerPromoHorizontal;
        private int _indicePromoVertical;
        private int _indicePromoHorizontal;
        private bool _carruselVerticalEsVideo;
        private bool _carruselHorizontalEsVideo;

        public VisorClienteWindow()
        {
            InitializeComponent();
            Closed += (_, __) => DetenerTodasPromociones();
            SizeChanged += (_, __) => AjustarLayoutPromociones();
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

            foreach (string ruta in LeerRutasPromocionesDesdeConfig())
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

            if (_rutasPromoHorizontal.Count > 0)
            {
                _indicePromoHorizontal = 0;
                MostrarPromoHorizontalActual();
            }
        }

        private void AjustarLayoutPromociones()
        {
            if (_rutasPromoVertical.Count > 0)
            {
                double h = ActualHeight > 100 ? ActualHeight : 768;
                double anchoIdeal = h * (1080.0 / 1440.0);
                double maxAncho = ActualWidth > 100 ? ActualWidth * 0.42 : 480;
                double ancho = Math.Min(Math.Max(anchoIdeal, 260), maxAncho);
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
                double alto = ActualHeight > 100 ? Math.Min(ActualHeight * 0.22, 220) : 160;
                alto = Math.Max(alto, 110);
                rowPromoInferior.Height = new GridLength(alto);
                panelPromoInferior.Visibility = Visibility.Visible;
            }
            else
            {
                rowPromoInferior.Height = new GridLength(0);
                panelPromoInferior.Visibility = Visibility.Collapsed;
            }
        }

        private static List<string> LeerRutasPromocionesDesdeConfig()
        {
            var list = new List<string>();
            try
            {
                var cfg = DatabaseService.GetConfiguracion();
                if (cfg == null || !cfg.Table.Columns.Contains("VisorPromoCarpeta")) return list;
                string dir = cfg["VisorPromoCarpeta"]?.ToString()?.Trim();
                if (string.IsNullOrEmpty(dir) || !Directory.Exists(dir)) return list;

                foreach (var f in Directory.GetFiles(dir))
                {
                    string ex = Path.GetExtension(f).ToLowerInvariant();
                    if (_extPromoImagen.Contains(ex) || _extPromoMedia.Contains(ex))
                        list.Add(f);
                }
                list.Sort(StringComparer.OrdinalIgnoreCase);
            }
            catch { }
            return list;
        }

        private static bool EsPromoVertical(string path)
        {
            string ext = Path.GetExtension(path).ToLowerInvariant();
            if (_extPromoMedia.Contains(ext))
                return true;

            try
            {
                var bi = new BitmapImage();
                bi.BeginInit();
                bi.UriSource = new Uri(path);
                bi.CacheOption = BitmapCacheOption.OnLoad;
                bi.EndInit();
                return bi.PixelHeight >= bi.PixelWidth;
            }
            catch
            {
                return true;
            }
        }

        private int LeerIntervaloPromoSeg()
        {
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

        private bool MostrarArchivoPromo(string path, System.Windows.Controls.Image img, MediaElement media)
        {
            string ext = Path.GetExtension(path).ToLowerInvariant();

            try { media.Stop(); } catch { }
            media.Source = null;
            media.Visibility = Visibility.Collapsed;
            img.Visibility = Visibility.Collapsed;
            img.Source = null;

            if (_extPromoMedia.Contains(ext))
            {
                try
                {
                    media.Source = new Uri(path);
                    media.Visibility = Visibility.Visible;
                    media.Play();
                    return true;
                }
                catch { }
            }

            try
            {
                var bi = new BitmapImage();
                bi.BeginInit();
                bi.CacheOption = BitmapCacheOption.OnLoad;
                bi.UriSource = new Uri(path);
                bi.EndInit();
                bi.Freeze();
                img.Source = bi;
                img.Visibility = Visibility.Visible;
            }
            catch { }

            return false;
        }

        private void AvanzarPromoVertical()
        {
            if (_rutasPromoVertical.Count == 0) return;
            _indicePromoVertical = (_indicePromoVertical + 1) % _rutasPromoVertical.Count;
            MostrarPromoVerticalActual();
        }

        private void AvanzarPromoHorizontal()
        {
            if (_rutasPromoHorizontal.Count == 0) return;
            _indicePromoHorizontal = (_indicePromoHorizontal + 1) % _rutasPromoHorizontal.Count;
            MostrarPromoHorizontalActual();
        }

        private void ReiniciarTimerPromoVertical()
        {
            DetenerTimerPromoVertical();
            if (_rutasPromoVertical.Count <= 1) return;
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
            if (_rutasPromoHorizontal.Count <= 1) return;
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
            try
            {
                mediaPromo.Stop();
                mediaPromo.Source = null;
                mediaPromoHorizontal.Stop();
                mediaPromoHorizontal.Source = null;
            }
            catch { }
            imgPromo.Source = null;
            imgPromoHorizontal.Source = null;
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
            if (_rutasPromoVertical.Count == 0 && _rutasPromoHorizontal.Count == 0)
                IniciarPromociones();
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

        public void MostrarGracias()
        {
            OcultarPanelesContenido();
            GridGracias.Visibility = Visibility.Visible;
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
