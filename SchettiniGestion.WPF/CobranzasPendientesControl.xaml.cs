using System;
using System.Data;
using System.Windows;
using System.Windows.Controls;
using SchettiniGestion;

namespace SchettiniGestion.WPF
{
    public partial class CobranzasPendientesControl : UserControl
    {
        private DataTable _dt;

        public CobranzasPendientesControl() { InitializeComponent(); }
        public CobranzasPendientesControl(object param) : this() { }

        private void Control_Loaded(object sender, RoutedEventArgs e) => CargarClientes();
        private void txtFiltro_TextChanged(object sender, TextChangedEventArgs e) => AplicarFiltro();
        private void btnBuscar_Click(object sender, RoutedEventArgs e) => CargarClientes();

        private void CargarClientes()
        {
            try
            {
                _dt = DatabaseService.GetClientes();
                bool tieneSaldo = _dt.Columns.Contains("SaldoDeuda");
                if (tieneSaldo)
                    _dt.DefaultView.RowFilter = "SaldoDeuda > 0";
                dgvClientes.ItemsSource = _dt.DefaultView;
                ActualizarTotal();
            }
            catch (Exception ex) { MessageBox.Show("Error: " + ex.Message); }
        }

        private void AplicarFiltro()
        {
            if (_dt == null) return;
            bool tieneSaldo = _dt.Columns.Contains("SaldoDeuda");
            string filtro = txtFiltro?.Text?.Trim().Replace("'", "''") ?? "";
            if (!string.IsNullOrWhiteSpace(filtro))
                _dt.DefaultView.RowFilter = tieneSaldo
                    ? $"SaldoDeuda > 0 AND (CUIT LIKE '%{filtro}%' OR RazonSocial LIKE '%{filtro}%')"
                    : $"CUIT LIKE '%{filtro}%' OR RazonSocial LIKE '%{filtro}%'";
            else
                _dt.DefaultView.RowFilter = tieneSaldo ? "SaldoDeuda > 0" : "";
            ActualizarTotal();
        }

        private void ActualizarTotal()
        {
            if (_dt == null) return;
            decimal total = 0;
            if (_dt.Columns.Contains("SaldoDeuda"))
                foreach (DataRowView rv in _dt.DefaultView)
                    total += rv["SaldoDeuda"] == DBNull.Value ? 0 : Convert.ToDecimal(rv["SaldoDeuda"]);
            lblTotalDeuda.Text = total.ToString("C2");
        }
    }
}
