using System.Windows;
using System.Windows.Input;

namespace SchettiniGestion.WPF
{
    public partial class ModernInputWindow : Window
    {
        public string ResultText { get; private set; }
        public string ResponseText => ResultText;

        public ModernInputWindow(string titulo) : this(titulo, titulo, "") { }

        public ModernInputWindow(string titulo, string etiqueta, string valorInicial = "")
        {
            InitializeComponent();
            lblTitulo.Text = titulo;
            lblPrompt.Text = etiqueta;
            txtInput.Text = valorInicial;

            Loaded += (s, e) =>
            {
                txtInput.Focus();
                txtInput.SelectAll();
            };

            KeyDown += (s, e) =>
            {
                if (e.Key == Key.Escape) { DialogResult = false; }
            };
        }

        private void btnAceptar_Click(object sender, RoutedEventArgs e)
        {
            ResultText = txtInput.Text;
            DialogResult = true;
        }

        private void btnCancelar_Click(object sender, RoutedEventArgs e)
            => DialogResult = false;

        private void btnTeclado_Click(object sender, RoutedEventArgs e)
            => KeyboardHelper.ShowOnScreenKeyboard();
    }
}
