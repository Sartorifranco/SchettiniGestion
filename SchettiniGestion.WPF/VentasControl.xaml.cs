using System;
using System.Data;
using System.Windows;
using System.Windows.Controls;
// Importamos la lógica de nuestro otro proyecto
using SchettiniGestion;

namespace SchettiniGestion.WPF
{
    /// <summary>
    /// Lógica de interacción para VentasControl.xaml
    /// </summary>
    public partial class VentasControl : UserControl
    {
        public VentasControl()
        {
            InitializeComponent();
        }

        private void VentasControl_Loaded(object sender, RoutedEventArgs e)
        {
            // Establecemos las fechas por defecto (hoy)
            dpDesde.SelectedDate = DateTime.Today;
            dpHasta.SelectedDate = DateTime.Today.AddDays(1).AddSeconds(-1); // Fin del día
        }

        private void btnBuscarVentas_Click(object sender, RoutedEventArgs e)
        {
            // Validamos las fechas
            if (dpDesde.SelectedDate == null || dpHasta.SelectedDate == null)
            {
                MessageBox.Show("Por favor, seleccione un rango de fechas.", "Error", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            DateTime fechaDesde = dpDesde.SelectedDate.Value;
            DateTime fechaHasta = dpHasta.SelectedDate.Value;

            if (fechaHasta < fechaDesde)
            {
                MessageBox.Show("La 'Fecha Hasta' no puede ser anterior a la 'Fecha Desde'.", "Error", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            // 1. Buscamos las facturas (encabezados)
            DataTable dtFacturas = DatabaseService.GetFacturasPorFecha(fechaDesde, fechaHasta);
            dgvFacturas.ItemsSource = dtFacturas.DefaultView;

            // 2. Limpiamos la grilla de detalle
            dgvFacturaDetalle.ItemsSource = null;
        }

        private void dgvFacturas_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            // Verificamos que haya una fila seleccionada
            if (dgvFacturas.SelectedItem is DataRowView filaSeleccionada)
            {
                // Obtenemos el ID de la factura
                int facturaID = Convert.ToInt32(filaSeleccionada["FacturaID"]);

                // Buscamos el detalle de esa factura
                DataTable dtDetalle = DatabaseService.GetFacturaDetalle(facturaID);
                dgvFacturaDetalle.ItemsSource = dtDetalle.DefaultView;
            }
            else
            {
                // Si no hay nada seleccionado (ej: después de buscar), limpiamos el detalle
                dgvFacturaDetalle.ItemsSource = null;
            }
        }
    }
}