using System.Windows;

namespace SchettiniGestion.WPF
{
    public partial class CajaMovimientoModalWindow : Window
    {
        public decimal Monto => numMonto.Value ?? 0;
        public string Concepto => txtConcepto.Text?.Trim() ?? "";

        public CajaMovimientoModalWindow(string titulo, string textoBotonGuardar = "Guardar")
        {
            InitializeComponent();
            lblTitulo.Text = titulo;
            btnGuardar.Content = textoBotonGuardar;
            numMonto.CultureInfo = AppCulture.Argentine;
            KeyboardHelper.AttachTouchKeyboardOnPointer(this);
        }

        private void btnTeclado_Click(object sender, RoutedEventArgs e)
        {
            KeyboardHelper.ShowOnScreenKeyboard();
        }

        private void btnCancelar_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }

        private void btnGuardar_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = true;
            Close();
        }
    }
}
