using System;
using System.Data;
using System.Windows;
using System.Windows.Controls;
using SchettiniGestion;

namespace SchettiniGestion.WPF
{
    public partial class CuponesTarjetasControl : UserControl
    {
        private DataTable _dt;

        public CuponesTarjetasControl() { InitializeComponent(); }
        public CuponesTarjetasControl(object param) : this() { }

        private void Control_Loaded(object sender, RoutedEventArgs e)
        {
            dpDesde.SelectedDate = DateTime.Today.AddMonths(-1);
            dpHasta.SelectedDate = DateTime.Today;
            CargarCupones();
        }

        private void btnBuscar_Click(object sender, RoutedEventArgs e) => CargarCupones();

        private void CargarCupones()
        {
            try
            {
                DateTime desde = dpDesde.SelectedDate ?? DateTime.Today.AddMonths(-1);
                DateTime hasta = (dpHasta.SelectedDate ?? DateTime.Today).AddDays(1).AddSeconds(-1);
                string filtro = txtFiltro?.Text?.Trim().Replace("'", "''") ?? "";

                using (var conn = new System.Data.SqlClient.SqlConnection(DatabaseService.ConnectionString))
                {
                    conn.Open();
                    string sql = $@"SELECT fc.CobranzaID, f.Fecha, fc.FacturaID, fc.NombreMedio, fc.NroTarjeta, fc.NroCuotas, fc.Monto
                                    FROM FacturasCobranza fc
                                    JOIN Facturas f ON fc.FacturaID=f.FacturaID
                                    WHERE f.Fecha >= @d AND f.Fecha <= @h
                                    AND fc.NombreMedio NOT IN ('Efectivo','Transferencia')
                                    {(string.IsNullOrWhiteSpace(filtro) ? "" : $"AND (fc.NombreMedio LIKE '%{filtro}%' OR fc.NroTarjeta LIKE '%{filtro}%')")}
                                    ORDER BY f.Fecha DESC";
                    _dt = new DataTable();
                    var da = new System.Data.SqlClient.SqlDataAdapter(sql, conn);
                    da.SelectCommand.Parameters.AddWithValue("@d", desde);
                    da.SelectCommand.Parameters.AddWithValue("@h", hasta);
                    da.Fill(_dt);
                }

                dgvCupones.ItemsSource = _dt.DefaultView;

                decimal total = 0;
                foreach (DataRow r in _dt.Rows) total += Convert.ToDecimal(r["Monto"]);
                lblTotal.Text = total.ToString("C2");
            }
            catch
            {
                // FacturasCobranza puede no tener datos aún
                _dt = new DataTable();
                _dt.Columns.Add("Fecha"); _dt.Columns.Add("FacturaID"); _dt.Columns.Add("NombreMedio");
                _dt.Columns.Add("NroTarjeta"); _dt.Columns.Add("NroCuotas"); _dt.Columns.Add("Monto");
                dgvCupones.ItemsSource = _dt.DefaultView;
                lblTotal.Text = "$0,00";
            }
        }
    }
}
