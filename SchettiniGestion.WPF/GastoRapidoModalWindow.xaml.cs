using System;
using System.Data;
using System.Windows;
using SchettiniGestion;

namespace SchettiniGestion.WPF
{
    public partial class GastoRapidoModalWindow : Window
    {
        public int ResultID { get; private set; } = 0;
        private readonly int _gastoId;
        private readonly Action _onGuardado;

        public GastoRapidoModalWindow() { InitializeComponent(); Loaded += OnLoaded; }
        public GastoRapidoModalWindow(Window owner, int gastoId, Action onGuardado) : this() { Owner = owner; _gastoId = gastoId; _onGuardado = onGuardado; }
        public GastoRapidoModalWindow(object p1, object p2) : this() { }
        public GastoRapidoModalWindow(object p1, object p2, object p3) : this() { }

        private void OnLoaded(object sender, RoutedEventArgs e)
        {
            if (_gastoId > 0)
            {
                lblTitulo.Text = "Editar Gasto";
                try
                {
                    var dt = DatabaseService.GetGastosRapidos();
                    foreach (DataRow r in dt.Rows)
                    {
                        if (Convert.ToInt32(r["GastoID"]) == _gastoId)
                        {
                            txtConcepto.Text = r["Concepto"]?.ToString() ?? "";
                            txtMonto.Text = Convert.ToDecimal(r["Monto"]).ToString("N2");
                            for (int i = 0; i < cmbCategoria.Items.Count; i++)
                                if ((cmbCategoria.Items[i] as System.Windows.Controls.ComboBoxItem)?.Content?.ToString() == r["Categoria"]?.ToString())
                                { cmbCategoria.SelectedIndex = i; break; }
                            for (int i = 0; i < cmbMedioPago.Items.Count; i++)
                                if ((cmbMedioPago.Items[i] as System.Windows.Controls.ComboBoxItem)?.Content?.ToString() == r["MedioPago"]?.ToString())
                                { cmbMedioPago.SelectedIndex = i; break; }
                            break;
                        }
                    }
                }
                catch { }
            }
        }

        private void btnGuardar_Click(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrWhiteSpace(txtConcepto.Text)) { ModernMessageBox.Show("El concepto es obligatorio.", "Atención", MessageBoxButton.OK, MessageBoxImage.Warning); return; }
            if (!decimal.TryParse(txtMonto.Text.Replace(",", "."), System.Globalization.NumberStyles.Number, System.Globalization.CultureInfo.InvariantCulture, out decimal monto) || monto <= 0)
            { ModernMessageBox.Show("Ingrese un monto válido.", "Atención", MessageBoxButton.OK, MessageBoxImage.Warning); return; }
            string cat = (cmbCategoria.SelectedItem as System.Windows.Controls.ComboBoxItem)?.Content?.ToString() ?? "Otros";
            string medio = (cmbMedioPago.SelectedItem as System.Windows.Controls.ComboBoxItem)?.Content?.ToString() ?? "Efectivo";
            bool ok = DatabaseService.GuardarGastoRapido(_gastoId, txtConcepto.Text.Trim(), cat, monto, medio);
            if (ok) { _onGuardado?.Invoke(); DialogResult = true; Close(); }
            else ModernMessageBox.Show("Error al guardar.", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
        }

        private void btnCancelar_Click(object sender, RoutedEventArgs e) { DialogResult = false; Close(); }
    }
}
