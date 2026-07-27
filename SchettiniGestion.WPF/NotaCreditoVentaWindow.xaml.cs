using SchettiniGestion;
using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Data;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Windows;
using System.Windows.Controls;

namespace SchettiniGestion.WPF
{
    public partial class NotaCreditoVentaWindow : Window
    {
        public int ResultID { get; private set; }

        private readonly int _facturaId;
        private readonly DataRow _encabezado;
        private readonly ObservableCollection<ItemNotaCredito> _items = new ObservableCollection<ItemNotaCredito>();

        public NotaCreditoVentaWindow(int facturaId)
        {
            _facturaId = facturaId;
            _encabezado = DatabaseService.GetFacturaPorID(facturaId);
            if (_encabezado == null)
                throw new InvalidOperationException("No se encontro la factura indicada.");

            InitializeComponent();

            lblFactura.Text = $"Factura #{_facturaId:D8}";
            lblCliente.Text = $"Cliente: {Valor("ClienteNombre", "-")}";
            lblTotal.Text = $"Total: {TotalFactura():C2}";

            dgvItems.ItemsSource = _items;

            CargarItems();
            ActualizarModo();
            ActualizarResumen();
        }

        private void CargarItems()
        {
            DataTable detalle = DatabaseService.GetFacturaDetalle(_facturaId);
            foreach (DataRow r in detalle.Rows)
            {
                var item = new ItemNotaCredito
                {
                    ProductoID = r.Table.Columns.Contains("ProductoID") && r["ProductoID"] != DBNull.Value ? Convert.ToInt32(r["ProductoID"]) : 0,
                    Codigo = r.Table.Columns.Contains("Codigo") ? r["Codigo"]?.ToString() ?? "" : "",
                    Descripcion = r.Table.Columns.Contains("Descripcion") ? r["Descripcion"]?.ToString() ?? "" : "",
                    CantidadVendida = r.Table.Columns.Contains("Cantidad") ? Convert.ToDecimal(r["Cantidad"]) : 0m,
                    CantidadNota = r.Table.Columns.Contains("Cantidad") ? Convert.ToDecimal(r["Cantidad"]) : 0m,
                    PrecioUnitario = r.Table.Columns.Contains("PrecioUnitario") ? Convert.ToDecimal(r["PrecioUnitario"]) : 0m
                };
                item.PropertyChanged += (s, e) => ActualizarResumen();
                _items.Add(item);
            }
        }

        private void Modo_Checked(object sender, RoutedEventArgs e)
        {
            ActualizarModo();
            ActualizarResumen();
        }

        private void txtMontoManual_TextChanged(object sender, TextChangedEventArgs e)
        {
            ActualizarResumen();
        }

        private void dgvItems_CellEditEnding(object sender, DataGridCellEditEndingEventArgs e)
        {
            Dispatcher.BeginInvoke(new Action(ActualizarResumen));
        }

        private void ActualizarModo()
        {
            bool parcial = rbParcial?.IsChecked == true;
            if (dgvItems != null) dgvItems.IsEnabled = parcial;
            if (txtMontoManual != null) txtMontoManual.IsEnabled = parcial;
        }

        private void ActualizarResumen()
        {
            if (lblResumen == null) return;
            decimal monto = CalcularMonto();
            lblResumen.Text = $"Monto a acreditar: {monto:C2}";
        }

        private decimal CalcularMonto()
        {
            if (rbTotal?.IsChecked == true)
                return TotalFactura();

            if (TryParseMonto(txtMontoManual?.Text, out decimal manual) && manual > 0)
                return manual;

            return _items
                .Where(i => i.Seleccionado)
                .Sum(i => i.SubtotalNota);
        }

        private void btnCancelar_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }

