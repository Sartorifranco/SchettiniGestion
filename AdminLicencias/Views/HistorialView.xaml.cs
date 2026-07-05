using AdminLicencias.Models;
using AdminLicencias.Services;
using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;

namespace AdminLicencias.Views
{
    public partial class HistorialView : Page
    {
        private List<LicenciaRow> _todas = new List<LicenciaRow>();

        public HistorialView()
        {
            InitializeComponent();
            CargarTodas();
            Aplicar();
        }

        private void CargarTodas()
        {
            _todas = DataStore.Licencias
                .Select(l => {
                    var c = DataStore.Clientes.FirstOrDefault(x => x.Id == l.ClienteId);
                    return new LicenciaRow(l, c);
                })
                .OrderByDescending(r => r._emision)
                .ToList();
        }

        private void Aplicar()
        {
            // Puede dispararse durante InitializeComponent antes de que los controles existan
            if (gridHistorial == null || txtBuscar == null || cbEstado == null) return;

            var lista = _todas.AsEnumerable();

            // Texto libre
            string q = txtBuscar.Text.Trim();
            if (!string.IsNullOrEmpty(q))
                lista = lista.Where(r =>
                    r.NombreCliente.IndexOf(q, StringComparison.OrdinalIgnoreCase) >= 0 ||
                    r.CUIT.Contains(q) ||
                    r.HWID.IndexOf(q, StringComparison.OrdinalIgnoreCase) >= 0);

            // Estado
            string est = (cbEstado.SelectedItem as ComboBoxItem)?.Content?.ToString();
            if (!string.IsNullOrEmpty(est) && est != "Todos los estados")
                lista = lista.Where(r => r.EstadoStr == est);

            // Fechas
            if (dpDesde.SelectedDate.HasValue)
                lista = lista.Where(r => r._emision >= dpDesde.SelectedDate.Value);
            if (dpHasta.SelectedDate.HasValue)
                lista = lista.Where(r => r._emision <= dpHasta.SelectedDate.Value.AddDays(1));

            var filas = lista.ToList();
            gridHistorial.ItemsSource = filas;

            decimal total = filas.Sum(r => r._monto);
            txtResumen.Text = $"{filas.Count} registro(s)  |  Total facturado en filtro: " +
                total.ToString("C0", new System.Globalization.CultureInfo("es-AR"));
        }

        // Handlers separados para que cada delegate type quede con firma exacta
        private void TxtBuscar_Changed(object s, System.Windows.Controls.TextChangedEventArgs e) => Aplicar();
        private void CbEstado_Changed(object s, System.Windows.Controls.SelectionChangedEventArgs e) => Aplicar();
        private void Dp_Changed(object s, System.Windows.Controls.SelectionChangedEventArgs e) => Aplicar();

        private void LimpiarFiltros_Click(object s, RoutedEventArgs e)
        {
            txtBuscar.Text = "";
            cbEstado.SelectedIndex = 0;
            dpDesde.SelectedDate = null;
            dpHasta.SelectedDate = null;
        }

        private void ExportCSV_Click(object s, RoutedEventArgs e)
        {
            var dlg = new SaveFileDialog
            {
                Title      = "Exportar historial CSV",
                Filter     = "CSV (*.csv)|*.csv",
                FileName   = $"historial_licencias_{DateTime.Today:yyyyMMdd}.csv"
            };
            if (dlg.ShowDialog() != true) return;

            try
            {
                var filas = (gridHistorial.ItemsSource as IEnumerable<LicenciaRow>)?
                    .Select(r => (
                        DataStore.Clientes.FirstOrDefault(c => c.CUIT == r.CUIT) ?? new Cliente { RazonSocial = r.NombreCliente, CUIT = r.CUIT },
                        DataStore.Licencias.FirstOrDefault(l => l.HWID == r.HWID &&
                            l.FechaEmision.ToString("dd/MM/yyyy") == r.FechaEmision)
                    ));
                LicenseService.ExportarCSV(dlg.FileName, filas);
                MessageBox.Show($"Exportado correctamente:\n{dlg.FileName}", "Éxito",
                    MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show("Error al exportar: " + ex.Message, "Error",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
    }

    internal class LicenciaRow
    {
        public string NombreCliente { get; }
        public string CUIT          { get; }
        public string HWID          { get; }
        public string Version       { get; }
        public string FechaEmision  { get; }
        public string Vencimiento   { get; }
        public string DiasRestantes { get; }
        public string EstadoStr     { get; }
        public string EstadoColor   { get; }
        public string Monto         { get; }
        public string Metodo        { get; }
        public string EsRenovacion  { get; }
        public string Modulos       { get; }

        internal DateTime _emision;
        internal decimal  _monto;

        public LicenciaRow(Licencia l, Cliente c)
        {
            NombreCliente = c?.RazonSocial ?? "(cliente eliminado)";
            CUIT          = c?.CUIT ?? "";
            HWID          = l.HWID;
            Version       = l.VersionSchpos;
            _emision      = l.FechaEmision;
            FechaEmision  = l.FechaEmision.ToString("dd/MM/yyyy");
            Vencimiento   = l.FechaVencimiento.ToString("dd/MM/yyyy");
            DiasRestantes = l.DiasRestantes.ToString();
            EstadoStr     = l.Estado.ToString();
            EstadoColor   = l.Estado == EstadoLicencia.Activa    ? "green"
                          : l.Estado == EstadoLicencia.PorVencer ? "orange"
                          : l.Estado == EstadoLicencia.Vencida   ? "red"
                          : "gray";
            _monto        = l.MontoVenta;
            Monto         = l.MontoVenta.ToString("C0", new System.Globalization.CultureInfo("es-AR"));
            Metodo        = l.MetodoPago;
            EsRenovacion  = l.EsRenovacion ? "Sí" : "No";
            Modulos       = l.ModulosResumen;
        }
    }
}
