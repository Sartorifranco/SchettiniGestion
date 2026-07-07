using AdminLicencias.Models;
using AdminLicencias.Services;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;

namespace AdminLicencias.Views
{
    public partial class NuevaLicenciaView : Page
    {
        private readonly MainWindow _main;
        private bool _suppressDateSync = false;

        public NuevaLicenciaView(MainWindow main, Cliente clientePreseleccionado)
        {
            InitializeComponent();
            _main = main;

            CargarClientes(clientePreseleccionado);
            dpVence.SelectedDate = DateTime.Today.AddDays(365);
            PrecargarHwid(clientePreseleccionado);
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
            // dpVence puede ser null si este evento se dispara durante InitializeComponent
            // antes de que el DatePicker sea creado por el BAML loader
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
            // Validaciones
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

            // Generar clave
            string clave;
            try { clave = LicenseService.GenerarClave(cliente.CUIT, hwid, vence, modulos); }
            catch (Exception ex)
            { MessageBox.Show("Error al generar: " + ex.Message, "Error", MessageBoxButton.OK, MessageBoxImage.Error); return; }

            txtClave.Text = clave;

            // Guardar en registro
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

            // Si es renovación, vincular con la anterior
            if (lic.EsRenovacion)
            {
                var anterior = DataStore.UltimaLicencia(cliente.Id);
                if (anterior != null) lic.LicenciaAnteriorId = anterior.Id;
            }

            DataStore.GuardarLicencia(lic);

            // Mostrar resumen
            resCliente.Text  = cliente.RazonSocial;
            resHWID.Text     = hwid;
            resVence.Text    = vence.ToString("dd/MM/yyyy") + $"  ({lic.DiasRestantes} días)";
            resModulos.Text  = lic.ModulosResumen;
            resMonto.Text    = monto.ToString("C0", new System.Globalization.CultureInfo("es-AR"));
            panelResumen.Visibility = Visibility.Visible;
        }

        private List<string> RecolectarModulos()
        {
            var lista = new List<string>();
            var checks = new[] { chkFacturacion, chkProductos, chkStock, chkVentas,
                                  chkClientes, chkProveedores, chkCompras, chkCaja,
                                  chkPresupuestos, chkPrecios, chkListas, chkCuentas };
            foreach (var ch in checks)
                if (ch.IsChecked == true) lista.Add(ch.Tag.ToString());

            // Siempre incluidos en toda licencia (no opcionales).
            lista.Add("ACCESO_USUARIOS");
            lista.Add("ACCESO_PERMISOS");
            lista.Add("ACCESO_CONFIGURACION");
            // Facturación requiere Productos
            if (lista.Contains("ACCESO_FACTURACION") && !lista.Contains("ACCESO_PRODUCTOS"))
                lista.Add("ACCESO_PRODUCTOS");

            return lista;
        }

        // ── Copiar ────────────────────────────────────────────────────────
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
