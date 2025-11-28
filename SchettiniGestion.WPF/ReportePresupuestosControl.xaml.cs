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
                CustomMessageBox.Show($"Error al cargar historial: {ex.Message}");
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
                CustomMessageBox.Show($"Error al cargar detalle: {ex.Message}");
            }
        }

        private void btnEliminar_Click(object sender, RoutedEventArgs e)
        {
            if (dgvPresupuestos.SelectedItem is DataRowView row)
            {
                if (CustomMessageBox.Show("¿Eliminar este presupuesto permanentemente?", "Confirmar", MessageBoxButton.YesNo, MessageBoxImage.Warning) == MessageBoxResult.Yes)
                {
                    int id = Convert.ToInt32(row["PresupuestoID"]);
                    if (DatabaseService.EliminarPresupuesto(id))
                    {
                        CustomMessageBox.Show("Eliminado correctamente.");
                        CargarPresupuestos(); // Recargar lista
                    }
                }
            }
            else
            {
                CustomMessageBox.Show("Seleccione un presupuesto para eliminar.");
            }
        }

        private void btnImprimir_Click(object sender, RoutedEventArgs e)
        {
            if (dgvPresupuestos.SelectedItem is DataRowView row)
            {
                // 1. Recuperar datos del presupuesto seleccionado
                int id = Convert.ToInt32(row["PresupuestoID"]);
                string cliente = row["RazonSocial"].ToString();
                DateTime fecha = Convert.ToDateTime(row["Fecha"]);
                decimal total = Convert.ToDecimal(row["Total"]);

                // 2. Recuperar los productos de la base de datos
                // (Usamos el mismo método que ya usamos para mostrar el detalle en pantalla)
                DataTable items = DatabaseService.GetPresupuestoDetalle(id);

                if (items.Rows.Count > 0)
                {
                    // 3. ¡Llamar al Motor de Impresión!
                    PrintService.ImprimirPresupuesto(id, cliente, fecha, items, total);
                }
                else
                {
                    CustomMessageBox.Show("El presupuesto está vacío.", "Error", MessageBoxButton.OK, MessageBoxImage.Warning);
                }
            }
            else
            {
                CustomMessageBox.Show("Seleccione un presupuesto de la lista para imprimir.", "Aviso", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }
    }
}