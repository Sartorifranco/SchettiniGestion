using System;
using System.Data;
using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using SchettiniGestion;

namespace SchettiniGestion.WPF
{
    public partial class InicioControl : UserControl
    {
        private static readonly CultureInfo CulturaAr = CultureInfo.GetCultureInfo("es-AR");
        private bool _inicializado;

        public InicioControl()
        {
            InitializeComponent();
        }

        private void InicioControl_Loaded(object sender, RoutedEventArgs e)
        {
            if (_inicializado) return;
            _inicializado = true;
            CargarIndicadores();
        }

        private void btnActualizar_Click(object sender, RoutedEventArgs e) => CargarIndicadores();

        private void CargarIndicadores()
        {
            try
            {
                DateTime hoy = DateTime.Today;
                DateTime finHoy = hoy.AddDays(1).AddSeconds(-1);
                DateTime ayer = hoy.AddDays(-1);
                DateTime finAyer = hoy.AddSeconds(-1);

                lblFechaHoy.Text = hoy.ToString("dddd d 'de' MMMM yyyy", CulturaAr);

                var resumenHoy = DatabaseService.GetResumenVentasPeriodo(hoy, finHoy);
                var resumenAyer = DatabaseService.GetResumenVentasPeriodo(ayer, finAyer);

                lblVentasHoy.Text = resumenHoy.TotalVendido.ToString("C2", CulturaAr);
                lblCantVentas.Text = resumenHoy.CantidadComprobantes.ToString("N0", CulturaAr);
                lblTicketPromedio.Text = "Ticket prom. " + resumenHoy.TicketPromedio.ToString("C2", CulturaAr);
                lblGananciaHoy.Text = resumenHoy.MargenEstimado.ToString("C2", CulturaAr);
                lblMargenPct.Text = string.Format("Margen {0:N1}%", resumenHoy.MargenPct);

                if (resumenAyer.TotalVendido > 0)
                {
                    decimal varPct = ((resumenHoy.TotalVendido - resumenAyer.TotalVendido) / resumenAyer.TotalVendido) * 100m;
                    string signo = varPct >= 0 ? "+" : "";
                    lblVsAyer.Text = string.Format("vs ayer: {0}{1:N1}% ({2:C0})", signo, varPct, resumenAyer.TotalVendido);
                    lblVsAyer.Foreground = varPct >= 0
                        ? (Brush)FindResource("SuccessColor")
                        : (Brush)FindResource("DangerColor");
                }
                else if (resumenHoy.TotalVendido > 0)
                {
                    lblVsAyer.Text = "vs ayer: sin ventas ayer";
                    lblVsAyer.Foreground = (Brush)FindResource("TextSecondary");
                }
                else
                {
                    lblVsAyer.Text = "vs ayer: —";
                    lblVsAyer.Foreground = (Brush)FindResource("TextSecondary");
                }

                lblCaja.Text = DatabaseService.GetSaldoCaja().ToString("C2", CulturaAr);
                lblProductos.Text = DatabaseService.GetCantidadProductos().ToString("N0", CulturaAr);
                lblClientes.Text = DatabaseService.GetCantidadClientes().ToString("N0", CulturaAr);

                int stockBajo = DatabaseService.GetCantidadProductosStockBajo();
                lblStockBajo.Text = stockBajo.ToString("N0", CulturaAr);

                DataTable top = DatabaseService.GetTopProductosVentas(hoy, finHoy, 5);
                dgvTopHoy.ItemsSource = top.DefaultView;
                bool sinTop = top.Rows.Count == 0;
                lblSinTopHoy.Visibility = sinTop ? Visibility.Visible : Visibility.Collapsed;
                dgvTopHoy.Visibility = sinTop ? Visibility.Collapsed : Visibility.Visible;

                DataTable medios = DatabaseService.GetVentasPorMedioPago(hoy, finHoy);
                dgvMediosHoy.ItemsSource = medios.DefaultView;
                bool sinMedios = medios.Rows.Count == 0;
                lblSinMediosHoy.Visibility = sinMedios ? Visibility.Visible : Visibility.Collapsed;
                dgvMediosHoy.Visibility = sinMedios ? Visibility.Collapsed : Visibility.Visible;

                DataTable alertas = DatabaseService.GetProductosStockBajo(10);
                dgvStockBajo.ItemsSource = alertas.DefaultView;
                bool sinAlertas = alertas.Rows.Count == 0;
                lblSinStockBajo.Visibility = sinAlertas ? Visibility.Visible : Visibility.Collapsed;
                dgvStockBajo.Visibility = sinAlertas ? Visibility.Collapsed : Visibility.Visible;
            }
            catch { }
        }
    }
}
