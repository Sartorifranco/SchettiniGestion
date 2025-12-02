using SchettiniGestion;
using System;
using System.Data;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using Xceed.Wpf.Toolkit;
// Alias para evitar conflictos
using WinForms = System.Windows.Forms;

namespace SchettiniGestion.WPF
{
    public partial class PreciosControl : UserControl
    {
        private DataRow _productoSeleccionado;
        private bool _ignorarPerdidaFoco = false;
        private Control _activeNumericControl = null;

        public PreciosControl()
        {
            InitializeComponent();
        }

        private void PreciosControl_Loaded(object sender, RoutedEventArgs e)
        {
            LimpiarCampos();
        }

        private void LimpiarCampos()
        {
            _productoSeleccionado = null;
            txtBuscarProducto.Text = "";
            lblProductoSeleccionado.Text = "Seleccione un producto...";
            numPrecioCosto.Value = 0;
            numPrecioVenta.Value = 0;
            numPorcentaje.Value = 0;
            panelPrecios.IsEnabled = false;
            txtBuscarProducto.Focus();
            _activeNumericControl = txtBuscarProducto;
        }

        // --- BÚSQUEDA ---
        private void txtBuscarProducto_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter) BuscarProducto(txtBuscarProducto.Text);
            else if (e.Key == Key.Down && popupProducto.IsOpen) { lstSugerenciasProducto.SelectedIndex = 0; lstSugerenciasProducto.Focus(); }
            else if (e.Key == Key.Escape) popupProducto.IsOpen = false;
        }

        private void BuscarProducto(string query)
        {
            if (string.IsNullOrWhiteSpace(query)) { popupProducto.IsOpen = false; return; }
            try
            {
                DataRow prod = DatabaseService.BuscarProducto(query);
                if (prod != null) SeleccionarProducto(prod);
                else
                {
                    DataTable dt = DatabaseService.BuscarProductosMultiples_ParaCompra(query);
                    lstSugerenciasProducto.ItemsSource = dt.DefaultView;
                    if (dt.Rows.Count > 0) { popupProducto.IsOpen = true; _ignorarPerdidaFoco = true; lstSugerenciasProducto.Focus(); }
                    else { popupProducto.IsOpen = false; }
                }
            }
            catch (Exception ex) { System.Windows.MessageBox.Show(ex.Message); }
        }

        private void SeleccionarProducto(DataRow row)
        {
            _productoSeleccionado = row;
            lblProductoSeleccionado.Text = row["Descripcion"].ToString();
            numPrecioCosto.Value = Convert.ToDecimal(row["PrecioCosto"]);
            numPrecioVenta.Value = Convert.ToDecimal(row["PrecioVenta"]);
            panelPrecios.IsEnabled = true;
            popupProducto.IsOpen = false;
            _ignorarPerdidaFoco = false;
            numPrecioVenta.Focus();
            _activeNumericControl = numPrecioVenta;
        }

        private void lstSugerencias_MouseUp(object sender, MouseButtonEventArgs e) { if (lstSugerenciasProducto.SelectedItem is DataRowView r) SeleccionarProducto(r.Row); }
        private void lstSugerencias_KeyDown(object sender, KeyEventArgs e) { if (e.Key == Key.Enter && lstSugerenciasProducto.SelectedItem is DataRowView r) SeleccionarProducto(r.Row); }
        private async void txtBuscar_LostFocus(object sender, RoutedEventArgs e) { await Task.Delay(150); if (!lstSugerenciasProducto.IsFocused) popupProducto.IsOpen = false; }

        // --- GUARDADO ---
        private void btnGuardarPrecios_Click(object sender, RoutedEventArgs e)
        {
            if (_productoSeleccionado == null) return;
            try
            {
                int id = Convert.ToInt32(_productoSeleccionado["ProductoID"]);
                bool ok = DatabaseService.ActualizarPreciosProducto(id, numPrecioCosto.Value ?? 0, numPrecioVenta.Value ?? 0);
                if (ok) { System.Windows.MessageBox.Show("Precio actualizado."); LimpiarCampos(); }
            }
            catch (Exception ex) { System.Windows.MessageBox.Show(ex.Message); }
        }

        private void btnAplicarPorcentaje_Click(object sender, RoutedEventArgs e)
        {
            if (numPrecioCosto.Value > 0)
                numPrecioVenta.Value = numPrecioCosto.Value * (1 + (numPorcentaje.Value / 100));
        }

        // --- TECLADO NUMÉRICO ---
        private void NumericInput_GotFocus(object sender, RoutedEventArgs e)
        {
            _activeNumericControl = sender as Control;
        }

        private void numericKeyboard_KeyPressed(object sender, string key)
        {
            if (_activeNumericControl != null)
            {
                _activeNumericControl.Focus();

                if (key == "BACKSPACE") WinForms.SendKeys.SendWait("{BACKSPACE}");
                else if (key == "ENTER") WinForms.SendKeys.SendWait("{ENTER}");
                else WinForms.SendKeys.SendWait(key);
            }
        }
    }
}