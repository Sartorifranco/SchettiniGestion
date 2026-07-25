using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.Data;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using SchettiniGestion;
using Xceed.Wpf.Toolkit;

namespace SchettiniGestion.WPF
{
    public partial class ProductoModalWindow : Window
    {
        private int _productoId;
        private int _stockActual;
        private string _rutaImagen = "";
        private bool _modoDuplicar;
        private Action _onGuardado;
        private bool _suspendCalculoPrecio;

        public ProductoModalWindow(int productoId, bool duplicar, Action onGuardado)
        {
            InitializeComponent();
            _productoId = productoId;
            _modoDuplicar = duplicar;
            _onGuardado = onGuardado;
            CargarCombos();
        }

        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            CargarListasPrecio();
            if (_productoId == 0)
            {
                txtTitulo.Text = "Nuevo Producto";
                btnDuplicar.Visibility = Visibility.Collapsed;
                btnEliminar.Visibility = Visibility.Collapsed;
                btnCrearYOtro.Visibility = Visibility.Visible;
                btnGuardar.Content = "💾 Crear";
                Limpiar();
            }
            else
            {
                txtTitulo.Text = _modoDuplicar ? "Duplicar Producto" : "Editar Producto";
                btnDuplicar.Visibility = _modoDuplicar ? Visibility.Collapsed : Visibility.Visible;
                btnEliminar.Visibility = _modoDuplicar ? Visibility.Collapsed : Visibility.Visible;
                btnCrearYOtro.Visibility = Visibility.Collapsed;
                btnGuardar.Content = "💾 Guardar";
                CargarProducto(_productoId);
                if (_modoDuplicar)
                {
                    _productoId = 0;
                    txtCodigo.Text = "";
                    txtCodigoBarra.Text = "";
                }
            }
        }

        private void CargarCombos()
        {
            string cat = ObtenerTextoLookupCombo(cmbCategoria);
            string sub = ObtenerTextoLookupCombo(cmbSubRubro);
            string prov = ObtenerTextoLookupCombo(cmbProveedor);

            cmbCategoria.ItemsSource = DatabaseService.GetCategoriasCatalogo();
            cmbSubRubro.ItemsSource = DatabaseService.GetSubRubrosCatalogo();
            cmbProveedor.ItemsSource = DatabaseService.GetProveedoresCatalogo();

            SeleccionarLookupPorNombre(cmbCategoria, cat);
            SeleccionarLookupPorNombre(cmbSubRubro, sub);
            SeleccionarLookupPorNombre(cmbProveedor, prov);
        }

        private static string ObtenerTextoLookupCombo(ComboBox cb)
        {
            if (cb == null) return "";
            string t = cb.Text?.Trim() ?? "";
            if (!string.IsNullOrEmpty(t)) return t;
            if (cb.SelectedItem is ComboLookupItem li && li.Id != 0)
                return li.Nombre?.Trim() ?? "";
            return "";
        }

        private static void SeleccionarLookupPorNombre(ComboBox cb, string valor)
        {
            if (cb?.ItemsSource == null) return;
            foreach (var o in cb.Items)
            {
                if (!(o is ComboLookupItem it)) continue;
                if (it.Id == 0 && string.IsNullOrWhiteSpace(valor))
                {
                    cb.SelectedItem = it;
                    cb.Text = "";
                    return;
                }
                if (it.Id != 0 && string.Equals(it.Nombre?.Trim(), valor?.Trim(), StringComparison.OrdinalIgnoreCase))
                {
                    cb.SelectedItem = it;
                    return;
                }
            }
            cb.SelectedItem = null;
            cb.Text = valor ?? "";
        }

