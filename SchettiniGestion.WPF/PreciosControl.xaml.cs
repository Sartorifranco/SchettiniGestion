using SchettiniGestion; // ¡Importante!
using System;
using System.Data;
using System.Threading.Tasks; // ¡Importante para el Foco!
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using Xceed.Wpf.Toolkit;
using System.Globalization; // Para el separador decimal

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
        }

        // --- 1. LÓGICA DE BÚSQUEDA DE PRODUCTO ---
        #region LogicaBusqueda
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
        #endregion

        // --- 2. LÓGICA DE CÁLCULO Y GUARDADO ---
        #region LogicaCalculoGuardado
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
        #endregion

        // ===== INICIO DE CÓDIGO DE TECLADO 100% FUNCIONAL =====

        private void OnNumericKeyPressed(object sender, string key)
        {
            // 1. Si no hay campo seleccionado, avisar o salir.
            if (_activeNumericControl == null) return;

            // 2. Recuperar el foco visual para que el usuario vea el cursor
            _activeNumericControl.Focus();

            // 3. Buscar la caja de texto interna usando nuestra "llave maestra"
            TextBox activeTextBox = null;

            // Si es el buscador (que es un TextBox normal)
            if (_activeNumericControl is TextBox tb)
            {
                activeTextBox = tb;
            }
            // Si es un control numérico (DecimalUpDown/IntegerUpDown)
            else
            {
                activeTextBox = VisualTreeHelpers.FindChild<TextBox>(_activeNumericControl);
            }

            if (activeTextBox == null) return;

            // 4. Escribir en la caja de texto
            int caretIndex = activeTextBox.CaretIndex;

            // Si hay texto seleccionado (ej: al hacer foco), lo borramos primero
            if (activeTextBox.SelectionLength > 0)
            {
                activeTextBox.Text = activeTextBox.Text.Remove(activeTextBox.SelectionStart, activeTextBox.SelectionLength);
                caretIndex = activeTextBox.SelectionStart;
            }

            if (key == "Back" || key == "⬅")
            {
                if (caretIndex > 0)
                {
                    activeTextBox.Text = activeTextBox.Text.Remove(caretIndex - 1, 1);
                    activeTextBox.CaretIndex = caretIndex - 1;
                }
            }
            else if (key == "Enter")
            {
                _activeNumericControl.MoveFocus(new TraversalRequest(FocusNavigationDirection.Next));
            }
            else if (key == ".")
            {
                string separator = CultureInfo.CurrentCulture.NumberFormat.NumberDecimalSeparator;
                if (!activeTextBox.Text.Contains(separator))
                {
                    activeTextBox.Text = activeTextBox.Text.Insert(caretIndex, separator);
                    activeTextBox.CaretIndex = caretIndex + separator.Length;
                }
            }
            else // Números normales
            {
                activeTextBox.Text = activeTextBox.Text.Insert(caretIndex, key);
                activeTextBox.CaretIndex = caretIndex + key.Length;
            }
        }

        // Este evento le dice al sistema: "El usuario hizo clic en este campo numérico"
        private void NumericInput_GotFocus(object sender, RoutedEventArgs e)
        {
            _activeNumericControl = sender as Control;
        }

        // Este evento le dice al sistema: "El usuario hizo clic en el buscador, ya no escribas números"
        private void txtBuscarProducto_GotFocus(object sender, RoutedEventArgs e)
        {
            // Para que el teclado funcione en el buscador también:
            _activeNumericControl = sender as Control;
            // Si NO quieres que funcione en el buscador, cambia la línea anterior por:
            // _activeNumericControl = null;
        }

        // ===== FIN DE CÓDIGO MODIFICADO =====
    }
}