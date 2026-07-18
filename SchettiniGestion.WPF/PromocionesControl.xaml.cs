using SchettiniGestion;
using System;
using System.Data;
using System.Windows;
using System.Windows.Controls;

namespace SchettiniGestion.WPF
{
    public partial class PromocionesControl : UserControl
    {
        private int _promoId;
        private DataTable _dtProductosBusqueda;

        public PromocionesControl()
        {
            InitializeComponent();
        }

        public PromocionesControl(object param) : this() { }

        private void UserControl_Loaded(object sender, RoutedEventArgs e)
        {
            CargarCategorias();
            CargarPromos();
            ActualizarVisibilidadTipo();
        }

        private void CargarCategorias()
        {
            try
            {
                cmbCategoria.ItemsSource = DatabaseService.GetCategoriasCatalogo();
            }
            catch { }
        }

        private void CargarPromos()
        {
            try
            {
                var dt = DatabaseService.GetPromociones();
                EnriquecerDetalle(dt);
                dgvPromos.ItemsSource = dt.DefaultView;
            }
            catch (Exception ex)
            {
                CustomMessageBox.Show("No se pudieron cargar las promos: " + ex.Message);
            }
        }

        private static void EnriquecerDetalle(DataTable dt)
        {
            if (dt == null) return;
            if (!dt.Columns.Contains("AlcanceDetalle"))
                dt.Columns.Add("AlcanceDetalle", typeof(string));

            foreach (DataRow row in dt.Rows)
            {
                string tipo = row["Tipo"]?.ToString() ?? "";
                if (tipo == DatabaseService.TiposPromo.PctProducto)
                    row["AlcanceDetalle"] = row["ProductoNombre"]?.ToString() ?? ("#" + row["ProductoID"]);
                else if (tipo == DatabaseService.TiposPromo.PctCategoria)
                    row["AlcanceDetalle"] = row["Categoria"]?.ToString() ?? "";
                else
                    row["AlcanceDetalle"] = "Todos los productos";
            }
        }

        private void Limpiar()
        {
            _promoId = 0;
            txtNombre.Text = "";
            numPorcentaje.Value = 10;
            SeleccionarComboPorTag(cmbTipo, DatabaseService.TiposPromo.PctProducto);
            cmbProducto.ItemsSource = null;
            cmbProducto.SelectedIndex = -1;
            txtBuscarProducto.Text = "";
            lblProductoElegido.Text = "";
            cmbCategoria.SelectedIndex = -1;
            dpDesde.SelectedDate = null;
            dpHasta.SelectedDate = null;
            chkActiva.IsChecked = true;
            txtObs.Text = "";
            btnGuardar.Content = "Guardar";
            ActualizarVisibilidadTipo();
        }

        private static void SeleccionarComboPorTag(ComboBox cb, string tag)
        {
            if (cb == null) return;
            for (int i = 0; i < cb.Items.Count; i++)
            {
                if ((cb.Items[i] as ComboBoxItem)?.Tag?.ToString() == tag)
                {
                    cb.SelectedIndex = i;
                    return;
                }
            }
            cb.SelectedIndex = 0;
        }

        private static string ObtenerTagCombo(ComboBox cb)
            => (cb?.SelectedItem as ComboBoxItem)?.Tag?.ToString() ?? "";

        private void cmbTipo_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            ActualizarVisibilidadTipo();
        }

        private void ActualizarVisibilidadTipo()
        {
            if (pnlProducto == null || pnlCategoria == null) return;
            string tipo = ObtenerTagCombo(cmbTipo);
            pnlProducto.Visibility = tipo == DatabaseService.TiposPromo.PctProducto ? Visibility.Visible : Visibility.Collapsed;
            pnlCategoria.Visibility = tipo == DatabaseService.TiposPromo.PctCategoria ? Visibility.Visible : Visibility.Collapsed;
        }

        private void btnBuscarProducto_Click(object sender, RoutedEventArgs e)
        {
            string q = (txtBuscarProducto.Text ?? "").Trim();
            if (q.Length < 1)
            {
                CustomMessageBox.Show("Escribí código o nombre del producto.");
                return;
            }
            try
            {
                _dtProductosBusqueda = DatabaseService.GetProductosListado(q);
                cmbProducto.ItemsSource = _dtProductosBusqueda?.DefaultView;
                if (_dtProductosBusqueda == null || _dtProductosBusqueda.Rows.Count == 0)
                {
                    lblProductoElegido.Text = "No encontré productos con eso.";
                    return;
                }
                cmbProducto.SelectedIndex = 0;
                lblProductoElegido.Text = $"{_dtProductosBusqueda.Rows.Count} resultado(s). Elegí en la lista.";
            }
            catch (Exception ex)
            {
                CustomMessageBox.Show("Error buscando: " + ex.Message);
            }
        }

