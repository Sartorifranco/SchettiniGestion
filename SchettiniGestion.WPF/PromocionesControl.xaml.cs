using SchettiniGestion;
using System;
using System.Data;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;

namespace SchettiniGestion.WPF
{
    public partial class PromocionesControl : UserControl
    {
        private int _promoId;
        private DataTable _dtProductosBusqueda;
        private DataTable _dtProductosCombo;

        public PromocionesControl()
        {
            InitializeComponent();
        }

        public PromocionesControl(object param) : this() { }

        private void UserControl_Loaded(object sender, RoutedEventArgs e)
        {
            CargarCategorias();
            PrepararTablaCombo();
            CargarPromos();
            ActualizarVisibilidadTipo();
            ActualizarVisibilidadModalidad();
        }

        private void PrepararTablaCombo()
        {
            _dtProductosCombo = new DataTable();
            _dtProductosCombo.Columns.Add("ProductoID", typeof(int));
            _dtProductosCombo.Columns.Add("Codigo", typeof(string));
            _dtProductosCombo.Columns.Add("CodigoBarra", typeof(string));
            _dtProductosCombo.Columns.Add("Descripcion", typeof(string));
            _dtProductosCombo.Columns.Add("Display", typeof(string));
            if (lstProductosCombo != null)
                lstProductosCombo.ItemsSource = _dtProductosCombo.DefaultView;
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
            if (!dt.Columns.Contains("ValorDetalle"))
                dt.Columns.Add("ValorDetalle", typeof(string));

            foreach (DataRow row in dt.Rows)
            {
                string tipo = row["Tipo"]?.ToString() ?? "";
                if (tipo == DatabaseService.TiposPromo.PctProducto)
                    row["AlcanceDetalle"] = row["ProductoNombre"]?.ToString() ?? ("#" + row["ProductoID"]);
                else if (tipo == DatabaseService.TiposPromo.PctCategoria)
                    row["AlcanceDetalle"] = row["Categoria"]?.ToString() ?? "";
                else if (tipo == DatabaseService.TiposPromo.ComboProductos)
                    row["AlcanceDetalle"] = row.Table.Columns.Contains("ComboProductos") ? row["ComboProductos"]?.ToString() ?? "" : "Combo";
                else
                    row["AlcanceDetalle"] = "Todos los productos";

                string modalidad = row.Table.Columns.Contains("Modalidad") ? row["Modalidad"]?.ToString() : DatabaseService.ModalidadesPromo.Porcentaje;
                decimal pct = ValorDecimal(row, "Porcentaje");
                decimal mf = ValorDecimal(row, "MontoFijo");
                decimal pc = ValorDecimal(row, "PrecioCombo");
                int cmin = ValorInt(row, "CantidadMinima");
                int cbon = ValorInt(row, "CantidadBonificada");
                row["ValorDetalle"] = DatabaseService.DescribirValorPromo(modalidad, pct, mf, pc, cmin, cbon);
            }
        }

        private static decimal ValorDecimal(DataRow row, string col)
            => row.Table.Columns.Contains(col) && row[col] != DBNull.Value ? Convert.ToDecimal(row[col]) : 0m;

        private static int ValorInt(DataRow row, string col)
            => row.Table.Columns.Contains(col) && row[col] != DBNull.Value ? Convert.ToInt32(row[col]) : 0;

        private void Limpiar()
        {
            _promoId = 0;
            txtNombre.Text = "";
            numPorcentaje.Value = 10;
            numMontoFijo.Value = 0;
            numPrecioCombo.Value = 0;
            numCantidadMinima.Value = 0;
            numCantidadBonificada.Value = 0;
            SeleccionarComboPorTag(cmbTipo, DatabaseService.TiposPromo.PctProducto);
            SeleccionarComboPorTag(cmbModalidad, DatabaseService.ModalidadesPromo.Porcentaje);
            cmbProducto.ItemsSource = null;
            cmbProducto.SelectedIndex = -1;
            txtBuscarProducto.Text = "";
            lblProductoElegido.Text = "";
            PrepararTablaCombo();
            cmbCategoria.SelectedIndex = -1;
            dpDesde.SelectedDate = null;
            dpHasta.SelectedDate = null;
            chkActiva.IsChecked = true;
            txtObs.Text = "";
            btnGuardar.Content = "Guardar";
            ActualizarVisibilidadTipo();
            ActualizarVisibilidadModalidad();
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

        private void cmbModalidad_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            ActualizarVisibilidadModalidad();
        }

