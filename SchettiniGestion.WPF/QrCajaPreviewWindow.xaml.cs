using Microsoft.Win32;
using System;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace SchettiniGestion.WPF
{
    public partial class QrCajaPreviewWindow : Window
    {
        private readonly byte[] _png;

        public QrCajaPreviewWindow(byte[] png, string posId)
        {
            InitializeComponent();
            _png = png ?? Array.Empty<byte>();
            if (!string.IsNullOrWhiteSpace(posId))
                lblAyuda.Text = $"Caja {posId}. Imprimí este código y dejalo fijo en el mostrador. Al cobrar con Mercado Pago, el cliente lo escanea y ve el monto de su compra.";
            CargarImagen();
        }

        private void CargarImagen()
        {
            if (_png == null || _png.Length == 0) return;
            try
            {
                var bi = new BitmapImage();
                using (var ms = new MemoryStream(_png))
                {
                    bi.BeginInit();
                    bi.CacheOption = BitmapCacheOption.OnLoad;
                    bi.StreamSource = ms;
                    bi.EndInit();
                    bi.Freeze();
                }
                imgQr.Source = bi;
            }
            catch { }
        }

        private void btnGuardar_Click(object sender, RoutedEventArgs e)
        {
            var dlg = new SaveFileDialog
            {
                Title = "Guardar QR de la caja",
                Filter = "Imagen PNG|*.png",
                FileName = "qr-caja-mercadopago.png"
            };
            if (dlg.ShowDialog(this) != true) return;
            try
            {
                File.WriteAllBytes(dlg.FileName, _png);
                CustomMessageBox.Show("Imagen guardada.");
            }
            catch (Exception ex)
            {
                CustomMessageBox.Show("No se pudo guardar: " + ex.Message, "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void btnImprimir_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var pd = new PrintDialog();
                if (pd.ShowDialog() != true) return;

                var visual = new DrawingVisual();
                using (var dc = visual.RenderOpen())
                {
                    var src = imgQr.Source as BitmapSource;
                    if (src == null) return;

                    double margen = 40;
                    double maxW = pd.PrintableAreaWidth - margen * 2;
                    double maxH = pd.PrintableAreaHeight - margen * 2 - 80;
                    double lado = Math.Min(Math.Min(maxW, maxH), 420);
                    double x = (pd.PrintableAreaWidth - lado) / 2;
                    double y = margen + 40;

                    var titulo = new FormattedText(
                        "Mercado Pago — QR de la caja",
                        System.Globalization.CultureInfo.CurrentCulture,
                        FlowDirection.LeftToRight,
                        new Typeface("Segoe UI"),
                        18,
                        Brushes.Black,
                        VisualTreeHelper.GetDpi(this).PixelsPerDip);
                    dc.DrawText(titulo, new Point((pd.PrintableAreaWidth - titulo.Width) / 2, margen));
                    dc.DrawImage(src, new Rect(x, y, lado, lado));

                    var pie = new FormattedText(
                        "Escaneá este código para pagar. El monto aparece en tu celular.",
                        System.Globalization.CultureInfo.CurrentCulture,
                        FlowDirection.LeftToRight,
                        new Typeface("Segoe UI"),
                        12,
                        Brushes.Gray,
                        VisualTreeHelper.GetDpi(this).PixelsPerDip);
                    dc.DrawText(pie, new Point((pd.PrintableAreaWidth - pie.Width) / 2, y + lado + 16));
                }

                pd.PrintVisual(visual, "QR caja Mercado Pago");
            }
            catch (Exception ex)
            {
                CustomMessageBox.Show("No se pudo imprimir: " + ex.Message, "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void btnCerrar_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }
    }
}
