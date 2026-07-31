using SchettiniGestion;
using System;
using System.Data;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace SchettiniGestion.WPF
{
    public partial class ComprasControl : UserControl
    {
        public ComprasControl()
        {
            InitializeComponent();
        }

        private bool _inicializado;

        private void UserControl_Loaded(object sender, RoutedEventArgs e)
        {
            if (_inicializado) return;
            _inicializado = true;
            CargarFacturasCompras();
            CargarRecepciones();
            CargarNotas();
            CargarGastos();
            CargarPagos();
            CargarOrdenes();
        }

        // ========== FACTURAS DE COMPRAS ==========
        private void CargarFacturasCompras()
        {
            try
            {
                string filtro = txtFiltroFacturas?.Text?.Trim() ?? "";
                dgvFacturasCompras.ItemsSource = DatabaseService.GetCompras(filtro).DefaultView;
            }
            catch (Exception ex) { MessageBox.Show(ex.Message, "Error", MessageBoxButton.OK, MessageBoxImage.Error); }
        }
        private void txtFiltroFacturas_TextChanged(object sender, TextChangedEventArgs e) => CargarFacturasCompras();
        private void RefrescarTrasFacturaCompra()
        {
            CargarFacturasCompras();
            CargarRecepciones();
            CargarOrdenes();
        }

        private void btnNuevaFacturaCompra_Click(object sender, RoutedEventArgs e)
        {
            var modal = new CompraModalWindow(Window.GetWindow(this), RefrescarTrasFacturaCompra);
            modal.ShowDialog();
        }
        private void btnEditarFacturaCompra_Click(object sender, RoutedEventArgs e)
        {
            int? id = ObtenerId(dgvFacturasCompras, "CompraID");
            if (!id.HasValue) { MessageBox.Show("Seleccione una factura.", "Atención", MessageBoxButton.OK, MessageBoxImage.Warning); return; }
            var modal = new CompraModalWindow(Window.GetWindow(this), RefrescarTrasFacturaCompra, id.Value);
            modal.ShowDialog();
        }
        private void btnEliminarFacturaCompra_Click(object sender, RoutedEventArgs e)
        {
            int? id = ObtenerId(dgvFacturasCompras, "CompraID");
            if (!id.HasValue) { MessageBox.Show("Seleccione una factura.", "Atención", MessageBoxButton.OK, MessageBoxImage.Warning); return; }
            bool recibioStock = false;
            if (dgvFacturasCompras.SelectedItem is DataRowView row && row.Row.Table.Columns.Contains("StockRecibido") && row["StockRecibido"] != DBNull.Value)
                recibioStock = Convert.ToBoolean(row["StockRecibido"]);
            string msg = recibioStock
                ? "¿Eliminar esta compra? Se revertirá el stock y los movimientos asociados."
                : "¿Eliminar esta compra? (No se movió stock al registrarla.)";
            if (MessageBox.Show(msg, "Confirmar", MessageBoxButton.YesNo, MessageBoxImage.Warning) != MessageBoxResult.Yes) return;
            try
            {
                if (DatabaseService.EliminarCompra(id.Value)) { MessageBox.Show("Compra eliminada.", "Éxito", MessageBoxButton.OK, MessageBoxImage.Information); RefrescarTrasFacturaCompra(); }
                else MessageBox.Show("No se pudo eliminar.", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            catch (Exception ex) { MessageBox.Show(ex.Message, "Error", MessageBoxButton.OK, MessageBoxImage.Error); }
        }
        private void dgvFacturasCompras_MouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            int? id = ObtenerId(dgvFacturasCompras, "CompraID");
            if (!id.HasValue) return;
            var modal = new CompraModalWindow(Window.GetWindow(this), RefrescarTrasFacturaCompra, id.Value);
            modal.ShowDialog();
        }

        // ========== RECEPCIONES ==========
        private void CargarRecepciones()
        {
            try { dgvRecepciones.ItemsSource = DatabaseService.GetRecepcionesCompra(txtFiltroRecepciones?.Text?.Trim() ?? "").DefaultView; }
            catch (Exception ex) { MessageBox.Show(ex.Message, "Error", MessageBoxButton.OK, MessageBoxImage.Error); }
        }
        private void txtFiltroRecepciones_TextChanged(object sender, TextChangedEventArgs e) => CargarRecepciones();
        private void btnNuevaRecepcion_Click(object sender, RoutedEventArgs e)
        {
            var modal = new RecepcionCompraModalWindow(Window.GetWindow(this), 0, CargarRecepciones);
            modal.ShowDialog();
        }
        private void btnEditarRecepcion_Click(object sender, RoutedEventArgs e)
        {
            int? id = ObtenerId(dgvRecepciones, "RecepcionID");
            if (!id.HasValue) { MessageBox.Show("Seleccione una recepción.", "Atención", MessageBoxButton.OK, MessageBoxImage.Warning); return; }
            var modal = new RecepcionCompraModalWindow(Window.GetWindow(this), id.Value, CargarRecepciones);
            modal.ShowDialog();
        }
        private void btnEliminarRecepcion_Click(object sender, RoutedEventArgs e)
        {
            int? id = ObtenerId(dgvRecepciones, "RecepcionID");
            if (!id.HasValue) { MessageBox.Show("Seleccione una recepción.", "Atención", MessageBoxButton.OK, MessageBoxImage.Warning); return; }
            if (MessageBox.Show("¿Eliminar esta recepción?", "Confirmar", MessageBoxButton.YesNo, MessageBoxImage.Warning) != MessageBoxResult.Yes) return;
            if (DatabaseService.EliminarRecepcionCompra(id.Value)) { MessageBox.Show("Recepción eliminada.", "Éxito", MessageBoxButton.OK, MessageBoxImage.Information); CargarRecepciones(); }
            else MessageBox.Show("Error al eliminar.", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
        }

        // ========== NOTAS CRÉDITO/DÉBITO ==========
        private void CargarNotas()
        {
            try { dgvNotas.ItemsSource = DatabaseService.GetNotasCreditoDebitoCompras(txtFiltroNotas?.Text?.Trim() ?? "").DefaultView; }
            catch (Exception ex) { MessageBox.Show(ex.Message, "Error", MessageBoxButton.OK, MessageBoxImage.Error); }
        }
        private void txtFiltroNotas_TextChanged(object sender, TextChangedEventArgs e) => CargarNotas();
        private void btnNuevaNota_Click(object sender, RoutedEventArgs e)
        {
            var modal = new NotaCreditoDebitoModalWindow(Window.GetWindow(this), 0, CargarNotas);
            modal.ShowDialog();
        }
        private void btnEditarNota_Click(object sender, RoutedEventArgs e)
        {
            int? id = ObtenerId(dgvNotas, "NotaID");
            if (!id.HasValue) { MessageBox.Show("Seleccione una nota.", "Atención", MessageBoxButton.OK, MessageBoxImage.Warning); return; }
            var modal = new NotaCreditoDebitoModalWindow(Window.GetWindow(this), id.Value, CargarNotas);
            modal.ShowDialog();
        }
        private void btnEliminarNota_Click(object sender, RoutedEventArgs e)
        {
            int? id = ObtenerId(dgvNotas, "NotaID");
            if (!id.HasValue) { MessageBox.Show("Seleccione una nota.", "Atención", MessageBoxButton.OK, MessageBoxImage.Warning); return; }
            if (MessageBox.Show("¿Eliminar esta nota?", "Confirmar", MessageBoxButton.YesNo, MessageBoxImage.Warning) != MessageBoxResult.Yes) return;
            if (DatabaseService.EliminarNotaCreditoDebitoCompra(id.Value)) { MessageBox.Show("Nota eliminada.", "Éxito", MessageBoxButton.OK, MessageBoxImage.Information); CargarNotas(); }
            else MessageBox.Show("Error al eliminar.", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
        }

        // ========== GASTOS RÁPIDOS ==========
        private void CargarGastos()
        {
            try { dgvGastos.ItemsSource = DatabaseService.GetGastosRapidos(txtFiltroGastos?.Text?.Trim() ?? "").DefaultView; }
            catch (Exception ex) { MessageBox.Show(ex.Message, "Error", MessageBoxButton.OK, MessageBoxImage.Error); }
        }
        private void txtFiltroGastos_TextChanged(object sender, TextChangedEventArgs e) => CargarGastos();
        private void btnNuevoGasto_Click(object sender, RoutedEventArgs e)
        {
            var modal = new GastoRapidoModalWindow(Window.GetWindow(this), 0, CargarGastos);
            modal.ShowDialog();
        }
        private void btnEditarGasto_Click(object sender, RoutedEventArgs e)
        {
            int? id = ObtenerId(dgvGastos, "GastoID");
            if (!id.HasValue) { MessageBox.Show("Seleccione un gasto.", "Atención", MessageBoxButton.OK, MessageBoxImage.Warning); return; }
            var modal = new GastoRapidoModalWindow(Window.GetWindow(this), id.Value, CargarGastos);
            modal.ShowDialog();
        }
        private void btnEliminarGasto_Click(object sender, RoutedEventArgs e)
        {
            int? id = ObtenerId(dgvGastos, "GastoID");
            if (!id.HasValue) { MessageBox.Show("Seleccione un gasto.", "Atención", MessageBoxButton.OK, MessageBoxImage.Warning); return; }
            if (MessageBox.Show("¿Eliminar este gasto?", "Confirmar", MessageBoxButton.YesNo, MessageBoxImage.Warning) != MessageBoxResult.Yes) return;
            if (DatabaseService.EliminarGastoRapido(id.Value)) { MessageBox.Show("Gasto eliminado.", "Éxito", MessageBoxButton.OK, MessageBoxImage.Information); CargarGastos(); }
            else MessageBox.Show("Error al eliminar.", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
        }

        // ========== PAGOS ==========
        private void CargarPagos()
        {
            try { dgvPagos.ItemsSource = DatabaseService.GetPagosProveedores(txtFiltroPagos?.Text?.Trim() ?? "").DefaultView; }
            catch (Exception ex) { MessageBox.Show(ex.Message, "Error", MessageBoxButton.OK, MessageBoxImage.Error); }
        }
        private void txtFiltroPagos_TextChanged(object sender, TextChangedEventArgs e) => CargarPagos();
        private void btnNuevoPago_Click(object sender, RoutedEventArgs e)
        {
            var modal = new PagoProveedorModalWindow(Window.GetWindow(this), 0, CargarPagos);
            modal.ShowDialog();
        }
        private void btnEditarPago_Click(object sender, RoutedEventArgs e)
        {
            int? id = ObtenerId(dgvPagos, "PagoID");
            if (!id.HasValue) { MessageBox.Show("Seleccione un pago.", "Atención", MessageBoxButton.OK, MessageBoxImage.Warning); return; }
            var modal = new PagoProveedorModalWindow(Window.GetWindow(this), id.Value, CargarPagos);
            modal.ShowDialog();
        }
        private void btnEliminarPago_Click(object sender, RoutedEventArgs e)
        {
            int? id = ObtenerId(dgvPagos, "PagoID");
            if (!id.HasValue) { MessageBox.Show("Seleccione un pago.", "Atención", MessageBoxButton.OK, MessageBoxImage.Warning); return; }
            if (MessageBox.Show("¿Eliminar este pago? Se revertirá el saldo del proveedor.", "Confirmar", MessageBoxButton.YesNo, MessageBoxImage.Warning) != MessageBoxResult.Yes) return;
            if (DatabaseService.EliminarPagoProveedor(id.Value)) { MessageBox.Show("Pago eliminado.", "Éxito", MessageBoxButton.OK, MessageBoxImage.Information); CargarPagos(); }
            else MessageBox.Show("Error al eliminar.", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
        }

        // ========== ÓRDENES DE COMPRA ==========
        private void CargarOrdenes()
        {
            try { dgvOrdenes.ItemsSource = DatabaseService.GetOrdenesCompra(txtFiltroOrdenes?.Text?.Trim() ?? "").DefaultView; }
            catch (Exception ex) { MessageBox.Show(ex.Message, "Error", MessageBoxButton.OK, MessageBoxImage.Error); }
        }
        private void txtFiltroOrdenes_TextChanged(object sender, TextChangedEventArgs e) => CargarOrdenes();
        private void btnNuevaOrden_Click(object sender, RoutedEventArgs e)
        {
            var modal = new OrdenCompraModalWindow(Window.GetWindow(this), 0, CargarOrdenes);
            modal.ShowDialog();
        }
        private void btnEditarOrden_Click(object sender, RoutedEventArgs e)
        {
            int? id = ObtenerId(dgvOrdenes, "OrdenCompraID");
            if (!id.HasValue) { MessageBox.Show("Seleccione una orden.", "Atención", MessageBoxButton.OK, MessageBoxImage.Warning); return; }
            var modal = new OrdenCompraModalWindow(Window.GetWindow(this), id.Value, CargarOrdenes);
            modal.ShowDialog();
        }
        private void btnEliminarOrden_Click(object sender, RoutedEventArgs e)
        {
            int? id = ObtenerId(dgvOrdenes, "OrdenCompraID");
            if (!id.HasValue) { MessageBox.Show("Seleccione una orden.", "Atención", MessageBoxButton.OK, MessageBoxImage.Warning); return; }
            if (MessageBox.Show("¿Eliminar esta orden?", "Confirmar", MessageBoxButton.YesNo, MessageBoxImage.Warning) != MessageBoxResult.Yes) return;
            if (DatabaseService.EliminarOrdenCompra(id.Value)) { MessageBox.Show("Orden eliminada.", "Éxito", MessageBoxButton.OK, MessageBoxImage.Information); CargarOrdenes(); }
            else MessageBox.Show("Error al eliminar.", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
        }
        private void dgvOrdenes_MouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            int? id = ObtenerId(dgvOrdenes, "OrdenCompraID");
            if (!id.HasValue) return;
            var modal = new OrdenCompraModalWindow(Window.GetWindow(this), id.Value, CargarOrdenes);
            modal.ShowDialog();
        }

        private int? ObtenerId(DataGrid dg, string colName)
        {
            if (dg?.SelectedItem is DataRowView drv && drv.Row.Table.Columns.Contains(colName) && drv.Row[colName] != DBNull.Value)
                return Convert.ToInt32(drv.Row[colName]);
            return null;
        }
    }
}
