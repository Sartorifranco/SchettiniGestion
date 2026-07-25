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
using System.Windows.Data;

namespace SchettiniGestion.WPF
{
    public class NotaCreditoVentaWindow : Window
    {
        public int ResultID { get; private set; }

        private readonly int _facturaId;
        private readonly DataRow _encabezado;
        private readonly ObservableCollection<ItemNotaCredito> _items = new ObservableCollection<ItemNotaCredito>();
        private RadioButton _rbTotal;
        private RadioButton _rbParcial;
        private DataGrid _grid;
        private TextBox _txtMontoManual;
        private TextBox _txtObservaciones;
        private TextBlock _lblResumen;

        public NotaCreditoVentaWindow(int facturaId)
        {
            _facturaId = facturaId;
            _encabezado = DatabaseService.GetFacturaPorID(facturaId);
            if (_encabezado == null)
                throw new InvalidOperationException("No se encontro la factura indicada.");

            Title = "Generar Nota de Credito";
            Width = 860;
            Height = 620;
            MinWidth = 760;
            MinHeight = 560;
            WindowStartupLocation = WindowStartupLocation.CenterOwner;
            ResizeMode = ResizeMode.CanResize;
            BuildUi();
            CargarItems();
            ActualizarModo();
            ActualizarResumen();
        }

        private void BuildUi()
        {
            var root = new Grid { Margin = new Thickness(18) };
            var hintStyle = TryFindResource("FieldHintTextStyle") as Style;
            root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            root.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
            root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            Content = root;

            var titulo = new TextBlock
            {
                Text = "Generar Nota de Credito desde venta",
                FontSize = 20,
                FontWeight = FontWeights.Bold,
                Margin = new Thickness(0, 0, 0, 8)
            };
            root.Children.Add(titulo);

            var ayuda = new TextBlock
            {
                Text = "Elegir TOTAL para anular todo el comprobante o PARCIAL para seleccionar renglones/cantidades o ingresar un importe manual. La nota queda vinculada a la factura original para auditoria.",
                TextWrapping = TextWrapping.Wrap,
                Margin = new Thickness(0, 30, 0, 12),
                Opacity = 0.78,
                Style = hintStyle
            };
            Grid.SetRow(ayuda, 0);
            root.Children.Add(ayuda);

            var datos = new Border
            {
                Padding = new Thickness(12),
                Margin = new Thickness(0, 0, 0, 12),
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(8)
            };
            Grid.SetRow(datos, 1);
            var datosPanel = new StackPanel { Orientation = Orientation.Horizontal };
            datos.Child = datosPanel;
            datosPanel.Children.Add(new TextBlock { Text = $"Factura #{_facturaId:D8}", FontWeight = FontWeights.SemiBold, Margin = new Thickness(0, 0, 18, 0) });
            datosPanel.Children.Add(new TextBlock { Text = $"Cliente: {Valor("ClienteNombre", "-")}", Margin = new Thickness(0, 0, 18, 0) });
            datosPanel.Children.Add(new TextBlock { Text = $"Total: {TotalFactura():C2}", FontWeight = FontWeights.Bold });
            root.Children.Add(datos);

            var panelCentro = new Grid();
            panelCentro.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            panelCentro.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
            Grid.SetRow(panelCentro, 2);
            root.Children.Add(panelCentro);

            var opciones = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 0, 0, 10) };
            _rbTotal = new RadioButton { Content = "TOTAL NC (importe completo)", IsChecked = true, Margin = new Thickness(0, 0, 24, 0), FontWeight = FontWeights.SemiBold, MinHeight = 36 };
            _rbParcial = new RadioButton { Content = "PARCIAL NC (items/cantidades o importe)", FontWeight = FontWeights.SemiBold, MinHeight = 36 };
            _rbTotal.Checked += (s, e) => { ActualizarModo(); ActualizarResumen(); };
            _rbParcial.Checked += (s, e) => { ActualizarModo(); ActualizarResumen(); };
            opciones.Children.Add(_rbTotal);
            opciones.Children.Add(_rbParcial);
            panelCentro.Children.Add(opciones);

