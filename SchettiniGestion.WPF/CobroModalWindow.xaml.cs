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

    /// <summary>Opción de medio de pago (clase pública para binding WPF en ComboBox).</summary>
    public class MedioPagoOpcion
    {
        public int MedioID { get; set; }
        public string Nombre { get; set; }
        public override string ToString() => Nombre ?? string.Empty;
    }

    public partial class CobroModalWindow : Window
    {
        public List<CobranzaItem> Cobranzas { get; private set; } = new List<CobranzaItem>();

        private readonly decimal _total;
        private readonly ObservableCollection<CobranzaItem> _cobros = new ObservableCollection<CobranzaItem>();

        public CobroModalWindow(Window owner, decimal total)
        {
            Owner = owner;
            _total = total;
            InitializeComponent();
            Loaded += CobroModalWindow_Loaded;
        }

        private void CobroModalWindow_Loaded(object sender, RoutedEventArgs e)
        {
            lblTotalACobrar.Text = _total.ToString("C2");
            dgvCobranzas.ItemsSource = _cobros;
            _cobros.CollectionChanged += (s, ev) => ActualizarResumen();

            CargarMediosPago();
            ActualizarResumen();

            txtMonto.Text = _total.ToString("N2");
            txtMonto.SelectAll();
            txtMonto.Focus();
        }

        private void CargarMediosPago()
        {
            try
            {
                var dt = DatabaseService.GetMediosPagoCompleto();
                var lista = new List<MedioPagoOpcion>();
                foreach (DataRow row in dt.Rows)
                {
                    if (row["Activo"] != DBNull.Value && Convert.ToBoolean(row["Activo"]))
                    {
                        lista.Add(new MedioPagoOpcion
                        {
                            MedioID = Convert.ToInt32(row["MedioID"]),
                            Nombre = row["Nombre"]?.ToString() ?? "Medio"
                        });
                    }
                }

                if (lista.Count == 0)
                    lista = CrearMediosFallback();

                cmbMediosPago.ItemsSource = lista;
                cmbMediosPago.SelectedIndex = 0;
            }
            catch
            {
                cmbMediosPago.ItemsSource = CrearMediosFallback();
                cmbMediosPago.SelectedIndex = 0;
            }
        }

        private static List<MedioPagoOpcion> CrearMediosFallback()
        {
            return new List<MedioPagoOpcion>
            {
                new MedioPagoOpcion { MedioID = 1, Nombre = "Efectivo" },
                new MedioPagoOpcion { MedioID = 2, Nombre = "Tarjeta Débito" },
                new MedioPagoOpcion { MedioID = 3, Nombre = "Tarjeta Crédito" },
                new MedioPagoOpcion { MedioID = 4, Nombre = "Transferencia" }
            };
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
            var medio = cmbMediosPago.SelectedItem as MedioPagoOpcion;
            if (medio == null) return;

            if (!decimal.TryParse(txtMonto.Text.Replace(",", "."), System.Globalization.NumberStyles.Number,
                System.Globalization.CultureInfo.InvariantCulture, out decimal monto) || monto <= 0)
            {
                ModernMessageBox.Show("Ingrese un monto válido mayor a cero.", "Monto inválido", MessageBoxButton.OK, MessageBoxImage.Warning);
                txtMonto.Focus();
                return;
            }

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
                ModernMessageBox.Show("Debe ingresar al menos un medio de pago.", "Sin cobros", MessageBoxButton.OK, MessageBoxImage.Warning);
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

        private static bool IsNumericInput(string text)
        {
            foreach (char c in text)
                if (!char.IsDigit(c) && c != '.' && c != ',') return false;
            return true;
        }

        private void txtMonto_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter) btnAgregarPago_Click(sender, e);
        }
    }
}
