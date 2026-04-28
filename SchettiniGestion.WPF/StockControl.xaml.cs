using System;
using System.Collections.Generic;
using System.Data;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using SchettiniGestion;

namespace SchettiniGestion.WPF
{
    internal class ReservaStockDialog : Window
    {
        public int ProductoID { get; private set; }
        public int Cantidad { get; private set; }
        public string Motivo { get; private set; }

        private TextBox _txtProducto, _txtCantidad;
        private DataRow _productoRow;

        public ReservaStockDialog()
        {
            Title = "Nueva Reserva de Stock";
            Width = 420; Height = 310;
            WindowStartupLocation = WindowStartupLocation.CenterOwner;
            Background = new SolidColorBrush(Color.FromRgb(30, 30, 46));
            ResizeMode = ResizeMode.NoResize;

            var grid = new Grid { Margin = new Thickness(20) };
            grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

            var title = new TextBlock { Text = "Nueva Reserva de Stock", FontSize = 16, FontWeight = FontWeights.Bold, Foreground = new SolidColorBrush(Color.FromRgb(137, 180, 250)), Margin = new Thickness(0, 0, 0, 16) };
            Grid.SetRow(title, 0); grid.Children.Add(title);

            var lbl1 = new TextBlock { Text = "Producto (buscar):", Foreground = Brushes.LightGray, Margin = new Thickness(0, 0, 0, 3) };
            Grid.SetRow(lbl1, 1); grid.Children.Add(lbl1);

            _txtProducto = new TextBox { Background = new SolidColorBrush(Color.FromRgb(49, 50, 68)), Foreground = Brushes.White, BorderBrush = new SolidColorBrush(Color.FromRgb(69, 71, 90)), Padding = new Thickness(7, 6, 7, 6), Margin = new Thickness(0, 0, 0, 10) };
            _txtProducto.TextChanged += TxtProducto_TextChanged;
            Grid.SetRow(_txtProducto, 2); grid.Children.Add(_txtProducto);

            var lbl2 = new TextBlock { Text = "Cantidad:", Foreground = Brushes.LightGray, Margin = new Thickness(0, 0, 0, 3) };
            Grid.SetRow(lbl2, 3); grid.Children.Add(lbl2);
            _txtCantidad = new TextBox { Background = new SolidColorBrush(Color.FromRgb(49, 50, 68)), Foreground = Brushes.White, BorderBrush = new SolidColorBrush(Color.FromRgb(69, 71, 90)), Padding = new Thickness(7, 6, 7, 6), Margin = new Thickness(0, 0, 0, 10), Text = "1" };
            Grid.SetRow(_txtCantidad, 4); grid.Children.Add(_txtCantidad);

            var btns = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right, Margin = new Thickness(0, 8, 0, 0) };
            Grid.SetRow(btns, 5); grid.Children.Add(btns);

            var btnOk = new Button { Content = "Reservar", Background = new SolidColorBrush(Color.FromRgb(137, 180, 250)), Foreground = new SolidColorBrush(Color.FromRgb(30, 30, 46)), FontWeight = FontWeights.Bold, Padding = new Thickness(14, 8, 14, 8), BorderThickness = new Thickness(0), Cursor = Cursors.Hand, Margin = new Thickness(8, 0, 0, 0) };
            btnOk.Click += BtnOk_Click;
            var btnCancel = new Button { Content = "Cancelar", Background = new SolidColorBrush(Color.FromRgb(69, 71, 90)), Foreground = Brushes.White, Padding = new Thickness(12, 8, 12, 8), BorderThickness = new Thickness(0), Cursor = Cursors.Hand };
            btnCancel.Click += (s, e) => { DialogResult = false; Close(); };
            btns.Children.Add(btnCancel);
            btns.Children.Add(btnOk);

            grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            Content = grid;
        }

        private void TxtProducto_TextChanged(object sender, TextChangedEventArgs e)
        {
            string q = _txtProducto.Text.Trim();
            if (q.Length < 2) { _productoRow = null; return; }
            var dt = DatabaseService.BuscarProductosMultiples_ParaCompra(q);
            if (dt.Rows.Count > 0) _productoRow = dt.Rows[0];
        }

