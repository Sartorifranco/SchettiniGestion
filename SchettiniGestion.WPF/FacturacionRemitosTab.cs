using System;
using System.Data;
using System.Windows;
using System.Windows.Controls;
using SchettiniGestion;

namespace SchettiniGestion.WPF
{
    public partial class FacturacionRemitosTab : UserControl
    {
        private DataTable _dt;

        public FacturacionRemitosTab() { InitializeComponent(); }

        private void Control_Loaded(object sender, RoutedEventArgs e) => Cargar();

        private void Cargar(string filtro = "")
        {
            try
            {
                using (var conn = new System.Data.SqlClient.SqlConnection(DatabaseService.ConnectionString))
                {
                    conn.Open();
                    string sql = string.IsNullOrWhiteSpace(filtro)
                        ? @"SELECT r.RemitoID, r.Fecha, ISNULL(c.RazonSocial,'') AS RazonSocial, r.FacturaID, r.Estado
                            FROM Remitos r LEFT JOIN Clientes c ON r.ClienteID=c.ClienteID ORDER BY r.Fecha DESC"
                        : @"SELECT r.RemitoID, r.Fecha, ISNULL(c.RazonSocial,'') AS RazonSocial, r.FacturaID, r.Estado
                            FROM Remitos r LEFT JOIN Clientes c ON r.ClienteID=c.ClienteID
                            WHERE c.RazonSocial LIKE @f ORDER BY r.Fecha DESC";
                    _dt = new DataTable();
                    var da = new System.Data.SqlClient.SqlDataAdapter(sql, conn);
                    if (!string.IsNullOrWhiteSpace(filtro)) da.SelectCommand.Parameters.AddWithValue("@f", "%" + filtro + "%");
                    da.Fill(_dt);
                }
                dgvRemitos.ItemsSource = _dt.DefaultView;
            }
            catch { }
        }

        private int GetSelectedId() => dgvRemitos.SelectedItem is DataRowView rv ? Convert.ToInt32(rv["RemitoID"]) : 0;

        private void txtFiltro_TextChanged(object sender, TextChangedEventArgs e) => Cargar(txtFiltro.Text.Trim());
        private void btnBuscar_Click(object sender, RoutedEventArgs e) => Cargar(txtFiltro.Text.Trim());

        private void btnImprimir_Click(object sender, RoutedEventArgs e)
        {
            int id = GetSelectedId();
            if (id == 0) { MessageBox.Show("Seleccione un remito.", "Atención", MessageBoxButton.OK, MessageBoxImage.Warning); return; }
            PrintService.ImprimirRemito(id);
        }

        private void dgvRemitos_MouseDoubleClick(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            int id = GetSelectedId();
            if (id == 0) return;
            string cliente = dgvRemitos.SelectedItem is DataRowView rv ? rv["RazonSocial"]?.ToString() : "";
            new DetalleVentaWindow(id, cliente, "Remito").ShowDialog();
        }

        private void btnEliminar_Click(object sender, RoutedEventArgs e)
        {
            int id = GetSelectedId();
            if (id == 0) { MessageBox.Show("Seleccione un remito.", "Atención", MessageBoxButton.OK, MessageBoxImage.Warning); return; }
            if (MessageBox.Show("¿Eliminar este remito?", "Confirmar", MessageBoxButton.YesNo, MessageBoxImage.Warning) == MessageBoxResult.Yes)
            {
                try
                {
                    using (var conn = new System.Data.SqlClient.SqlConnection(DatabaseService.ConnectionString))
                    {
                        conn.Open();
                        new System.Data.SqlClient.SqlCommand($"DELETE FROM RemitoDetalle WHERE RemitoID={id}; DELETE FROM Remitos WHERE RemitoID={id}", conn).ExecuteNonQuery();
                    }
                    Cargar(txtFiltro.Text.Trim());
                }
                catch (Exception ex) { MessageBox.Show("Error: " + ex.Message); }
            }
        }
    }
}
