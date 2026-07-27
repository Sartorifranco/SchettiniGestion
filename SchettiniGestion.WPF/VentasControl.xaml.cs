using System;
using System.Data;
using System.Windows;
using System.Windows.Controls;
using SchettiniGestion;

namespace SchettiniGestion.WPF
{
    public partial class VentasControl : UserControl
    {
        public VentasControl()
        {
            InitializeComponent();
        }

        private void VentasControl_Loaded(object sender, RoutedEventArgs e)
        {
            // Por defecto mostramos el mes actual
            EstablecerFechas("Mes");
        }

        // --- FILTROS RÁPIDOS ---
        private void btnFiltroRapido_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button btn && btn.Tag != null)
            {
                string opcion = btn.Tag.ToString();
                EstablecerFechas(opcion);
            }
        }

        private void EstablecerFechas(string opcion)
        {
            DateTime hoy = DateTime.Today;

            switch (opcion)
            {
                case "Hoy":
                    dtpDesde.SelectedDate = hoy;
                    dtpHasta.SelectedDate = hoy.AddDays(1).AddSeconds(-1);
                    break;
                case "Ayer":
                    dtpDesde.SelectedDate = hoy.AddDays(-1);
                    dtpHasta.SelectedDate = hoy.AddDays(-1);
                    break;
                case "Semana":
                    dtpDesde.SelectedDate = hoy.AddDays(-7);
                    dtpHasta.SelectedDate = hoy;
                    break;
                case "Mes":
                    dtpDesde.SelectedDate = new DateTime(hoy.Year, hoy.Month, 1);
                    dtpHasta.SelectedDate = dtpDesde.SelectedDate.Value.AddMonths(1).AddDays(-1);
                    break;
                case "Todo":
                    dtpDesde.SelectedDate = new DateTime(2020, 1, 1);
                    dtpHasta.SelectedDate = hoy;
                    break;
            }
            CargarVentas();
        }

        private void btnBuscar_Click(object sender, RoutedEventArgs e)
        {
            CargarVentas();
        }

        private void CargarVentas()
        {
            if (dtpDesde.SelectedDate == null || dtpHasta.SelectedDate == null) return;

            try
            {
                DateTime desde = dtpDesde.SelectedDate.Value;
                DateTime hasta = dtpHasta.SelectedDate.Value.Date.AddDays(1).AddSeconds(-1);

                DataTable dt = DatabaseService.GetFacturasPorFecha(desde, hasta);
                dgvVentas.ItemsSource = dt.DefaultView;

                decimal total = 0;
                foreach (DataRow row in dt.Rows)
                {
                    total += Convert.ToDecimal(row["Total"]);
                }
                lblTotalPeriodo.Text = total.ToString("C2");
            }
            catch (Exception ex)
            {
                MessageBox.Show("Error al cargar ventas: " + ex.Message);
            }
        }

        // --- ABRIR DETALLE MODERNO ---
        private void btnVerDetalle_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button btn && btn.DataContext is DataRowView row)
            {
                int idFactura = Convert.ToInt32(row["FacturaID"]);
                string cliente = row["RazonSocial"].ToString();

                try
                {
                    // Abrimos la ventana de detalle (asumiendo que existe en tu proyecto)
                    DetalleVentaWindow detalle = new DetalleVentaWindow(idFactura, cliente);
                    detalle.ShowDialog();
                }
                catch (Exception ex)
                {
                    MessageBox.Show("No se pudo abrir el detalle: " + ex.Message);
                }
            }
        }

        private void btnGenerarNotaCredito_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button btn && btn.DataContext is DataRowView row)
            {
                int idFactura = Convert.ToInt32(row["FacturaID"]);
                try
                {
                    var win = new NotaCreditoVentaWindow(idFactura)
                    {
                        Owner = Window.GetWindow(this)
                    };
                    if (win.ShowDialog() == true)
                        CargarVentas();
                }
                catch (Exception ex)
                {
                    ModernMessageBox.Show("No se pudo generar la nota de crédito: " + ex.Message, "Nota de Crédito", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }
    }
}