using System.Windows;

namespace SchettiniGestion.WPF
{
    public partial class CierreCajaConfirmacionWindow : Window
    {
        public bool Confirmado { get; private set; }

        public CierreCajaConfirmacionWindow(decimal saldoApertura, decimal ingresos, decimal egresos, decimal saldoCierre)
        {
            InitializeComponent();
            lblSaldoApertura.Text = saldoApertura.ToString("C2");
            lblIngresos.Text = ingresos.ToString("C2");
            lblEgresos.Text = egresos.ToString("C2");
            lblSaldoCierre.Text = saldoCierre.ToString("C2");
        }

        public static bool Mostrar(decimal saldoApertura, decimal ingresos, decimal egresos, decimal saldoCierre, Window owner = null)
        {
            var win = new CierreCajaConfirmacionWindow(saldoApertura, ingresos, egresos, saldoCierre);
            if (owner != null)
            {
                win.Owner = owner;
                win.WindowStartupLocation = WindowStartupLocation.CenterOwner;
            }
            win.ShowDialog();
            return win.Confirmado;
        }

        private void btnConfirmar_Click(object sender, RoutedEventArgs e)
        {
            Confirmado = true;
            Close();
        }

        private void btnCancelar_Click(object sender, RoutedEventArgs e)
        {
            Confirmado = false;
            Close();
        }
    }
}