        private void btnNuevaCategoria_Click(object sender, RoutedEventArgs e)
        {
            var dlg = new ModernInputWindow("Nueva categoría", "Nombre:") { Owner = this };
            if (dlg.ShowDialog() != true) return;
            string n = dlg.ResultText?.Trim();
            if (string.IsNullOrEmpty(n))
            {
                ModernMessageBox.Show("Ingrese un nombre.", "Aviso", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            int id = DatabaseService.InsertCategoria(n);
            if (id <= 0)
            {
                ModernMessageBox.Show("No se pudo guardar la categoría.", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            CargarCombos();
            SeleccionarLookupPorNombre(cmbCategoria, n);
        }

        private void btnNuevoSubRubro_Click(object sender, RoutedEventArgs e)
        {
            var dlg = new ModernInputWindow("Nuevo sub-rubro", "Nombre:") { Owner = this };
            if (dlg.ShowDialog() != true) return;
            string n = dlg.ResultText?.Trim();
            if (string.IsNullOrEmpty(n))
            {
                ModernMessageBox.Show("Ingrese un nombre.", "Aviso", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            int id = DatabaseService.InsertSubRubro(n);
            if (id <= 0)
            {
                ModernMessageBox.Show("No se pudo guardar el sub-rubro.", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            CargarCombos();
            SeleccionarLookupPorNombre(cmbSubRubro, n);
        }

        private void btnNuevoProveedor_Click(object sender, RoutedEventArgs e)
        {
            var dlg = new ModernInputWindow("Nuevo proveedor", "Razón social:") { Owner = this };
            if (dlg.ShowDialog() != true) return;
            string n = dlg.ResultText?.Trim();
            if (string.IsNullOrEmpty(n))
            {
                ModernMessageBox.Show("Ingrese la razón social.", "Aviso", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            int id = DatabaseService.InsertProveedorNombre(n);
            if (id <= 0)
            {
                ModernMessageBox.Show("No se pudo guardar el proveedor.", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            CargarCombos();
            SeleccionarLookupPorNombre(cmbProveedor, n);
        }

        private void CargarListasPrecio()
        {
            if (pnlListasPrecio == null) return;
            pnlListasPrecio.Items.Clear();
            try
            {
                var dt = DatabaseService.GetListasPrecios();
                if (dt == null || dt.Rows.Count == 0)
                {
                    txtSinListas.Visibility = Visibility.Visible;
                    return;
                }
                txtSinListas.Visibility = Visibility.Collapsed;
                foreach (DataRow r in dt.Rows)
                {
                    int listaId = Convert.ToInt32(r["ListaID"]);
                    string nombre = r["Nombre"]?.ToString() ?? "";
                    decimal pct = r["Porcentaje"] != DBNull.Value ? Convert.ToDecimal(r["Porcentaje"]) : 0;
                    string tipo = DatabaseService.ObtenerTipoLista(r);
                    var chk = new CheckBox
                    {
                        Content = nombre,
                        Foreground = (Brush)FindResource("TextPrimary"),
                        Style = (Style)FindResource("ProductoTouchCheckBox"),
                        Margin = new Thickness(0, 6, 0, 6),
                        MinWidth = 160
                    };
                    chk.Checked += (s, e) => ActualizarPrecioLista(chk);
                    chk.Unchecked += (s, e) => ActualizarPrecioLista(chk);
                    var sp = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 4, 0, 4) };
                    sp.Children.Add(chk);

                    DecimalUpDown precioFijoInput = null;
                    if (tipo == DatabaseService.TiposListaPrecio.PrecioFijo)
                    {
                        precioFijoInput = new DecimalUpDown
                        {
                            Minimum = 0,
                            FormatString = "N2",
                            Width = 120,
                            Margin = new Thickness(8, 0, 0, 0),
                            Style = (Style)FindResource("ProductoTouchDecimal"),
                            Visibility = Visibility.Collapsed
                        };
                        precioFijoInput.ValueChanged += (s, e) => ActualizarPrecioLista(chk);
                        sp.Children.Add(precioFijoInput);
                    }

                    chk.Tag = new ListaPrecioItem
                    {
                        ListaID = listaId,
                        NombreLista = nombre,
                        Porcentaje = pct,
                        TipoLista = tipo,
                        ListaRow = r,
                        CheckBox = chk,
                        PrecioFijoInput = precioFijoInput
                    };
                    pnlListasPrecio.Items.Add(sp);
                }
            }
            catch
            {
                txtSinListas.Visibility = Visibility.Visible;
            }
        }

        private class ListaPrecioItem
        {
            public int ListaID { get; set; }
            public string NombreLista { get; set; }
            public decimal Porcentaje { get; set; }
            public string TipoLista { get; set; }
            public DataRow ListaRow { get; set; }
            public CheckBox CheckBox { get; set; }
            public DecimalUpDown PrecioFijoInput { get; set; }
        }

        private class PrecioListaPreviewItem
        {
            public string NombreLista { get; set; }
            public decimal CostoBase { get; set; }
            public string TipoLista { get; set; }
            public string Regla { get; set; }
            public decimal PrecioVentaFinal { get; set; }
        }

        private DataRow ConstruirProductoPreview()
        {
            var dt = DatabaseService.GetProductos("");
            if (dt == null) return null;
            var row = dt.NewRow();
            row["ProductoID"] = _productoId > 0 ? _productoId : 0;
            row["PrecioCosto"] = numCosto?.Value ?? 0m;
            row["ImpuestoInterno"] = numImpuestoInterno?.Value ?? 0m;
            row["CostoIncluyeIva"] = chkCostoIncluyeIva?.IsChecked == true;
            row["TipoIVA"] = (cmbTipoIVA?.SelectedItem as ComboBoxItem)?.Content?.ToString() ?? "21.0";
            row["PrecioVenta"] = 0m;
            return row;
        }

        private void ActualizarPrecioLista(CheckBox chk)
        {
            if (chk?.Tag is ListaPrecioItem item)
            {
                if (item.PrecioFijoInput != null)
                    item.PrecioFijoInput.Visibility = chk.IsChecked == true ? Visibility.Visible : Visibility.Collapsed;
                ActualizarEtiquetaPrecioLista(chk, item);
            }
            ActualizarGrillaPreciosPreview();
        }

        private void ActualizarEtiquetaPrecioLista(CheckBox chk, ListaPrecioItem item)
        {
            if (chk == null || item == null || item.ListaRow == null) return;
            decimal? precioFijo = null;
            if (item.TipoLista == DatabaseService.TiposListaPrecio.PrecioFijo && item.PrecioFijoInput != null)
                precioFijo = item.PrecioFijoInput.Value;

            decimal precio = 0m;
            var prod = ConstruirProductoPreview();
            if (prod != null)
                precio = DatabaseService.CalcularPrecioLista(prod, item.ListaRow, precioFijo);
            chk.Content = $"{item.NombreLista}: {precio:C2}";
        }

        private void ActualizarGrillaPreciosPreview()
        {
            if (dgvPreciosPreview == null) return;

            var items = new List<PrecioListaPreviewItem>();
            decimal costoBase = ObtenerCostoCompraFinal();
            var prod = ConstruirProductoPreview();

            if (pnlListasPrecio?.Items != null && prod != null)
            {
                foreach (var item in pnlListasPrecio.Items)
                {
                    if (item is StackPanel sp && sp.Children.Count > 0 && sp.Children[0] is CheckBox chk
                        && chk.Tag is ListaPrecioItem lp && lp.ListaRow != null)
                    {
                        decimal? precioFijo = null;
                        if (lp.TipoLista == DatabaseService.TiposListaPrecio.PrecioFijo && lp.PrecioFijoInput != null)
                            precioFijo = lp.PrecioFijoInput.Value;

                        ActualizarEtiquetaPrecioLista(chk, lp);
                        if (chk.IsChecked == true)
                        {
                            items.Add(new PrecioListaPreviewItem
                            {
                                NombreLista = lp.NombreLista ?? "",
                                CostoBase = costoBase,
                                TipoLista = DatabaseService.EtiquetaTipoLista(lp.TipoLista),
                                Regla = ObtenerReglaLista(lp),
                                PrecioVentaFinal = DatabaseService.CalcularPrecioLista(prod, lp.ListaRow, precioFijo)
                            });
                        }
                    }
                }
            }

            dgvPreciosPreview.ItemsSource = items;
        }

        private static string ObtenerReglaLista(ListaPrecioItem lp)
        {
            if (lp == null) return "";

            switch (lp.TipoLista)
            {
                case DatabaseService.TiposListaPrecio.PrecioFijo:
                    if (lp.PrecioFijoInput?.Value > 0)
                        return $"Manual: {lp.PrecioFijoInput.Value:C2}";
                    return "Precio manual";

                case DatabaseService.TiposListaPrecio.ListaRelacionada:
                    int? parentId = lp.ListaRow?.Table.Columns.Contains("ListaRelacionadaID") == true
                        && lp.ListaRow["ListaRelacionadaID"] != DBNull.Value
                        ? (int?)Convert.ToInt32(lp.ListaRow["ListaRelacionadaID"])
                        : null;
                    if (parentId.HasValue && parentId.Value > 0)
                    {
                        var parent = DatabaseService.GetListaPrecioRow(parentId.Value);
                        string parentName = parent?["Nombre"]?.ToString() ?? $"Lista #{parentId.Value}";
                        return $"{parentName} + {lp.Porcentaje:N1}%";
                    }
                    return $"{lp.Porcentaje:N1}% sobre costo";

                default:
                    return $"{lp.Porcentaje:N1}%";
            }
        }

        private void CalcularPreciosListas(object sender, RoutedEventArgs e)
        {
            ActualizarGrillaPreciosPreview();
        }

        private decimal ObtenerIvaDecimal()
        {
            string ivaStr = (cmbTipoIVA.SelectedItem as ComboBoxItem)?.Content?.ToString() ?? "21%";
            if (ivaStr.Contains("%")) ivaStr = ivaStr.Split('%')[0].Trim();
            return decimal.TryParse(ivaStr, System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out decimal d) ? d : 21;
        }

        private void CargarProducto(int id)
        {
            var dt = DatabaseService.GetProductos("", true);
            if (dt == null) return;
            var rows = dt.Select($"ProductoID={id}");
            if (rows.Length == 0) return;
            var r = rows[0];

            txtCodigo.Text = r["Codigo"]?.ToString() ?? "";
            txtCodigoBarra.Text = r["CodigoBarra"]?.ToString() ?? "";
            txtDescripcion.Text = r["Descripcion"]?.ToString() ?? "";
            SeleccionarLookupPorNombre(cmbCategoria, V(r, "Categoria"));
            SeleccionarLookupPorNombre(cmbSubRubro, V(r, "SubRubro"));
            txtMarca.Text = V(r, "Marca");
            SeleccionarLookupPorNombre(cmbProveedor, V(r, "Proveedor"));
            txtCodigoExterno.Text = V(r, "CodigoExterno");

            string moneda = V(r, "TipoMoneda");
            if (string.IsNullOrEmpty(moneda)) moneda = "Pesos";
            EstablecerCombo(cmbTipoMoneda, moneda);

            string iva = r["TipoIVA"]?.ToString() ?? "21";
            if (cmbTipoIVA?.Items != null)
            {
                foreach (ComboBoxItem item in cmbTipoIVA.Items)
                {
                    if (item?.Content?.ToString()?.StartsWith(iva) == true)
                    {
                        cmbTipoIVA.SelectedItem = item;
                        break;
                    }
                }
            }

            _suspendCalculoPrecio = true;
            try
            {
            numCosto.Value = r["PrecioCosto"] != DBNull.Value ? Convert.ToDecimal(r["PrecioCosto"]) : 0;
            numImpuestoInterno.Value = r["ImpuestoInterno"] != DBNull.Value ? Convert.ToDecimal(r["ImpuestoInterno"]) : 0;
            if (r.Table.Columns.Contains("CostoIncluyeIva") && r["CostoIncluyeIva"] != DBNull.Value)
                chkCostoIncluyeIva.IsChecked = Convert.ToBoolean(r["CostoIncluyeIva"]);
            else
                chkCostoIncluyeIva.IsChecked = false;
            _stockActual = r["StockActual"] != DBNull.Value ? Convert.ToInt32(r["StockActual"]) : 0;

            chkPermiteModificarPrecioVenta.IsChecked = r.Table.Columns.Contains("PermiteModificarPrecioVenta") && r["PermiteModificarPrecioVenta"] != DBNull.Value && Convert.ToBoolean(r["PermiteModificarPrecioVenta"]);
            chkEsStockeable.IsChecked = !r.Table.Columns.Contains("EsStockeable") || r["EsStockeable"] == DBNull.Value || Convert.ToBoolean(r["EsStockeable"]);
            chkAceptaStockNegativo.IsChecked = r.Table.Columns.Contains("AceptaStockNegativo") && r["AceptaStockNegativo"] != DBNull.Value && Convert.ToBoolean(r["AceptaStockNegativo"]);
            chkUsaVariantes.IsChecked = r.Table.Columns.Contains("UsaVariantes") && r["UsaVariantes"] != DBNull.Value && Convert.ToBoolean(r["UsaVariantes"]);
            chkEsCombo.IsChecked = r.Table.Columns.Contains("EsCombo") && r["EsCombo"] != DBNull.Value && Convert.ToBoolean(r["EsCombo"]);
            chkActivo.IsChecked = !r.Table.Columns.Contains("Activo") || r["Activo"] == DBNull.Value || Convert.ToBoolean(r["Activo"]);

            txtVarianteColor.Text = V(r, "VarianteColor");
            txtVarianteTalle.Text = V(r, "VarianteTalle");
            txtVarianteUnidadMedida.Text = V(r, "VarianteUnidadMedida");

            numStockDisponible.Value = _stockActual;
            numStockMinimo.Value = r.Table.Columns.Contains("StockMinimo") && r["StockMinimo"] != DBNull.Value ? Convert.ToInt32(r["StockMinimo"]) : 0;
            numStockIdeal.Value = r.Table.Columns.Contains("StockIdeal") && r["StockIdeal"] != DBNull.Value ? Convert.ToInt32(r["StockIdeal"]) : 0;

            _rutaImagen = r["ImagenPath"]?.ToString() ?? "";
            CargarImagen(_rutaImagen);

            var listasDetalle = DatabaseService.GetProductoListasDetalle(id);
            if (pnlListasPrecio?.Items != null)
            {
                foreach (var item in pnlListasPrecio.Items)
                {
                    if (item is StackPanel sp && sp.Children.Count > 0 && sp.Children[0] is CheckBox chk && chk.Tag is ListaPrecioItem lp)
                    {
                        chk.IsChecked = listasDetalle.ContainsKey(lp.ListaID);
                        if (lp.PrecioFijoInput != null && listasDetalle.TryGetValue(lp.ListaID, out decimal? pf) && pf.HasValue)
                            lp.PrecioFijoInput.Value = pf.Value;
                        ActualizarPrecioLista(chk);
                    }
                }
            }

            if (chkEsCombo?.IsChecked == true)
            {
                var componentes = DatabaseService.GetProductoComboDetalle(id);
                if (componentes != null)
                {
                    var lineas = componentes.Select(c =>
                    {
                        var prod = DatabaseService.GetProductos("", true);
                        if (prod == null) return $"{c.ProductoComponenteID}:{c.Cantidad}";
                        var row = prod.Select($"ProductoID={c.ProductoComponenteID}").FirstOrDefault();
                        string cod = row?["Codigo"]?.ToString() ?? c.ProductoComponenteID.ToString();
                        return $"{cod}:{c.Cantidad}";
                    });
                    if (txtComponentes != null) txtComponentes.Text = string.Join(", ", lineas);
                }
            }

            chkEsStockeable_Changed(null, null);
            chkUsaVariantes_Changed(null, null);
            chkEsCombo_Changed(null, null);
            CalcularPreciosListas(null, null);
            ActualizarCostoCompraFinal();
            ActualizarAyudaCostoIva();
            ActualizarGrillaPreciosPreview();
            ConfigurarBotonBaja();
            }
            finally { _suspendCalculoPrecio = false; }
        }

        private static string V(DataRow r, string col) => r.Table.Columns.Contains(col) && r[col] != DBNull.Value ? r[col].ToString() : "";

        private void EstablecerCombo(ComboBox cb, string valor)
        {
            if (cb == null) return;
            if (string.IsNullOrEmpty(valor)) { cb.Text = ""; return; }
            for (int i = 0; i < cb.Items.Count; i++)
            {
                var item = cb.Items[i];
                string c = (item as ComboBoxItem)?.Content?.ToString() ?? item?.ToString() ?? "";
                if (c.Equals(valor, StringComparison.OrdinalIgnoreCase)) { cb.SelectedIndex = i; return; }
            }
            cb.Text = valor;
        }

        private void MarcarTodasListasPrecioPorDefecto()
        {
            if (pnlListasPrecio?.Items == null) return;

            foreach (var item in pnlListasPrecio.Items)
            {
                if (item is StackPanel sp && sp.Children.Count > 0 && sp.Children[0] is CheckBox chk)
                {
                    chk.IsChecked = true;
                    if (chk.Tag is ListaPrecioItem lp && lp.PrecioFijoInput != null)
                        lp.PrecioFijoInput.Visibility = Visibility.Visible;
                }
            }

            ActualizarGrillaPreciosPreview();
        }

        private void Limpiar()
        {
            txtCodigo.Text = "";
            txtCodigoBarra.Text = "";
            txtDescripcion.Text = "";
            SeleccionarLookupPorNombre(cmbCategoria, "");
            SeleccionarLookupPorNombre(cmbSubRubro, "");
            txtMarca.Text = "";
            SeleccionarLookupPorNombre(cmbProveedor, "");
            txtCodigoExterno.Text = "";
            cmbTipoMoneda.SelectedIndex = 0;
            cmbTipoIVA.SelectedIndex = 0;
            chkGenerarCodigoBarra.IsChecked = false;
            chkPermiteModificarPrecioVenta.IsChecked = false;
            chkEsStockeable.IsChecked = true;
            chkAceptaStockNegativo.IsChecked = false;
            chkUsaVariantes.IsChecked = false;
            chkEsCombo.IsChecked = false;
            chkActivo.IsChecked = true;
            txtVarianteColor.Text = "";
            txtVarianteTalle.Text = "";
            txtVarianteUnidadMedida.Text = "";
            txtComponentes.Text = "";
            _suspendCalculoPrecio = true;
            try
            {
            numCosto.Value = 0;
            numImpuestoInterno.Value = 0;
            chkCostoIncluyeIva.IsChecked = false;
            numStockDisponible.Value = 0;
            numStockMinimo.Value = 0;
            numStockIdeal.Value = 0;
            _rutaImagen = "";
            CargarImagen("");
            MarcarTodasListasPrecioPorDefecto();
            chkEsStockeable_Changed(null, null);
            chkUsaVariantes_Changed(null, null);
            chkEsCombo_Changed(null, null);
            ActualizarCostoCompraFinal();
            ActualizarAyudaCostoIva();
            }
            finally { _suspendCalculoPrecio = false; }
        }

        private void CargarImagen(string ruta)
        {
            try
            {
                if (imgProducto == null) return;
                if (!string.IsNullOrEmpty(ruta) && (ruta.StartsWith("pack://") || File.Exists(ruta)))
                    imgProducto.Source = new BitmapImage(new Uri(ruta));
                else
                {
                    try { imgProducto.Source = new BitmapImage(new Uri("pack://application:,,,/SchettiniGestion.WPF;component/Resources/no-image.png")); }
                    catch { imgProducto.Source = null; }
                }
            }
            catch { if (imgProducto != null) imgProducto.Source = null; }
        }

        private decimal ObtenerCostoCompraFinal()
        {
            decimal costo = numCosto?.Value ?? 0;
            decimal imp = numImpuestoInterno?.Value ?? 0;
            bool conIva = chkCostoIncluyeIva?.IsChecked == true;
            return DatabaseService.CalcularCostoCompraFinal(costo, conIva, ObtenerIvaDecimal(), imp);
        }

        private void ActualizarCostoCompraFinal()
        {
            if (lblCostoCompraFinal == null) return;
            decimal final = ObtenerCostoCompraFinal();
            lblCostoCompraFinal.Text = $"Costo final: {final:C2}";
        }

        private void ActualizarAyudaCostoIva()
        {
            if (txtAyudaCostoIva == null) return;
            txtAyudaCostoIva.Text = chkCostoIncluyeIva?.IsChecked == true
                ? "El costo ingresado ya incluye IVA. El costo final es ese valor más el impuesto interno."
                : "El costo ingresado no incluye IVA. Al costo final se le suma el IVA y el impuesto interno.";
        }

        private void chkCostoIncluyeIva_Changed(object sender, RoutedEventArgs e)
        {
            ActualizarAyudaCostoIva();
            ActualizarCostoCompraFinal();
            CalcularPrecio_ValueChanged(numCosto, null);
        }

        private void CalcularPrecio_ValueChanged(object sender, RoutedPropertyChangedEventArgs<object> e)
        {
            if (_suspendCalculoPrecio || numCosto == null) return;
            ActualizarCostoCompraFinal();
            ActualizarGrillaPreciosPreview();
        }

        private decimal ObtenerPrecioVentaReferencia()
        {
            var prod = ConstruirProductoPreview();
            if (prod == null) return 0m;

            if (pnlListasPrecio?.Items != null)
            {
                foreach (var item in pnlListasPrecio.Items)
                {
                    if (item is StackPanel sp && sp.Children.Count > 0 && sp.Children[0] is CheckBox chk
                        && chk.IsChecked == true && chk.Tag is ListaPrecioItem lp && lp.ListaRow != null)
                    {
                        decimal? precioFijo = null;
                        if (lp.TipoLista == DatabaseService.TiposListaPrecio.PrecioFijo && lp.PrecioFijoInput != null)
                            precioFijo = lp.PrecioFijoInput.Value;
                        return DatabaseService.CalcularPrecioLista(prod, lp.ListaRow, precioFijo);
                    }
                }
            }

            try
            {
                var dt = DatabaseService.GetListasPrecios();
                if (dt != null && dt.Rows.Count > 0)
                    return DatabaseService.CalcularPrecioLista(prod, dt.Rows[0], null);
            }
            catch { }

            return ObtenerCostoCompraFinal();
        }

        private void cmbTipoIVA_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            CalcularPrecio_ValueChanged(numCosto, null);
        }

        private void chkGenerarCodigoBarra_Changed(object sender, RoutedEventArgs e)
        {
            if (chkGenerarCodigoBarra?.IsChecked == true && string.IsNullOrWhiteSpace(txtCodigoBarra?.Text))
            {
                string cod = txtCodigo?.Text?.Trim() ?? "";
                if (!string.IsNullOrEmpty(cod))
                    txtCodigoBarra.Text = GenerarEan13(cod);
                else
                    txtCodigoBarra.Text = GenerarEan13(Guid.NewGuid().ToString("N").Substring(0, 12));
            }
        }

        private static string GenerarEan13(string base12)
        {
            string s = Regex.Replace(base12, @"\D", "");
            if (s.Length > 12) s = s.Substring(0, 12);
            while (s.Length < 12) s = "0" + s;
            int sum = 0;
            for (int i = 0; i < 12; i++)
                sum += (s[i] - '0') * (i % 2 == 0 ? 1 : 3);
            int check = (10 - (sum % 10)) % 10;
            return s + check;
        }

        private void chkEsStockeable_Changed(object sender, RoutedEventArgs e)
        {
            if (pnlStockGrid == null) return;
            bool stockeable = chkEsStockeable?.IsChecked == true;
            pnlStockGrid.Visibility = stockeable ? Visibility.Visible : Visibility.Collapsed;
        }

        private void chkUsaVariantes_Changed(object sender, RoutedEventArgs e)
        {
            if (pnlVariantes == null) return;
            pnlVariantes.Visibility = chkUsaVariantes?.IsChecked == true ? Visibility.Visible : Visibility.Collapsed;
        }

        private void chkEsCombo_Changed(object sender, RoutedEventArgs e)
        {
            if (pnlCombo == null) return;
            pnlCombo.Visibility = chkEsCombo?.IsChecked == true ? Visibility.Visible : Visibility.Collapsed;
        }

        private void chkActivo_Changed(object sender, RoutedEventArgs e)
        {
            ConfigurarBotonBaja();
        }

        private bool GuardarProductoActual(bool crearYOtro)
        {
            if (string.IsNullOrWhiteSpace(txtCodigo.Text) || string.IsNullOrWhiteSpace(txtDescripcion.Text))
            {
                ModernMessageBox.Show("El Código y la Descripción son obligatorios.", "Aviso", MessageBoxButton.OK, MessageBoxImage.Warning);
                return false;
            }

            string codigoBarra = txtCodigoBarra.Text?.Trim() ?? "";
            if (chkGenerarCodigoBarra?.IsChecked == true && string.IsNullOrEmpty(codigoBarra))
                codigoBarra = GenerarEan13(txtCodigo.Text.Trim());

            string ivaStr = (cmbTipoIVA.SelectedItem as ComboBoxItem)?.Content?.ToString() ?? "21%";
            if (ivaStr.Contains("%")) ivaStr = ivaStr.Split('%')[0].Trim();

            string tipoMoneda = (cmbTipoMoneda.SelectedItem as ComboBoxItem)?.Content?.ToString() ?? "ARS";
            int stock = chkEsStockeable?.IsChecked == true ? (int)(numStockDisponible?.Value ?? 0) : (_productoId != 0 ? _stockActual : 0);

            if (DatabaseService.ExisteProductoDuplicado(_productoId, txtCodigo.Text.Trim(), codigoBarra, out string duplicadoMsg))
            {
                ModernMessageBox.Show("No se puede guardar porque hay códigos duplicados:\n\n" + duplicadoMsg,
                    "Producto duplicado", MessageBoxButton.OK, MessageBoxImage.Warning);
                return false;
            }

            int productoId = DatabaseService.GuardarProducto(
                _productoId,
                txtCodigo.Text.Trim(),
                codigoBarra,
                txtDescripcion.Text.Trim(),
                ObtenerTextoLookupCombo(cmbCategoria),
                ObtenerTextoLookupCombo(cmbSubRubro),
                txtMarca?.Text?.Trim() ?? "",
                ObtenerTextoLookupCombo(cmbProveedor),
                ivaStr,
                numCosto.Value ?? 0,
                0,
                numImpuestoInterno.Value ?? 0,
                ObtenerPrecioVentaReferencia(),
                stock,
                _rutaImagen,
                tipoMoneda,
                chkPermiteModificarPrecioVenta?.IsChecked == true,
                chkEsStockeable?.IsChecked != false,
                chkAceptaStockNegativo?.IsChecked == true,
                chkUsaVariantes?.IsChecked == true,
                chkEsCombo?.IsChecked == true,
                numStockMinimo?.Value,
                numStockIdeal?.Value,
                txtCodigoExterno?.Text?.Trim(),
                chkUsaVariantes?.IsChecked == true ? txtVarianteColor?.Text?.Trim() : null,
                chkUsaVariantes?.IsChecked == true ? txtVarianteTalle?.Text?.Trim() : null,
                chkUsaVariantes?.IsChecked == true ? txtVarianteUnidadMedida?.Text?.Trim() : null,
                true,
                chkCostoIncluyeIva?.IsChecked == true,
                chkActivo?.IsChecked != false
            );

            if (productoId <= 0)
            {
                ModernMessageBox.Show("Error al guardar.", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                return false;
            }

            var asignaciones = new List<DatabaseService.ProductoListaAsignacion>();
            if (pnlListasPrecio?.Items != null)
            {
                foreach (var item in pnlListasPrecio.Items)
                {
                    if (item is StackPanel sp && sp.Children.Count > 0 && sp.Children[0] is CheckBox chk && chk.IsChecked == true && chk.Tag is ListaPrecioItem lp)
                    {
                        decimal? pf = null;
                        if (lp.TipoLista == DatabaseService.TiposListaPrecio.PrecioFijo && lp.PrecioFijoInput != null)
                            pf = lp.PrecioFijoInput.Value;
                        asignaciones.Add(new DatabaseService.ProductoListaAsignacion { ListaID = lp.ListaID, PrecioFijo = pf });
                    }
                }
            }
            DatabaseService.GuardarProductoListas(productoId, asignaciones);

            if (chkEsCombo?.IsChecked == true && !string.IsNullOrWhiteSpace(txtComponentes?.Text))
            {
                var componentes = new List<(int, int)>();
                foreach (var parte in txtComponentes.Text.Split(new[] { ',', ';', '\n' }, StringSplitOptions.RemoveEmptyEntries))
                {
                    var m = Regex.Match(parte.Trim(), @"^(.+?)\s*:\s*(\d+)$");
                    if (m.Success)
                    {
                        string cod = m.Groups[1].Value.Trim();
                        int cant = int.Parse(m.Groups[2].Value);
                        var prod = DatabaseService.GetProductos("", true);
                        if (prod != null)
                        {
                            var rows = prod.Select($"Codigo='{cod.Replace("'", "''")}'");
                            if (rows.Length > 0)
                                componentes.Add((Convert.ToInt32(rows[0]["ProductoID"]), cant));
                        }
                    }
                }
                DatabaseService.GuardarProductoComboDetalle(productoId, componentes);
            }

            _onGuardado?.Invoke();
            if (!crearYOtro)
            {
                ModernMessageBox.Show("Producto guardado correctamente.", "Éxito", MessageBoxButton.OK, MessageBoxImage.Information);
                DialogResult = true;
                Close();
            }
            else
            {
                _productoId = 0;
                Limpiar();
                txtTitulo.Text = "Nuevo Producto (otro)";
            }
            return true;
        }

        private void btnGuardar_Click(object sender, RoutedEventArgs e)
        {
            GuardarProductoActual(false);
        }

        private void btnCrearYOtro_Click(object sender, RoutedEventArgs e)
        {
            GuardarProductoActual(true);
        }

        private void btnDuplicar_Click(object sender, RoutedEventArgs e)
        {
            _productoId = 0;
            txtCodigo.Text = "";
            txtCodigoBarra.Text = "";
            txtTitulo.Text = "Duplicar Producto (nuevo)";
            btnDuplicar.Visibility = Visibility.Collapsed;
            btnEliminar.Visibility = Visibility.Collapsed;
            btnCrearYOtro.Visibility = Visibility.Visible;
            btnGuardar.Content = "💾 Crear";
            chkActivo.IsChecked = true;
            ConfigurarBotonBaja();
        }

        private void ConfigurarBotonBaja()
        {
            if (btnEliminar == null || _productoId == 0) return;
            bool activo = chkActivo?.IsChecked != false;
            if (SesionUsuario.EsUsuarioTecnico)
            {
                btnEliminar.Content = "Eliminar";
                btnEliminar.ToolTip = "Eliminación física reservada para usuario técnico.";
            }
            else
            {
                btnEliminar.Content = activo ? "Deshabilitar" : "Reactivar";
                btnEliminar.ToolTip = activo
                    ? "Oculta el producto del POS sin borrar historial."
                    : "Vuelve a mostrar el producto en el POS.";
            }
        }

        private void btnEliminar_Click(object sender, RoutedEventArgs e)
        {
            if (SesionUsuario.EsUsuarioTecnico)
            {
                if (ModernMessageBox.Show("¿Eliminar definitivamente este producto? Esta acción es solo para soporte técnico.", "Confirmar eliminación técnica", MessageBoxButton.YesNo, MessageBoxImage.Warning) != MessageBoxResult.Yes)
                    return;

                if (DatabaseService.EliminarProducto(_productoId))
                {
                    _onGuardado?.Invoke();
                    DialogResult = true;
                    Close();
                }
                else
                    ModernMessageBox.Show("No se pudo eliminar.", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            bool activo = chkActivo?.IsChecked != false;
            string accion = activo ? "deshabilitar" : "reactivar";
            if (ModernMessageBox.Show($"¿{char.ToUpper(accion[0]) + accion.Substring(1)} este producto?", "Confirmar", MessageBoxButton.YesNo, MessageBoxImage.Warning) != MessageBoxResult.Yes)
                return;

            bool ok = activo
                ? DatabaseService.DeshabilitarProducto(_productoId)
                : DatabaseService.RehabilitarProducto(_productoId);
            if (ok)
            {
                chkActivo.IsChecked = !activo;
                ConfigurarBotonBaja();
                _onGuardado?.Invoke();
                DialogResult = true;
                Close();
            }
            else
            {
                ModernMessageBox.Show("No se pudo actualizar el estado del producto.", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void btnCambiarImagen_Click(object sender, RoutedEventArgs e)
        {
            var dlg = new OpenFileDialog
            {
                Filter = "Imágenes|*.jpg;*.jpeg;*.png;*.bmp;*.gif",
                Title = "Seleccionar imagen del producto"
            };
            if (dlg.ShowDialog() == true)
            {
                _rutaImagen = dlg.FileName;
                CargarImagen(_rutaImagen);
            }
        }

        private void btnCancelar_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }
    }
}
