using System;
using System.Data;
using System.Windows;
using System.Windows.Controls;
using SchettiniGestion;

namespace SchettiniGestion.WPF
{
    public partial class CobranzasControl : UserControl
    {
        public CobranzasControl() { InitializeComponent(); }
        public CobranzasControl(object param) : this() { }

        private void Control_Loaded(object sender, RoutedEventArgs e)
        {
            dpDesde.SelectedDate = DateTime.Today;
            dpHasta.SelectedDate = DateTime.Today;
            CargarDatos();
        }

        private void btnActualizar_Click(object sender, RoutedEventArgs e) => CargarDatos();

        private void CargarDatos()
        {
            try
            {
                DateTime desde = dpDesde.SelectedDate ?? DateTime.Today;
                DateTime hasta = dpHasta.SelectedDate ?? DateTime.Today;
                if (hasta < desde)
                {
                    CustomMessageBox.Show("La fecha «Hasta» no puede ser anterior a «Desde».", "Fechas",
                        MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                string estado = (cmbEstado.SelectedItem as ComboBoxItem)?.Content?.ToString() ?? "Todos";
                DataTable dt = DatabaseService.GetFacturasEstadoCobranza(desde, hasta, estado);
                dgvCobranzas.ItemsSource = dt.DefaultView;
            }
            catch (Exception ex)
            {
                CustomMessageBox.Show("Error al cargar cobranzas: " + ex.Message, "Error",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
    }
}