        private void BtnOk_Click(object sender, RoutedEventArgs e)
        {
            if (_productoRow == null) { MessageBox.Show("Ingrese un producto válido.", "Atención", MessageBoxButton.OK, MessageBoxImage.Warning); return; }
            if (!int.TryParse(_txtCantidad.Text, out int cant) || cant <= 0) { MessageBox.Show("Cantidad inválida.", "Atención", MessageBoxButton.OK, MessageBoxImage.Warning); return; }
            ProductoID = Convert.ToInt32(_productoRow["ProductoID"]);
            Cantidad = cant;
            Motivo = "Reserva manual";
            DialogResult = true;
            Close();
        }
    }

    public partial class StockControl : UserControl
    {
        private DataRow _productoSeleccionado = null;
        private bool _ignorarPerdidaFoco = false;

        public StockControl()
        {
            InitializeComponent();
        }

        private void StockControl_Loaded(object sender, RoutedEventArgs e)
        {
            cmbTipoMovimiento.Items.Clear();
            cmbTipoMovimiento.Items.Add("Ingreso por Compra");
            cmbTipoMovimiento.Items.Add("Ajuste Manual (Suma)");
            cmbTipoMovimiento.Items.Add("Ajuste por Rotura (Resta)");
            cmbTipoMovimiento.Items.Add("Ajuste Manual (Resta)");
            cmbTipoMovimiento.SelectedIndex = 0;

            LimpiarCampos();
        }

        private void LimpiarCampos()
        {
            _productoSeleccionado = null;
            lblProductoSeleccionado.Text = "Seleccione un producto...";
            txtBuscarProducto.Clear();
            numCantidad.Value = 0;
            cmbTipoMovimiento.SelectedIndex = 0;
            popupProducto.IsOpen = false;
            txtBuscarProducto.Focus();
        }

        private void btnGuardarAjuste_Click(object sender, RoutedEventArgs e)
        {
            // 1. Validaciones
            if (_productoSeleccionado == null)
            {
                CustomMessageBox.Show("Debe seleccionar un producto.", "Error", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            int cantidad = numCantidad.Value ?? 0;
            if (cantidad == 0)
            {
                CustomMessageBox.Show("La cantidad no puede ser cero.", "Error", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            if (cmbTipoMovimiento.SelectedItem == null)
            {
                CustomMessageBox.Show("Debe seleccionar un tipo de movimiento.", "Error", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            string tipoMovimiento = cmbTipoMovimiento.SelectedItem.ToString();

            // Corrección de signo
            if ((tipoMovimiento.Contains("(Resta)") || tipoMovimiento.Contains("Rotura")) && cantidad > 0)
            {
                cantidad = cantidad * -1;
            }
            // Asegurar positivo para ingresos/sumas si el usuario puso negativo por error
            if ((tipoMovimiento.Contains("(Suma)") || tipoMovimiento.Contains("Ingreso")) && cantidad < 0)
            {
                cantidad = cantidad * -1;
            }

            // 2. Confirmación
            int productoID = Convert.ToInt32(_productoSeleccionado["ProductoID"]);
            string nombreProducto = _productoSeleccionado["Descripcion"].ToString();

            MessageBoxResult confirmacion = CustomMessageBox.Show(
                $"¿Está seguro que desea registrar el siguiente movimiento?\n\n" +
                $"Producto: {nombreProducto}\n" +
                $"Cantidad: {cantidad}\n" +
                $"Motivo: {tipoMovimiento}",
                "Confirmar Movimiento",
                MessageBoxButton.YesNo,
                MessageBoxImage.Question);

            if (confirmacion == MessageBoxResult.No) return;

            // 3. Llamar a la Base de Datos
            bool exito = DatabaseService.AjustarStock(productoID, cantidad, tipoMovimiento);

            if (exito)
            {
                CustomMessageBox.Show("¡Movimiento de stock guardado exitosamente!", "Éxito", MessageBoxButton.OK, MessageBoxImage.Information);
                LimpiarCampos();
            }
            else
            {
                CustomMessageBox.Show("No se pudo guardar el movimiento.", "Error Grave", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        // --- Lógica de Búsqueda ---
        #region LogicaBusquedaPredictiva

        private void txtBuscarProducto_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (txtBuscarProducto.Text.Length < 2) { popupProducto.IsOpen = false; _productoSeleccionado = null; lblProductoSeleccionado.Text = "Seleccione un producto..."; return; }

            // Usamos _ParaCompra porque muestra todos los productos (con o sin stock)
            DataTable productos = DatabaseService.BuscarProductosMultiples_ParaCompra(txtBuscarProducto.Text);

            if (productos.Rows.Count > 0) { lstSugerenciasProducto.ItemsSource = productos.DefaultView; popupProducto.IsOpen = true; }
            else { popupProducto.IsOpen = false; _productoSeleccionado = null; }
        }

        private void lstSugerenciasProducto_MouseUp(object sender, MouseButtonEventArgs e)
        {
            if (lstSugerenciasProducto.SelectedItem is DataRowView filaSeleccionada) { SeleccionarProducto(filaSeleccionada); }
        }

        private void SeleccionarProducto(DataRowView filaSeleccionada)
        {
            _productoSeleccionado = filaSeleccionada.Row;
            _ignorarPerdidaFoco = true;
            txtBuscarProducto.Text = filaSeleccionada["Descripcion"].ToString();
            lblProductoSeleccionado.Text = $"ID: {filaSeleccionada["ProductoID"]} | Stock Actual: {filaSeleccionada["StockActual"]}";
            _ignorarPerdidaFoco = false;
            popupProducto.IsOpen = false;
            numCantidad.Focus();
        }

        private void txtBuscar_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            if (popupProducto.IsOpen)
            {
                if (e.Key == Key.Down) { lstSugerenciasProducto.SelectedIndex = 0; lstSugerenciasProducto.Focus(); e.Handled = true; }
                else if (e.Key == Key.Escape) { popupProducto.IsOpen = false; e.Handled = true; }
            }
        }

        private void lstSugerencias_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter)
            {
                if (lstSugerenciasProducto.SelectedItem is DataRowView producto) { SeleccionarProducto(producto); }
                e.Handled = true;
            }
        }

        private async void txtBuscar_LostFocus(object sender, RoutedEventArgs e)
        {
            if (_ignorarPerdidaFoco) return;
            await System.Threading.Tasks.Task.Delay(150);
            if (!lstSugerenciasProducto.IsFocused) { popupProducto.IsOpen = false; }
        }

        #endregion

        // ========== PESTAÑA: STOCK GENERAL ==========
        private void txtFiltroGeneral_TextChanged(object sender, System.Windows.Controls.TextChangedEventArgs e)
        {
            CargarStockGeneral();
        }

        private void btnBuscarGeneral_Click(object sender, RoutedEventArgs e)
        {
            CargarStockGeneral();
        }

        private void CargarStockGeneral()
        {
            try
            {
                var dt = DatabaseService.GetStockGeneral(txtFiltroGeneral?.Text?.Trim() ?? "");
                dgvStockGeneral.ItemsSource = dt.DefaultView;
            }
            catch (Exception ex)
            {
                MessageBox.Show("Error al cargar stock: " + ex.Message);
            }
        }

        // ========== PESTAÑA: MOVIMIENTOS ==========
        private void btnBuscarMovimientos_Click(object sender, RoutedEventArgs e)
        {
            CargarMovimientos();
        }

        private void CargarMovimientos()
        {
            try
            {
                var tipos = new System.Collections.Generic.List<string>();
                if (chkMovCompra.IsChecked == true) tipos.Add("Compra");
                if (chkMovRecepcion.IsChecked == true) tipos.Add("Recepción compra");
                if (chkMovAjuste.IsChecked == true) { tipos.Add("Ajuste Manual (Suma)"); tipos.Add("Ajuste Manual (Resta)"); tipos.Add("Ajuste por Rotura (Resta)"); }
                if (chkMovIngreso.IsChecked == true) tipos.Add("Ingreso por Compra");
                if (chkMovVenta.IsChecked == true) tipos.Add("Venta");

                var dt = DatabaseService.GetMovimientosStockFiltrado(
                    dpMovDesde.SelectedDate,
                    dpMovHasta.SelectedDate,
                    txtFiltroMov?.Text?.Trim() ?? "",
                    tipos.Count > 0 ? tipos : null
                );
                dgvMovimientos.ItemsSource = dt.DefaultView;
            }
            catch (Exception ex)
            {
                MessageBox.Show("Error al cargar movimientos: " + ex.Message);
            }
        }

        // ========== PESTAÑA: RESERVAS ==========
        private void btnBuscarReservas_Click(object sender, RoutedEventArgs e)
        {
            CargarReservas();
        }

        private void CargarReservas()
        {
            try
            {
                string estado = (cmbResEstado.SelectedItem as ComboBoxItem)?.Content?.ToString() ?? "";
                var dt = DatabaseService.GetReservasStock(
                    dpResDesde.SelectedDate,
                    dpResHasta.SelectedDate,
                    txtResFiltro?.Text?.Trim() ?? "",
                    estado
                );
                dgvReservas.ItemsSource = dt.DefaultView;
            }
            catch (Exception ex)
            {
                MessageBox.Show("Error al cargar reservas: " + ex.Message);
            }
        }

        private void btnNuevaReserva_Click(object sender, RoutedEventArgs e)
        {
            // Dialogo simple para nueva reserva
            var dialog = new ReservaStockDialog();
            dialog.Owner = Window.GetWindow(this);
            if (dialog.ShowDialog() == true && dialog.ProductoID > 0 && dialog.Cantidad > 0)
            {
                bool ok = DatabaseService.GuardarReservaStock(dialog.ProductoID, dialog.Cantidad, dialog.Motivo);
                if (ok) { MessageBox.Show("Reserva creada correctamente.", "Éxito", MessageBoxButton.OK, MessageBoxImage.Information); CargarReservas(); }
                else MessageBox.Show("Error al crear reserva.", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void btnAnularReserva_Click(object sender, RoutedEventArgs e)
        {
            if (dgvReservas.SelectedItem is System.Data.DataRowView row)
            {
                int id = Convert.ToInt32(row["ReservaID"]);
                if (MessageBox.Show("¿Anular esta reserva?", "Confirmar", MessageBoxButton.YesNo, MessageBoxImage.Warning) == MessageBoxResult.Yes)
                {
                    if (DatabaseService.AnularReservaStock(id)) CargarReservas();
                    else MessageBox.Show("Error al anular.", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
            else MessageBox.Show("Seleccione una reserva.", "Atención", MessageBoxButton.OK, MessageBoxImage.Warning);
        }

        // ========== PESTAÑA: AJUSTES ==========
        private void btnBuscarAjustes_Click(object sender, RoutedEventArgs e)
        {
            CargarAjustes();
        }

        private void CargarAjustes()
        {
            try
            {
                var ajusteTipos = new System.Collections.Generic.List<string>
                    { "Ajuste Manual (Suma)", "Ajuste Manual (Resta)", "Ajuste por Rotura (Resta)", "Ingreso por Compra" };
                var dt = DatabaseService.GetMovimientosStockFiltrado(
                    dpAjusteDesde.SelectedDate,
                    dpAjusteHasta.SelectedDate,
                    txtAjusteFiltro?.Text?.Trim() ?? "",
                    ajusteTipos
                );
                dgvAjustes.ItemsSource = dt.DefaultView;

                // Calcular sumarios
                int ingresos = 0, egresos = 0;
                foreach (System.Data.DataRow r in dt.Rows)
                {
                    int cant = Convert.ToInt32(r["Cantidad"]);
                    if (cant > 0) ingresos += cant;
                    else egresos += Math.Abs(cant);
                }
                lblAjusteIngresos.Text = ingresos.ToString();
                lblAjusteEgresos.Text = egresos.ToString();
                lblAjusteValorSinIva.Text = "—";
                lblAjusteValorConIva.Text = "—";
            }
            catch (Exception ex)
            {
                MessageBox.Show("Error al cargar ajustes: " + ex.Message);
            }
        }

        private void btnNuevoAjuste_Click(object sender, RoutedEventArgs e)
        {
            // Redirigir a la pestaña de registrar movimiento
            tabMain.SelectedIndex = 1;
        }

        private void btnAnularAjuste_Click(object sender, RoutedEventArgs e)
        {
            MessageBox.Show("Los ajustes de stock no se pueden anular directamente.\nRealice un ajuste inverso desde la pestaña 'Registrar movimiento'.",
                "Información", MessageBoxButton.OK, MessageBoxImage.Information);
        }

        // ========== PESTAÑA: DEPÓSITOS ==========
        private void btnBuscarDepositos_Click(object sender, RoutedEventArgs e)
        {
            CargarDepositos();
        }

        private void CargarDepositos()
        {
            try
            {
                string filtro = txtFiltroDep?.Text?.Trim() ?? "";
                string tipoStock = (cmbTipoStockDep.SelectedItem as ComboBoxItem)?.Content?.ToString() ?? "Todos";
                var dt = DatabaseService.GetStockGeneral(filtro);

                // Filtrar según tipo de stock seleccionado
                if (tipoStock == "Sin stock")
                    dt.DefaultView.RowFilter = "StockReal = 0";
                else if (tipoStock == "Stock distinto a cero")
                    dt.DefaultView.RowFilter = "StockReal > 0";
                else if (tipoStock == "Bajo stock")
                    dt.DefaultView.RowFilter = "StockReal < 5";
                else
                    dt.DefaultView.RowFilter = "";

                // Rename columns for deposit grid compatibility
                if (!dt.Columns.Contains("Stock") && dt.Columns.Contains("StockReal"))
                    dt.Columns["StockReal"].ColumnName = "Stock";

                dgvDepositos.ItemsSource = dt.DefaultView;
            }
            catch (Exception ex)
            {
                MessageBox.Show("Error al cargar depósitos: " + ex.Message);
            }
        }
    }
}