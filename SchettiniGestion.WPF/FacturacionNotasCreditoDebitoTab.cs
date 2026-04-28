using System;
using System.Data;
using System.Windows;
using System.Windows.Controls;
using SchettiniGestion;

namespace SchettiniGestion.WPF
{
    public partial class FacturacionNotasCreditoDebitoTab : UserControl
    {
        private DataTable _dt;

        public FacturacionNotasCreditoDebitoTab() { InitializeComponent(); }

        private void Control_Loaded(object sender, RoutedEventArgs e) => Cargar();

        private void Cargar(string filtro = "")
        {
            try
            {
                using (var conn = new System.Data.SqlClient.SqlConnection(DatabaseService.ConnectionString))
                {
                    conn.Open();
                    string sql = string.IsNullOrWhiteSpace(filtro)
                        ? @"SELECT n.NotaID, n.Tipo, n.Fecha, ISNULL(c.RazonSocial,'') AS RazonSocial,
                                   n.Monto, n.Descripcion, n.NumeroComprobante
                            FROM NotasCreditoDebitoVentas n LEFT JOIN Clientes c ON n.ClienteID=c.ClienteID
                            ORDER BY n.Fecha DESC"
                        : @"SELECT n.NotaID, n.Tipo, n.Fecha, ISNULL(c.RazonSocial,'') AS RazonSocial,
                                   n.Monto, n.Descripcion, n.NumeroComprobante
                            FROM NotasCreditoDebitoVentas n LEFT JOIN Clientes c ON n.ClienteID=c.ClienteID
                            WHERE c.RazonSocial LIKE @f OR n.Descripcion LIKE @f
                            ORDER BY n.Fecha DESC";
                    _dt = new DataTable();
                    var da = new System.Data.SqlClient.SqlDataAdapter(sql, conn);
                    if (!string.IsNullOrWhiteSpace(filtro)) da.SelectCommand.Parameters.AddWithValue("@f", "%" + filtro + "%");
                    da.Fill(_dt);
                }
                dgvNotas.ItemsSource = _dt.DefaultView;
            }
            catch { }
        }

        private int GetSelectedId() => dgvNotas.SelectedItem is DataRowView rv ? Convert.ToInt32(rv["NotaID"]) : 0;

        private void txtFiltro_TextChanged(object sender, TextChangedEventArgs e) => Cargar(txtFiltro.Text.Trim());
        private void btnBuscar_Click(object sender, RoutedEventArgs e) => Cargar(txtFiltro.Text.Trim());

        private void btnEliminar_Click(object sender, RoutedEventArgs e)
        {
            int id = GetSelectedId();
            if (id == 0) { MessageBox.Show("Seleccione una nota.", "Atención", MessageBoxButton.OK, MessageBoxImage.Warning); return; }
            if (MessageBox.Show("¿Eliminar esta nota?", "Confirmar", MessageBoxButton.YesNo, MessageBoxImage.Warning) == MessageBoxResult.Yes)
            {
                try
                {
                    using (var conn = new System.Data.SqlClient.SqlConnection(DatabaseService.ConnectionString))
                    {
                        conn.Open();
                        new System.Data.SqlClient.SqlCommand($"DELETE FROM NotasCreditoDebitoVentas WHERE NotaID={id}", conn).ExecuteNonQuery();
                    }
                    Cargar(txtFiltro.Text.Trim());
                }
                catch (Exception ex) { MessageBox.Show("Error: " + ex.Message); }
            }
        }
    }
}
