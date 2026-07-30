using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Data;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using Microsoft.Win32;
using SchettiniGestion;

namespace SchettiniGestion.WPF
{
    public partial class CompraModalWindow : Window
    {
        public int ResultID { get; private set; } = 0;

        private readonly int _compraId;
        private readonly Action _onGuardado;
        private int _proveedorId = 0;
        private int _productoId = 0;
        private ObservableCollection<CompraItem> _items = new ObservableCollection<CompraItem>();
        private bool _ignorarTextChanged = false;
        private bool _cargandoOrdenes = false;
        private bool _cargandoItemsOc = false;

        public CompraModalWindow()
        {
            InitializeComponent();
            _items.CollectionChanged += (s, e) => ActualizarTotal();
            dgvItems.ItemsSource = _items;
            Loaded += OnLoaded;
        }

        public CompraModalWindow(Window owner, Action onGuardado) : this()
        {
            Owner = owner;
            _onGuardado = onGuardado;
        }

        public CompraModalWindow(Window owner, Action onGuardado, int compraId) : this(owner, onGuardado)
        {
            _compraId = compraId;
        }

        private void OnLoaded(object sender, RoutedEventArgs e)
        {
            if (_compraId > 0)
            {
                lblTitulo.Text = "Ver Factura de Compra";
                CargarCompraExistente();
                chkRecepcionarStock.IsEnabled = false;
                cmbOrdenCompra.IsEnabled = false;
                txtBuscarProveedor.IsEnabled = false;
                txtBuscarProducto.IsEnabled = false;
                txtCantidad.IsEnabled = false;
                txtCosto.IsEnabled = false;
                cmbTipoComprobante.IsEnabled = false;
                if (btnImportarPdf != null) btnImportarPdf.Visibility = Visibility.Collapsed;
                if (FindName("btnGuardar") is Button btn) btn.IsEnabled = false;
            }
            else
            {
                CargarOrdenesCompra();
            }
            _ignorarTextChanged = true;
            txtBuscarProveedor.Text = "";
            _ignorarTextChanged = false;
            ActualizarTotal();
        }

        private void CargarCompraExistente()
        {
            try
            {
                var dt = DatabaseService.GetCompras();
                foreach (DataRow r in dt.Rows)
                {
                    if (Convert.ToInt32(r["CompraID"]) == _compraId)
                    {
                        _proveedorId = Convert.ToInt32(r["ProveedorID"]);
                        lblProveedorSel.Text = r["Proveedor"]?.ToString();
                        if (r.Table.Columns.Contains("OrdenCompraID") && r["OrdenCompraID"] != DBNull.Value)
                        {
                            int ocId = Convert.ToInt32(r["OrdenCompraID"]);
                            if (ocId > 0) lblProveedorSel.Text += $"  |  OC #{ocId}";
                        }
                        if (r.Table.Columns.Contains("StockRecibido") && r["StockRecibido"] != DBNull.Value)
                            chkRecepcionarStock.IsChecked = Convert.ToBoolean(r["StockRecibido"]);
                        for (int i = 0; i < cmbTipoComprobante.Items.Count; i++)
                        {
                            if ((cmbTipoComprobante.Items[i] as ComboBoxItem)?.Content?.ToString() == r["TipoComprobante"]?.ToString())
                            { cmbTipoComprobante.SelectedIndex = i; break; }
                        }
                        break;
                    }
                }
                var det = DatabaseService.GetCompraDetalle(_compraId);
                foreach (DataRow r in det.Rows)
                    _items.Add(new CompraItem
                    {
                        ProductoID = Convert.ToInt32(r["ProductoID"]),
                        Codigo = r["Codigo"]?.ToString() ?? "",
                        Descripcion = r["Descripcion"]?.ToString() ?? "",
                        Cantidad = Convert.ToInt32(r["Cantidad"]),
                        Costo = Convert.ToDecimal(r["PrecioCosto"])
                    });
            }
            catch { }
        }

