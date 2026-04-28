using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Data;
using System.Linq;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using SchettiniGestion;

namespace SchettiniGestion.WPF
{
    public class CobranzaItem
    {
        public int MedioPagoID { get; set; }
        public string nombreMedio { get; set; }
        public decimal monto { get; set; }
        public string MontoFormateado => monto.ToString("C2");
    }

    public partial class CobroModalWindow : Window
    {
        public List<CobranzaItem> Cobranzas { get; private set; } = new List<CobranzaItem>();
        public int ResultID { get; private set; } = 0;

        private readonly decimal _total;
        private ObservableCollection<CobranzaItem> _cobros = new ObservableCollection<CobranzaItem>();
        private DataTable _mediosPago;

        public CobroModalWindow()
        {
            InitializeComponent();
            _total = 0;
            Loaded += CobroModalWindow_Loaded;
        }

        public CobroModalWindow(Window owner, decimal total) : this()
        {
            Owner = owner;
            _total = total;
        }

        public CobroModalWindow(object param) : this() { }
        public CobroModalWindow(object p1, object p2) : this() { }
        public CobroModalWindow(object p1, object p2, object p3) : this() { }

        private void CobroModalWindow_Loaded(object sender, RoutedEventArgs e)
        {
            lblTotalACobrar.Text = _total.ToString("C2");
            dgvCobranzas.ItemsSource = _cobros;
            _cobros.CollectionChanged += (s, ev) => ActualizarResumen();

            CargarMediosPago();
            ActualizarResumen();

            // Pre-seleccionar efectivo y poner el total pendiente como monto sugerido
            if (cmbMediosPago.Items.Count > 0) cmbMediosPago.SelectedIndex = 0;
            txtMonto.Text = _total.ToString("N2");
            txtMonto.SelectAll();
            txtMonto.Focus();
        }

        private void CargarMediosPago()
        {
            try
            {
                _mediosPago = DatabaseService.GetMediosPagoCompleto();
                var lista = new List<MedioPagoItem>();
                foreach (DataRow row in _mediosPago.Rows)
                {
                    if (row["Activo"] != DBNull.Value && Convert.ToBoolean(row["Activo"]))
                        lista.Add(new MedioPagoItem { MedioID = Convert.ToInt32(row["MedioID"]), Nombre = row["Nombre"].ToString() });
                }
                cmbMediosPago.ItemsSource = lista;
                if (lista.Count > 0) cmbMediosPago.SelectedIndex = 0;
            }
            catch
            {
                var fallback = new List<MedioPagoItem>
                {
                    new MedioPagoItem { MedioID = 1, Nombre = "Efectivo" },
                    new MedioPagoItem { MedioID = 2, Nombre = "Tarjeta Débito" },
                    new MedioPagoItem { MedioID = 3, Nombre = "Tarjeta Crédito" },
                    new MedioPagoItem { MedioID = 4, Nombre = "Transferencia" }
                };
                cmbMediosPago.ItemsSource = fallback;
                cmbMediosPago.SelectedIndex = 0;
            }
        }

        private void cmbMediosPago_SelectionChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
        {
            if (txtMonto == null) return;
            decimal pendiente = _total - _cobros.Sum(c => c.monto);
            if (pendiente > 0)
                txtMonto.Text = pendiente.ToString("N2");
        }

        private void ActualizarResumen()
        {
            decimal cobrado = _cobros.Sum(c => c.monto);
            decimal pendiente = _total - cobrado;
            decimal vuelto = cobrado > _total ? cobrado - _total : 0;

            lblTotalCobrado.Text = cobrado.ToString("C2");
            lblPendiente.Text = (pendiente > 0 ? pendiente : 0).ToString("C2");
            lblVuelto.Text = vuelto.ToString("C2");

            lblPendiente.Foreground = pendiente > 0
                ? BrushFromTheme("DangerColor")
                : BrushFromTheme("SuccessColor");

            btnConfirmar.IsEnabled = cobrado >= _total;
        }

        private static Brush BrushFromTheme(string key) =>
            Application.Current?.TryFindResource(key) as Brush ?? Brushes.Gray;

        private void btnAgregarPago_Click(object sender, RoutedEventArgs e)
        {
            var medio = cmbMediosPago.SelectedItem as MedioPagoItem;
            if (medio == null) return;
            if (!decimal.TryParse(txtMonto.Text.Replace(",", "."), System.Globalization.NumberStyles.Number,
                System.Globalization.CultureInfo.InvariantCulture, out decimal monto) || monto <= 0)
            {
                CustomMessageBox.Show("Ingrese un monto válido mayor a cero.", "Monto inválido", MessageBoxButton.OK, MessageBoxImage.Warning);
                txtMonto.Focus();
                return;
            }

            // Si ya existe ese medio, sumar al existente
            var existente = _cobros.FirstOrDefault(c => c.nombreMedio == medio.Nombre);
            if (existente != null)
            {
                _cobros.Remove(existente);
                _cobros.Add(new CobranzaItem
                {
                    MedioPagoID = medio.MedioID,
                    nombreMedio = medio.Nombre,
                    monto = existente.monto + monto
                });
            }
            else
            {
                _cobros.Add(new CobranzaItem { MedioPagoID = medio.MedioID, nombreMedio = medio.Nombre, monto = monto });
            }

            // Sugerir el pendiente restante para el próximo medio
            decimal pendiente = _total - _cobros.Sum(c => c.monto);
            txtMonto.Text = pendiente > 0 ? pendiente.ToString("N2") : "0";
        }

        private void btnQuitarCobro_Click(object sender, RoutedEventArgs e)
        {
            if (sender is FrameworkElement fe && fe.DataContext is CobranzaItem item)
            {
                _cobros.Remove(item);
                decimal pendiente = _total - _cobros.Sum(c => c.monto);
                txtMonto.Text = pendiente > 0 ? pendiente.ToString("N2") : "0";
            }
        }

        private void btnConfirmar_Click(object sender, RoutedEventArgs e)
        {
            if (_cobros.Count == 0)
            {
                CustomMessageBox.Show("Debe ingresar al menos un medio de pago.", "Sin cobros", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }
            Cobranzas = _cobros.ToList();
            DialogResult = true;
            Close();
        }

        private void btnCancelar_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }

        private void NumericOnly_PreviewTextInput(object sender, TextCompositionEventArgs e)
        {
            e.Handled = !IsNumericInput(e.Text);
        }

        private bool IsNumericInput(string text)
        {
            foreach (char c in text)
                if (!char.IsDigit(c) && c != '.' && c != ',') return false;
            return true;
        }

        private void txtMonto_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter) btnAgregarPago_Click(sender, e);
        }

        private class MedioPagoItem
        {
            public int MedioID { get; set; }
            public string Nombre { get; set; }
        }
    }
}
