using SchettiniGestion; // ¡Importante!
using System;
using System.Data;
using System.Threading.Tasks; // ¡Importante para el Foco!
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using Xceed.Wpf.Toolkit;
// NO incluimos 'using System.Windows.Forms;' al inicio para evitar conflictos con 'MessageBox'

namespace SchettiniGestion.WPF
{
    public partial class PreciosControl : UserControl
    {
        private DataRow _productoSeleccionado;
        private bool _ignorarPerdidaFoco = false;
        private Control _activeNumericControl = null; // Rastrea qué campo numérico está activo

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
        }

        // --- 1. LÓGICA DE BÚSQUEDA DE PRODUCTO ---

        private void BuscarProducto(string query)
        {
            if (string.IsNullOrWhiteSpace(query))
            {
                popupProducto.IsOpen = false;
                return;
            }

            try
            {
                DataRow productoExacto = DatabaseService.BuscarProducto(query);
                if (productoExacto != null)
                {
                    SeleccionarProducto(productoExacto);
                }
                else
                {
                    DataTable dt = DatabaseService.BuscarProductosMultiples_ParaCompra(query);
                    lstSugerenciasProducto.ItemsSource = dt.DefaultView;

                    if (dt.Rows.Count > 0)
                    {
                        lstSugerenciasProducto.SelectedIndex = 0;
                        popupProducto.IsOpen = true;
                        _ignorarPerdidaFoco = true;
                        lstSugerenciasProducto.Focus();
                    }
                    else
                    {
                        popupProducto.IsOpen = false;
                        LimpiarCampos();
                        lblProductoSeleccionado.Text = "Producto: (No encontrado)";
                    }
                }
            }
            catch (Exception ex)
            {
                // Usamos System.Windows.MessageBox explícitamente
                System.Windows.MessageBox.Show($"Error al buscar productos: {ex.Message}");
            }
        }

        private void SeleccionarProducto(DataRow drv)
        {
            _productoSeleccionado = drv;

            lblProductoSeleccionado.Text = _productoSeleccionado["Descripcion"].ToString();
            numPrecioCosto.Value = Convert.ToDecimal(_productoSeleccionado["PrecioCosto"]);
            numPrecioVenta.Value = Convert.ToDecimal(_productoSeleccionado["PrecioVenta"]);
            numPorcentaje.Value = 0;

            panelPrecios.IsEnabled = true;
            popupProducto.IsOpen = false;
            _ignorarPerdidaFoco = false;
            numPrecioVenta.Focus();
        }

        private void txtBuscarProducto_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter)
            {
                BuscarProducto(txtBuscarProducto.Text);
            }
            else if (e.Key == Key.Down)
            {
                if (popupProducto.IsOpen && lstSugerenciasProducto.Items.Count > 0)
                {
                    lstSugerenciasProducto.Focus();
                }
            }
            else if (e.Key == Key.Escape)
            {
                popupProducto.IsOpen = false;
            }
        }

        private async void txtBuscarProducto_LostFocus(object sender, RoutedEventArgs e)
        {
            await Task.Delay(200);

            if (!_ignorarPerdidaFoco && !popupProducto.IsOpen && _productoSeleccionado == null)
            {
                BuscarProducto(txtBuscarProducto.Text);
            }
            _ignorarPerdidaFoco = false;
        }

        private void lstSugerenciasProducto_MouseUp(object sender, MouseButtonEventArgs e)
        {
            if (lstSugerenciasProducto.SelectedItem is DataRowView drv)
            {
                SeleccionarProducto(drv.Row);
            }
        }

        private void lstSugerencias_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter && lstSugerenciasProducto.SelectedItem is DataRowView drv)
            {
                SeleccionarProducto(drv.Row);
            }
        }

        // --- 2. LÓGICA DE CÁLCULO Y GUARDADO ---

        private void btnAplicarPorcentaje_Click(object sender, RoutedEventArgs e)
        {
            decimal costo = numPrecioCosto.Value ?? 0;
            decimal porcentaje = numPorcentaje.Value ?? 0;

            if (costo <= 0)
            {
                System.Windows.MessageBox.Show("El precio de costo debe ser mayor a cero para calcular la ganancia.", "Error", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            decimal nuevoPrecioVenta = costo * (1 + (porcentaje / 100));
            numPrecioVenta.Value = Math.Round(nuevoPrecioVenta, 2);
        }

        private void btnGuardarPrecios_Click(object sender, RoutedEventArgs e)
        {
            if (_productoSeleccionado == null)
            {
                System.Windows.MessageBox.Show("No hay ningún producto seleccionado.", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            int productoID = Convert.ToInt32(_productoSeleccionado["ProductoID"]);
            decimal nuevoCosto = numPrecioCosto.Value ?? 0;
            decimal nuevoVenta = numPrecioVenta.Value ?? 0;

            if (nuevoVenta <= 0)
            {
                System.Windows.MessageBox.Show("El precio de venta no puede ser cero o negativo.", "Error", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            if (nuevoCosto > nuevoVenta)
            {
                if (System.Windows.MessageBox.Show("El precio de venta es menor que el precio de costo. ¿Está seguro de que desea continuar?", "Advertencia", MessageBoxButton.YesNo, MessageBoxImage.Warning) == MessageBoxResult.No)
                {
                    return;
                }
            }

            try
            {
                bool exito = DatabaseService.ActualizarPreciosProducto(productoID, nuevoCosto, nuevoVenta);
                if (exito)
                {
                    System.Windows.MessageBox.Show("Precios actualizados correctamente.", "Éxito", MessageBoxButton.OK, MessageBoxImage.Information);
                    LimpiarCampos();
                }
            }
            catch (Exception ex)
            {
                System.Windows.MessageBox.Show($"Error al guardar los precios: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        // --- 3. LÓGICA DEL TECLADO NUMÉRICO ---

        private void numericKeyboard_KeyPressed(object sender, KeyEventArgs e)
        {
            if (_activeNumericControl == null) return;

            // Asegura que el control tenga el foco antes de enviar la tecla
            _activeNumericControl.Focus();

            // Usamos la ruta completa para SendKeys para evitar ambigüedades
            // y asegurarnos de usar la versión de Windows Forms que funciona globalmente
            if (e.Key == Key.Back)
            {
                System.Windows.Forms.SendKeys.SendWait("{BACKSPACE}");
            }
            else if (e.Key == Key.Enter)
            {
                System.Windows.Forms.SendKeys.SendWait("{ENTER}");
            }
            else if (e.Key == Key.Decimal)
            {
                // Envía el separador decimal correcto según la configuración regional
                System.Windows.Forms.SendKeys.SendWait(System.Globalization.CultureInfo.CurrentCulture.NumberFormat.NumberDecimalSeparator);
            }
            else if (e.Key >= Key.D0 && e.Key <= Key.D9)
            {
                // Envía el número (eliminando la 'D' del nombre de la tecla, ej: D7 -> 7)
                System.Windows.Forms.SendKeys.SendWait(e.Key.ToString().Replace("D", ""));
            }
        }

        private void NumericInput_GotFocus(object sender, RoutedEventArgs e)
        {
            _activeNumericControl = sender as Control;
        }

        private async void NumericInput_LostFocus(object sender, RoutedEventArgs e)
        {
            await Task.Delay(150);
            // Si el foco se fue al teclado, no lo perdemos. Si se fue a otro lado, sí.
            if (!numericKeyboard.IsKeyboardFocusWithin)
            {
                _activeNumericControl = null;
            }
        }
    }
}