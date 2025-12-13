using SchettiniGestion;
using System;
using System.Windows;
using System.Windows.Controls;

namespace SchettiniGestion.WPF
{
    public partial class DashboardControl : UserControl
    {
        public DashboardControl()
        {
            InitializeComponent();
        }

        private void UserControl_Loaded(object sender, RoutedEventArgs e)
        {
            lblUsuario.Text = " " + SesionUsuario.NombreUsuario + "!";
            CargarDatos();
        }

        private void btnActualizar_Click(object sender, RoutedEventArgs e)
        {
            CargarDatos();
        }

        private void CargarDatos()
        {
            try
            {
                lblCantVentas.Text = DatabaseService.GetCantidadVentasHoy().ToString();
                lblTotalVentas.Text = DatabaseService.GetTotalVentasHoy().ToString("C2");
                lblRentabilidad.Text = DatabaseService.GetRentabilidadHoy().ToString("C2");

                lblProductos.Text = DatabaseService.GetCantidadProductos().ToString();
                lblClientes.Text = DatabaseService.GetCantidadClientes().ToString();
            }
            catch { }
        }
    }
}