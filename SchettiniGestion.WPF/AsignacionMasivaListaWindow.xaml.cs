using SchettiniGestion;
using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Windows;
using System.Windows.Controls;

namespace SchettiniGestion.WPF
{
    public partial class AsignacionMasivaListaWindow : Window
    {
        private readonly int _listaIdInicial;
        private List<ItemAsignacion> _items = new List<ItemAsignacion>();

        public AsignacionMasivaListaWindow(int listaId)
        {
            InitializeComponent();
            _listaIdInicial = listaId;
        }

        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            var dt = DatabaseService.GetListasPrecios();
            cmbLista.ItemsSource = dt?.DefaultView;
            if (_listaIdInicial > 0)
                cmbLista.SelectedValue = _listaIdInicial;
            else if (dt != null && dt.Rows.Count > 0)
                cmbLista.SelectedIndex = 0;
            ActualizarFiltroVisible();
        }

        private int ListaIdSeleccionada()
        {
            if (cmbLista?.SelectedValue != null && cmbLista.SelectedValue != DBNull.Value)
            {
                try { return Convert.ToInt32(cmbLista.SelectedValue); }
                catch { }
            }
            if (cmbLista?.SelectedItem is DataRowView rv)
                return Convert.ToInt32(rv["ListaID"]);
            return 0;
        }

        private static string TagCombo(ComboBox cb)
        {
            return (cb?.SelectedItem as ComboBoxItem)?.Tag?.ToString() ?? "todos";
        }

        private void cmbLista_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            dgvProductos.ItemsSource = null;
            _items = new List<ItemAsignacion>();
            ActualizarResumen();
        }

        private void cmbAlcance_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            ActualizarFiltroVisible();
            dgvProductos.ItemsSource = null;
            _items = new List<ItemAsignacion>();
            ActualizarResumen();
        }

        private void ActualizarFiltroVisible()
        {
            if (pnlFiltro == null || cmbFiltro == null || lblFiltro == null) return;
            string alcance = TagCombo(cmbAlcance);
            if (alcance == "marca")
            {
                pnlFiltro.Visibility = Visibility.Visible;
                lblFiltro.Text = "Marca";
                cmbFiltro.ItemsSource = DatabaseService.GetMarcasProductos();
                cmbFiltro.SelectedIndex = cmbFiltro.Items.Count > 0 ? 0 : -1;
            }
            else if (alcance == "categoria")
            {
                pnlFiltro.Visibility = Visibility.Visible;
                lblFiltro.Text = "Categoría";
                cmbFiltro.ItemsSource = DatabaseService.GetCategoriasProductos();
                cmbFiltro.SelectedIndex = cmbFiltro.Items.Count > 0 ? 0 : -1;
            }
            else
            {
                pnlFiltro.Visibility = Visibility.Collapsed;
                cmbFiltro.ItemsSource = null;
            }
        }

        private void btnCargar_Click(object sender, RoutedEventArgs e)
        {
            int listaId = ListaIdSeleccionada();
            if (listaId <= 0)
            {
                CustomMessageBox.Show("Seleccione una lista.");
                return;
            }

            string alcance = TagCombo(cmbAlcance);
            string filtro = "";
            if (alcance == "marca" || alcance == "categoria")
            {
                filtro = cmbFiltro.SelectedItem?.ToString() ?? cmbFiltro.Text ?? "";
                if (string.IsNullOrWhiteSpace(filtro))
                {
                    CustomMessageBox.Show(alcance == "marca"
                        ? "No hay marcas cargadas en los productos."
                        : "No hay categorías cargadas en los productos.");
                    return;
                }
            }

            var raw = DatabaseService.GetProductosParaAsignacionLista(listaId, alcance, filtro);
            _items = raw.Select(p => new ItemAsignacion
            {
                Incluir = p.Incluir,
                ProductoID = p.ProductoID,
                Codigo = p.Codigo,
                Descripcion = p.Descripcion,
                Marca = p.Marca,
                Categoria = p.Categoria,
                YaAsignado = p.YaAsignado
            }).ToList();
            dgvProductos.ItemsSource = _items;
            ActualizarResumen();
        }

        private void btnMarcarTodos_Click(object sender, RoutedEventArgs e)
        {
            foreach (var i in _items) i.Incluir = true;
            dgvProductos.Items.Refresh();
            ActualizarResumen();
        }

        private void btnDesmarcarTodos_Click(object sender, RoutedEventArgs e)
        {
            foreach (var i in _items) i.Incluir = false;
            dgvProductos.Items.Refresh();
            ActualizarResumen();
        }

        private void ActualizarResumen()
        {
            if (lblResumen == null) return;
            if (_items == null || _items.Count == 0)
            {
                lblResumen.Text = "Cargá un conjunto de productos para empezar.";
                return;
            }
            int marcados = _items.Count(x => x.Incluir);
            lblResumen.Text = $"{marcados} de {_items.Count} productos se quedarán con esta lista.";
        }

        private void btnAplicar_Click(object sender, RoutedEventArgs e)
        {
            int listaId = ListaIdSeleccionada();
            if (listaId <= 0)
            {
                CustomMessageBox.Show("Seleccione una lista.");
                return;
            }
            if (_items == null || _items.Count == 0)
            {
                CustomMessageBox.Show("Cargá primero los productos.");
                return;
            }

            dgvProductos.CommitEdit();
            dgvProductos.CommitEdit(DataGridEditingUnit.Row, true);

            var incluir = _items.Where(x => x.Incluir).Select(x => x.ProductoID).ToList();
            var excluir = _items.Where(x => !x.Incluir).Select(x => x.ProductoID).ToList();
            int n = DatabaseService.SincronizarAsignacionLista(listaId, incluir, excluir);
            if (n < 0)
            {
                CustomMessageBox.Show("No se pudo guardar la asignación.", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            CustomMessageBox.Show(
                $"Lista aplicada.\n\nIncluidos: {incluir.Count}\nExcepciones (sin esta lista): {excluir.Count}");
            DialogResult = true;
            Close();
        }

        private void btnCancelar_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }

        private class ItemAsignacion
        {
            public bool Incluir { get; set; }
            public int ProductoID { get; set; }
            public string Codigo { get; set; }
            public string Descripcion { get; set; }
            public string Marca { get; set; }
            public string Categoria { get; set; }
            public bool YaAsignado { get; set; }
            public string YaAsignadoTexto => YaAsignado ? "Sí" : "No";
        }
    }
}
