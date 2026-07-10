using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Windows;
using System.Windows.Input;

namespace SchettiniGestion.WPF
{
    public partial class ResultadoImportacionWindow : Window
    {
        public ResultadoImportacionWindow(int creados, int actualizados, int errores = 0)
            : this(creados, actualizados, errores, 0, null, false)
        {
        }

        public ResultadoImportacionWindow(
            int creados,
            int actualizados,
            int errores,
            int sinCambios,
            IEnumerable<string> detalleErrores,
            bool esFalloTotal)
        {
            InitializeComponent();

            lblCreados.Text = creados.ToString();
            lblActualizados.Text = actualizados.ToString();
            lblErrores.Text = errores.ToString();

            if (sinCambios > 0)
            {
                pnlSinCambios.Visibility = Visibility.Visible;
                lblSinCambios.Text = sinCambios.ToString();
            }

            if (creados == 0 && !esFalloTotal)
                pnlFilaCreados.Visibility = Visibility.Collapsed;

            if (errores > 0)
                pnlErrores.Visibility = Visibility.Visible;

            if (esFalloTotal)
            {
                lblTitulo.Text = "Error en importación";
                lblTitulo.Foreground = (System.Windows.Media.Brush)FindResource("DangerColor");
                pnlResumenExitoso.Visibility = Visibility.Collapsed;
                pnlErrorFatal.Visibility = Visibility.Visible;

                if (detalleErrores != null)
                {
                    var sb = new StringBuilder();
                    sb.AppendLine("La transacción fue revertida. No se guardó ningún cambio.");
                    sb.AppendLine();
                    foreach (var linea in detalleErrores.Take(10))
                        sb.AppendLine("• " + linea);
                    txtErrorFatal.Text = sb.ToString().TrimEnd();
                }
            }
            else if (detalleErrores != null && detalleErrores.Any())
            {
                pnlDetalleErrores.Visibility = Visibility.Visible;
                txtDetalleErrores.Text = string.Join("\n", detalleErrores.Take(8).Select(e => "• " + e));
            }
        }

        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            btnAceptar.Focus();
            Keyboard.Focus(btnAceptar);
        }

        private void Window_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter || e.Key == Key.Escape)
            {
                btnAceptar_Click(btnAceptar, new RoutedEventArgs());
                e.Handled = true;
            }
        }

        private void btnAceptar_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = true;
            Close();
        }
    }
}