        private void ActualizarVisibilidadTipo()
        {
            if (pnlProducto == null || pnlCategoria == null) return;
            string tipo = ObtenerTagCombo(cmbTipo);
            bool esProducto = tipo == DatabaseService.TiposPromo.PctProducto;
            bool esCombo = tipo == DatabaseService.TiposPromo.ComboProductos;
            pnlProducto.Visibility = (esProducto || esCombo) ? Visibility.Visible : Visibility.Collapsed;
            pnlCategoria.Visibility = tipo == DatabaseService.TiposPromo.PctCategoria ? Visibility.Visible : Visibility.Collapsed;
            if (lblProductoTitulo != null)
                lblProductoTitulo.Text = esCombo ? "Productos del combo:" : "Producto:";
            if (btnAgregarProductoCombo != null)
                btnAgregarProductoCombo.Visibility = esCombo ? Visibility.Visible : Visibility.Collapsed;
            if (lstProductosCombo != null)
                lstProductosCombo.Visibility = esCombo ? Visibility.Visible : Visibility.Collapsed;
            if (btnQuitarProductoCombo != null)
                btnQuitarProductoCombo.Visibility = esCombo ? Visibility.Visible : Visibility.Collapsed;
        }

        private void ActualizarVisibilidadModalidad()
        {
            if (pnlPorcentaje == null) return;
            string modalidad = ObtenerTagCombo(cmbModalidad);
            bool usaPct = modalidad == DatabaseService.ModalidadesPromo.Porcentaje
                          || modalidad == DatabaseService.ModalidadesPromo.EscalaCantidad;
            pnlPorcentaje.Visibility = usaPct ? Visibility.Visible : Visibility.Collapsed;
            pnlMontoFijo.Visibility = modalidad == DatabaseService.ModalidadesPromo.MontoFijo ? Visibility.Visible : Visibility.Collapsed;
            pnlPrecioCombo.Visibility = modalidad == DatabaseService.ModalidadesPromo.PrecioFinal ? Visibility.Visible : Visibility.Collapsed;
            pnlCantidadMinima.Visibility = (modalidad == DatabaseService.ModalidadesPromo.Bonificar
                                            || modalidad == DatabaseService.ModalidadesPromo.EscalaCantidad)
                                            ? Visibility.Visible : Visibility.Collapsed;
            pnlCantidadBonificada.Visibility = modalidad == DatabaseService.ModalidadesPromo.Bonificar ? Visibility.Visible : Visibility.Collapsed;
            if (modalidad == DatabaseService.ModalidadesPromo.DosPorUno)
            {
                numCantidadMinima.Value = 2;
                numCantidadBonificada.Value = 1;
            }
            else if (modalidad == DatabaseService.ModalidadesPromo.TresPorDos)
            {
                numCantidadMinima.Value = 3;
                numCantidadBonificada.Value = 1;
            }
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
                _dtProductosBusqueda = DatabaseService.BuscarProductosParaPromocion(q);
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

        private void btnAgregarProductoCombo_Click(object sender, RoutedEventArgs e)
        {
            if (!(cmbProducto.SelectedItem is DataRowView row))
            {
                CustomMessageBox.Show("Buscá y elegí un producto para agregar al combo.");
                return;
            }
            AgregarProductoACombo(row.Row);
        }

        private void btnQuitarProductoCombo_Click(object sender, RoutedEventArgs e)
        {
            if (!(lstProductosCombo.SelectedItem is DataRowView row)) return;
            _dtProductosCombo.Rows.Remove(row.Row);
            lblProductoElegido.Text = $"{_dtProductosCombo.Rows.Count} producto(s) en el combo.";
        }

        private void AgregarProductoACombo(DataRow producto)
        {
            if (producto == null) return;
            int pid = Convert.ToInt32(producto["ProductoID"]);
            foreach (DataRow r in _dtProductosCombo.Rows)
                if (Convert.ToInt32(r["ProductoID"]) == pid)
                {
                    lblProductoElegido.Text = "Ese producto ya está en el combo.";
                    return;
                }

            string codigo = producto.Table.Columns.Contains("Codigo") ? producto["Codigo"]?.ToString() ?? "" : "";
            string barra = producto.Table.Columns.Contains("CodigoBarra") ? producto["CodigoBarra"]?.ToString() ?? "" : "";
            string desc = producto["Descripcion"]?.ToString() ?? "";
            string display = producto.Table.Columns.Contains("Display")
                ? producto["Display"]?.ToString() ?? desc
                : $"{codigo}{(string.IsNullOrWhiteSpace(barra) ? "" : " / " + barra)} - {desc}";
            _dtProductosCombo.Rows.Add(pid, codigo, barra, desc, display);
            lblProductoElegido.Text = $"{_dtProductosCombo.Rows.Count} producto(s) en el combo.";
        }

        private void btnNuevo_Click(object sender, RoutedEventArgs e) => Limpiar();

        private void btnGuardar_Click(object sender, RoutedEventArgs e)
        {
            string tipo = ObtenerTagCombo(cmbTipo);
            string modalidad = ObtenerTagCombo(cmbModalidad);
            int? productoId = null;
            string categoria = null;
            var productosCombo = new List<int>();

            if (tipo == DatabaseService.TiposPromo.PctProducto)
            {
                if (cmbProducto.SelectedValue == null)
                {
                    CustomMessageBox.Show("Buscá y elegí el producto.");
                    return;
                }
                productoId = Convert.ToInt32(cmbProducto.SelectedValue);
            }
            else if (tipo == DatabaseService.TiposPromo.ComboProductos)
            {
                foreach (DataRow r in _dtProductosCombo.Rows)
                    productosCombo.Add(Convert.ToInt32(r["ProductoID"]));
                if (productosCombo.Count < 2)
                {
                    CustomMessageBox.Show("Agregá al menos dos productos al combo.");
                    return;
                }
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
            decimal montoFijo = numMontoFijo.Value ?? 0;
            decimal precioCombo = numPrecioCombo.Value ?? 0;
            int cantidadMinima = numCantidadMinima.Value ?? 0;
            int cantidadBonificada = numCantidadBonificada.Value ?? 0;
            bool ok = DatabaseService.GuardarPromocion(
                _promoId,
                txtNombre.Text,
                tipo,
                modalidad,
                productoId,
                categoria,
                pct,
                montoFijo,
                precioCombo,
                cantidadMinima,
                cantidadBonificada,
                productosCombo,
                dpDesde.SelectedDate,
                dpHasta.SelectedDate,
                chkActiva.IsChecked == true,
                txtObs.Text);

            if (ok)
            {
                CustomMessageBox.Show("Promo guardada.\n\nEn Ventas vas a ver el descuento con el nombre de la promo en el carrito (ej: 🎯 Promo verano · -50%).");
                CargarPromos();
                Limpiar();
            }
        }

        private void dgvPromos_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (!(dgvPromos.SelectedItem is DataRowView row)) return;

            _promoId = Convert.ToInt32(row["PromoID"]);
            txtNombre.Text = row["Nombre"]?.ToString() ?? "";
            numPorcentaje.Value = ValorDecimal(row.Row, "Porcentaje");
            numMontoFijo.Value = ValorDecimal(row.Row, "MontoFijo");
            numPrecioCombo.Value = ValorDecimal(row.Row, "PrecioCombo");
            numCantidadMinima.Value = ValorInt(row.Row, "CantidadMinima");
            numCantidadBonificada.Value = ValorInt(row.Row, "CantidadBonificada");
            SeleccionarComboPorTag(cmbTipo, row["Tipo"]?.ToString());
            SeleccionarComboPorTag(cmbModalidad, row.Row.Table.Columns.Contains("Modalidad") ? row["Modalidad"]?.ToString() : DatabaseService.ModalidadesPromo.Porcentaje);
            ActualizarVisibilidadTipo();
            ActualizarVisibilidadModalidad();

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
                dt.Columns.Add("Display", typeof(string));
                dt.Rows.Add(pid, desc, desc);
                cmbProducto.ItemsSource = dt.DefaultView;
                cmbProducto.SelectedValue = pid;
                lblProductoElegido.Text = desc;
            }
            else if (tipo == DatabaseService.TiposPromo.ComboProductos)
            {
                var dt = DatabaseService.GetPromoProductos(_promoId);
                PrepararTablaCombo();
                foreach (DataRow pr in dt.Rows)
                    AgregarProductoACombo(pr);
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
