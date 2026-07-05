using System.Windows;
using System.Windows.Input;

namespace SchettiniGestion.WPF
{
    public partial class ModernInputWindow : Window
    {
        public string ResponseText { get; private set; }
        public string ResultText => ResponseText;

        public ModernInputWindow(string titulo) : this(titulo, titulo) { }

        public ModernInputWindow(string titulo, string etiqueta, string valorInicial = "")
        {
            InitializeComponent();
            Title = titulo;
            lblTitulo.Text = titulo;
            lblEtiqueta.Text = etiqueta;
            txtValor.Text = valorInicial ?? string.Empty;
            Loaded += (_, __) =>
            {
                txtValor.Focus();
                txtValor.SelectAll();
            };
        }

        private void btnAceptar_Click(object sender, RoutedEventArgs e) => Confirmar();

        private void btnCancelar_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }

        private void txtValor_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter)
            {
                Confirmar();
                e.Handled = true;
            }
            else if (e.Key == Key.Escape)
            {
                DialogResult = false;
                Close();
                e.Handled = true;
            }
        }

        private void Confirmar()
        {
            ResponseText = txtValor.Text;
            DialogResult = true;
            Close();
        }
    }
}
