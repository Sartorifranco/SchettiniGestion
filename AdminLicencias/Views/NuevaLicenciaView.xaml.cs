using AdminLicencias.Models;
using AdminLicencias.Services;
using SchettiniGestion;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace AdminLicencias.Views
{
    public partial class NuevaLicenciaView : Page
    {
        private readonly MainWindow _main;
        private bool _suppressDateSync = false;
        private readonly List<CheckBox> _checksModulos = new List<CheckBox>();

        private static readonly string[] OrdenGrupos =
        {
            ModulosCatalog.GrupoLiteBase,
            ModulosCatalog.GrupoModuloAdicional,
            ModulosCatalog.GrupoExtraUnico,
            ModulosCatalog.GrupoAbonoMensual
        };

        public NuevaLicenciaView(MainWindow main, Cliente clientePreseleccionado)
        {
            InitializeComponent();
            _main = main;

            ConstruirCheckboxesModulos();
            AplicarPresetLite();
            CargarClientes(clientePreseleccionado);
            dpVence.SelectedDate = DateTime.Today.AddDays(365);
            PrecargarHwid(clientePreseleccionado);
        }

        private void ConstruirCheckboxesModulos()
        {
            panelModulosLicencia.Children.Clear();
            _checksModulos.Clear();

            foreach (string grupo in OrdenGrupos)
            {
                var mods = ModulosCatalog.ObtenerPorGrupo(grupo);
                if (mods.Count == 0) continue;

                panelModulosLicencia.Children.Add(new TextBlock
                {
                    Text = ModulosCatalog.ObtenerTituloGrupo(grupo),
                    FontWeight = FontWeights.SemiBold,
                    Foreground = (Brush)FindResource("TextPrimary"),
                    Margin = new Thickness(0, grupo == ModulosCatalog.GrupoLiteBase ? 0 : 14, 0, 6)
                });

                if (grupo == ModulosCatalog.GrupoAbonoMensual)
                {
                    panelModulosLicencia.Children.Add(new TextBlock
                    {
                        Text = "Renovar periódicamente al emitir una nueva licencia con estos ítems tildados.",
                        FontSize = 10,
                        Foreground = (Brush)FindResource("TextSecondary"),
                        TextWrapping = TextWrapping.Wrap,
                        Margin = new Thickness(0, 0, 0, 6)
                    });
                }

                var grid = new System.Windows.Controls.Primitives.UniformGrid
                {
                    Columns = 2,
                    Margin = new Thickness(0, 0, 0, 4)
                };

                foreach (var mod in mods)
                {
                    string etiqueta = mod.EsAbonoMensual ? mod.Nombre + "  (abono)" : mod.Nombre;
                    var chk = new CheckBox
                    {
                        Content = etiqueta,
                        Tag = mod.Codigo,
                        Margin = new Thickness(0, 5, 0, 5)
                    };
                    grid.Children.Add(chk);
                    _checksModulos.Add(chk);
                }

                panelModulosLicencia.Children.Add(grid);
            }

            var implicitos = ModulosCatalog.ObtenerImplicitos()
                .Select(ModulosCatalog.ObtenerNombreLegible)
                .ToList();

            txtModulosImplicitos.Text = implicitos.Count > 0
                ? "Siempre incluidos: " + string.Join(", ", implicitos) + "."
                : "";
        }

        private void AplicarPresetLite()
        {
            var lite = new HashSet<string>(ModulosCatalog.ObtenerPresetLite(), StringComparer.OrdinalIgnoreCase);
            foreach (var chk in _checksModulos)
            {
                if (chk.Tag is string codigo)
                    chk.IsChecked = lite.Contains(codigo);
            }
        }

        private void PresetLite_Click(object sender, RoutedEventArgs e) => AplicarPresetLite();

        private void LimpiarModulos_Click(object sender, RoutedEventArgs e)
        {
            foreach (var chk in _checksModulos)
                chk.IsChecked = false;
        }

        private void PrecargarHwid(Cliente cliente)
        {
            if (cliente == null) return;
            var ultima = DataStore.UltimaLicencia(cliente.Id);
            if (ultima != null && !string.IsNullOrWhiteSpace(ultima.HWID))
                txtHWID.Text = ultima.HWID.Trim().ToUpperInvariant();
        }

        // ── Clientes ──────────────────────────────────────────────────────
        private void CargarClientes(Cliente presel)
        {
            cbCliente.ItemsSource   = DataStore.Clientes.Where(c => c.Activo).OrderBy(c => c.RazonSocial).ToList();
            cbCliente.DisplayMemberPath = "RazonSocial";

            if (presel != null)
                cbCliente.SelectedItem = DataStore.Clientes.FirstOrDefault(c => c.Id == presel.Id);
        }

        private void CbCliente_Changed(object s, SelectionChangedEventArgs e)
        {
            if (cbCliente.SelectedItem is Cliente c)
                txtCuit.Text = c.CUIT;
            else
                txtCuit.Text = "";
        }

        private void NuevoCliente_Click(object s, RoutedEventArgs e)
        {
            var dlg = new ClienteFormWindow(null) { Owner = Window.GetWindow(this) };
            if (dlg.ShowDialog() == true)
            {
                DataStore.GuardarCliente(dlg.ClienteResultado);
                CargarClientes(dlg.ClienteResultado);
            }
        }

        // ── Sincronización días ↔ fecha ──────────────────────────────────
        private void Dias_Changed(object s, TextChangedEventArgs e)
        {
            if (_suppressDateSync || dpVence == null) return;
            if (int.TryParse(txtDias.Text, out int d) && d > 0)
            {
                _suppressDateSync = true;
                dpVence.SelectedDate = DateTime.Today.AddDays(d);
                _suppressDateSync = false;
            }
        }

        private void Dp_Changed(object s, SelectionChangedEventArgs e)
        {
            if (_suppressDateSync || txtDias == null) return;
            if (dpVence.SelectedDate.HasValue)
            {
                _suppressDateSync = true;
                int dias = (int)(dpVence.SelectedDate.Value - DateTime.Today).TotalDays;
                txtDias.Text = Math.Max(dias, 1).ToString();
                _suppressDateSync = false;
            }
        }

        // ── Generar ───────────────────────────────────────────────────────
        private void Generar_Click(object s, RoutedEventArgs e)
        {
            if (cbCliente.SelectedItem == null)
            { MessageBox.Show("Seleccioná un cliente.", "Validación", MessageBoxButton.OK, MessageBoxImage.Warning); return; }

            if (string.IsNullOrWhiteSpace(txtHWID.Text))
            { MessageBox.Show("Ingresá el Hardware ID del equipo del cliente.", "Validación", MessageBoxButton.OK, MessageBoxImage.Warning); return; }

            if (!dpVence.SelectedDate.HasValue || dpVence.SelectedDate.Value <= DateTime.Today)
            { MessageBox.Show("La fecha de vencimiento debe ser futura.", "Validación", MessageBoxButton.OK, MessageBoxImage.Warning); return; }

            var cliente  = (Cliente)cbCliente.SelectedItem;
            var hwid     = txtHWID.Text.Trim().ToUpperInvariant();
            var vence    = dpVence.SelectedDate.Value;
            var modulos  = RecolectarModulos();
            decimal monto = decimal.TryParse(txtMonto.Text.Replace(",", "."),
                System.Globalization.NumberStyles.Any,
                System.Globalization.CultureInfo.InvariantCulture, out decimal m) ? m : 0;

            string clave;
            try { clave = LicenseService.GenerarClave(cliente.CUIT, hwid, vence, modulos); }
            catch (Exception ex)
            { MessageBox.Show("Error al generar: " + ex.Message, "Error", MessageBoxButton.OK, MessageBoxImage.Error); return; }

            txtClave.Text = clave;

            var lic = new Licencia
            {
                ClienteId        = cliente.Id,
                HWID             = hwid,
                LicenseKey       = clave,
                FechaEmision     = DateTime.Today,
                FechaVencimiento = vence,
                Modulos          = modulos,
                MontoVenta       = monto,
                MetodoPago       = (cbPago.SelectedItem as ComboBoxItem)?.Content?.ToString() ?? "Transferencia",
                VersionSchpos    = txtVersion.Text.Trim(),
                EsRenovacion     = chkRenovacion.IsChecked == true,
                Observaciones    = txtObs.Text.Trim()
            };

            if (lic.EsRenovacion)
            {
                var anterior = DataStore.UltimaLicencia(cliente.Id);
                if (anterior != null) lic.LicenciaAnteriorId = anterior.Id;
            }

            DataStore.GuardarLicencia(lic);

            resCliente.Text  = cliente.RazonSocial;
            resHWID.Text     = hwid;
            resVence.Text    = vence.ToString("dd/MM/yyyy") + $"  ({lic.DiasRestantes} días)";
            resModulos.Text  = lic.ModulosResumen;
            resMonto.Text    = monto.ToString("C0", new System.Globalization.CultureInfo("es-AR"));
            panelResumen.Visibility = Visibility.Visible;
        }

        private List<string> RecolectarModulos()
        {
            var seleccionados = _checksModulos
                .Where(ch => ch.IsChecked == true && ch.Tag != null)
                .Select(ch => ch.Tag.ToString())
                .ToList();

            return ModulosCatalog.ResolverLicencia(seleccionados);
        }

        private void Copiar_Click(object s, RoutedEventArgs e)
        {
            if (!string.IsNullOrWhiteSpace(txtClave.Text) &&
                txtClave.Text != "(aparecerá aquí tras generar)")
            {
                Clipboard.SetText(txtClave.Text);
                MessageBox.Show("Clave copiada al portapapeles.", "Copiado",
                    MessageBoxButton.OK, MessageBoxImage.Information);
            }
        }
    }
}