            _grid = new DataGrid
            {
                AutoGenerateColumns = false,
                CanUserAddRows = false,
                IsReadOnly = false,
                ItemsSource = _items,
                RowHeight = 34
            };
            _grid.Columns.Add(new DataGridCheckBoxColumn { Header = "Sel.", Binding = new Binding("Seleccionado") { Mode = BindingMode.TwoWay }, Width = 55 });
            _grid.Columns.Add(new DataGridTextColumn { Header = "Codigo", Binding = new Binding("Codigo"), Width = 90, IsReadOnly = true });
            _grid.Columns.Add(new DataGridTextColumn { Header = "Descripcion", Binding = new Binding("Descripcion"), Width = new DataGridLength(1, DataGridLengthUnitType.Star), IsReadOnly = true });
            _grid.Columns.Add(new DataGridTextColumn { Header = "Vend.", Binding = new Binding("CantidadVendida"), Width = 75, IsReadOnly = true });
            _grid.Columns.Add(new DataGridTextColumn { Header = "Cant. NC", Binding = new Binding("CantidadNota") { Mode = BindingMode.TwoWay, UpdateSourceTrigger = UpdateSourceTrigger.PropertyChanged }, Width = 90 });
            _grid.Columns.Add(new DataGridTextColumn { Header = "P. Unit.", Binding = new Binding("PrecioUnitarioFmt"), Width = 100, IsReadOnly = true });
            _grid.Columns.Add(new DataGridTextColumn { Header = "Subtotal NC", Binding = new Binding("SubtotalNotaFmt"), Width = 120, IsReadOnly = true });
            _grid.CellEditEnding += (s, e) => Dispatcher.BeginInvoke(new Action(ActualizarResumen));
            Grid.SetRow(_grid, 1);
            panelCentro.Children.Add(_grid);

