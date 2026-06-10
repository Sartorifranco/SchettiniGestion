using System;
using System.Data;
using System.Windows;
using System.Windows.Controls;
using SchettiniGestion;

namespace SchettiniGestion.WPF
{
    public partial class FacturacionPresupuestosTab : UserControl
    {
        private DataTable _dt;

        public FacturacionPresupuestosTab() { InitializeComponent(); }

        private void Control_Loaded(object sender, RoutedEventArgs e) => Cargar();

        private void Cargar(string filtro = "")
        {
            try
            {
                using (var conn = new System.Data.SqlClient.SqlConnection(DatabaseService.ConnectionString))
                {
                    conn.Open();
                    string sql = string.IsNullOrWhiteSpace(filtro)
                        ? @"SELECT p.PresupuestoID, p.Fecha, ISNULL(c.RazonSocial,'Consumidor Final') AS RazonSocial, p.Total, p.Estado
                            FROM Presupuestos p LEFT JOIN Clientes c ON p.ClienteID=c.ClienteID ORDER BY p.Fecha DESC"
                        : $@"SELECT p.PresupuestoID, p.Fecha, ISNULL(c.RazonSocial,'Consumidor Final') AS RazonSocial, p.Total, p.Estado
                             FROM Presupuestos p LEFT JOIN Clientes c ON p.ClienteID=c.ClienteID
                             WHERE c.RazonSocial LIKE @f ORDER BY p.Fecha DESC";
                    _dt = new DataTable();
                         var da = new System.Data.SqlClient.SqlDataAdapter(sql, conn);
                    if (!string.IsNullOrWhiteSpace(filtro)) da.SelectCommand.Parameters.AddWithValue("@f", "%" + filtro + "%");
                    da.Fill(_dt);
                }
                dgvPresupuestos.ItemsSource = _dt.DefaultView;
            }
            catch { }
        }

        private int GetSelectedId()
        {
            return dgvPresupuestos.SelectedItem is DataRowView rv ? Convert.ToInt32(rv["PresupuestoID"]) : 0;
        }

        private void txtFiltro_TextChanged(object sender, TextChangedEventArgs e) => Cargar(txtFiltro.Text.Trim());
        private void btnBuscar_Click(object sender, RoutedEventArgs e) => Cargar(txtFiltro.Text.Trim());

        private void btnImprimir_Click(object sender, RoutedEventArgs e)
        {
            int id = GetSelectedId();
            if (id == 0) { ModernMessageBox.Show("Seleccione un presupuesto.", "Atención", MessageBoxButton.OK, MessageBoxImage.Warning); return; }
            PrintService.ImprimirPresupuesto(id);
        }

        private void btnConvertir_Click(object sender, RoutedEventArgs e)
        {
            int id = GetSelectedId();
            if (id == 0) { ModernMessageBox.Show("Seleccione un presupuesto.", "Atención", MessageBoxButton.OK, MessageBoxImage.Warning); return; }
            ModernMessageBox.Show("Para convertir a factura, use el botón 'Cargar Presupuesto' en la pestaña principal de Facturación.",
                "Información", MessageBoxButton.OK, MessageBoxImage.Information);
        }

        private void btnEliminar_Click(object sender, RoutedEventArgs e)
        {
            int id = GetSelectedId();
            if (id == 0) { ModernMessageBox.Show("Seleccione un presupuesto.", "Atención", MessageBoxButton.OK, MessageBoxImage.Warning); return; }
            if (ModernMessageBox.Show("¿Eliminar este presupuesto?", "Confirmar", MessageBoxButton.YesNo, MessageBoxImage.Warning) == MessageBoxResult.Yes)
            {
                try
                {
                    using (var conn = new System.Data.SqlClient.SqlConnection(DatabaseService.ConnectionString))
                    {
                        conn.Open();
                        new System.Data.SqlClient.SqlCommand($"DELETE FROM PresupuestoDetalle WHERE PresupuestoID={id}; DELETE FROM Presupuestos WHERE PresupuestoID={id}", conn).ExecuteNonQuery();
                    }
                    Cargar(txtFiltro.Text.Trim());
                }
                catch (Exception ex) { ModernMessageBox.Show("Error: " + ex.Message); }
            }
        }

        private void dgvPresupuestos_MouseDoubleClick(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            int id = GetSelectedId();
            if (id == 0) return;
            string cliente = dgvPresupuestos.SelectedItem is DataRowView rv ? rv["RazonSocial"]?.ToString() ?? "" : "";
            var w = new DetalleVentaWindow(id, cliente, "Presupuesto");
            w.Owner = Window.GetWindow(this);
            w.ShowDialog();
        }
    }
}
