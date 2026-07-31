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
        private bool _cargandoOpciones;
        private bool _sincronizandoMedida;
        private static readonly string[] PresetsEtiqueta =
        {
            "30 x 20 mm", "50 x 25 mm", "50 x 30 mm", "55 x 44 mm", "64 x 32 mm",
            "80 x 40 mm", "100 x 80 mm", "100 x 100 mm", "100 x 150 mm", "Personalizado"
        };

        public EtiquetasControl()
        {
            InitializeComponent();
        }

        public EtiquetasControl(object param) : this() { }

        private void UserControl_Loaded(object sender, RoutedEventArgs e)
        {
            dgvProductos.ItemsSource = _filas;
            CargarCombosFormato();
            CargarOpcionesEnUi();
            ActualizarLblTamano();
            ActualizarResaltadoOrientacion();
            Buscar("");
        }

        private void CargarCombosFormato()
        {
            cmbPresetEtiqueta.ItemsSource = PresetsEtiqueta;
            cmbModoImpresion.ItemsSource = new[] { "Rollo", "A4", "Cartel", "Gondola" };
            cmbOrientacion.ItemsSource = new[] { "Vertical", "Horizontal" };
        }

        private void CargarOpcionesEnUi()
        {
            _cargandoOpciones = true;
            var op = DatabaseService.GetOpcionesEtiqueta();
            txtAnchoMm.Text = op.AnchoMm.ToString();
            txtAltoMm.Text = op.AltoMm.ToString();
            txtGapH.Text = op.GapHorizontalMm.ToString();
            txtGapV.Text = op.GapVerticalMm.ToString();
            txtMargenIzq.Text = op.MargenIzquierdoMm.ToString();
            txtMargenSup.Text = op.MargenSuperiorMm.ToString();
            txtMargenDer.Text = op.MargenDerechoMm.ToString();
            txtMargenInf.Text = op.MargenInferiorMm.ToString();
            txtColumnas.Text = op.Columnas.ToString();
            cmbModoImpresion.SelectedItem = string.IsNullOrWhiteSpace(op.ModoImpresion) ? "Rollo" : op.ModoImpresion;
            cmbOrientacion.SelectedItem = string.IsNullOrWhiteSpace(op.Orientacion) ? "Vertical" : op.Orientacion;
            SeleccionarPreset(op.AnchoMm, op.AltoMm);
            chkDesc.IsChecked = op.MostrarDescripcion;
            chkDescExtra.IsChecked = op.MostrarDescripcionExtra;
            chkPrecio.IsChecked = op.MostrarPrecio;
            chkCodigo.IsChecked = op.MostrarCodigo;
            chkBarras.IsChecked = op.MostrarCodigoBarras;
            chkMarca.IsChecked = op.MostrarMarca;
            _cargandoOpciones = false;
        }

        private void ActualizarLblTamano()
        {
            var op = DatabaseService.GetOpcionesEtiqueta();
            string impresora = DatabaseService.GetImpresoraEtiquetas();
            lblTamano.Text = string.IsNullOrWhiteSpace(impresora)
                ? $"Medida: {op.AnchoMm}x{op.AltoMm} mm · {op.ModoImpresion} · Impresora: (preguntar)"
                : $"Medida: {op.AnchoMm}x{op.AltoMm} mm · {op.ModoImpresion} · {impresora}";
        }

        private OpcionesEtiqueta LeerOpcionesDesdeUi()
        {
            var op = DatabaseService.GetOpcionesEtiqueta();
            op.AnchoMm = LeerEntero(txtAnchoMm.Text, 50, 10, 300);
            op.AltoMm = LeerEntero(txtAltoMm.Text, 25, 10, 300);
            op.GapHorizontalMm = LeerEntero(txtGapH.Text, 2, 0, 50);
            op.GapVerticalMm = LeerEntero(txtGapV.Text, 2, 0, 50);
            op.MargenIzquierdoMm = LeerEntero(txtMargenIzq.Text, 5, 0, 50);
            op.MargenSuperiorMm = LeerEntero(txtMargenSup.Text, 5, 0, 50);
            op.MargenDerechoMm = LeerEntero(txtMargenDer.Text, 5, 0, 50);
            op.MargenInferiorMm = LeerEntero(txtMargenInf.Text, 5, 0, 50);
            op.Columnas = LeerEntero(txtColumnas.Text, 3, 1, 12);
            op.ModoImpresion = cmbModoImpresion.SelectedItem?.ToString() ?? "Rollo";
            op.Orientacion = cmbOrientacion.SelectedItem?.ToString() ?? "Vertical";
            op.MostrarDescripcion = chkDesc.IsChecked == true;
            op.MostrarDescripcionExtra = chkDescExtra.IsChecked == true;
            op.MostrarPrecio = chkPrecio.IsChecked == true;
            op.MostrarCodigo = chkCodigo.IsChecked == true;
            op.MostrarCodigoBarras = chkBarras.IsChecked == true;
            op.MostrarMarca = chkMarca.IsChecked == true;
            return op;
        }

        private static int LeerEntero(string texto, int defecto, int min, int max)
        {
            if (!int.TryParse((texto ?? "").Trim(), out int v)) v = defecto;
            return Math.Max(min, Math.Min(max, v));
        }

        private void SeleccionarPreset(int ancho, int alto)
        {
            string buscado = $"{ancho} x {alto} mm";
            cmbPresetEtiqueta.SelectedItem = PresetsEtiqueta.Contains(buscado) ? buscado : "Personalizado";
        }

        private void cmbPresetEtiqueta_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (_cargandoOpciones || _sincronizandoMedida) return;
            string sel = cmbPresetEtiqueta.SelectedItem?.ToString() ?? "";
            if (sel.StartsWith("Personalizado", StringComparison.OrdinalIgnoreCase)) return;
            var partes = sel.Replace("mm", "").Split('x');
            if (partes.Length == 2
                && int.TryParse(partes[0].Trim(), out int w)
                && int.TryParse(partes[1].Trim(), out int h))
            {
                txtAnchoMm.Text = w.ToString();
                txtAltoMm.Text = h.ToString();
            }
        }

        private void cmbOrientacion_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            ActualizarResaltadoOrientacion();
        }

        private void txtMedidaEtiqueta_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (_cargandoOpciones || _sincronizandoMedida) return;
            _sincronizandoMedida = true;
            try
            {
                SeleccionarPreset(LeerEntero(txtAnchoMm?.Text, 50, 10, 300), LeerEntero(txtAltoMm?.Text, 25, 10, 300));
            }
            finally { _sincronizandoMedida = false; }
            ActualizarResaltadoOrientacion();
        }

        private void btnIntercambiarMedida_Click(object sender, RoutedEventArgs e)
        {
            if (txtAnchoMm == null || txtAltoMm == null) return;
            string ancho = txtAnchoMm.Text;
            txtAnchoMm.Text = txtAltoMm.Text;
            txtAltoMm.Text = ancho;
            ActualizarResaltadoOrientacion();
        }

        private void cmbModoImpresion_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            ActualizarResaltadoOrientacion();
        }

        /// <summary>
        /// Resalta en la ayuda visual cuál de las dos tarjetas (Vertical/Horizontal)
        /// corresponde a la orientación actualmente elegida, y dibuja ambas con la
        /// proporción real de la etiqueta configurada (la orientación gira el
        /// contenido, nunca el tamaño físico del papel).
        /// </summary>
        private void ActualizarResaltadoOrientacion()
        {
            if (bdPreviewVertical == null || bdPreviewHorizontal == null) return;
            bool horizontal = string.Equals(cmbOrientacion?.SelectedItem?.ToString(), "Horizontal", StringComparison.OrdinalIgnoreCase);

            var colorSeleccionado = (System.Windows.Media.Brush)Application.Current.TryFindResource("PrimaryColor")
                ?? System.Windows.Media.Brushes.DodgerBlue;
            var colorNormal = (System.Windows.Media.Brush)Application.Current.TryFindResource("BorderColor")
                ?? System.Windows.Media.Brushes.Gray;

            bdPreviewVertical.BorderBrush = horizontal ? colorNormal : colorSeleccionado;
            bdPreviewHorizontal.BorderBrush = horizontal ? colorSeleccionado : colorNormal;

            int anchoMm = LeerEntero(txtAnchoMm?.Text, 50, 10, 300);
            int altoMm = LeerEntero(txtAltoMm?.Text, 25, 10, 300);
            const double ladoMayor = 150;
            double escala = ladoMayor / Math.Max(anchoMm, altoMm);
            double w = Math.Max(48, anchoMm * escala);
            double h = Math.Max(34, altoMm * escala);

            bdPreviewVertical.Width = w;
            bdPreviewVertical.Height = h;
            bdPreviewHorizontal.Width = w;
            bdPreviewHorizontal.Height = h;

            if (lblMedidaFisica != null)
            {
                lblMedidaFisica.Text = $"Etiqueta física: {anchoMm} × {altoMm} mm. "
                    + "Este tamaño es el mismo en Vertical y en Horizontal; debe coincidir con el rollo cargado en la impresora.";
            }
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
                        DescripcionExtra = ConstruirDescripcionExtra(r),
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

        private static string ConstruirDescripcionExtra(DataRow r)
        {
            if (r == null) return "";
            string categoria = r.Table.Columns.Contains("Categoria") ? r["Categoria"]?.ToString() ?? "" : "";
            string subrubro = r.Table.Columns.Contains("SubRubro") ? r["SubRubro"]?.ToString() ?? "" : "";
            string proveedor = r.Table.Columns.Contains("Proveedor") ? r["Proveedor"]?.ToString() ?? "" : "";
            return string.Join(" - ", new[] { categoria, subrubro, proveedor }.Where(x => !string.IsNullOrWhiteSpace(x)).Distinct());
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
                    DescripcionExtra = f.DescripcionExtra,
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
                && chkBarras.IsChecked != true && chkMarca.IsChecked != true && chkDescExtra.IsChecked != true)
            {
                CustomMessageBox.Show("Marcá al menos un dato para mostrar en la etiqueta o cartel.");
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
            public string DescripcionExtra { get; set; }
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
