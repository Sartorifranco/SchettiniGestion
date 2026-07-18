using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Data;
using System.Linq;
using System.Windows;
using System.Windows.Input;
using SchettiniGestion;

namespace SchettiniGestion.WPF
{
    public partial class OrdenCompraModalWindow : Window
    {
        public int ResultID { get; private set; } = 0;
        private readonly int _ordenId;
        private readonly Action _onGuardado;
        private int _proveedorId = 0;
        private int _productoId = 0;
        private ObservableCollection<OrdenItem> _items = new ObservableCollection<OrdenItem>();
        private bool _ignorarTC = false;

        public OrdenCompraModalWindow() { InitializeComponent(); _items.CollectionChanged += (s, e) => ActualizarTotal(); dgvItems.ItemsSource = _items; Loaded += OnLoaded; }
        public OrdenCompraModalWindow(Window owner, int ordenId, Action onGuardado) : this() { Owner = owner; _ordenId = ordenId; _onGuardado = onGuardado; }
        public OrdenCompraModalWindow(object p1, object p2) : this() { }
        public OrdenCompraModalWindow(object p1, object p2, object p3) : this() { }

        private void OnLoaded(object sender, RoutedEventArgs e)
        {
            if (_ordenId == 0)
            {
                cmbEstado.IsEnabled = false;
                SeleccionarEstado("Pendiente");
            }
            else
            {
                lblTitulo.Text = "Editar Orden de Compra";
                cmbEstado.IsEnabled = true;
                try
                {
                    var dt = DatabaseService.GetOrdenesCompra();
                    foreach (DataRow r in dt.Rows)
                    {
                        if (Convert.ToInt32(r["OrdenCompraID"]) == _ordenId)
                        {
                            _proveedorId = Convert.ToInt32(r["ProveedorID"]);
                            _ignorarTC = true;
                            txtBuscarProveedor.Text = r["Proveedor"]?.ToString() ?? "";
                            _ignorarTC = false;
                            if (r["FechaEntrega"] != DBNull.Value) dtpFechaEntrega.SelectedDate = Convert.ToDateTime(r["FechaEntrega"]);
                            txtObservaciones.Text = r["Observaciones"]?.ToString() ?? "";
                            SeleccionarEstado(r["Estado"]?.ToString() ?? "Pendiente");
                            break;
                        }
                    }
                    var det = DatabaseService.GetOrdenCompraDetalleFull(_ordenId);
                    foreach (DataRow r in det.Rows)
                        _items.Add(new OrdenItem { ProductoID = Convert.ToInt32(r["ProductoID"]), Descripcion = r["Descripcion"]?.ToString() ?? "", Cantidad = Convert.ToInt32(r["Cantidad"]), Costo = Convert.ToDecimal(r["PrecioCosto"]) });
                }
                catch { }
            }
            ActualizarTotal();
        }

        private void SeleccionarEstado(string estado)
        {
            if (cmbEstado == null) return;
            foreach (var item in cmbEstado.Items)
            {
                if (item is System.Windows.Controls.ComboBoxItem cbi
                    && string.Equals(cbi.Content?.ToString(), estado, StringComparison.OrdinalIgnoreCase))
                {
                    cmbEstado.SelectedItem = item;
                    return;
                }
            }
            cmbEstado.SelectedIndex = 0;
        }

        private string ObtenerEstadoSeleccionado()
        {
            if (cmbEstado?.SelectedItem is System.Windows.Controls.ComboBoxItem cbi)
                return cbi.Content?.ToString() ?? "Pendiente";
            return "Pendiente";
        }

        private void txtBuscarProveedor_TextChanged(object sender, System.Windows.Controls.TextChangedEventArgs e)
        {
            if (_ignorarTC) return;
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
                _ignorarTC = true; txtBuscarProveedor.Text = row["RazonSocial"].ToString(); _ignorarTC = false;
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
                if (txtCosto.Text == "0") txtCosto.Text = Convert.ToDecimal(row["PrecioCosto"]).ToString("N2");
                popupProductos.IsOpen = false;
                txtCantidad.Focus();
            }
        }

        private void btnAgregarItem_Click(object sender, RoutedEventArgs e)
        {
            if (_productoId == 0) { MessageBox.Show("Seleccione un producto.", "Atención", MessageBoxButton.OK, MessageBoxImage.Warning); return; }
            if (!int.TryParse(txtCantidad.Text, out int cant) || cant <= 0) { MessageBox.Show("Cantidad inválida.", "Atención", MessageBoxButton.OK, MessageBoxImage.Warning); return; }
            decimal.TryParse(txtCosto.Text.Replace(",", "."), System.Globalization.NumberStyles.Number, System.Globalization.CultureInfo.InvariantCulture, out decimal costo);
            _items.Add(new OrdenItem { ProductoID = _productoId, Descripcion = txtBuscarProducto.Text, Cantidad = cant, Costo = costo });
            txtBuscarProducto.Text = ""; txtCantidad.Text = "1"; txtCosto.Text = "0"; _productoId = 0;
        }

        private void btnQuitarItem_Click(object sender, RoutedEventArgs e)
        {
            if (sender is FrameworkElement fe && fe.DataContext is OrdenItem item) _items.Remove(item);
        }

        private void ActualizarTotal()
        {
            decimal t = _items.Sum(i => i.Subtotal);
            lblTotal.Text = t.ToString("C2");
        }

        private void btnGuardar_Click(object sender, RoutedEventArgs e)
        {
            if (_proveedorId == 0) { MessageBox.Show("Seleccione un proveedor.", "Atención", MessageBoxButton.OK, MessageBoxImage.Warning); return; }
            if (_items.Count == 0) { MessageBox.Show("Agregue al menos un ítem.", "Atención", MessageBoxButton.OK, MessageBoxImage.Warning); return; }
            var items = new List<(int, int, decimal)>();
            foreach (var it in _items) items.Add((it.ProductoID, it.Cantidad, it.Costo));
            int id = DatabaseService.GuardarOrdenCompra(_ordenId, _proveedorId, dtpFechaEntrega.SelectedDate, txtObservaciones.Text.Trim(), items, ObtenerEstadoSeleccionado());
            if (id > 0) { _onGuardado?.Invoke(); DialogResult = true; Close(); }
            else MessageBox.Show("Error al guardar.", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
        }

        private void btnCancelar_Click(object sender, RoutedEventArgs e) { DialogResult = false; Close(); }

        private class OrdenItem
        {
            public int ProductoID { get; set; }
            public string Descripcion { get; set; }
            public int Cantidad { get; set; }
            public decimal Costo { get; set; }
            public decimal Subtotal => Cantidad * Costo;
            public string CostoFmt => Costo.ToString("C2");
            public string SubtotalFmt => Subtotal.ToString("C2");
        }
    }
}
