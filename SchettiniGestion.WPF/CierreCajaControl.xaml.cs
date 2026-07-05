using System;
using System.Data;
using System.Windows;
using System.Windows.Controls;
using SchettiniGestion;

namespace SchettiniGestion.WPF
{
    public partial class CierreCajaControl : UserControl
    {
        private decimal _saldoApertura = 0;
        private decimal _ingresos = 0;
        private decimal _egresos = 0;

        public CierreCajaControl() { InitializeComponent(); }
        public CierreCajaControl(object param) : this() { }

        private void Control_Loaded(object sender, RoutedEventArgs e)
        {
            CargarResumen();
        }

        private void CargarResumen()
        {
            try
            {
                var dt = DatabaseService.GetMovimientosCaja(DateTime.Today);
                dgvMovimientos.ItemsSource = dt.DefaultView;

                _ingresos = 0; _egresos = 0;
                foreach (DataRow r in dt.Rows)
                {
                    decimal m = Convert.ToDecimal(r["Monto"]);
                    if (r["Tipo"]?.ToString() == "Ingreso") _ingresos += m;
                    else _egresos += m;
                }

                // El saldo anterior es el saldo total menos los del día
                decimal saldoTotal = DatabaseService.GetSaldoCaja();
                _saldoApertura = saldoTotal - _ingresos + _egresos;

                lblSaldoApertura.Text = _saldoApertura.ToString("C2");
                lblIngresos.Text = _ingresos.ToString("C2");
                lblEgresos.Text = _egresos.ToString("C2");
                lblSaldoCierre.Text = saldoTotal.ToString("C2");
            }
            catch (Exception ex)
            {
                MessageBox.Show("Error al cargar datos: " + ex.Message);
            }
        }

        private static bool YaTieneCierreHoy()
        {
            try
            {
                using (var c = new System.Data.SqlClient.SqlConnection(DatabaseService.ConnectionString))
                {
                    c.Open();
                    var cmd = new System.Data.SqlClient.SqlCommand(
                        "SELECT COUNT(*) FROM CierresCaja WHERE CAST(Fecha AS DATE) = CAST(GETDATE() AS DATE)", c);
                    return Convert.ToInt32(cmd.ExecuteScalar()) > 0;
                }
            }
            catch { return false; }
        }

        private void btnCerrarCaja_Click(object sender, RoutedEventArgs e)
        {
            // Advertir si ya existe un cierre registrado hoy
            if (YaTieneCierreHoy())
            {
                var confirmar = MessageBox.Show(
                    "⚠️  Ya existe un cierre de caja registrado para hoy.\n\n" +
                    "Registrar otro cierre puede duplicar los totales en los informes.\n\n" +
                    "¿Desea registrar un cierre adicional de todas formas?",
                    "Cierre ya registrado",
                    MessageBoxButton.YesNo,
                    MessageBoxImage.Warning);
                if (confirmar != MessageBoxResult.Yes) return;
            }

            var res = MessageBox.Show(
                $"¿Confirma el cierre de caja?\n\nSaldo apertura: {_saldoApertura:C2}\nIngresos: {_ingresos:C2}\nEgresos: {_egresos:C2}\nSaldo cierre: {(_saldoApertura + _ingresos - _egresos):C2}",
                "Confirmar Cierre", MessageBoxButton.YesNo, MessageBoxImage.Question);

            if (res != MessageBoxResult.Yes) return;

            try
            {
                decimal saldoCierre = _saldoApertura + _ingresos - _egresos;

                // Desglose real por medio de pago desde FacturasCobranza del día
                decimal totalEfectivo = 0, totalTarjeta = 0, totalTransferencia = 0;
                var desglose = DatabaseService.GetDesgloseMediosPagoHoy();
                foreach (DataRow dr in desglose.Rows)
                {
                    string medio = (dr["NombreMedio"] as string ?? "").ToLowerInvariant();
                    decimal monto = Convert.ToDecimal(dr["Total"]);
                    if (medio.Contains("efectivo"))
                        totalEfectivo += monto;
                    else if (medio.Contains("tarjeta") || medio.Contains("débito") || medio.Contains("crédito") || medio.Contains("debito") || medio.Contains("credito"))
                        totalTarjeta += monto;
                    else
                        totalTransferencia += monto; // transferencia, MP, cheque, etc.
                }

                using (var c = new System.Data.SqlClient.SqlConnection(DatabaseService.ConnectionString))
                {
                    c.Open();
                    var cmd = new System.Data.SqlClient.SqlCommand(@"
                        INSERT INTO CierresCaja (Fecha,SaldoApertura,TotalIngresos,TotalEgresos,SaldoCierre,TotalEfectivo,TotalTarjeta,TotalTransferencia,Observaciones,Usuario)
                        VALUES (@f,@sa,@ti,@te,@sc,@ef,@tar,@trans,@obs,@u)", c);
                    cmd.Parameters.AddWithValue("@f",     DateTime.Now);
                    cmd.Parameters.AddWithValue("@sa",    _saldoApertura);
                    cmd.Parameters.AddWithValue("@ti",    _ingresos);
                    cmd.Parameters.AddWithValue("@te",    _egresos);
                    cmd.Parameters.AddWithValue("@sc",    saldoCierre);
                    cmd.Parameters.AddWithValue("@ef",    totalEfectivo);
                    cmd.Parameters.AddWithValue("@tar",   totalTarjeta);
                    cmd.Parameters.AddWithValue("@trans", totalTransferencia);
                    cmd.Parameters.AddWithValue("@obs",   txtObservaciones.Text ?? "");
                    cmd.Parameters.AddWithValue("@u",     SchettiniGestion.SesionUsuario.NombreUsuario ?? "");
                    cmd.ExecuteNonQuery();
                }

                MessageBox.Show("Cierre de caja registrado correctamente.", "Éxito", MessageBoxButton.OK, MessageBoxImage.Information);
                CargarResumen();
            }
            catch (Exception ex)
            {
                MessageBox.Show("Error al registrar cierre: " + ex.Message);
            }
        }
    }
}
