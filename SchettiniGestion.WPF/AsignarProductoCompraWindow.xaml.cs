using System;
using System.Data;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace SchettiniGestion.WPF
{
    public partial class AsignarProductoCompraWindow : Window
    {
        public int ProductoID { get; private set; }
        public string CodigoProducto { get; private set; } = "";
        public string DescripcionProducto { get; private set; } = "";

        private readonly string _codigoDetectado;
        private readonly string _descripcionDetectada;
        private readonly decimal _costoDetectado;
        private bool _inicializando;

        public AsignarProductoCompraWindow(Window owner, string codigo, string descripcion, decimal costo)
        {
            _inicializando = true;
            InitializeComponent();
            Owner = owner;
            _codigoDetectado = codigo?.Trim() ?? "";
            _descripcionDetectada = descripcion?.Trim() ?? "";
            _costoDetectado = costo;
            lblLineaDetectada.Text =
                $"Detectado en la factura: {(_codigoDetectado.Length > 0 ? _codigoDetectado + " — " : "")}{_descripcionDetectada} · Costo {_costoDetectado:C2}";
            txtBuscar.Text = _descripcionDetectada;
            _inicializando = false;
            Buscar();
            Loaded += (_, __) =>
            {
                UiScaleHelper.FitWindowToWorkArea(this, 820, 560, 640, 440);
                txtBuscar.Focus();
                txtBuscar.SelectAll();
            };
        }

        private void txtBuscar_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (!_inicializando) Buscar();
        }

        private void Buscar()
        {
            string texto = txtBuscar?.Text?.Trim() ?? "";
            if (texto.Length < 2)
            {
                dgvProductos.ItemsSource = null;
                lblEstadoBusqueda.Text = "Escribí al menos dos caracteres para buscar.";
                return;
            }

            var dt = DatabaseService.BuscarProductosMultiples_ParaCompra(texto);
            dgvProductos.ItemsSource = dt?.DefaultView;
            int cantidad = dt?.Rows.Count ?? 0;
            lblEstadoBusqueda.Text = cantidad == 0
                ? "No hay coincidencias. Probá con menos palabras o creá el producto sin salir de esta pantalla."
                : cantidad == 1 ? "Se encontró 1 producto." : $"Se encontraron {cantidad} productos.";
            if (cantidad == 1) dgvProductos.SelectedIndex = 0;
        }

        private void btnAsignar_Click(object sender, RoutedEventArgs e)
        {
            AsignarSeleccionado();
        }

        private void dgvProductos_MouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            AsignarSeleccionado();
        }

        private void AsignarSeleccionado()
        {
            if (!(dgvProductos.SelectedItem is DataRowView row))
            {
                ModernMessageBox.Show(
                    "Seleccioná un producto de la lista. Si todavía no existe, usá “Crear producto nuevo”.",
                    "Falta seleccionar", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            ProductoID = Convert.ToInt32(row["ProductoID"]);
            CodigoProducto = row["Codigo"]?.ToString() ?? "";
            DescripcionProducto = row["Descripcion"]?.ToString() ?? "";
            DialogResult = true;
            Close();
        }

        private void btnCrearProducto_Click(object sender, RoutedEventArgs e)
        {
            var modal = new ProductoModalWindow(0, false, null) { Owner = this };
            modal.PrecargarDesdeFactura(_codigoDetectado, _descripcionDetectada, _costoDetectado);
            if (modal.ShowDialog() != true || modal.ResultID <= 0) return;

            var catalogo = DatabaseService.GetProductosCatalogoMatchCompra();
            var creado = catalogo?.Find(p => p.ProductoID == modal.ResultID);
            if (creado == null)
            {
                txtBuscar.Text = !string.IsNullOrWhiteSpace(_codigoDetectado) ? _codigoDetectado : _descripcionDetectada;
                Buscar();
                ModernMessageBox.Show(
                    "El producto se creó correctamente. Seleccionalo en la lista para asignarlo.",
                    "Producto creado", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            ProductoID = creado.ProductoID;
            CodigoProducto = creado.Codigo ?? "";
            DescripcionProducto = creado.Descripcion ?? "";
            DialogResult = true;
            Close();
        }

        private void btnCancelar_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }
    }
}
