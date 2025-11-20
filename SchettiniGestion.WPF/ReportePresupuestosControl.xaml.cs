using SchettiniGestion;
using System;
using System.Data;
using System.Windows;
using System.Windows.Controls;

namespace SchettiniGestion.WPF
{
    public partial class ReportePresupuestosControl : UserControl
    {
        public ReportePresupuestosControl()
        {
            InitializeComponent();
        }

        private void UserControl_Loaded(object sender, RoutedEventArgs e)
        {
            // Fechas por defecto: Hoy
            dpDesde.SelectedDate = DateTime.Today;
            dpHasta.SelectedDate = DateTime.Today;
            CargarPresupuestos();
        }

        private void btnBuscar_Click(object sender, RoutedEventArgs e)
        {
            CargarPresupuestos();
        }

        private void CargarPresupuestos()
        {
            try
            {
                DateTime desde = dpDesde.SelectedDate ?? DateTime.Today;
                DateTime hasta = dpHasta.SelectedDate ?? DateTime.Today;

                // Traemos los datos usando el método que ya existe en DatabaseService
                DataTable dt = DatabaseService.GetPresupuestos(desde, hasta);
                dgvPresupuestos.ItemsSource = dt.DefaultView;

                // Limpiar detalle
                dgvDetalle.ItemsSource = null;
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error al cargar historial: {ex.Message}");
            }
        }

        private void dgvPresupuestos_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (dgvPresupuestos.SelectedItem is DataRowView row)
            {
                int id = Convert.ToInt32(row["PresupuestoID"]);
                CargarDetalle(id);
            }
        }

        private void CargarDetalle(int presupuestoID)
        {
            try
            {
                DataTable dt = DatabaseService.GetPresupuestoDetalle(presupuestoID);
                dgvDetalle.ItemsSource = dt.DefaultView;
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error al cargar detalle: {ex.Message}");
            }
        }

        private void btnEliminar_Click(object sender, RoutedEventArgs e)
        {
            if (dgvPresupuestos.SelectedItem is DataRowView row)
            {
                if (MessageBox.Show("¿Eliminar este presupuesto permanentemente?", "Confirmar", MessageBoxButton.YesNo, MessageBoxImage.Warning) == MessageBoxResult.Yes)
                {
                    int id = Convert.ToInt32(row["PresupuestoID"]);
                    if (DatabaseService.EliminarPresupuesto(id))
                    {
                        MessageBox.Show("Eliminado correctamente.");
                        CargarPresupuestos(); // Recargar lista
                    }
                }
            }
            else
            {
                MessageBox.Show("Seleccione un presupuesto para eliminar.");
            }
        }

        private void btnImprimir_Click(object sender, RoutedEventArgs e)
        {
            if (dgvPresupuestos.SelectedItem == null)
            {
                MessageBox.Show("Seleccione un presupuesto para imprimir.");
                return;
            }

            // AQUÍ IRÁ LA LÓGICA DE IMPRESIÓN EN EL FUTURO
            MessageBox.Show("Funcionalidad de impresión pendiente de configurar (Drivers Fiscales/No Fiscales).", "Próximamente", MessageBoxButton.OK, MessageBoxImage.Information);
        }
    }
}