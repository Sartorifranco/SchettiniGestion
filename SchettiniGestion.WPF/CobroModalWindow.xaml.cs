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

        /// <summary>El usuario eligió cobrar con Mercado Pago QR desde el modal.</summary>
        public bool SolicitoMercadoPagoQR { get; private set; }

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
            AplicarVisibilidadMercadoPago();

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
                {
                    lista = CrearMediosFallback();
                    _usandoFallback = true;
                }

                cmbMediosPago.ItemsSource = lista;
                cmbMediosPago.SelectedIndex = 0;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine("[CobroModal] Error cargando medios de pago: " + ex.Message);
                cmbMediosPago.ItemsSource = CrearMediosFallback();
                cmbMediosPago.SelectedIndex = 0;
                _usandoFallback = true;
            }

            if (_usandoFallback)
            {
                // Los IDs del fallback podrían no coincidir con los de la BD.
                // Avisamos al operador para que configure los medios de pago.
                var aviso = new System.Windows.Controls.TextBlock
                {
                    Text = "⚠️ Los medios de pago no pudieron cargarse desde la base de datos. Configure los medios en Configuración.",
                    Foreground = System.Windows.Media.Brushes.OrangeRed,
                    FontSize = 12,
                    TextWrapping = System.Windows.TextWrapping.Wrap,
                    Margin = new System.Windows.Thickness(0, 4, 0, 0)
                };
                // Insertar aviso justo encima del combo si el panel padre lo permite
                if (cmbMediosPago.Parent is System.Windows.Controls.Panel panel)
                {
                    int idx = panel.Children.IndexOf(cmbMediosPago);
                    if (idx >= 0) panel.Children.Insert(idx, aviso);
                }
            }
        }

        private bool _usandoFallback = false;

        private static List<MedioPagoOpcion> CrearMediosFallback()
        {
            return new List<MedioPagoOpcion>
            {
                new MedioPagoOpcion { MedioID = 0, Nombre = "Efectivo (sin ID)" },
                new MedioPagoOpcion { MedioID = 0, Nombre = "Tarjeta (sin ID)" },
                new MedioPagoOpcion { MedioID = 0, Nombre = "Transferencia (sin ID)" }
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

            // Formato argentino: "9.000,00" → quitar separador de miles (.) → reemplazar decimal (,→.) → "9000.00"
            string montoStr = txtMonto.Text.Trim().Replace(".", "").Replace(",", ".");
            if (!decimal.TryParse(montoStr, System.Globalization.NumberStyles.Number,
                System.Globalization.CultureInfo.InvariantCulture, out decimal monto) || monto <= 0)
            {
                CustomMessageBox.Show("Ingrese un monto válido mayor a cero.", "Monto inválido", MessageBoxButton.OK, MessageBoxImage.Warning);
                txtMonto.Focus();
                return;
            }

            decimal pendienteAntes = _total - _cobros.Sum(c => c.monto);
            if (pendienteAntes <= 0)
            {
                CustomMessageBox.Show("El total ya fue cubierto.", "Cobro completo", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }
            bool esEfectivo = (medio.Nombre ?? "").IndexOf("efectivo", StringComparison.OrdinalIgnoreCase) >= 0;
            // En efectivo permitir billete mayor (vuelto); en otros medios solo lo imputable
            if (!esEfectivo && monto > pendienteAntes) monto = pendienteAntes;

            var existente = _cobros.FirstOrDefault(c => c.MedioPagoID == medio.MedioID && medio.MedioID > 0)
                ?? _cobros.FirstOrDefault(c => c.nombreMedio == medio.Nombre);
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

        private void AplicarVisibilidadMercadoPago()
        {
            if (btnMercadoPagoQR == null) return;
            btnMercadoPagoQR.Visibility = LicenseManager.TieneMercadoPagoQr()
                ? Visibility.Visible
                : Visibility.Collapsed;
        }

        private void btnMercadoPagoQR_Click(object sender, RoutedEventArgs e)
        {
            if (!LicenseManager.TieneMercadoPagoQr())
            {
                CustomMessageBox.Show(
                    "Mercado Pago QR no está incluido en su licencia.\n\n" +
                    "Solicite el abono «Mercado Pago QR» para cobrar con código QR.",
                    "Abono no habilitado", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            SolicitoMercadoPagoQR = true;
            DialogResult = true;
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