        private void CargarOrdenesCompra()
        {
            _cargandoOrdenes = true;
            try
            {
                var lista = new List<OrdenCompraOpcion> { new OrdenCompraOpcion { OrdenCompraID = 0, Etiqueta = "(Sin orden de compra)" } };
                if (_proveedorId > 0)
                {
                    var dt = DatabaseService.GetOrdenesCompraAbiertas(_proveedorId);
                    foreach (DataRow r in dt.Rows)
                    {
                        int oid = Convert.ToInt32(r["OrdenCompraID"]);
                        string est = r["Estado"]?.ToString() ?? "";
                        string fecha = r["Fecha"] != DBNull.Value ? Convert.ToDateTime(r["Fecha"]).ToString("dd/MM/yyyy") : "";
                        lista.Add(new OrdenCompraOpcion
                        {
                            OrdenCompraID = oid,
                            Etiqueta = $"OC #{oid} — {fecha} — {est}"
                        });
                    }
                }
                cmbOrdenCompra.ItemsSource = lista;
                cmbOrdenCompra.SelectedIndex = 0;
            }
            finally { _cargandoOrdenes = false; }
        }

        private void txtBuscarProveedor_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (_ignorarTextChanged) return;
            if (lstProveedores == null || popupProveedores == null || txtBuscarProveedor == null) return;
            string q = txtBuscarProveedor?.Text?.Trim() ?? "";
            if (q.Length < 2) { popupProveedores.IsOpen = false; return; }
            var dt = DatabaseService.BuscarProveedoresMultiples(q);
            if (dt == null) { popupProveedores.IsOpen = false; return; }
            lstProveedores.ItemsSource = dt.DefaultView;
            popupProveedores.IsOpen = dt.Rows.Count > 0;
        }

        private void lstProveedores_MouseUp(object sender, MouseButtonEventArgs e)
        {
            if (lstProveedores.SelectedItem is DataRowView row)
            {
                _proveedorId = Convert.ToInt32(row["ProveedorID"]);
                _ignorarTextChanged = true;
                txtBuscarProveedor.Text = row["RazonSocial"].ToString();
                lblProveedorSel.Text = row["RazonSocial"].ToString();
                _ignorarTextChanged = false;
                popupProveedores.IsOpen = false;
                CargarOrdenesCompra();
            }
        }

        private void chkRecepcionarStock_Changed(object sender, RoutedEventArgs e)
        {
            // Sin acción extra: el checkbox solo controla el flag al guardar.
        }

        private void cmbOrdenCompra_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (_cargandoOrdenes || _cargandoItemsOc || _compraId > 0) return;
            if (!(cmbOrdenCompra.SelectedItem is OrdenCompraOpcion sel) || sel.OrdenCompraID <= 0) return;

            if (_items.Count > 0)
            {
                if (MessageBox.Show("¿Cargar los ítems de la orden de compra seleccionada? Se reemplazará el detalle actual.",
                    "Cargar OC", MessageBoxButton.YesNo, MessageBoxImage.Question) != MessageBoxResult.Yes)
                    return;
            }

