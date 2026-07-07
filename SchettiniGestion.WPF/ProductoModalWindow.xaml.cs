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
                    var chk = new CheckBox
                    {
                        Content = nombre,
                        Foreground = (Brush)FindResource("TextPrimary"),
                        Style = (Style)FindResource("ProductoTouchCheckBox"),
                        Margin = new Thickness(0, 6, 0, 6)
                    };
                    chk.Checked += (s, e) => ActualizarPrecioLista(chk);
                    chk.Unchecked += (s, e) => ActualizarPrecioLista(chk);
                    var sp = new StackPanel { Orientation = Orientation.Horizontal };
                    sp.Children.Add(chk);
                    var lbl = new TextBlock
                    {
                        Foreground = (Brush)FindResource("TextSecondary"),
                        FontSize = 13,
                        Margin = new Thickness(10, 0, 0, 0),
                        VerticalAlignment = VerticalAlignment.Center
                    };
                    sp.Children.Add(lbl);
                    chk.Tag = new ListaPrecioItem { ListaID = listaId, Porcentaje = pct, CheckBox = chk, LabelPrecio = lbl };
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
            public decimal Porcentaje { get; set; }
            public CheckBox CheckBox { get; set; }
            public TextBlock LabelPrecio { get; set; }
        }

        private void ActualizarPrecioLista(CheckBox chk)
        {
            if (chk?.Tag is ListaPrecioItem item && item.LabelPrecio != null)
            {
                decimal precioBase = numPrecioFinal?.Value ?? 0;
                decimal precioLista = precioBase * (1 + item.Porcentaje / 100m);
                bool cobraIva = chkCobraIvaAlCliente?.IsChecked == true;
                string txt = cobraIva
                    ? $" → ${precioLista:N2} (IVA incl.)"
                    : $" → ${precioLista:N2} (sin IVA)";
                decimal tipoCambio = DatabaseService.GetTipoCambioUSD() ?? 0;
                string moneda = (cmbTipoMoneda?.SelectedItem as ComboBoxItem)?.Content?.ToString() ?? "Pesos";
                if (tipoCambio > 0)
                {
                    decimal usd = moneda == "USD" ? precioLista : precioLista / tipoCambio;
                    decimal ars = moneda == "USD" ? precioLista * tipoCambio : precioLista;
                    txt = cobraIva
                        ? $" → ${ars:N0} ARS / USD {usd:N2} (IVA incl.)"
                        : $" → ${ars:N0} ARS / USD {usd:N2} (sin IVA)";
                }
                item.LabelPrecio.Text = chk.IsChecked == true ? txt : "";
            }
        }

        private void CalcularPreciosListas(object sender, RoutedEventArgs e)
        {
            if (pnlListasPrecio?.Items == null) return;
            foreach (var item in pnlListasPrecio.Items)
            {
                if (item is StackPanel sp && sp.Children.Count > 0 && sp.Children[0] is CheckBox chk)
                    ActualizarPrecioLista(chk);
            }
        }

        private decimal ObtenerIvaDecimal()
        {
            string ivaStr = (cmbTipoIVA.SelectedItem as ComboBoxItem)?.Content?.ToString() ?? "21%";
            if (ivaStr.Contains("%")) ivaStr = ivaStr.Split('%')[0].Trim();
            return decimal.TryParse(ivaStr, System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out decimal d) ? d : 21;
        }

        private void CargarProducto(int id)
        {
            var dt = DatabaseService.GetProductos("");
            if (dt == null) return;
            var rows = dt.Select($"ProductoID={id}");
            if (rows.Length == 0) return;
            var r = rows[0];

            txtCodigo.Text = r["Codigo"]?.ToString() ?? "";
            txtCodigoBarra.Text = r["CodigoBarra"]?.ToString() ?? "";
            txtDescripcion.Text = r["Descripcion"]?.ToString() ?? "";
            SeleccionarLookupPorNombre(cmbCategoria, V(r, "Categoria"));
            SeleccionarLookupPorNombre(cmbSubRubro, V(r, "SubRubro"));
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
            numGanancia.Value = r["Ganancia"] != DBNull.Value ? Convert.ToDecimal(r["Ganancia"]) : 30;
            numPrecioFinal.Value = r["PrecioVenta"] != DBNull.Value ? Convert.ToDecimal(r["PrecioVenta"]) : 0;
            _stockActual = r["StockActual"] != DBNull.Value ? Convert.ToInt32(r["StockActual"]) : 0;

            if (r.Table.Columns.Contains("CobraIvaAlCliente") && r["CobraIvaAlCliente"] != DBNull.Value)
                chkCobraIvaAlCliente.IsChecked = Convert.ToBoolean(r["CobraIvaAlCliente"]);
            else
                chkCobraIvaAlCliente.IsChecked = true;
            ActualizarAyudaPrecioIva();

            chkPermiteModificarPrecioVenta.IsChecked = r.Table.Columns.Contains("PermiteModificarPrecioVenta") && r["PermiteModificarPrecioVenta"] != DBNull.Value && Convert.ToBoolean(r["PermiteModificarPrecioVenta"]);
            chkEsStockeable.IsChecked = !r.Table.Columns.Contains("EsStockeable") || r["EsStockeable"] == DBNull.Value || Convert.ToBoolean(r["EsStockeable"]);
            chkAceptaStockNegativo.IsChecked = r.Table.Columns.Contains("AceptaStockNegativo") && r["AceptaStockNegativo"] != DBNull.Value && Convert.ToBoolean(r["AceptaStockNegativo"]);
            chkUsaVariantes.IsChecked = r.Table.Columns.Contains("UsaVariantes") && r["UsaVariantes"] != DBNull.Value && Convert.ToBoolean(r["UsaVariantes"]);
            chkEsCombo.IsChecked = r.Table.Columns.Contains("EsCombo") && r["EsCombo"] != DBNull.Value && Convert.ToBoolean(r["EsCombo"]);

            txtVarianteColor.Text = V(r, "VarianteColor");
            txtVarianteTalle.Text = V(r, "VarianteTalle");
            txtVarianteUnidadMedida.Text = V(r, "VarianteUnidadMedida");

            numStockDisponible.Value = _stockActual;
            numStockMinimo.Value = r.Table.Columns.Contains("StockMinimo") && r["StockMinimo"] != DBNull.Value ? Convert.ToInt32(r["StockMinimo"]) : 0;
            numStockIdeal.Value = r.Table.Columns.Contains("StockIdeal") && r["StockIdeal"] != DBNull.Value ? Convert.ToInt32(r["StockIdeal"]) : 0;

            _rutaImagen = r["ImagenPath"]?.ToString() ?? "";
            CargarImagen(_rutaImagen);

            var listas = DatabaseService.GetProductoListas(id) ?? new List<int>();
            if (pnlListasPrecio?.Items != null)
            {
                foreach (var item in pnlListasPrecio.Items)
                {
                    if (item is StackPanel sp && sp.Children.Count > 0 && sp.Children[0] is CheckBox chk && chk.Tag is ListaPrecioItem lp)
                        chk.IsChecked = listas.Contains(lp.ListaID);
                }
            }

            if (chkEsCombo?.IsChecked == true)
            {
                var componentes = DatabaseService.GetProductoComboDetalle(id);
                if (componentes != null)
                {
                    var lineas = componentes.Select(c =>
                    {
                        var prod = DatabaseService.GetProductos("");
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

        private void Limpiar()
        {
            txtCodigo.Text = "";
            txtCodigoBarra.Text = "";
            txtDescripcion.Text = "";
            SeleccionarLookupPorNombre(cmbCategoria, "");
            SeleccionarLookupPorNombre(cmbSubRubro, "");
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
            txtVarianteColor.Text = "";
            txtVarianteTalle.Text = "";
            txtVarianteUnidadMedida.Text = "";
            txtComponentes.Text = "";
            _suspendCalculoPrecio = true;
            try
            {
            numCosto.Value = 0;
            numGanancia.Value = 30;
            numPrecioFinal.Value = 0;
            numStockDisponible.Value = 0;
            numStockMinimo.Value = 0;
            numStockIdeal.Value = 0;
            _rutaImagen = "";
            CargarImagen("");
            if (pnlListasPrecio?.Items != null)
            {
                foreach (var item in pnlListasPrecio.Items)
                {
                    if (item is StackPanel sp && sp.Children.Count > 0 && sp.Children[0] is CheckBox chk)
                        chk.IsChecked = false;
                }
            }
            chkEsStockeable_Changed(null, null);
            chkUsaVariantes_Changed(null, null);
            chkEsCombo_Changed(null, null);
            chkCobraIvaAlCliente.IsChecked = true;
            ActualizarAyudaPrecioIva();
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

        private void CalcularPrecio_ValueChanged(object sender, RoutedPropertyChangedEventArgs<object> e)
        {
            if (_suspendCalculoPrecio || numCosto == null || numGanancia == null || numPrecioFinal == null) return;
            decimal costo = numCosto.Value ?? 0;
            decimal gan = numGanancia.Value ?? 0;
            decimal basePrecio = costo + (costo * gan / 100m);
            if (chkCobraIvaAlCliente?.IsChecked == true)
                numPrecioFinal.Value = Math.Round(basePrecio * (1 + ObtenerIvaDecimal() / 100m), 2);
            else
                numPrecioFinal.Value = basePrecio;
            CalcularPreciosListas(null, null);
        }

        private void cmbTipoIVA_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            CalcularPrecio_ValueChanged(numCosto, null);
            ActualizarAyudaPrecioIva();
        }

        private void chkCobraIvaAlCliente_Changed(object sender, RoutedEventArgs e)
        {
            ActualizarAyudaPrecioIva();
            CalcularPrecio_ValueChanged(numCosto, null);
        }

        private void ActualizarAyudaPrecioIva()
        {
            if (txtAyudaPrecioIva == null) return;
            txtAyudaPrecioIva.Text = chkCobraIvaAlCliente?.IsChecked == true
                ? "El precio de venta incluye IVA (se discrimina en factura)."
                : "El precio de venta no incluye IVA (no se cobra IVA al cliente).";
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

            string tipoMoneda = (cmbTipoMoneda.SelectedItem as ComboBoxItem)?.Content?.ToString() ?? "Pesos";
            int stock = chkEsStockeable?.IsChecked == true ? (int)(numStockDisponible?.Value ?? 0) : (_productoId != 0 ? _stockActual : 0);

            int productoId = DatabaseService.GuardarProducto(
                _productoId,
                txtCodigo.Text.Trim(),
                codigoBarra,
                txtDescripcion.Text.Trim(),
                ObtenerTextoLookupCombo(cmbCategoria),
                ObtenerTextoLookupCombo(cmbSubRubro),
                "",
                ObtenerTextoLookupCombo(cmbProveedor),
                ivaStr,
                numCosto.Value ?? 0,
                numGanancia.Value ?? 0,
                0,
                numPrecioFinal.Value ?? 0,
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
                chkCobraIvaAlCliente?.IsChecked != false
            );

            if (productoId <= 0)
            {
                ModernMessageBox.Show("Error al guardar.", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                return false;
            }

            var listaIds = new List<int>();
            if (pnlListasPrecio?.Items != null)
            {
                foreach (var item in pnlListasPrecio.Items)
                {
                    if (item is StackPanel sp && sp.Children.Count > 0 && sp.Children[0] is CheckBox chk && chk.IsChecked == true && chk.Tag is ListaPrecioItem lp)
                        listaIds.Add(lp.ListaID);
                }
            }
            DatabaseService.GuardarProductoListas(productoId, listaIds);

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
                        var prod = DatabaseService.GetProductos("");
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
        }

        private void btnEliminar_Click(object sender, RoutedEventArgs e)
        {
            if (ModernMessageBox.Show("¿Eliminar este producto?", "Confirmar", MessageBoxButton.YesNo, MessageBoxImage.Warning) == MessageBoxResult.Yes)
            {
                if (DatabaseService.EliminarProducto(_productoId))
                {
                    _onGuardado?.Invoke();
                    DialogResult = true;
                    Close();
                }
                else
                    ModernMessageBox.Show("No se pudo eliminar.", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
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