        private void btnNuevo_Click(object sender, RoutedEventArgs e) => Limpiar();

        private void btnGuardar_Click(object sender, RoutedEventArgs e)
        {
            string tipo = ObtenerTagCombo(cmbTipo);
            int? productoId = null;
            string categoria = null;

            if (tipo == DatabaseService.TiposPromo.PctProducto)
            {
                if (cmbProducto.SelectedValue == null)
                {
                    CustomMessageBox.Show("Buscá y elegí el producto.");
                    return;
                }
                productoId = Convert.ToInt32(cmbProducto.SelectedValue);
            }
            else if (tipo == DatabaseService.TiposPromo.PctCategoria)
            {
                categoria = (cmbCategoria.SelectedValue ?? cmbCategoria.Text)?.ToString();
                if (string.IsNullOrWhiteSpace(categoria))
                {
                    CustomMessageBox.Show("Elegí la categoría.");
                    return;
                }
            }

            decimal pct = numPorcentaje.Value ?? 0;
            bool ok = DatabaseService.GuardarPromocion(
                _promoId,
                txtNombre.Text,
                tipo,
                productoId,
                categoria,
                pct,
                dpDesde.SelectedDate,
                dpHasta.SelectedDate,
                chkActiva.IsChecked == true,
                txtObs.Text);

            if (ok)
            {
                CustomMessageBox.Show("Promo guardada. En la caja se aplica sola al cargar el producto.");
                CargarPromos();
                Limpiar();
            }
        }

        private void dgvPromos_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (!(dgvPromos.SelectedItem is DataRowView row)) return;

            _promoId = Convert.ToInt32(row["PromoID"]);
            txtNombre.Text = row["Nombre"]?.ToString() ?? "";
            numPorcentaje.Value = Convert.ToDecimal(row["Porcentaje"]);
            SeleccionarComboPorTag(cmbTipo, row["Tipo"]?.ToString());
            ActualizarVisibilidadTipo();

            dpDesde.SelectedDate = row["FechaDesde"] == DBNull.Value ? (DateTime?)null : Convert.ToDateTime(row["FechaDesde"]);
            dpHasta.SelectedDate = row["FechaHasta"] == DBNull.Value ? (DateTime?)null : Convert.ToDateTime(row["FechaHasta"]);
            chkActiva.IsChecked = Convert.ToBoolean(row["Activo"]);
            txtObs.Text = row["Observaciones"] == DBNull.Value ? "" : row["Observaciones"]?.ToString();

            string tipo = row["Tipo"]?.ToString() ?? "";
            if (tipo == DatabaseService.TiposPromo.PctProducto && row["ProductoID"] != DBNull.Value)
            {
                int pid = Convert.ToInt32(row["ProductoID"]);
                string desc = row["ProductoNombre"]?.ToString() ?? ("Producto #" + pid);
                var dt = new DataTable();
                dt.Columns.Add("ProductoID", typeof(int));
                dt.Columns.Add("Descripcion", typeof(string));
                dt.Rows.Add(pid, desc);
                cmbProducto.ItemsSource = dt.DefaultView;
                cmbProducto.SelectedValue = pid;
                lblProductoElegido.Text = desc;
            }
            else if (tipo == DatabaseService.TiposPromo.PctCategoria)
            {
                cmbCategoria.SelectedValue = row["Categoria"]?.ToString();
            }

            btnGuardar.Content = "Modificar";
        }

        private void btnEliminar_Click(object sender, RoutedEventArgs e)
        {
            var row = (sender as Button)?.DataContext as DataRowView
                      ?? dgvPromos.SelectedItem as DataRowView;
            if (row == null) return;

            int id = Convert.ToInt32(row["PromoID"]);
            if (CustomMessageBox.Show("¿Borrar esta promoción?", "Confirmar", MessageBoxButton.YesNo) != MessageBoxResult.Yes)
                return;

            if (DatabaseService.EliminarPromocion(id))
            {
                CargarPromos();
                Limpiar();
            }
        }
    }
}