            _cargandoItemsOc = true;
            try
            {
                _items.Clear();
                var det = DatabaseService.GetOrdenCompraDetalleFull(sel.OrdenCompraID);
                foreach (DataRow r in det.Rows)
                {
                    _items.Add(new CompraItem
                    {
                        ProductoID = Convert.ToInt32(r["ProductoID"]),
                        Codigo = r["Codigo"]?.ToString() ?? "",
                        Descripcion = r["Descripcion"]?.ToString() ?? "",
                        Cantidad = Convert.ToInt32(r["Cantidad"]),
                        Costo = Convert.ToDecimal(r["PrecioCosto"])
                    });
                }
            }
            finally { _cargandoItemsOc = false; }
        }

        private void btnImportarPdf_Click(object sender, RoutedEventArgs e)
        {
            if (_compraId > 0) return;
            var dlg = new OpenFileDialog
            {
                Title = "Seleccionar factura PDF",
                Filter = "PDF (*.pdf)|*.pdf",
                CheckFileExists = true
            };
            if (dlg.ShowDialog(this) != true) return;

            var win = new ImportarFacturaCompraWindow(this);
            if (!win.CargarDesdeArchivo(dlg.FileName)) return;
            if (win.ShowDialog() != true || win.LineasConfirmadas == null || win.LineasConfirmadas.Count == 0)
                return;
            AplicarImportacionPdf(win);
        }

        private void AplicarImportacionPdf(ImportarFacturaCompraWindow win)
        {
            if (win.ProveedorID > 0)
            {
                _proveedorId = win.ProveedorID;
                _ignorarTextChanged = true;
                txtBuscarProveedor.Text = win.ProveedorNombre ?? "";
                lblProveedorSel.Text = win.ProveedorNombre ?? "";
                _ignorarTextChanged = false;
                CargarOrdenesCompra();
            }

            string tipo = win.TipoComprobante ?? "Factura A";
            for (int i = 0; i < cmbTipoComprobante.Items.Count; i++)
            {
                if ((cmbTipoComprobante.Items[i] as ComboBoxItem)?.Content?.ToString() == tipo)
                { cmbTipoComprobante.SelectedIndex = i; break; }
            }

            if (_items.Count > 0)
            {
                if (MessageBox.Show("¿Reemplazar el detalle actual por los ítems del PDF?",
                    "Importar PDF", MessageBoxButton.YesNo, MessageBoxImage.Question) != MessageBoxResult.Yes)
                    return;
            }

            _items.Clear();
            foreach (var l in win.LineasConfirmadas)
            {
                _items.Add(new CompraItem
                {
                    ProductoID = l.ProductoID,
                    Codigo = l.CodigoProducto ?? "",
                    Descripcion = l.DescripcionProducto ?? l.DescripcionPdf ?? "",
                    Cantidad = l.Cantidad > 0 ? l.Cantidad : 1,
                    Costo = l.CostoUnitario
                });
            }
            ActualizarTotal();
        }

        private void txtBuscarProducto_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (lstProductos == null || popupProductos == null || txtBuscarProducto == null) return;
            string q = txtBuscarProducto?.Text?.Trim() ?? "";
            if (q.Length < 2) { popupProductos.IsOpen = false; return; }
            var dt = DatabaseService.BuscarProductosMultiples_ParaCompra(q);
            if (dt == null) { popupProductos.IsOpen = false; return; }
            lstProductos.ItemsSource = dt.DefaultView;
            popupProductos.IsOpen = dt.Rows.Count > 0;
        }

        private void lstProductos_MouseUp(object sender, MouseButtonEventArgs e)
        {
            if (lstProductos.SelectedItem is DataRowView row)
            {
                _productoId = Convert.ToInt32(row["ProductoID"]);
                txtBuscarProducto.Text = row["Descripcion"].ToString();
                if (txtCosto.Text == "0" || string.IsNullOrWhiteSpace(txtCosto.Text))
                    txtCosto.Text = Convert.ToDecimal(row["PrecioCosto"]).ToString("N2");
                popupProductos.IsOpen = false;
                txtCantidad.Focus();
            }
        }

        private void btnAgregarItem_Click(object sender, RoutedEventArgs e)
        {
            if (_productoId == 0) { MessageBox.Show("Seleccione un producto.", "Atención", MessageBoxButton.OK, MessageBoxImage.Warning); return; }
            if (!int.TryParse(txtCantidad.Text, out int cant) || cant <= 0) { MessageBox.Show("Cantidad inválida.", "Atención", MessageBoxButton.OK, MessageBoxImage.Warning); return; }
            if (!decimal.TryParse(txtCosto.Text.Replace(",", "."), System.Globalization.NumberStyles.Number, System.Globalization.CultureInfo.InvariantCulture, out decimal costo) || costo < 0)
            { MessageBox.Show("Costo inválido.", "Atención", MessageBoxButton.OK, MessageBoxImage.Warning); return; }

            CompraItem existente = null;
            foreach (var it in _items) if (it.ProductoID == _productoId) { existente = it; break; }
            if (existente != null)
            {
                _items.Remove(existente);
                _items.Add(new CompraItem
                {
                    ProductoID = existente.ProductoID,
                    Codigo = existente.Codigo,
                    Descripcion = existente.Descripcion,
                    Cantidad = existente.Cantidad + cant,
                    Costo = costo
                });
            }
            else
            {
                _items.Add(new CompraItem
                {
                    ProductoID = _productoId,
                    Codigo = "",
                    Descripcion = txtBuscarProducto.Text,
                    Cantidad = cant,
                    Costo = costo
                });
            }

            txtBuscarProducto.Text = "";
            txtCantidad.Text = "1";
            txtCosto.Text = "0";
            _productoId = 0;
        }

        private void btnQuitarItem_Click(object sender, RoutedEventArgs e)
        {
            if (sender is FrameworkElement fe && fe.DataContext is CompraItem item) _items.Remove(item);
        }

        private void ActualizarTotal()
        {
            decimal t = 0;
            foreach (var it in _items) t += it.Subtotal;
            lblTotal.Text = t.ToString("C2");
        }

        private void btnGuardar_Click(object sender, RoutedEventArgs e)
        {
            if (_compraId > 0)
            {
                MessageBox.Show("La edición de facturas de compra no está disponible en esta versión.", "Atención", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }
            if (_proveedorId == 0) { MessageBox.Show("Seleccione un proveedor.", "Atención", MessageBoxButton.OK, MessageBoxImage.Warning); return; }
            if (_items.Count == 0) { MessageBox.Show("Agregue al menos un ítem.", "Atención", MessageBoxButton.OK, MessageBoxImage.Warning); return; }

            string tipo = (cmbTipoComprobante.SelectedItem as ComboBoxItem)?.Content?.ToString() ?? "Factura A";
            decimal total = 0;
            foreach (var it in _items) total += it.Subtotal;
            var items = new List<(int, int, decimal)>();
            foreach (var it in _items) items.Add((it.ProductoID, it.Cantidad, it.Costo));

            int? ordenId = null;
            if (cmbOrdenCompra.SelectedItem is OrdenCompraOpcion oc && oc.OrdenCompraID > 0)
                ordenId = oc.OrdenCompraID;

            bool recepcionar = chkRecepcionarStock.IsChecked == true;

            if (!recepcionar)
            {
                string msg = ordenId.HasValue
                    ? "Se registrará la factura vinculada a la OC sin mover stock. ¿Continuar?"
                    : "Se registrará la factura sin recepcionar mercadería (no se moverá el stock). ¿Continuar?";
                if (MessageBox.Show(msg, "Confirmar", MessageBoxButton.YesNo, MessageBoxImage.Question) != MessageBoxResult.Yes)
                    return;
            }

            bool ok = DatabaseService.GuardarCompra(_proveedorId, tipo, total, items, "Contado", ordenId, recepcionar);
            if (ok)
            {
                _onGuardado?.Invoke();
                DialogResult = true;
                Close();
            }
            else MessageBox.Show("Error al guardar.", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
        }

        private void btnCancelar_Click(object sender, RoutedEventArgs e) { DialogResult = false; Close(); }

        private class CompraItem
        {
            public int ProductoID { get; set; }
            public string Codigo { get; set; }
            public string Descripcion { get; set; }
            public int Cantidad { get; set; }
            public decimal Costo { get; set; }
            public decimal Subtotal => Cantidad * Costo;
            public string CostoFmt => Costo.ToString("C2");
            public string SubtotalFmt => Subtotal.ToString("C2");
        }

        private class OrdenCompraOpcion
        {
            public int OrdenCompraID { get; set; }
            public string Etiqueta { get; set; }
        }
    }
}
