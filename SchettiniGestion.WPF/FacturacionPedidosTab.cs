using System;
using System.Data;
using System.Windows;
using System.Windows.Controls;
using SchettiniGestion;

namespace SchettiniGestion.WPF
{
    public partial class FacturacionPedidosTab : UserControl
    {
        private DataTable _dt;

        public FacturacionPedidosTab() { InitializeComponent(); }

        private void Control_Loaded(object sender, RoutedEventArgs e) => Cargar();

        private void Cargar(string filtro = "")
        {
            try
            {
                using (var conn = new System.Data.SqlClient.SqlConnection(DatabaseService.ConnectionString))
                {
                    conn.Open();
                    string sql = string.IsNullOrWhiteSpace(filtro)
                        ? @"SELECT p.PedidoID, p.Fecha, ISNULL(c.RazonSocial,'') AS RazonSocial, p.FechaEntrega, p.Total, p.Estado
                            FROM Pedidos p LEFT JOIN Clientes c ON p.ClienteID=c.ClienteID ORDER BY p.Fecha DESC"
                        : @"SELECT p.PedidoID, p.Fecha, ISNULL(c.RazonSocial,'') AS RazonSocial, p.FechaEntrega, p.Total, p.Estado
                            FROM Pedidos p LEFT JOIN Clientes c ON p.ClienteID=c.ClienteID
                            WHERE c.RazonSocial LIKE @f ORDER BY p.Fecha DESC";
                    _dt = new DataTable();
                    var da = new System.Data.SqlClient.SqlDataAdapter(sql, conn);
                    if (!string.IsNullOrWhiteSpace(filtro)) da.SelectCommand.Parameters.AddWithValue("@f", "%" + filtro + "%");
                    da.Fill(_dt);
                }
                dgvPedidos.ItemsSource = _dt.DefaultView;
            }
            catch { }
        }

        private int GetSelectedId() => dgvPedidos.SelectedItem is DataRowView rv ? Convert.ToInt32(rv["PedidoID"]) : 0;

        private void txtFiltro_TextChanged(object sender, TextChangedEventArgs e) => Cargar(txtFiltro.Text.Trim());
        private void btnBuscar_Click(object sender, RoutedEventArgs e) => Cargar(txtFiltro.Text.Trim());

        private void btnImprimir_Click(object sender, RoutedEventArgs e)
        {
            int id = GetSelectedId();
            if (id == 0) { MessageBox.Show("Seleccione un pedido.", "Atención", MessageBoxButton.OK, MessageBoxImage.Warning); return; }
            PrintService.ImprimirPedido(id);
        }

        private void dgvPedidos_MouseDoubleClick(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            int id = GetSelectedId();
            if (id == 0) return;
            string cliente = dgvPedidos.SelectedItem is DataRowView rv ? rv["RazonSocial"]?.ToString() : "";
            new DetalleVentaWindow(id, cliente, "Pedido").ShowDialog();
        }

        private void btnConfirmar_Click(object sender, RoutedEventArgs e)
        {
            int id = GetSelectedId();
            if (id == 0) { MessageBox.Show("Seleccione un pedido.", "Atención", MessageBoxButton.OK, MessageBoxImage.Warning); return; }
            try
            {
                using (var conn = new System.Data.SqlClient.SqlConnection(DatabaseService.ConnectionString))
                {
                    conn.Open();
                    new System.Data.SqlClient.SqlCommand($"UPDATE Pedidos SET Estado='Confirmado' WHERE PedidoID={id}", conn).ExecuteNonQuery();
                }
                Cargar(txtFiltro.Text.Trim());
            }
            catch (Exception ex) { MessageBox.Show("Error: " + ex.Message); }
        }

        private void btnEliminar_Click(object sender, RoutedEventArgs e)
        {
            int id = GetSelectedId();
            if (id == 0) { MessageBox.Show("Seleccione un pedido.", "Atención", MessageBoxButton.OK, MessageBoxImage.Warning); return; }
            if (MessageBox.Show("¿Eliminar este pedido?", "Confirmar", MessageBoxButton.YesNo, MessageBoxImage.Warning) == MessageBoxResult.Yes)
            {
                try
                {
                    using (var conn = new System.Data.SqlClient.SqlConnection(DatabaseService.ConnectionString))
                    {
                        conn.Open();
                        new System.Data.SqlClient.SqlCommand($"DELETE FROM PedidoDetalle WHERE PedidoID={id}; DELETE FROM Pedidos WHERE PedidoID={id}", conn).ExecuteNonQuery();
                    }
                    Cargar(txtFiltro.Text.Trim());
                }
                catch (Exception ex) { MessageBox.Show("Error: " + ex.Message); }
            }
        }
    }
}
