using SchettiniGestion;
using System;
using System.Data;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

namespace SchettiniGestion.WPF
{
    public partial class CuentasCorrientesControl : UserControl
    {
        private DataRow _entidadSeleccionada;
        private bool _modoClientes = true; // True = Clientes, False = Proveedores

        public CuentasCorrientesControl()
        {
            InitializeComponent();
        }

        private void UserControl_Loaded(object sender, RoutedEventArgs e)
        {
            // Aseguramos que al cargar la pantalla se limpie todo correctamente
            LimpiarTodo();
        }

        private void rbTipo_Checked(object sender, RoutedEventArgs e)
        {
            // ===== CORRECCIÓN DEL ERROR NULLREFERENCE =====
            // Este evento se dispara muy rápido al iniciar la app, antes de que
            // existan los cuadros de texto. Si txtBuscar es null, salimos.
            if (txtBuscar == null) return;
            // ==============================================

            _modoClientes = (rbClientes.IsChecked == true);
            LimpiarTodo();

            // Cambiar texto del botón según contexto (verificamos que el botón exista)
            if (btnAsentarPago != null)
                btnAsentarPago.Content = _modoClientes ? "💵 Asentar Cobro" : "💵 Asentar Pago";
        }

        private void LimpiarTodo()
        {
            // ===== CORRECCIÓN DE SEGURIDAD EXTRA =====
            if (txtBuscar == null || panelSaldo == null || dgvMovimientos == null) return;
            // =========================================

            _entidadSeleccionada = null;
            txtBuscar.Text = "";
            panelSaldo.Visibility = Visibility.Collapsed;
            dgvMovimientos.ItemsSource = null;
            txtBuscar.Focus();
        }

        // --- BÚSQUEDA ---
        private void txtBuscar_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (txtBuscar.Text.Length < 2) { popupBusqueda.IsOpen = false; return; }

            try
            {
                DataTable dt;
                if (_modoClientes)
                    dt = DatabaseService.BuscarClientesMultiples(txtBuscar.Text);
                else
                    dt = DatabaseService.BuscarProveedoresMultiples(txtBuscar.Text);

                lstResultados.ItemsSource = dt.DefaultView;
                popupBusqueda.IsOpen = dt.Rows.Count > 0;
            }
            catch { }
        }

        private void SeleccionarEntidad(DataRow row)
        {
            _entidadSeleccionada = row;
            popupBusqueda.IsOpen = false;
            txtBuscar.Text = "";

            // Cargar Datos
            lblNombreEntidad.Text = row["RazonSocial"].ToString();

            // El saldo viene en la búsqueda, pero es mejor recargarlo fresco de la BD
            // Nota: El saldo viene como 'SaldoDeuda' en la tabla
            decimal saldo = 0;
            if (row.Table.Columns.Contains("SaldoDeuda") && row["SaldoDeuda"] != DBNull.Value)
            {
                saldo = Convert.ToDecimal(row["SaldoDeuda"]);
            }

            ActualizarVisualizacionSaldo(saldo);

            panelSaldo.Visibility = Visibility.Visible;
            CargarHistorial();
        }

        private void ActualizarVisualizacionSaldo(decimal saldo)
        {
            lblSaldo.Text = saldo.ToString("C2");
            // Si debe (saldo positivo), en rojo. Si está en 0 o a favor, en verde.
            lblSaldo.Foreground = saldo > 0 ? new SolidColorBrush(Color.FromRgb(255, 82, 82)) : new SolidColorBrush(Colors.LightGreen);
        }

        private void CargarHistorial()
        {
            int id = Convert.ToInt32(_entidadSeleccionada[_modoClientes ? "ClienteID" : "ProveedorID"]);
            DataTable dt = DatabaseService.GetMovimientosCC(
                _modoClientes ? id : (int?)null,
                _modoClientes ? (int?)null : id
            );
            dgvMovimientos.ItemsSource = dt.DefaultView;
        }

        // --- EVENTOS DE LISTA ---
        private void lstResultados_MouseUp(object sender, MouseButtonEventArgs e)
        {
            if (lstResultados.SelectedItem is DataRowView drv) SeleccionarEntidad(drv.Row);
        }
        private void txtBuscar_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Down && popupBusqueda.IsOpen) { lstResultados.SelectedIndex = 0; lstResultados.Focus(); }
        }
        private void lstResultados_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter && lstResultados.SelectedItem is DataRowView drv) SeleccionarEntidad(drv.Row);
        }

        // --- PAGOS ---
        private void btnAsentarPago_Click(object sender, RoutedEventArgs e)
        {
            numMontoPago.Value = 0;
            popupPago.Visibility = Visibility.Visible;
            numMontoPago.Focus();
        }

        private void btnCancelarPopup_Click(object sender, RoutedEventArgs e)
        {
            popupPago.Visibility = Visibility.Collapsed;
        }

        private void btnConfirmarPago_Click(object sender, RoutedEventArgs e)
        {
            decimal monto = numMontoPago.Value ?? 0;
            if (monto <= 0) { MessageBox.Show("El monto debe ser mayor a 0."); return; }

            bool exito = false;
            int id = Convert.ToInt32(_entidadSeleccionada[_modoClientes ? "ClienteID" : "ProveedorID"]);

            if (_modoClientes)
                exito = DatabaseService.RegistrarPagoCliente(id, monto);
            else
                exito = DatabaseService.RegistrarPagoProveedor(id, monto);

            if (exito)
            {
                MessageBox.Show("Movimiento registrado correctamente.", "Éxito", MessageBoxButton.OK, MessageBoxImage.Information);
                popupPago.Visibility = Visibility.Collapsed;

                // Recargar historial para ver el nuevo movimiento
                CargarHistorial();

                // Ocultamos el panel para obligar a refrescar el saldo buscando de nuevo 
                // (o podríamos recalcularlo, pero esto es más seguro para evitar desincronización)
                panelSaldo.Visibility = Visibility.Collapsed;
                txtBuscar.Focus();
            }
        }
    }
}