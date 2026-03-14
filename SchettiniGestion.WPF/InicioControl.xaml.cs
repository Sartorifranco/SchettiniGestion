using System;
using System.Windows.Controls;
using SchettiniGestion;

namespace SchettiniGestion.WPF
{
    public partial class InicioControl : UserControl
    {
        public InicioControl()
        {
            InitializeComponent();
        }

        private void InicioControl_Loaded(object sender, System.Windows.RoutedEventArgs e)
        {
            CargarIndicadores();
        }

        private void CargarIndicadores()
        {
            try
            {
                decimal totalVentas = DatabaseService.GetTotalVentasHoy();
                int cantVentas = DatabaseService.GetCantidadVentasHoy();
                decimal ganancia = DatabaseService.GetRentabilidadHoy();
                decimal saldoCaja = DatabaseService.GetSaldoCaja();
                int productos = DatabaseService.GetCantidadProductos();

                lblVentasHoy.Text = totalVentas.ToString("C2");
                lblCantVentas.Text = $"{cantVentas} operaciones";
                lblGananciaHoy.Text = ganancia.ToString("C2");
                lblCaja.Text = saldoCaja.ToString("C2");
                lblProductos.Text = productos.ToString("N0");
            }
            catch { }
        }
    }
}