        private void btnGuardar_Click(object sender, RoutedEventArgs e)
        {
            dgvItems.CommitEdit(DataGridEditingUnit.Cell, true);
            dgvItems.CommitEdit(DataGridEditingUnit.Row, true);

            decimal monto = CalcularMonto();
            if (monto <= 0)
            {
                ModernMessageBox.Show("Ingrese un importe o seleccione ítems/cantidades para la nota parcial.", "Nota de Crédito", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            decimal total = TotalFactura();
            if (monto > total)
            {
                ModernMessageBox.Show("El monto de la nota no puede superar el total de la factura original.", "Nota de Crédito", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            string tipoNc = rbTotal.IsChecked == true ? "TOTAL" : "PARCIAL";
            string descripcion = ConstruirDescripcion(tipoNc, monto);
            int clienteId = _encabezado.Table.Columns.Contains("ClienteID") && _encabezado["ClienteID"] != DBNull.Value
                ? Convert.ToInt32(_encabezado["ClienteID"])
                : 0;

            if (clienteId <= 0)
            {
                ModernMessageBox.Show("No se pudo identificar el cliente de la factura original.", "Nota de Crédito", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            int id = DatabaseService.GuardarNotaCreditoDebitoVenta(clienteId, "NC", monto, descripcion, facturaId: _facturaId);
            if (id <= 0)
            {
                ModernMessageBox.Show("No se pudo guardar la nota de crédito.", "Nota de Crédito", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            ResultID = id;
            if (ModernMessageBox.Show("Nota de crédito generada. ¿Imprimir ahora?", "Nota de Crédito", MessageBoxButton.YesNo, MessageBoxImage.Question) == MessageBoxResult.Yes)
                PrintService.ImprimirNotaCreditoDebitoVenta(id);
            DialogResult = true;
            Close();
        }

        private string ConstruirDescripcion(string tipoNc, decimal monto)
        {
            var sb = new StringBuilder();
            sb.Append($"NC {tipoNc} generada desde FacturaID {_facturaId:D8}. ");
            sb.Append($"Factura fecha {Convert.ToDateTime(_encabezado["Fecha"]):dd/MM/yyyy HH:mm}, cliente {Valor("ClienteNombre", "-")}, total original {TotalFactura():C2}, monto NC {monto:C2}.");
            if (!string.IsNullOrWhiteSpace(txtObservaciones.Text))
                sb.Append(" Motivo: ").Append(txtObservaciones.Text.Trim());

            var seleccionados = rbTotal.IsChecked == true ? _items : _items.Where(i => i.Seleccionado);
            string detalle = string.Join("; ", seleccionados.Select(i => $"{i.Codigo} {i.Descripcion} x{i.CantidadNota:0.##} @ {i.PrecioUnitario:C2}"));
            if (!string.IsNullOrWhiteSpace(detalle))
                sb.Append(" Items: ").Append(detalle);
            return sb.ToString();
        }

        private string Valor(string col, string fallback)
        {
            return _encabezado.Table.Columns.Contains(col) && _encabezado[col] != DBNull.Value
                ? _encabezado[col]?.ToString() ?? fallback
                : fallback;
        }

        private decimal TotalFactura()
        {
            return _encabezado.Table.Columns.Contains("Total") && _encabezado["Total"] != DBNull.Value
                ? Convert.ToDecimal(_encabezado["Total"])
                : 0m;
        }

        private static bool TryParseMonto(string texto, out decimal monto)
        {
            texto = (texto ?? "").Trim();
            return decimal.TryParse(texto, NumberStyles.Number | NumberStyles.AllowCurrencySymbol, CultureInfo.CurrentCulture, out monto)
                || decimal.TryParse(texto.Replace(",", "."), NumberStyles.Number, CultureInfo.InvariantCulture, out monto);
        }

        private class ItemNotaCredito : INotifyPropertyChanged
        {
            private bool _seleccionado;
            private decimal _cantidadNota;

            public int ProductoID { get; set; }
            public string Codigo { get; set; }
            public string Descripcion { get; set; }
            public decimal CantidadVendida { get; set; }
            public decimal PrecioUnitario { get; set; }

            public bool Seleccionado
            {
                get { return _seleccionado; }
                set { _seleccionado = value; OnPropertyChanged(nameof(Seleccionado)); }
            }

            public decimal CantidadNota
            {
                get { return _cantidadNota; }
                set
                {
                    decimal max = CantidadVendida <= 0 ? 0 : CantidadVendida;
                    _cantidadNota = Math.Max(0, Math.Min(max, value));
                    if (_cantidadNota > 0) Seleccionado = true;
                    OnPropertyChanged(nameof(CantidadNota));
                    OnPropertyChanged(nameof(SubtotalNota));
                    OnPropertyChanged(nameof(SubtotalNotaFmt));
                }
            }

            public decimal SubtotalNota { get { return CantidadNota * PrecioUnitario; } }
            public string PrecioUnitarioFmt { get { return PrecioUnitario.ToString("C2"); } }
            public string SubtotalNotaFmt { get { return SubtotalNota.ToString("C2"); } }

            public event PropertyChangedEventHandler PropertyChanged;
            private void OnPropertyChanged(string prop) { PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(prop)); }
        }
    }
}
