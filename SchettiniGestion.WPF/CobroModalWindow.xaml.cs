using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Data;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
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
        public int NroCuotas { get; set; } = 1;
        public string UltimosDigitosTarjeta { get; set; }
        public string MarcaTarjeta { get; set; }
        public string OperacionExternaID { get; set; }
        public string MontoFormateado => monto.ToString("C2");
    }

    /// <summary>Opción de medio de pago (clase pública para binding WPF en ComboBox).</summary>
    public class MedioPagoOpcion
    {
        public int MedioID { get; set; }
        public string Nombre { get; set; }
        public decimal RecargoDescuentoPct { get; set; }
        public override string ToString() => Nombre ?? string.Empty;
    }

    public partial class CobroModalWindow : Window
    {
        public List<CobranzaItem> Cobranzas { get; private set; } = new List<CobranzaItem>();

        /// <summary>El usuario eligió cobrar con Mercado Pago QR desde el modal.</summary>
        public bool SolicitoMercadoPagoQR { get; private set; }
        /// <summary>El usuario eligió enviar el cobro al Point Smart.</summary>
        public bool SolicitoMercadoPagoPoint { get; private set; }

        /// <summary>Recargo (+) o descuento (−) del medio usado en cobro rápido. 0 si no aplica.</summary>
        public decimal RecargoPorcentajeAplicado { get; private set; }

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

            var medios = CargarMediosPago();
            ActualizarResumen();
            AplicarVisibilidadMercadoPago();
            ConstruirBotonesCobroRapido(medios);

            txtMonto.Text = _total.ToString("N2");
        }

        private List<MedioPagoOpcion> CargarMediosPago()
        {
            var lista = new List<MedioPagoOpcion>();
            try
            {
                var dt = DatabaseService.GetMediosPagoCompleto();
                foreach (DataRow row in dt.Rows)
                {
                    if (row["Activo"] != DBNull.Value && Convert.ToBoolean(row["Activo"]))
                    {
                        lista.Add(new MedioPagoOpcion
                        {
                            MedioID = Convert.ToInt32(row["MedioID"]),
                            Nombre = row["Nombre"]?.ToString() ?? "Medio",
                            RecargoDescuentoPct = row.Table.Columns.Contains("RecargoDescuentoPct") && row["RecargoDescuentoPct"] != DBNull.Value
                                ? Convert.ToDecimal(row["RecargoDescuentoPct"])
                                : 0m
                        });
                    }
                }

                if (lista.Count == 0)
                    lista = CrearMediosFallback();

                cmbMediosPago.ItemsSource = lista;
                PreferirEfectivo(lista);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine("[CobroModal] Error cargando medios de pago: " + ex.Message);
                lista = CrearMediosFallback();
                cmbMediosPago.ItemsSource = lista;
                cmbMediosPago.SelectedIndex = 0;
            }

            return lista;
        }

        private void PreferirEfectivo(List<MedioPagoOpcion> lista)
        {
            int idx = lista.FindIndex(m => (m.Nombre ?? "").IndexOf("efectivo", StringComparison.OrdinalIgnoreCase) >= 0);
            cmbMediosPago.SelectedIndex = idx >= 0 ? idx : 0;
        }

        private void ConstruirBotonesCobroRapido(List<MedioPagoOpcion> medios)
        {
            panelCobroRapido.Children.Clear();
            if (medios == null || medios.Count == 0) return;

            // Priorizar Efectivo / Tarjeta / Transferencia al frente
            var ordenados = medios
                .OrderBy(m => PrioridadMedio(m.Nombre))
                .ThenBy(m => m.Nombre)
                .Take(6)
                .ToList();

            foreach (var medio in ordenados)
            {
                var btn = new Button
                {
                    Content = EtiquetaMedio(medio),
                    Tag = medio,
                    MinWidth = 150,
                    MinHeight = 56,
                    Margin = new Thickness(0, 0, 8, 8),
                    FontSize = 15,
                    FontWeight = FontWeights.SemiBold,
                    Cursor = Cursors.Hand,
                    Style = TryFindResource("ButtonStyle") as Style
                };
                if (EsEfectivo(medio.Nombre))
                    btn.Background = TryFindResource("SuccessColor") as Brush ?? btn.Background;
                btn.Click += (s, e) =>
                {
                    if (s is Button b && b.Tag is MedioPagoOpcion m)
                        CobrarRapidoYCerrar(m);
                };
                panelCobroRapido.Children.Add(btn);
            }
        }

        private static string EtiquetaMedio(MedioPagoOpcion medio)
        {
            if (medio == null) return "";
            if (medio.RecargoDescuentoPct == 0m) return medio.Nombre;
            string signo = medio.RecargoDescuentoPct > 0 ? "+" : "";
            return $"{medio.Nombre} ({signo}{medio.RecargoDescuentoPct:0.##}%)";
        }

        private static int PrioridadMedio(string nombre)
        {
            string n = (nombre ?? "").ToLowerInvariant();
            if (n.Contains("efectivo")) return 0;
            if (n.Contains("tarjeta") || n.Contains("débito") || n.Contains("debito") || n.Contains("crédito") || n.Contains("credito")) return 1;
            if (n.Contains("transfer")) return 2;
            return 10;
        }

        private static bool EsEfectivo(string nombre) =>
            (nombre ?? "").IndexOf("efectivo", StringComparison.OrdinalIgnoreCase) >= 0;

        private void CobrarRapidoYCerrar(MedioPagoOpcion medio)
        {
            if (medio == null || _total <= 0) return;
            decimal pct = medio.RecargoDescuentoPct;
            decimal monto = _total;
            if (pct != 0m)
            {
                monto = Math.Round(_total * (1 + pct / 100m), 2, MidpointRounding.AwayFromZero);
                string tipo = pct > 0 ? "un recargo" : "un descuento";
                string signo = pct > 0 ? "+" : "";
                if (CustomMessageBox.Show(
                        $"{medio.Nombre} aplica {tipo} del {signo}{pct:0.##}%.\n\n" +
                        $"Total: {_total:C2}  →  {monto:C2}\n\n¿Cobrar?",
                        "Ajuste por medio de pago",
                        MessageBoxButton.YesNo, MessageBoxImage.Question) != MessageBoxResult.Yes)
                    return;
            }

            RecargoPorcentajeAplicado = pct;
            Cobranzas = new List<CobranzaItem>
            {
                new CobranzaItem
                {
                    MedioPagoID = medio.MedioID,
                    nombreMedio = medio.Nombre,
                    monto = monto
                }
            };
            DialogResult = true;
            Close();
        }

        private static List<MedioPagoOpcion> CrearMediosFallback()
        {
            return new List<MedioPagoOpcion>
            {
                new MedioPagoOpcion { MedioID = 0, Nombre = "Efectivo (sin ID)" },
                new MedioPagoOpcion { MedioID = 0, Nombre = "Tarjeta (sin ID)" },
                new MedioPagoOpcion { MedioID = 0, Nombre = "Transferencia (sin ID)" }
            };
        }

        private void cmbMediosPago_SelectionChanged(object sender, SelectionChangedEventArgs e)
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

            btnConfirmar.IsEnabled = cobrado >= _total && _cobros.Count > 0;
        }

        private static Brush BrushFromTheme(string key) =>
            Application.Current?.TryFindResource(key) as Brush ?? Brushes.Gray;

        private void btnAgregarPago_Click(object sender, RoutedEventArgs e)
        {
            var medio = cmbMediosPago.SelectedItem as MedioPagoOpcion;
            if (medio == null) return;

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
            bool esEfectivo = EsEfectivo(medio.Nombre);
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
                // Sin líneas mixtas: confirmar con el medio seleccionado al total (atajo)
                if (cmbMediosPago.SelectedItem is MedioPagoOpcion medio)
                {
                    CobrarRapidoYCerrar(medio);
                    return;
                }
                CustomMessageBox.Show("Elegí un medio de pago rápido o agregá un cobro.", "Sin cobros", MessageBoxButton.OK, MessageBoxImage.Warning);
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

        private void Window_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Escape)
            {
                btnCancelar_Click(sender, e);
                e.Handled = true;
                return;
            }

            // Enter / F2: cobro rápido con Efectivo (o medio seleccionado)
            if (e.Key == Key.F2 || (e.Key == Key.Enter && !EsFocoEnMontoMixto()))
            {
                if (_cobros.Count > 0 && btnConfirmar.IsEnabled)
                {
                    btnConfirmar_Click(sender, e);
                    e.Handled = true;
                    return;
                }

                if (cmbMediosPago.SelectedItem is MedioPagoOpcion medio)
                {
                    CobrarRapidoYCerrar(medio);
                    e.Handled = true;
                }
            }
        }

        private bool EsFocoEnMontoMixto() =>
            txtMonto != null && txtMonto.IsKeyboardFocusWithin;

        private void AplicarVisibilidadMercadoPago()
        {
            if (btnMercadoPagoQR != null)
                btnMercadoPagoQR.Visibility = LicenseManager.TieneMercadoPagoQr()
                    ? Visibility.Visible
                    : Visibility.Collapsed;

            bool pointConfigurado = false;
            try
            {
                DataRow config = DatabaseService.GetConfiguracion();
                pointConfigurado = config != null
                    && config.Table.Columns.Contains("MPPointTerminalId")
                    && !string.IsNullOrWhiteSpace(config["MPPointTerminalId"]?.ToString())
                    && config.Table.Columns.Contains("MPPointAutomatico")
                    && config["MPPointAutomatico"] != DBNull.Value
                    && Convert.ToBoolean(config["MPPointAutomatico"]);
            }
            catch { }

            if (btnMercadoPagoPoint != null)
                btnMercadoPagoPoint.Visibility = LicenseManager.TieneMercadoPagoPoint() && pointConfigurado
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

        private void btnMercadoPagoPoint_Click(object sender, RoutedEventArgs e)
        {
            if (!LicenseManager.TieneMercadoPagoPoint())
            {
                CustomMessageBox.Show(
                    "Mercado Pago Point no está incluido en su licencia.\n\n" +
                    "Puede continuar cobrando manualmente con cualquier posnet.",
                    "Abono no habilitado", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            SolicitoMercadoPagoPoint = true;
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
            if (e.Key == Key.Enter)
            {
                btnAgregarPago_Click(sender, e);
                e.Handled = true;
            }
        }
    }
}
