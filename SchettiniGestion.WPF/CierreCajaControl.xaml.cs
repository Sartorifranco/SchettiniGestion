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

                DatabaseService.GetResumenCajaDelDia(out _saldoApertura, out _ingresos, out _egresos, out decimal saldoTotal);

                lblSaldoApertura.Text = _saldoApertura.ToString("C2");
                lblIngresos.Text = _ingresos.ToString("C2");
                lblEgresos.Text = _egresos.ToString("C2");
                lblSaldoCierre.Text = saldoTotal.ToString("C2");
            }
            catch (Exception ex)
            {
                CustomMessageBox.Show("Error al cargar datos: " + ex.Message, "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private static bool YaTieneCierreHoy() => DatabaseService.TieneCierreCajaHoy();

        private void btnCerrarCaja_Click(object sender, RoutedEventArgs e)
        {
            var owner = Window.GetWindow(this);

            if (!DatabaseService.TieneAperturaCajaHoy())
            {
                CustomMessageBox.Show(
                    "No hay apertura de caja registrada para hoy.\n\n" +
                    "Primero abrí la caja en la pestaña «Apertura de caja» indicando el fondo fijo.",
                    "Sin apertura", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            // Advertir si ya existe un cierre registrado hoy
            if (YaTieneCierreHoy())
            {
                if (CustomMessageBox.Show(
                    "⚠️  Ya existe un cierre de caja registrado para hoy.\n\n" +
                    "Registrar otro cierre puede duplicar los totales en los informes.\n\n" +
                    "¿Desea registrar un cierre adicional de todas formas?",
                    "Cierre ya registrado",
                    MessageBoxButton.YesNo,
                    MessageBoxImage.Warning) != MessageBoxResult.Yes)
                    return;
            }

            decimal saldoCierre = _saldoApertura + _ingresos - _egresos;
            if (!CierreCajaConfirmacionWindow.Mostrar(_saldoApertura, _ingresos, _egresos, saldoCierre, owner))
                return;

            try
            {
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
                        totalTransferencia += monto;
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

                CustomMessageBox.Show("Cierre de caja registrado correctamente.", "Éxito", MessageBoxButton.OK, MessageBoxImage.Information);
                CargarResumen();
            }
            catch (Exception ex)
            {
                CustomMessageBox.Show("Error al registrar cierre: " + ex.Message, "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
    }
}
