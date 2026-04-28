using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Data;
using System.Windows;
using System.Windows.Input;
using SchettiniGestion;

namespace SchettiniGestion.WPF
{
    public partial class RecepcionCompraModalWindow : Window
    {
        public int ResultID { get; private set; } = 0;
        private readonly int _recepcionId;
        private readonly Action _onGuardado;
        private int _proveedorId = 0;
        private int _productoId = 0;
        private ObservableCollection<RecepcionItem> _items = new ObservableCollection<RecepcionItem>();
        private bool _ignorarTC = false;

        public RecepcionCompraModalWindow() { InitializeComponent(); dgvItems.ItemsSource = _items; Loaded += OnLoaded; }
        public RecepcionCompraModalWindow(Window owner, int recepcionId, Action onGuardado) : this() { Owner = owner; _recepcionId = recepcionId; _onGuardado = onGuardado; }
        public RecepcionCompraModalWindow(object p1, object p2) : this() { }
        public RecepcionCompraModalWindow(object p1, object p2, object p3) : this() { }

        private void OnLoaded(object sender, RoutedEventArgs e)
        {
            if (_recepcionId > 0)
            {
                lblTitulo.Text = "Editar Recepción";
                try
                {
                    var dt = DatabaseService.GetRecepcionesCompra();
                    foreach (DataRow r in dt.Rows)
                    {
                        if (Convert.ToInt32(r["RecepcionID"]) == _recepcionId)
                        {
                            _proveedorId = Convert.ToInt32(r["ProveedorID"]);
                            _ignorarTC = true;
                            txtBuscarProveedor.Text = r["Proveedor"]?.ToString() ?? "";
                            _ignorarTC = false;
                            txtObservaciones.Text = r["Observaciones"]?.ToString() ?? "";
                            for (int i = 0; i < cmbEstado.Items.Count; i++)
                                if ((cmbEstado.Items[i] as System.Windows.Controls.ComboBoxItem)?.Content?.ToString() == r["Estado"]?.ToString())
                                { cmbEstado.SelectedIndex = i; break; }
                            break;
                        }
                    }
                    var det = DatabaseService.GetRecepcionCompraDetalle(_recepcionId);
                    foreach (DataRow r in det.Rows)
                        _items.Add(new RecepcionItem { ProductoID = Convert.ToInt32(r["ProductoID"]), Descripcion = r["Descripcion"]?.ToString() ?? "", CantEsperada = Convert.ToInt32(r["CantidadEsperada"]), CantRecibida = Convert.ToInt32(r["CantidadRecibida"]), Costo = Convert.ToDecimal(r["PrecioCosto"]) });
                }
                catch { }
            }
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
                txtRecibido.Focus();
            }
        }

        private void btnAgregarItem_Click(object sender, RoutedEventArgs e)
        {
            if (_productoId == 0) { MessageBox.Show("Seleccione un producto.", "Atención", MessageBoxButton.OK, MessageBoxImage.Warning); return; }
            if (!int.TryParse(txtRecibido.Text, out int recibido) || recibido < 0) { MessageBox.Show("Cantidad recibida inválida.", "Atención", MessageBoxButton.OK, MessageBoxImage.Warning); return; }
            int.TryParse(txtEsperado.Text, out int esperado);
            decimal.TryParse(txtCosto.Text.Replace(",", "."), System.Globalization.NumberStyles.Number, System.Globalization.CultureInfo.InvariantCulture, out decimal costo);
            _items.Add(new RecepcionItem { ProductoID = _productoId, Descripcion = txtBuscarProducto.Text, CantEsperada = esperado, CantRecibida = recibido, Costo = costo });
            txtBuscarProducto.Text = ""; txtEsperado.Text = "0"; txtRecibido.Text = "0"; txtCosto.Text = "0"; _productoId = 0;
        }

        private void btnQuitarItem_Click(object sender, RoutedEventArgs e)
        {
            if (sender is FrameworkElement fe && fe.DataContext is RecepcionItem item) _items.Remove(item);
        }

        private void btnGuardar_Click(object sender, RoutedEventArgs e)
        {
            if (_proveedorId == 0) { MessageBox.Show("Seleccione un proveedor.", "Atención", MessageBoxButton.OK, MessageBoxImage.Warning); return; }
            if (_items.Count == 0) { MessageBox.Show("Agregue al menos un ítem.", "Atención", MessageBoxButton.OK, MessageBoxImage.Warning); return; }
            string estado = (cmbEstado.SelectedItem as System.Windows.Controls.ComboBoxItem)?.Content?.ToString() ?? "Recibido";
            var items = new List<(int, int, int, decimal)>();
            foreach (var it in _items) items.Add((it.ProductoID, it.CantEsperada, it.CantRecibida, it.Costo));
            int id = DatabaseService.GuardarRecepcionCompra(_recepcionId, _proveedorId, null, estado, txtObservaciones.Text.Trim(), items);
            if (id > 0) { _onGuardado?.Invoke(); DialogResult = true; Close(); }
            else MessageBox.Show("Error al guardar.", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
        }

        private void btnCancelar_Click(object sender, RoutedEventArgs e) { DialogResult = false; Close(); }

        private class RecepcionItem
        {
            public int ProductoID { get; set; }
            public string Descripcion { get; set; }
            public int CantEsperada { get; set; }
            public int CantRecibida { get; set; }
            public decimal Costo { get; set; }
            public string CostoFmt => Costo.ToString("C2");
        }
    }
}
