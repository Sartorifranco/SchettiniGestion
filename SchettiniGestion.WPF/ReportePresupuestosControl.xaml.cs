using System;
using System.Data;
using System.Windows;
using System.Windows.Controls;
using SchettiniGestion;

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
            EstablecerFechas("Mes");
        }

        // --- FILTROS RÁPIDOS ---
        private void btnFiltroRapido_Click(object sender, RoutedEventArgs e)
        {
            string opcion = (sender as Button).Tag.ToString();
            EstablecerFechas(opcion);
        }

        private void EstablecerFechas(string opcion)
        {
            DateTime hoy = DateTime.Today;

            switch (opcion)
            {
                case "Hoy":
                    dtpDesde.SelectedDate = hoy;
                    dtpHasta.SelectedDate = hoy;
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
            CargarDatos();
        }

        private void btnBuscar_Click(object sender, RoutedEventArgs e)
        {
            CargarDatos();
        }

        public void Refrescar() { CargarDatos(); }

        private void CargarDatos()
        {
            if (dtpDesde.SelectedDate == null || dtpHasta.SelectedDate == null) return;

            try
            {
                DateTime desde = dtpDesde.SelectedDate.Value;
                // Ajuste crítico para que aparezcan los registros de hoy a la tarde
                DateTime hasta = dtpHasta.SelectedDate.Value.Date.AddDays(1).AddSeconds(-1);

                DataTable dt = DatabaseService.GetPresupuestos(desde, hasta);
                dgvPresupuestos.ItemsSource = dt.DefaultView;
            }
            catch (Exception ex)
            {
                ModernMessageBox.Show("Error al cargar lista: " + ex.Message);
            }
        }

        // --- ACCIONES ---
        private void btnVer_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button btn && btn.DataContext is DataRowView row)
            {
                int id = Convert.ToInt32(row["PresupuestoID"]);
                string cliente = row["RazonSocial"].ToString();

                // ABRIMOS LA VENTANA MODERNA EN MODO PRESUPUESTO
                DetalleVentaWindow detalle = new DetalleVentaWindow(id, cliente, "Presupuesto");
                detalle.ShowDialog();
            }
        }

        private void btnImprimir_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button btn && btn.DataContext is DataRowView row)
            {
                int id = Convert.ToInt32(row["PresupuestoID"]);

                // CORRECCIÓN: Llamamos al servicio unificado PrintService
                PrintService.ImprimirPresupuesto(id);
            }
        }

        private void btnEliminar_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button btn && btn.DataContext is DataRowView row)
            {
                int id = Convert.ToInt32(row["PresupuestoID"]);

                if (ModernMessageBox.Show($"¿Eliminar Presupuesto #{id}?", "Confirmar", MessageBoxButton.YesNo, MessageBoxImage.Warning) == MessageBoxResult.Yes)
                {
                    if (DatabaseService.EliminarPresupuesto(id))
                    {
                        CargarDatos();
                    }
                    else
                    {
                        ModernMessageBox.Show("Error al eliminar.", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                    }
                }
            }
        }
    }
}