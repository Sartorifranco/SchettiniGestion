using SchettiniGestion;
using System;
using System.Data;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace SchettiniGestion.WPF
{
    public partial class ProveedoresControl : UserControl
    {
        private DataTable _proveedoresTodos;

        public ProveedoresControl()
        {
            InitializeComponent();
        }

        private void ProveedoresControl_Loaded(object sender, RoutedEventArgs e)
        {
            CargarProveedores();
        }

        private void CargarProveedores()
        {
            try
            {
                _proveedoresTodos = DatabaseService.GetProveedores();
                AplicarFiltro();
            }
            catch (Exception ex)
            {
                CustomMessageBox.Show($"Error al cargar proveedores: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void AplicarFiltro()
        {
            if (_proveedoresTodos == null) { dgvProveedores.ItemsSource = null; return; }

            string t = (txtFiltroProveedores?.Text ?? "").Trim();
            if (string.IsNullOrEmpty(t))
            {
                dgvProveedores.ItemsSource = _proveedoresTodos.DefaultView;
                return;
            }

            string filtro = t.Replace("'", "''");
            try
            {
                _proveedoresTodos.DefaultView.RowFilter = $"CUIT LIKE '%{filtro}%' OR RazonSocial LIKE '%{filtro}%'";
                dgvProveedores.ItemsSource = _proveedoresTodos.DefaultView;
            }
            catch
            {
                dgvProveedores.ItemsSource = _proveedoresTodos.DefaultView;
            }
        }

        private void txtFiltroProveedores_TextChanged(object sender, TextChangedEventArgs e)
        {
            AplicarFiltro();
        }

        private void btnNuevo_Click(object sender, RoutedEventArgs e)
        {
            var modal = new ProveedorModalWindow(0, CargarProveedores) { Owner = Window.GetWindow(this) };
            modal.ShowDialog();
        }

        private void dgvProveedores_MouseDoubleClick(object sender, MouseButtonEventArgs e) => AbrirEditar();

        private void MenuItemEditar_Click(object sender, RoutedEventArgs e) => AbrirEditar();

        private void MenuItemEliminar_Click(object sender, RoutedEventArgs e)
        {
            if (!(dgvProveedores.SelectedItem is DataRowView row)) return;

            int id = Convert.ToInt32(row["ProveedorID"]);
            string nombre = row["RazonSocial"]?.ToString() ?? "";

            var confirmacion = CustomMessageBox.Show($"¿Está seguro de que desea eliminar al proveedor '{nombre}'?",
                "Confirmar eliminación", MessageBoxButton.YesNo, MessageBoxImage.Warning);
            if (confirmacion != MessageBoxResult.Yes) return;

            if (DatabaseService.EliminarProveedor(id))
                CargarProveedores();
            else
                CustomMessageBox.Show("No se pudo eliminar el proveedor. Puede tener compras o pagos asociados.", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
        }

        private void AbrirEditar()
        {
            if (!(dgvProveedores.SelectedItem is DataRowView row)) return;
            int id = Convert.ToInt32(row["ProveedorID"]);
            var modal = new ProveedorModalWindow(id, CargarProveedores) { Owner = Window.GetWindow(this) };
            modal.ShowDialog();
        }
    }
}