            var parcial = new Grid { Margin = new Thickness(0, 12, 0, 10) };
            parcial.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(180) });
            parcial.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(180) });
            parcial.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            Grid.SetRow(parcial, 3);
            root.Children.Add(parcial);

            parcial.Children.Add(new TextBlock { Text = "Importe parcial manual:", VerticalAlignment = VerticalAlignment.Center });
            _txtMontoManual = new TextBox { Margin = new Thickness(8, 0, 12, 0), Height = 34, VerticalContentAlignment = VerticalAlignment.Center, ToolTip = "Opcional. Si se completa, reemplaza la suma de items seleccionados." };
            _txtMontoManual.TextChanged += (s, e) => ActualizarResumen();
            Grid.SetColumn(_txtMontoManual, 1);
            parcial.Children.Add(_txtMontoManual);
            _lblResumen = new TextBlock { VerticalAlignment = VerticalAlignment.Center, FontWeight = FontWeights.SemiBold };
            Grid.SetColumn(_lblResumen, 2);
            parcial.Children.Add(_lblResumen);

            var obsPanel = new StackPanel { Margin = new Thickness(0, 0, 0, 12) };
            Grid.SetRow(obsPanel, 4);
            obsPanel.Children.Add(new TextBlock { Text = "Observaciones / motivo (opcional)", Margin = new Thickness(0, 0, 0, 4) });
            obsPanel.Children.Add(new TextBlock
            {
                Text = "Se guarda junto con el detalle de origen para dejar trazabilidad de la nota.",
                FontSize = 11,
                Opacity = 0.72,
                Margin = new Thickness(0, 0, 0, 4),
                Style = hintStyle
            });
            _txtObservaciones = new TextBox { Height = 54, AcceptsReturn = true, TextWrapping = TextWrapping.Wrap };
            obsPanel.Children.Add(_txtObservaciones);

            var botones = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right, Margin = new Thickness(0, 10, 0, 0) };
            var btnGuardar = new Button { Content = "Generar Nota de Credito", MinWidth = 190, Height = 40, Margin = new Thickness(0, 0, 8, 0) };
            btnGuardar.Click += btnGuardar_Click;
            var btnCancelar = new Button { Content = "Cancelar", MinWidth = 110, Height = 40 };
            btnCancelar.Click += (s, e) => Close();
            botones.Children.Add(btnGuardar);
            botones.Children.Add(btnCancelar);
            obsPanel.Children.Add(botones);
            root.Children.Add(obsPanel);
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

        private void ActualizarModo()
        {
            bool parcial = _rbParcial?.IsChecked == true;
            if (_grid != null) _grid.IsEnabled = parcial;
            if (_txtMontoManual != null) _txtMontoManual.IsEnabled = parcial;
        }

        private void ActualizarResumen()
        {
            if (_lblResumen == null) return;
            decimal monto = CalcularMonto();
            _lblResumen.Text = $"Monto a acreditar: {monto:C2}";
        }

        private decimal CalcularMonto()
        {
            if (_rbTotal?.IsChecked == true)
                return TotalFactura();

            if (TryParseMonto(_txtMontoManual?.Text, out decimal manual) && manual > 0)
                return manual;

            return _items
                .Where(i => i.Seleccionado)
                .Sum(i => i.SubtotalNota);
        }

        private void btnGuardar_Click(object sender, RoutedEventArgs e)
        {
            _grid.CommitEdit(DataGridEditingUnit.Cell, true);
            _grid.CommitEdit(DataGridEditingUnit.Row, true);

            decimal monto = CalcularMonto();
            if (monto <= 0)
            {
                MessageBox.Show("Ingrese un importe o seleccione items/cantidades para la nota parcial.", "Nota de Credito", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            decimal total = TotalFactura();
            if (monto > total)
            {
                MessageBox.Show("El monto de la nota no puede superar el total de la factura original.", "Nota de Credito", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            string tipoNc = _rbTotal.IsChecked == true ? "TOTAL" : "PARCIAL";
            string descripcion = ConstruirDescripcion(tipoNc, monto);
            int clienteId = _encabezado.Table.Columns.Contains("ClienteID") && _encabezado["ClienteID"] != DBNull.Value
                ? Convert.ToInt32(_encabezado["ClienteID"])
                : 0;

            if (clienteId <= 0)
            {
                MessageBox.Show("No se pudo identificar el cliente de la factura original.", "Nota de Credito", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            int id = DatabaseService.GuardarNotaCreditoDebitoVenta(clienteId, "NC", monto, descripcion, facturaId: _facturaId);
            if (id <= 0)
            {
                MessageBox.Show("No se pudo guardar la nota de credito.", "Nota de Credito", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            ResultID = id;
            if (MessageBox.Show("Nota de credito generada. ¿Imprimir ahora?", "Nota de Credito", MessageBoxButton.YesNo, MessageBoxImage.Question) == MessageBoxResult.Yes)
                PrintService.ImprimirNotaCreditoDebitoVenta(id);
            DialogResult = true;
            Close();
        }

        private string ConstruirDescripcion(string tipoNc, decimal monto)
        {
            var sb = new StringBuilder();
            sb.Append($"NC {tipoNc} generada desde FacturaID {_facturaId:D8}. ");
            sb.Append($"Factura fecha {Convert.ToDateTime(_encabezado["Fecha"]):dd/MM/yyyy HH:mm}, cliente {Valor("ClienteNombre", "-")}, total original {TotalFactura():C2}, monto NC {monto:C2}.");
            if (!string.IsNullOrWhiteSpace(_txtObservaciones.Text))
                sb.Append(" Motivo: ").Append(_txtObservaciones.Text.Trim());

            var seleccionados = _rbTotal.IsChecked == true ? _items : _items.Where(i => i.Seleccionado);
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
