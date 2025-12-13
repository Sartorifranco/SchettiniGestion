using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media.Imaging;
using QRCoder;
using System.IO;
using System.Drawing;
using SchettiniGestion; // Para poder usar FacturaItem

namespace SchettiniGestion.WPF
{
    public partial class VisorClienteWindow : Window
    {
        public event Action<string> OnOpcionSeleccionada;

        public VisorClienteWindow()
        {
            InitializeComponent();
        }

        // --- ESTE ES EL MÉTODO QUE EL COMPILADOR NO ENCONTRABA ---
        public void ActualizarGrilla(List<FacturaItem> items, decimal total)
        {
            OcultarTodo();
            GridVenta.Visibility = Visibility.Visible;

            dgvDetalleCliente.ItemsSource = null; // Limpiar para refrescar
            dgvDetalleCliente.ItemsSource = items; // Asignar lista nueva

            // Auto-scroll al último ítem para que el cliente vea lo que se agrega
            if (items.Count > 0)
            {
                dgvDetalleCliente.ScrollIntoView(items[items.Count - 1]);
            }

            lblTotal.Text = $"$ {total:N2}";
        }
        // ---------------------------------------------------------

        // --- ESTE MÉTODO TAMBIÉN DABA ERROR PORQUE BUSCABA LABELS VIEJOS ---
        public void Reiniciar()
        {
            OcultarTodo();
            GridVenta.Visibility = Visibility.Visible;
            dgvDetalleCliente.ItemsSource = null; // Limpiamos la grilla
            lblTotal.Text = "$ 0.00";
        }
        // -------------------------------------------------------------------

        public void MostrarSeleccionPago()
        {
            OcultarTodo();
            GridPago.Visibility = Visibility.Visible;
        }

        public void MostrarQR(decimal monto)
        {
            OcultarTodo();
            GridQR.Visibility = Visibility.Visible;
            lblTotalQR.Text = $"$ {monto:N2}";
            string dataMP = $"https://mercadopago.com.ar/pagar?monto={monto}";
            GenerarQREnPantalla(dataMP);
        }

        public void MostrarGracias()
        {
            OcultarTodo();
            GridGracias.Visibility = Visibility.Visible;
        }

        // Métodos internos
        private void OcultarTodo()
        {
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
                Bitmap qrBmp = qrCode.GetGraphic(20);
                using (MemoryStream ms = new MemoryStream())
                {
                    qrBmp.Save(ms, System.Drawing.Imaging.ImageFormat.Png);
                    ms.Position = 0;
                    BitmapImage bi = new BitmapImage();
                    bi.BeginInit(); bi.StreamSource = ms; bi.CacheOption = BitmapCacheOption.OnLoad; bi.EndInit();
                    imgQR.Source = bi;
                }
            }
            catch { }
        }
    }
}