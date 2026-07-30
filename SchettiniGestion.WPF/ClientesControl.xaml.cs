using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using SchettiniGestion;

namespace SchettiniGestion.WPF
{
    public partial class ClientesControl : UserControl
    {
        private List<ClienteListadoItem> _clientesTodos = new List<ClienteListadoItem>();

        public ClientesControl()
        {
            InitializeComponent();
        }

        private void ClientesControl_Loaded(object sender, RoutedEventArgs e)
        {
            CargarClientes();
        }

        private void CargarClientes()
        {
            try
            {
                _clientesTodos = DatabaseService.GetClientesLista() ?? new List<ClienteListadoItem>();
                AplicarFiltro();
            }
            catch (Exception ex)
            {
                CustomMessageBox.Show("Error al cargar clientes: " + ex.Message);
            }
        }

        private void AplicarFiltro()
        {
            if (_clientesTodos == null) { dgvClientes.ItemsSource = null; return; }

            string t = (txtFiltroClientes?.Text ?? "").Trim();
            IEnumerable<ClienteListadoItem> q = _clientesTodos;
            if (!string.IsNullOrEmpty(t))
            {
                q = q.Where(c =>
                    (c.RazonSocial ?? "").IndexOf(t, StringComparison.OrdinalIgnoreCase) >= 0
                    || (c.CUIT ?? "").IndexOf(t, StringComparison.OrdinalIgnoreCase) >= 0);
            }

            dgvClientes.ItemsSource = q.ToList();
        }

        private void txtFiltroClientes_TextChanged(object sender, TextChangedEventArgs e)
        {
            AplicarFiltro();
        }

        private void btnNuevo_Click(object sender, RoutedEventArgs e)
        {
            var modal = new ClienteModalWindow(0, CargarClientes) { Owner = Window.GetWindow(this) };
            modal.ShowDialog();
        }

        private void dgvClientes_MouseDoubleClick(object sender, MouseButtonEventArgs e) => AbrirEditar();

        private void MenuItemEditar_Click(object sender, RoutedEventArgs e) => AbrirEditar();

        private void MenuItemEliminar_Click(object sender, RoutedEventArgs e)
        {
            if (!(dgvClientes.SelectedItem is ClienteListadoItem item)) return;

            if (CustomMessageBox.Show($"¿Eliminar el cliente '{item.RazonSocial}'?", "Confirmar", MessageBoxButton.YesNo) == MessageBoxResult.Yes)
            {
                if (DatabaseService.EliminarCliente(item.ClienteID))
                    CargarClientes();
                else
                    CustomMessageBox.Show("No se pudo eliminar el cliente. Puede tener facturas o movimientos asociados.", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void AbrirEditar()
        {
            if (!(dgvClientes.SelectedItem is ClienteListadoItem item)) return;
            var modal = new ClienteModalWindow(item.ClienteID, CargarClientes) { Owner = Window.GetWindow(this) };
            modal.ShowDialog();
        }
    }
}
