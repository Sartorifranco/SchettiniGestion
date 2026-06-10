using System;
using System.Data;
using System.Windows;
using System.Windows.Input;
using SchettiniGestion;

namespace SchettiniGestion.WPF
{
    public partial class PagoProveedorModalWindow : Window
    {
        public int ResultID { get; private set; } = 0;
        private readonly int _pagoId;
        private readonly Action _onGuardado;
        private int _proveedorId = 0;
        private bool _ignorarTC = false;

        public PagoProveedorModalWindow() { InitializeComponent(); Loaded += OnLoaded; }
        public PagoProveedorModalWindow(Window owner, int pagoId, Action onGuardado) : this() { Owner = owner; _pagoId = pagoId; _onGuardado = onGuardado; }
        public PagoProveedorModalWindow(object p1, object p2) : this() { }
        public PagoProveedorModalWindow(object p1, object p2, object p3) : this() { }

        private void OnLoaded(object sender, RoutedEventArgs e)
        {
            if (_pagoId > 0)
            {
                lblTitulo.Text = "Editar Pago";
                try
                {
                    var dt = DatabaseService.GetPagosProveedores();
                    foreach (DataRow r in dt.Rows)
                    {
                        if (Convert.ToInt32(r["PagoID"]) == _pagoId)
                        {
                            _proveedorId = Convert.ToInt32(r["ProveedorID"]);
                            _ignorarTC = true;
                            txtBuscarProveedor.Text = r["Proveedor"]?.ToString() ?? "";
                            _ignorarTC = false;
                            txtMonto.Text = Convert.ToDecimal(r["Monto"]).ToString("N2");
                            txtConcepto.Text = r["Concepto"]?.ToString() ?? "";
                            txtNroComprobante.Text = r["NumeroComprobante"]?.ToString() ?? "";
                            for (int i = 0; i < cmbMedioPago.Items.Count; i++)
                                if ((cmbMedioPago.Items[i] as System.Windows.Controls.ComboBoxItem)?.Content?.ToString() == r["FormaPago"]?.ToString())
                                { cmbMedioPago.SelectedIndex = i; break; }
                            break;
                        }
                    }
                }
                catch { }
            }
        }

        private void txtBuscarProveedor_TextChanged(object sender, System.Windows.Controls.TextChangedEventArgs e)
        {
            if (_ignorarTC) return;
            string q = txtBuscarProveedor.Text.Trim();
            if (q.Length < 2) { popupProveedores.IsOpen = false; return; }
            var dt = DatabaseService.BuscarProveedoresMultiples(q);
            lstProveedores.ItemsSource = dt.DefaultView;
            popupProveedores.IsOpen = dt.Rows.Count > 0;
        }

        private void lstProveedores_MouseUp(object sender, MouseButtonEventArgs e)
        {
            if (lstProveedores.SelectedItem is DataRowView row)
            {
                _proveedorId = Convert.ToInt32(row["ProveedorID"]);
                _ignorarTC = true;
                txtBuscarProveedor.Text = row["RazonSocial"].ToString();
                _ignorarTC = false;
                popupProveedores.IsOpen = false;
                decimal saldo = row["SaldoDeuda"] != DBNull.Value ? Convert.ToDecimal(row["SaldoDeuda"]) : 0;
                lblSaldoDeuda.Text = saldo > 0 ? $"Saldo deuda: {saldo:C2}" : "Sin deuda registrada";
                if (saldo > 0) txtMonto.Text = saldo.ToString("N2");
            }
        }

        private void btnGuardar_Click(object sender, RoutedEventArgs e)
        {
            if (_proveedorId == 0) { ModernMessageBox.Show("Seleccione un proveedor.", "Atención", MessageBoxButton.OK, MessageBoxImage.Warning); return; }
            if (!decimal.TryParse(txtMonto.Text.Replace(",", "."), System.Globalization.NumberStyles.Number, System.Globalization.CultureInfo.InvariantCulture, out decimal monto) || monto <= 0)
            { ModernMessageBox.Show("Ingrese un monto válido.", "Atención", MessageBoxButton.OK, MessageBoxImage.Warning); return; }
            string medio = (cmbMedioPago.SelectedItem as System.Windows.Controls.ComboBoxItem)?.Content?.ToString() ?? "Efectivo";
            bool ok = DatabaseService.GuardarPagoProveedor(_pagoId, _proveedorId, monto, medio, txtConcepto.Text.Trim(), txtNroComprobante.Text.Trim());
            if (ok) { _onGuardado?.Invoke(); DialogResult = true; Close(); }
            else ModernMessageBox.Show("Error al guardar.", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
        }

        private void btnCancelar_Click(object sender, RoutedEventArgs e) { DialogResult = false; Close(); }
    }
}
