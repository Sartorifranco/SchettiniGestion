using AdminLicencias.Models;
using AdminLicencias.Services;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;

namespace AdminLicencias.Views
{
    public partial class ClientesView : Page
    {
        private readonly MainWindow _main;
        private Cliente _selected;
        private List<ClienteRow> _rows = new List<ClienteRow>();

        public ClientesView(MainWindow main)
        {
            InitializeComponent();
            _main = main;
            CollapseDetail();
            CargarGrid();
        }

        // ── Grid ──────────────────────────────────────────────────────────
        private void CargarGrid(string filtro = "")
        {
            _rows = DataStore.Clientes
                .Where(c => string.IsNullOrEmpty(filtro) ||
                            c.RazonSocial.IndexOf(filtro, StringComparison.OrdinalIgnoreCase) >= 0 ||
                            c.CUIT.Contains(filtro) ||
                            c.Ciudad.IndexOf(filtro, StringComparison.OrdinalIgnoreCase) >= 0)
                .OrderBy(c => c.RazonSocial)
                .Select(c => new ClienteRow(c))
                .ToList();

            gridClientes.ItemsSource = _rows;
            txtTotalClientes.Text = $"{_rows.Count} cliente(s) encontrado(s)";
        }

        private void Buscar_Changed(object s, TextChangedEventArgs e)
            => CargarGrid(txtBuscar.Text.Trim());

        private void Grid_SelectionChanged(object s, SelectionChangedEventArgs e)
        {
            if (gridClientes.SelectedItem is ClienteRow row)
                MostrarDetalle(row.Id);
        }

        // ── Detalle ───────────────────────────────────────────────────────
        private void MostrarDetalle(Guid id)
        {
            _selected = DataStore.Clientes.FirstOrDefault(c => c.Id == id);
            if (_selected == null) return;

            colDetalle.Width = new GridLength(300);

            txtDetalleNombre.Text = _selected.RazonSocial;
            dCUIT.Text     = _selected.CUIT;
            dCiudad.Text   = string.IsNullOrWhiteSpace(_selected.Provincia)
                ? _selected.Ciudad
                : $"{_selected.Ciudad}, {_selected.Provincia}";
            dContacto.Text  = _selected.Contacto;
            dTelefono.Text  = _selected.Telefono;
            dEmail.Text     = _selected.Email;
            dPuestos.Text   = $"{_selected.CantidadPuestos} puesto(s)";
            dAlta.Text      = _selected.FechaAlta.ToString("dd/MM/yyyy");
            dNotas.Text     = string.IsNullOrWhiteSpace(_selected.Notas) ? "Sin notas." : _selected.Notas;

            dIP.Text = string.IsNullOrWhiteSpace(_selected.IPServidor)
                ? "Sin IP configurada"
                : $"{_selected.IPServidor}:{_selected.PuertoServidor}";
            txtPingResult.Text = "";

            var lic = DataStore.UltimaLicencia(_selected.Id);
            if (lic != null)
            {
                panelUltimaLic.Visibility = Visibility.Visible;
                dSinLicencia.Visibility   = Visibility.Collapsed;
                dLicVersion.Text    = $"SCHPOS {lic.VersionSchpos}";
                dLicVencimiento.Text = $"Vence: {lic.FechaVencimiento:dd/MM/yyyy}  ({lic.DiasRestantes} días)";
                dLicModulos.Text    = lic.ModulosResumen;
                dLicHWID.Text       = string.IsNullOrWhiteSpace(lic.HWID) ? "—" : lic.HWID;
                dLicMonto.Text      = lic.MontoVenta > 0
                    ? lic.MontoVenta.ToString("C0", new System.Globalization.CultureInfo("es-AR"))
                    : "Sin monto registrado";
            }
            else
            {
                panelUltimaLic.Visibility = Visibility.Collapsed;
                dSinLicencia.Visibility   = Visibility.Visible;
            }
        }

        private void CollapseDetail()
        {
            colDetalle.Width = new GridLength(0);
            _selected = null;
        }

        private void CerrarDetalle_Click(object s, RoutedEventArgs e)
        {
            CollapseDetail();
            gridClientes.SelectedItem = null;
        }

        // ── Ping ──────────────────────────────────────────────────────────
        private async void TestConexion_Click(object s, RoutedEventArgs e)
        {
            if (_selected == null) return;
            txtPingResult.Text = "Probando…";
            var (ok, msg) = await LicenseService.TestConexionAsync(
                _selected.IPServidor, _selected.PuertoServidor);
            txtPingResult.Foreground = ok
                ? (System.Windows.Media.Brush)FindResource("GreenBrush")
                : (System.Windows.Media.Brush)FindResource("RedBrush");
            txtPingResult.Text = msg;
        }

        // ── Botones acción ────────────────────────────────────────────────
        private void NuevoCliente_Click(object s, RoutedEventArgs e)
            => AbrirFormCliente(null);

        private void EditarCliente_Click(object s, RoutedEventArgs e)
        {
            if (_selected != null) AbrirFormCliente(_selected);
        }

        private void AbrirFormCliente(Cliente c)
        {
            var dlg = new ClienteFormWindow(c) { Owner = Window.GetWindow(this) };
            if (dlg.ShowDialog() == true)
            {
                DataStore.GuardarCliente(dlg.ClienteResultado);
                CargarGrid(txtBuscar.Text.Trim());
                if (dlg.ClienteResultado != null)
                    MostrarDetalle(dlg.ClienteResultado.Id);
            }
        }

        private void GenLicencia_Click(object s, RoutedEventArgs e)
        {
            if (_selected != null)
                _main.NavigateTo("nueva", _selected);
        }

        private void VerHistorial_Click(object s, RoutedEventArgs e)
        {
            if (_selected != null)
                _main.NavigateTo("historial", _selected);
        }
    }

    // ── Clase de fila para el DataGrid ──────────────────────────────────
    internal class ClienteRow
    {
        public Guid   Id           { get; }
        public string RazonSocial  { get; }
        public string CUIT         { get; }
        public string Ciudad       { get; }
        public string Contacto     { get; }
        public string Version      { get; }
        public string Vencimiento  { get; }
        public string DiasRestantes { get; }
        public string LicEstado    { get; }
        public string EstadoColor  { get; }

        public ClienteRow(Cliente c)
        {
            Id          = c.Id;
            RazonSocial = c.RazonSocial;
            CUIT        = c.CUIT;
            Ciudad      = c.Ciudad;
            Contacto    = c.Contacto;

            var lic = DataStore.UltimaLicencia(c.Id);
            if (lic != null)
            {
                Version       = lic.VersionSchpos;
                Vencimiento   = lic.FechaVencimiento.ToString("dd/MM/yyyy");
                DiasRestantes = lic.DiasRestantes.ToString();
                LicEstado     = lic.Estado.ToString();
                EstadoColor   = lic.Estado == EstadoLicencia.Activa   ? "green"
                              : lic.Estado == EstadoLicencia.PorVencer ? "orange"
                              : "red";
            }
            else
            {
                Version = DiasRestantes = "-"; Vencimiento = "Sin licencia";
                LicEstado = "Sin lic."; EstadoColor = "gray";
            }
        }
    }
}
