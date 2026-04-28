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

        private DispatcherTimer _timerVueltaLogo;



        private static readonly string[] _extPromoImagen = { ".jpg", ".jpeg", ".png", ".bmp" };

        private static readonly string[] _extPromoMedia = { ".gif", ".mp4", ".wmv", ".mpeg", ".mpg", ".avi" };



        private readonly List<string> _rutasPromo = new List<string>();

        private DispatcherTimer _timerPromo;

        private int _indicePromo;

        private bool _carruselEsVideo;



        public VisorClienteWindow()

        {

            InitializeComponent();

            Closed += (_, __) => DetenerPromocionesInterno();

            CargarLogo();

            IniciarPromociones();

        }



        private void CargarLogo()

        {

            try

            {

                var config = DatabaseService.GetConfiguracion();

                ImageSource src = null;

                if (config != null && config.Table.Columns.Contains("LogoPath") && config["LogoPath"] != DBNull.Value)

                {

                    string path = config["LogoPath"]?.ToString()?.Trim();

                    if (!string.IsNullOrEmpty(path))

                        src = SvgLogoHelper.LoadImageFromPath(path);

                }

                imgLogo.Source = src ?? SvgLogoHelper.LoadEmbeddedLogo();

                if (config != null && config.Table.Columns.Contains("NombreFantasia") && config["NombreFantasia"] != DBNull.Value)

                {

                    string nom = config["NombreFantasia"]?.ToString()?.Trim();

                    if (!string.IsNullOrEmpty(nom)) txtNombreNegocio.Text = nom;

                }

            }

            catch { }

        }



        private void IniciarPromociones()

        {

            DetenerPromocionesInterno();

            _rutasPromo.Clear();

            _rutasPromo.AddRange(LeerRutasPromocionesDesdeConfig());



            if (_rutasPromo.Count == 0)

            {

                borderPromoFondo.Visibility = Visibility.Collapsed;

                borderMarca.VerticalAlignment = VerticalAlignment.Center;

                borderMarca.HorizontalAlignment = HorizontalAlignment.Center;

                borderMarca.Background = System.Windows.Media.Brushes.Transparent;

                borderMarca.Margin = new Thickness(0);

                imgLogo.Height = 200;

                txtNombreNegocio.FontSize = 36;

                return;

            }



            borderPromoFondo.Visibility = Visibility.Visible;

            borderMarca.VerticalAlignment = VerticalAlignment.Bottom;

            borderMarca.HorizontalAlignment = HorizontalAlignment.Center;

            borderMarca.Background = new SolidColorBrush(System.Windows.Media.Color.FromArgb(210, 20, 20, 20));

            borderMarca.Margin = new Thickness(24, 0, 24, 32);

            imgLogo.Height = 120;

            txtNombreNegocio.FontSize = 28;

            _indicePromo = 0;

            MostrarPromoActual();

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



        private void MostrarPromoActual()

        {

            if (_rutasPromo.Count == 0) return;



            string path = _rutasPromo[_indicePromo % _rutasPromo.Count];

            string ext = Path.GetExtension(path).ToLowerInvariant();



            try { mediaPromo.Stop(); } catch { }

            mediaPromo.Source = null;

            mediaPromo.Visibility = Visibility.Collapsed;

            imgPromo.Visibility = Visibility.Collapsed;

            imgPromo.Source = null;



            if (_extPromoMedia.Contains(ext))

            {

                try

                {

                    mediaPromo.Source = new Uri(path);

                    mediaPromo.Visibility = Visibility.Visible;

                    mediaPromo.Play();

                    _carruselEsVideo = true;

                    DetenerTimerPromoSilencioso();

                    return;

                }

                catch { /* intentar como imagen estática */ }

            }



            try

            {

                var bi = new BitmapImage();

                bi.BeginInit();

                bi.CacheOption = BitmapCacheOption.OnLoad;

                bi.UriSource = new Uri(path);

                bi.EndInit();

                bi.Freeze();

                imgPromo.Source = bi;

                imgPromo.Visibility = Visibility.Visible;

            }

            catch { }



            _carruselEsVideo = false;

            ReiniciarTimerPromo();

        }



        private void AvanzarPromo()

        {

            if (_rutasPromo.Count == 0) return;

            _indicePromo = (_indicePromo + 1) % _rutasPromo.Count;

            MostrarPromoActual();

        }



        private void ReiniciarTimerPromo()

        {

            DetenerTimerPromoSilencioso();

            if (_rutasPromo.Count <= 1) return;

            int seg = LeerIntervaloPromoSeg();

            _timerPromo = new DispatcherTimer { Interval = TimeSpan.FromSeconds(seg) };

            _timerPromo.Tick += (_, __) =>

            {

                if (!_carruselEsVideo)

                    AvanzarPromo();

            };

            _timerPromo.Start();

        }



        private void DetenerTimerPromoSilencioso()

        {

            if (_timerPromo != null)

            {

                _timerPromo.Stop();

                _timerPromo = null;

            }

        }



        private void DetenerPromocionesInterno()

        {

            DetenerTimerPromoSilencioso();

            _carruselEsVideo = false;

            try

            {

                mediaPromo.Stop();

                mediaPromo.Source = null;

            }

            catch { }

            imgPromo.Source = null;

        }



        private void MediaPromo_MediaEnded(object sender, RoutedEventArgs e)

        {

            _carruselEsVideo = false;

            AvanzarPromo();

        }



        private void MediaPromo_MediaFailed(object sender, ExceptionRoutedEventArgs e)

        {

            _carruselEsVideo = false;

            AvanzarPromo();

        }



        public void ActualizarGrilla(List<FacturaItem> items, decimal total)

        {

            OcultarTodo();

            GridVenta.Visibility = Visibility.Visible;

            dgvDetalleCliente.ItemsSource = null;

            dgvDetalleCliente.ItemsSource = items;



            if (items.Count > 0) dgvDetalleCliente.ScrollIntoView(items[items.Count - 1]);

            lblTotal.Text = $"$ {total:N2}";

        }



        public void Reiniciar()

        {

            CancelarTimerVueltaLogo();

            OcultarTodo();

            GridLogo.Visibility = Visibility.Visible;

            dgvDetalleCliente.ItemsSource = null;

            lblTotal.Text = "$ 0.00";

            IniciarPromociones();

        }



        public void MostrarSeleccionPago()

        {

            OcultarTodo();

            GridPago.Visibility = Visibility.Visible;

        }



        public void MostrarQR(string dataQR, decimal monto)

        {

            OcultarTodo();

            GridQR.Visibility = Visibility.Visible;

            lblTotalQR.Text = $"$ {monto:N2}";

            lblEstadoQR.Text = "Escanee el código con su celular";



            lblEstadoQR.Foreground = System.Windows.Media.Brushes.White;



            GenerarQREnPantalla(dataQR);

        }



        public void ActualizarEstadoQR(string mensaje, System.Windows.Media.Brush color)

        {

            lblEstadoQR.Text = mensaje;

            lblEstadoQR.Foreground = color;

        }



        public void MostrarGracias()

        {

            OcultarTodo();

            GridGracias.Visibility = Visibility.Visible;

            ProgramarVueltaAlLogo();

        }



        private void ProgramarVueltaAlLogo()

        {

            CancelarTimerVueltaLogo();

            _timerVueltaLogo = new DispatcherTimer { Interval = TimeSpan.FromSeconds(4) };

            _timerVueltaLogo.Tick += (s, e) =>

            {

                CancelarTimerVueltaLogo();

                OcultarTodo();

                GridLogo.Visibility = Visibility.Visible;

                IniciarPromociones();

            };

            _timerVueltaLogo.Start();

        }



        private void CancelarTimerVueltaLogo()

        {

            _timerVueltaLogo?.Stop();

            _timerVueltaLogo = null;

        }



        private void OcultarTodo()

        {

            DetenerPromocionesInterno();

            GridLogo.Visibility = Visibility.Collapsed;

            GridVenta.Visibility = Visibility.Collapsed;

            GridPago.Visibility = Visibility.Collapsed;

            GridQR.Visibility = Visibility.Collapsed;

            GridGracias.Visibility = Visibility.Collapsed;

        }



        private void BtnPago_Click(object sender, RoutedEventArgs e)

        {

            if (sender is Button btn && btn.Tag != null) OnOpcionSeleccionada?.Invoke(btn.Tag.ToString());

        }



        private void GenerarQREnPantalla(string data)

        {

            try

            {

                QRCodeGenerator qrGen = new QRCodeGenerator();

                QRCodeData qrData = qrGen.CreateQrCode(data, QRCodeGenerator.ECCLevel.Q);

                QRCode qrCode = new QRCode(qrData);



                using (System.Drawing.Bitmap qrBmp = qrCode.GetGraphic(20))

                {

                    using (MemoryStream ms = new MemoryStream())

                    {

                        qrBmp.Save(ms, System.Drawing.Imaging.ImageFormat.Png);

                        ms.Position = 0;

                        BitmapImage bi = new BitmapImage();

                        bi.BeginInit(); bi.StreamSource = ms; bi.CacheOption = BitmapCacheOption.OnLoad; bi.EndInit();

                        imgQR.Source = bi;

                    }

                }

            }

            catch { }

        }

    }

}

