using SchettiniGestion;
using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Data;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace SchettiniGestion.WPF
{
    public partial class EtiquetasControl : UserControl
    {
        private readonly ObservableCollection<EtiquetaFila> _filas = new ObservableCollection<EtiquetaFila>();

        public EtiquetasControl()
        {
            InitializeComponent();
        }

        public EtiquetasControl(object param) : this() { }

        private void UserControl_Loaded(object sender, RoutedEventArgs e)
        {
            dgvProductos.ItemsSource = _filas;
            CargarOpcionesEnUi();
            ActualizarLblTamano();
            Buscar("");
        }

        private void CargarOpcionesEnUi()
        {
            var op = DatabaseService.GetOpcionesEtiqueta();
            chkDesc.IsChecked = op.MostrarDescripcion;
            chkPrecio.IsChecked = op.MostrarPrecio;
            chkCodigo.IsChecked = op.MostrarCodigo;
            chkBarras.IsChecked = op.MostrarCodigoBarras;
            chkMarca.IsChecked = op.MostrarMarca;
        }

        private void ActualizarLblTamano()
        {
            var op = DatabaseService.GetOpcionesEtiqueta();
            string impresora = DatabaseService.GetImpresoraEtiquetas();
            lblTamano.Text = string.IsNullOrWhiteSpace(impresora)
                ? $"Medida: {op.AnchoMm}×{op.AltoMm} mm · Impresora: (preguntar)"
                : $"Medida: {op.AnchoMm}×{op.AltoMm} mm · {impresora}";
        }

        private OpcionesEtiqueta LeerOpcionesDesdeUi()
        {
            var op = DatabaseService.GetOpcionesEtiqueta();
            op.MostrarDescripcion = chkDesc.IsChecked == true;
            op.MostrarPrecio = chkPrecio.IsChecked == true;
            op.MostrarCodigo = chkCodigo.IsChecked == true;
            op.MostrarCodigoBarras = chkBarras.IsChecked == true;
            op.MostrarMarca = chkMarca.IsChecked == true;
            return op;
        }

        private void btnGuardarOpciones_Click(object sender, RoutedEventArgs e)
        {
            var op = LeerOpcionesDesdeUi();
            string impresora = DatabaseService.GetImpresoraEtiquetas();
            if (DatabaseService.GuardarConfigEtiquetas(impresora, op))
                CustomMessageBox.Show("Opciones de etiqueta guardadas.");
            else
                CustomMessageBox.Show("No se pudieron guardar las opciones.");
        }

        private void txtBuscar_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter)
            {
                Buscar(txtBuscar.Text);
                e.Handled = true;
            }
        }

        private void btnBuscar_Click(object sender, RoutedEventArgs e) => Buscar(txtBuscar.Text);

        private void Buscar(string filtro)
        {
            try
            {
                var seleccionPrev = _filas
                    .Where(f => f.Seleccionado)
                    .ToDictionary(f => f.ProductoID, f => f.Cantidad);

                _filas.Clear();
                DataTable dt = DatabaseService.GetProductosListado(filtro ?? "");
                foreach (DataRow r in dt.Rows)
                {
                    int id = Convert.ToInt32(r["ProductoID"]);
                    int cant = 1;
                    bool sel = false;
                    if (seleccionPrev.TryGetValue(id, out int cPrev))
                    {
                        sel = true;
                        cant = cPrev;
                    }

                    _filas.Add(new EtiquetaFila
                    {
                        ProductoID = id,
                        Codigo = r["Codigo"]?.ToString() ?? "",
                        CodigoBarra = r.Table.Columns.Contains("CodigoBarra") ? (r["CodigoBarra"]?.ToString() ?? "") : "",
                        Descripcion = r["Descripcion"]?.ToString() ?? "",
                        Marca = r.Table.Columns.Contains("Marca") ? (r["Marca"]?.ToString() ?? "") : "",
                        PrecioVenta = r["PrecioVenta"] != DBNull.Value ? Convert.ToDecimal(r["PrecioVenta"]) : 0m,
                        Cantidad = cant,
                        Seleccionado = sel
                    });
                }
                ActualizarResumen();
            }
            catch (Exception ex)
            {
                CustomMessageBox.Show("Error al buscar productos: " + ex.Message);
            }
        }

        private void btnSelTodos_Click(object sender, RoutedEventArgs e)
        {
            foreach (var f in _filas) f.Seleccionado = true;
            dgvProductos.Items.Refresh();
            ActualizarResumen();
        }

        private void btnSelNinguno_Click(object sender, RoutedEventArgs e)
        {
            foreach (var f in _filas) f.Seleccionado = false;
            dgvProductos.Items.Refresh();
            ActualizarResumen();
        }

        private void ActualizarResumen()
        {
            int prod = _filas.Count(f => f.Seleccionado);
            int etiq = _filas.Where(f => f.Seleccionado).Sum(f => Math.Max(1, f.Cantidad));
            lblResumen.Text = prod == 0 ? "Ningún producto seleccionado." : $"{prod} producto(s) · {etiq} etiqueta(s)";
        }

        private void btnImprimir_Click(object sender, RoutedEventArgs e)
        {
            if (!LicenseManager.TieneEtiquetas())
            {
                CustomMessageBox.Show("La impresión de etiquetas no está incluida en su licencia.");
                return;
            }

            dgvProductos.CommitEdit(DataGridEditingUnit.Cell, true);
            dgvProductos.CommitEdit(DataGridEditingUnit.Row, true);

            var items = _filas
                .Where(f => f.Seleccionado)
                .Select(f => new EtiquetaPrintItem
                {
                    ProductoID = f.ProductoID,
                    Codigo = f.Codigo,
                    CodigoBarra = f.CodigoBarra,
                    Descripcion = f.Descripcion,
                    Marca = f.Marca,
                    PrecioVenta = f.PrecioVenta,
                    Cantidad = Math.Max(1, f.Cantidad)
                })
                .ToList();

            if (items.Count == 0)
            {
                CustomMessageBox.Show("Seleccioná al menos un producto (tilde Sel.) e indicá la cantidad.");
                return;
            }

            if (chkDesc.IsChecked != true && chkPrecio.IsChecked != true && chkCodigo.IsChecked != true
                && chkBarras.IsChecked != true && chkMarca.IsChecked != true)
            {
                CustomMessageBox.Show("Marcá al menos un dato para mostrar en la etiqueta.");
                return;
            }

            var op = LeerOpcionesDesdeUi();
            string impresora = DatabaseService.GetImpresoraEtiquetas();
            DatabaseService.GuardarConfigEtiquetas(impresora, op);
            ActualizarLblTamano();
            PrintService.ImprimirEtiquetas(items, op);
            ActualizarResumen();
        }

        private class EtiquetaFila : INotifyPropertyChanged
        {
            private bool _seleccionado;
            private int _cantidad = 1;

            public int ProductoID { get; set; }
            public string Codigo { get; set; }
            public string CodigoBarra { get; set; }
            public string Descripcion { get; set; }
            public string Marca { get; set; }
            public decimal PrecioVenta { get; set; }

            public bool Seleccionado
            {
                get => _seleccionado;
                set { _seleccionado = value; OnPropertyChanged(nameof(Seleccionado)); }
            }

            public int Cantidad
            {
                get => _cantidad;
                set { _cantidad = value < 1 ? 1 : value; OnPropertyChanged(nameof(Cantidad)); }
            }

            public event PropertyChangedEventHandler PropertyChanged;
            private void OnPropertyChanged(string n) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(n));
        }
    }
}
