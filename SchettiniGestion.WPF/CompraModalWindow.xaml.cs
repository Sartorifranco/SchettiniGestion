using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Data;
using System.Windows;
using System.Windows.Input;
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

        public CompraModalWindow() { InitializeComponent(); _items.CollectionChanged += (s, e) => ActualizarTotal(); dgvItems.ItemsSource = _items; Loaded += OnLoaded; }
        public CompraModalWindow(Window owner, Action onGuardado) : this() { Owner = owner; _onGuardado = onGuardado; }
        public CompraModalWindow(Window owner, Action onGuardado, int compraId) : this(owner, onGuardado) { _compraId = compraId; }

        private void OnLoaded(object sender, RoutedEventArgs e)
        {
            if (_compraId > 0)
            {
                lblTitulo.Text = "Editar Factura de Compra";
                CargarCompraExistente();
            }
            txtBuscarProveedor.Text = "";
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
                        for (int i = 0; i < cmbTipoComprobante.Items.Count; i++)
                            if ((cmbTipoComprobante.Items[i] as System.Windows.Controls.ComboBoxItem)?.Content?.ToString() == r["TipoComprobante"]?.ToString())
                            { cmbTipoComprobante.SelectedIndex = i; break; }
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

        private void txtBuscarProveedor_TextChanged(object sender, System.Windows.Controls.TextChangedEventArgs e)
        {
            if (_ignorarTextChanged) return;
            string q = txtBuscarProveedor.Text.Trim();
            if (q.Length < 2) { popupProveedores.IsOpen = false; return; }
            var dt = DatabaseService.BuscarProveedoresMultiples(q);
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
            }
        }

        private void txtBuscarProducto_TextChanged(object sender, System.Windows.Controls.TextChangedEventArgs e)
        {
            string q = txtBuscarProducto.Text.Trim();
            if (q.Length < 2) { popupProductos.IsOpen = false; return; }
            var dt = DatabaseService.BuscarProductosMultiples_ParaCompra(q);
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
            if (_productoId == 0) { ModernMessageBox.Show("Seleccione un producto.", "Atención", MessageBoxButton.OK, MessageBoxImage.Warning); return; }
            if (!int.TryParse(txtCantidad.Text, out int cant) || cant <= 0) { ModernMessageBox.Show("Cantidad inválida.", "Atención", MessageBoxButton.OK, MessageBoxImage.Warning); return; }
            if (!decimal.TryParse(txtCosto.Text.Replace(",", "."), System.Globalization.NumberStyles.Number, System.Globalization.CultureInfo.InvariantCulture, out decimal costo) || costo < 0)
            { ModernMessageBox.Show("Costo inválido.", "Atención", MessageBoxButton.OK, MessageBoxImage.Warning); return; }

            var existente = null as CompraItem;
            foreach (var it in _items) if (it.ProductoID == _productoId) { existente = it; break; }
            if (existente != null) { _items.Remove(existente); _items.Add(new CompraItem { ProductoID = existente.ProductoID, Codigo = existente.Codigo, Descripcion = existente.Descripcion, Cantidad = existente.Cantidad + cant, Costo = costo }); }
            else _items.Add(new CompraItem { ProductoID = _productoId, Codigo = "", Descripcion = txtBuscarProducto.Text, Cantidad = cant, Costo = costo });

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
            if (_proveedorId == 0) { ModernMessageBox.Show("Seleccione un proveedor.", "Atención", MessageBoxButton.OK, MessageBoxImage.Warning); return; }
            if (_items.Count == 0) { ModernMessageBox.Show("Agregue al menos un ítem.", "Atención", MessageBoxButton.OK, MessageBoxImage.Warning); return; }
            string tipo = (cmbTipoComprobante.SelectedItem as System.Windows.Controls.ComboBoxItem)?.Content?.ToString() ?? "Factura A";
            decimal total = 0; foreach (var it in _items) total += it.Subtotal;
            var items = new List<(int, int, decimal)>();
            foreach (var it in _items) items.Add((it.ProductoID, it.Cantidad, it.Costo));
            bool ok = DatabaseService.GuardarCompra(_proveedorId, tipo, total, items, "Contado");
            if (ok) { _onGuardado?.Invoke(); DialogResult = true; Close(); }
            else ModernMessageBox.Show("Error al guardar.", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
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
    }
}
