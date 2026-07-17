using System;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Media;
using System.Windows.Threading;

namespace SchettiniGestion.WPF
{
    public partial class PointCobroWindow : Window
    {
        private readonly decimal _total;
        private readonly string _referencia;
        private DispatcherTimer _timer;
        private bool _consultando;
        private bool _aprobado;
        private string _ordenId = "";

        public EstadoPagoPoint PagoAprobado { get; private set; }

        public PointCobroWindow(Window owner, decimal total)
        {
            Owner = owner;
            _total = total;
            _referencia = "SchPoint_" + DateTime.Now.Ticks;
            InitializeComponent();
            Loaded += PointCobroWindow_Loaded;
            Closing += PointCobroWindow_Closing;
        }

        private async void PointCobroWindow_Loaded(object sender, RoutedEventArgs e)
        {
            lblMonto.Text = _total.ToString("C2");
            CustomerScreenService.PantallaPoint(_total, "Enviando el importe al Point...");

            var respuesta = await MercadoPagoPointService.CrearOrden(_total, _referencia, "Compra SCHPOS");
            if (!respuesta.Exito)
            {
                lblEstado.Text = respuesta.Error;
                lblEstado.Foreground = Brushes.OrangeRed;
                prgEspera.Visibility = Visibility.Collapsed;
                btnCancelar.Content = "Cerrar";
                CustomerScreenService.ActualizarEstadoPoint("No se pudo iniciar el cobro.", Brushes.OrangeRed);
                return;
            }

            _ordenId = respuesta.OrdenId;
            lblEstado.Text = "Importe enviado. Complete el pago en el Point.";
            CustomerScreenService.ActualizarEstadoPoint("Complete el pago en la terminal", Brushes.White);

            _timer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(2) };
            _timer.Tick += Timer_Tick;
            _timer.Start();
        }

        private async void Timer_Tick(object sender, EventArgs e)
        {
            if (_consultando || string.IsNullOrWhiteSpace(_ordenId)) return;
            _consultando = true;
            try
            {
                EstadoPagoPoint estado = await MercadoPagoPointService.ConsultarEstado(_ordenId);
                if (estado.Estado == "approved")
                {
                    _timer?.Stop();
                    _aprobado = true;
                    PagoAprobado = estado;
                    lblEstado.Text = "¡Pago aprobado!";
                    lblEstado.Foreground = Brushes.LimeGreen;
                    prgEspera.Visibility = Visibility.Collapsed;
                    CustomerScreenService.PantallaGracias();
                    await Task.Delay(1500);
                    DialogResult = true;
                    Close();
                }
                else if (estado.Estado == "in_process")
                {
                    lblEstado.Text = estado.EstadoDetalle == "action_required"
                        ? "Revise y confirme el resultado en la terminal."
                        : "Operación en curso en el Point...";
                    CustomerScreenService.ActualizarEstadoPoint(lblEstado.Text, Brushes.White);
                }
                else if (estado.Estado == "rejected")
                {
                    _timer?.Stop();
                    lblEstado.Text = TraducirRechazo(estado.EstadoDetalle);
                    lblEstado.Foreground = Brushes.OrangeRed;
                    prgEspera.Visibility = Visibility.Collapsed;
                    btnCancelar.Content = "Cerrar";
                    CustomerScreenService.ActualizarEstadoPoint(lblEstado.Text, Brushes.OrangeRed);
                }
            }
            finally
            {
                _consultando = false;
            }
        }

        private async void btnCancelar_Click(object sender, RoutedEventArgs e)
        {
            btnCancelar.IsEnabled = false;
            _timer?.Stop();
            if (!_aprobado && !string.IsNullOrWhiteSpace(_ordenId))
                await MercadoPagoPointService.CancelarOrden(_ordenId);
            DialogResult = false;
            Close();
        }

        private void PointCobroWindow_Closing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            _timer?.Stop();
            if (!_aprobado && !string.IsNullOrWhiteSpace(_ordenId))
                _ = MercadoPagoPointService.CancelarOrden(_ordenId);
        }

        private static string TraducirRechazo(string detalle)
        {
            switch (detalle)
            {
                case "insufficient_amount": return "Fondos o límite insuficiente.";
                case "card_disabled": return "La tarjeta está inhabilitada.";
                case "invalid_installments": return "La cantidad de cuotas no es válida.";
                case "required_call_for_authorize": return "La tarjeta requiere autorización.";
                case "high_risk": return "Mercado Pago rechazó la operación por seguridad.";
                case "canceled":
                case "canceled_on_terminal": return "El cobro fue cancelado.";
                default: return "Pago rechazado. Revise el detalle en la terminal.";
            }
        }
    }
}
