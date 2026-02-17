using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using QRCoder;
using System.IO;
using System.Drawing;
using SchettiniGestion;

namespace SchettiniGestion.WPF
{
    public partial class VisorClienteWindow : Window
    {
        public event Action<string> OnOpcionSeleccionada;
        private DispatcherTimer _timerVueltaLogo;

        public VisorClienteWindow()
        {
            InitializeComponent();
            CargarLogo();
        }

        private void CargarLogo()
        {
            try
            {
                var config = DatabaseService.GetConfiguracion();
                if (config != null && config.Table.Columns.Contains("LogoPath") && config["LogoPath"] != DBNull.Value)
                {
                    string path = config["LogoPath"]?.ToString()?.Trim();
                    if (!string.IsNullOrEmpty(path) && File.Exists(path))
                    {
                        imgLogo.Source = new BitmapImage(new Uri(path, UriKind.Absolute));
                    }
                }
                if (config != null && config.Table.Columns.Contains("NombreFantasia") && config["NombreFantasia"] != DBNull.Value)
                {
                    string nom = config["NombreFantasia"]?.ToString()?.Trim();
                    if (!string.IsNullOrEmpty(nom)) txtNombreNegocio.Text = nom;
                }
            }
            catch { }
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

            // CORRECCION: Usamos System.Windows.Media.Brushes explícitamente
            lblEstadoQR.Foreground = System.Windows.Media.Brushes.White;

            GenerarQREnPantalla(dataQR);
        }

        // CORRECCION: El tipo de dato debe ser explícito
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

        private void BtnVolver_Click(object sender, RoutedEventArgs e) { MostrarSeleccionPago(); }

        private void GenerarQREnPantalla(string data)
        {
            try
            {
                QRCodeGenerator qrGen = new QRCodeGenerator();
                QRCodeData qrData = qrGen.CreateQrCode(data, QRCodeGenerator.ECCLevel.Q);
                QRCode qrCode = new QRCode(qrData);

                using (Bitmap qrBmp = qrCode.GetGraphic(20))
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