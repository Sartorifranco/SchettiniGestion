using SchettiniGestion;
using System;
using System.Data;
using System.Linq;
using System.Windows;
using System.Windows.Controls;

namespace SchettiniGestion.WPF
{
    public partial class ListasPreciosControl : UserControl
    {
        private int _listaID = 0;
        private DataTable _dtListas;
        private bool _inicializado;

        public ListasPreciosControl()
        {
            InitializeComponent();
        }

        public ListasPreciosControl(object param) : this() { }

        private void UserControl_Loaded(object sender, RoutedEventArgs e)
        {
            if (_inicializado) return;
            _inicializado = true;
            CargarListas();
            ActualizarVisibilidadTipo();
        }

        private void CargarListas()
        {
            try
            {
                _dtListas = DatabaseService.GetListasPrecios();
                EnriquecerListasParaGrilla(_dtListas);
                dgvListas.ItemsSource = _dtListas.DefaultView;
                CargarComboListasRelacionadas();
            }
            catch { }
        }

        private static void EnriquecerListasParaGrilla(DataTable dt)
        {
            if (dt == null) return;
            if (!dt.Columns.Contains("TipoListaEtiqueta"))
                dt.Columns.Add("TipoListaEtiqueta", typeof(string));
            if (!dt.Columns.Contains("TipoRedondeoEtiqueta"))
                dt.Columns.Add("TipoRedondeoEtiqueta", typeof(string));
            if (!dt.Columns.Contains("ListaRelacionadaNombre"))
                dt.Columns.Add("ListaRelacionadaNombre", typeof(string));

            foreach (DataRow row in dt.Rows)
            {
                string tipo = DatabaseService.ObtenerTipoLista(row);
                row["TipoListaEtiqueta"] = DatabaseService.EtiquetaTipoLista(tipo);
                row["TipoRedondeoEtiqueta"] = DatabaseService.EtiquetaTipoRedondeo(DatabaseService.ObtenerTipoRedondeoLista(row));

                string padre = "—";
                if (row.Table.Columns.Contains("ListaRelacionadaID") && row["ListaRelacionadaID"] != DBNull.Value)
                {
                    int pid = Convert.ToInt32(row["ListaRelacionadaID"]);
                    var parent = dt.Select($"ListaID={pid}").FirstOrDefault();
                    if (parent != null) padre = parent["Nombre"]?.ToString() ?? pid.ToString();
                }
                row["ListaRelacionadaNombre"] = padre;
            }
        }

        private void CargarComboListasRelacionadas()
        {
            if (cmbListaRelacionada == null) return;
            var dt = DatabaseService.GetListasPrecios();
            cmbListaRelacionada.ItemsSource = dt?.DefaultView;
            cmbListaRelacionada.SelectedIndex = -1;
        }

        private void Limpiar()
        {
            _listaID = 0;
            txtNombreLista.Text = "";
            numPorcentaje.Value = 0;
            SeleccionarComboPorTag(cmbTipoLista, DatabaseService.TiposListaPrecio.SobreCosto);
            SeleccionarComboPorTag(cmbTipoRedondeo, DatabaseService.TiposRedondeoLista.Sin);
            cmbListaRelacionada.SelectedIndex = -1;
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
        {
            return (cb?.SelectedItem as ComboBoxItem)?.Tag?.ToString() ?? "";
        }

        private void cmbTipoLista_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            ActualizarVisibilidadTipo();
        }

