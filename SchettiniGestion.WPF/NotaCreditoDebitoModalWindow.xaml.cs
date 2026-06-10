using System;
using System.Data;
using System.Windows;
using System.Windows.Input;
using SchettiniGestion;

namespace SchettiniGestion.WPF
{
    public partial class NotaCreditoDebitoModalWindow : Window
    {
        public int ResultID { get; private set; } = 0;
        private readonly int _notaId;
        private readonly Action _onGuardado;
        private int _proveedorId = 0;
        private bool _ignorarTC = false;

        public NotaCreditoDebitoModalWindow() { InitializeComponent(); Loaded += OnLoaded; }
        public NotaCreditoDebitoModalWindow(Window owner, int notaId, Action onGuardado) : this() { Owner = owner; _notaId = notaId; _onGuardado = onGuardado; }
        public NotaCreditoDebitoModalWindow(object p1, object p2) : this() { }
        public NotaCreditoDebitoModalWindow(object p1, object p2, object p3) : this() { }

        private void OnLoaded(object sender, RoutedEventArgs e)
        {
            if (_notaId > 0)
            {
                lblTitulo.Text = "Editar Nota";
                try
                {
                    var dt = DatabaseService.GetNotasCreditoDebitoCompras();
                    foreach (DataRow r in dt.Rows)
                    {
                        if (Convert.ToInt32(r["NotaID"]) == _notaId)
                        {
                            _proveedorId = Convert.ToInt32(r["ProveedorID"]);
                            _ignorarTC = true;
                            txtBuscarProveedor.Text = r["Proveedor"]?.ToString() ?? "";
                            _ignorarTC = false;
                            txtMonto.Text = Convert.ToDecimal(r["Monto"]).ToString("N2");
                            txtDescripcion.Text = r["Descripcion"]?.ToString() ?? "";
                            txtNroComprobante.Text = r["NumeroComprobante"]?.ToString() ?? "";
                            for (int i = 0; i < cmbTipo.Items.Count; i++)
                                if ((cmbTipo.Items[i] as System.Windows.Controls.ComboBoxItem)?.Content?.ToString() == r["Tipo"]?.ToString())
                                { cmbTipo.SelectedIndex = i; break; }
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
            }
        }

        private void btnGuardar_Click(object sender, RoutedEventArgs e)
        {
            if (_proveedorId == 0) { ModernMessageBox.Show("Seleccione un proveedor.", "Atención", MessageBoxButton.OK, MessageBoxImage.Warning); return; }
            if (!decimal.TryParse(txtMonto.Text.Replace(",", "."), System.Globalization.NumberStyles.Number, System.Globalization.CultureInfo.InvariantCulture, out decimal monto) || monto <= 0)
            { ModernMessageBox.Show("Ingrese un monto válido.", "Atención", MessageBoxButton.OK, MessageBoxImage.Warning); return; }
            string tipo = (cmbTipo.SelectedItem as System.Windows.Controls.ComboBoxItem)?.Content?.ToString() ?? "NC";
            bool ok = DatabaseService.GuardarNotaCreditoDebitoCompra(_notaId, _proveedorId, tipo, monto, txtDescripcion.Text.Trim(), txtNroComprobante.Text.Trim());
            if (ok) { _onGuardado?.Invoke(); DialogResult = true; Close(); }
            else ModernMessageBox.Show("Error al guardar.", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
        }

        private void btnCancelar_Click(object sender, RoutedEventArgs e) { DialogResult = false; Close(); }
    }
}