        private void ActualizarVisibilidadTipo()
        {
            string tipo = ObtenerTagCombo(cmbTipoLista);
            bool esRelacionada = tipo == DatabaseService.TiposListaPrecio.ListaRelacionada;
            bool esFijo = tipo == DatabaseService.TiposListaPrecio.PrecioFijo;

            if (pnlPorcentaje != null)
                pnlPorcentaje.Visibility = esFijo ? Visibility.Collapsed : Visibility.Visible;
            if (pnlListaRelacionada != null)
                pnlListaRelacionada.Visibility = esRelacionada ? Visibility.Visible : Visibility.Collapsed;
            if (pnlPrecioFijo != null)
                pnlPrecioFijo.Visibility = esFijo ? Visibility.Visible : Visibility.Collapsed;

            if (lblPorcentaje != null)
                lblPorcentaje.Text = esRelacionada ? "% adicional sobre lista padre:" : "% sobre costo de compra:";
            if (txtAyudaPorcentaje != null)
                txtAyudaPorcentaje.Text = esRelacionada
                    ? "Ej: lista padre +10% y esta lista +40% → precio = (costo×1,10)×1,40."
                    : "Ej: 30 = costo de compra final + 30% de ganancia.";
        }

        private void btnNuevo_Click(object sender, RoutedEventArgs e) { Limpiar(); }

        private void btnGuardar_Click(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrWhiteSpace(txtNombreLista.Text))
            {
                CustomMessageBox.Show("Ingrese un nombre.");
                return;
            }

            string tipo = ObtenerTagCombo(cmbTipoLista);
            if (string.IsNullOrWhiteSpace(tipo))
                tipo = DatabaseService.TiposListaPrecio.SobreCosto;

            int? listaRelId = null;
            if (tipo == DatabaseService.TiposListaPrecio.ListaRelacionada)
            {
                if (cmbListaRelacionada?.SelectedValue == null)
                {
                    CustomMessageBox.Show("Seleccione la lista padre para una lista relacionada.");
                    return;
                }
                listaRelId = Convert.ToInt32(cmbListaRelacionada.SelectedValue);
            }

            string redondeo = ObtenerTagCombo(cmbTipoRedondeo);
            if (string.IsNullOrWhiteSpace(redondeo))
                redondeo = DatabaseService.TiposRedondeoLista.Sin;

            decimal porcentaje = tipo == DatabaseService.TiposListaPrecio.PrecioFijo ? 0 : (numPorcentaje.Value ?? 0);

            if (DatabaseService.GuardarListaPrecio(_listaID, txtNombreLista.Text.Trim(), porcentaje, tipo, listaRelId, redondeo))
            {
                CustomMessageBox.Show("Guardado.");
                CargarListas();
                Limpiar();
            }
        }

        private void dgvListas_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (dgvListas.SelectedItem is DataRowView row)
            {
                _listaID = Convert.ToInt32(row["ListaID"]);
                txtNombreLista.Text = row["Nombre"].ToString();
                numPorcentaje.Value = Convert.ToDecimal(row["Porcentaje"]);
                SeleccionarComboPorTag(cmbTipoLista, DatabaseService.ObtenerTipoLista(row.Row));
                SeleccionarComboPorTag(cmbTipoRedondeo, DatabaseService.ObtenerTipoRedondeoLista(row.Row));

                if (row.Row.Table.Columns.Contains("ListaRelacionadaID") && row["ListaRelacionadaID"] != DBNull.Value)
                    cmbListaRelacionada.SelectedValue = Convert.ToInt32(row["ListaRelacionadaID"]);
                else
                    cmbListaRelacionada.SelectedIndex = -1;

                btnGuardar.Content = "Modificar";
                ActualizarVisibilidadTipo();
            }
        }

        private void btnEliminar_Click(object sender, RoutedEventArgs e)
        {
            DataRowView row = (sender as Button)?.DataContext as DataRowView
                ?? dgvListas.SelectedItem as DataRowView;
            if (row == null) return;

            int id = Convert.ToInt32(row["ListaID"]);
            if (CustomMessageBox.Show("¿Eliminar esta lista?", "Confirmar", MessageBoxButton.YesNo) == MessageBoxResult.Yes)
            {
                if (DatabaseService.EliminarListaPrecio(id))
                {
                    CargarListas();
                    Limpiar();
                }
                else
                    CustomMessageBox.Show("No se pudo eliminar la lista.", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
    }
}
