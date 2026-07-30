using System;
using System.Data;
using System.Windows;
using System.Windows.Controls;

namespace SchettiniGestion.WPF
{
    public partial class ProveedorModalWindow : Window
    {
        private readonly int _proveedorId;
        private readonly Action _onGuardado;

        public ProveedorModalWindow(int proveedorId, Action onGuardado)
        {
            InitializeComponent();
            _proveedorId = proveedorId;
            _onGuardado = onGuardado;
            Loaded += ProveedorModalWindow_Loaded;
        }

        private void ProveedorModalWindow_Loaded(object sender, RoutedEventArgs e)
        {
            if (_proveedorId > 0)
            {
                lblTitulo.Text = "Editar Proveedor";
                btnEliminar.Visibility = Visibility.Visible;
                CargarDatos();
            }
            else
            {
                lblTitulo.Text = "Nuevo Proveedor";
                btnEliminar.Visibility = Visibility.Collapsed;
            }
            txtCuit.Focus();
        }

        private void CargarDatos()
        {
            try
            {
                var dt = DatabaseService.GetProveedores();
                foreach (DataRow r in dt.Rows)
                {
                    if (Convert.ToInt32(r["ProveedorID"]) != _proveedorId) continue;
                    txtCuit.Text = ValorCol(r, "CUIT");
                    txtRazonSocial.Text = ValorCol(r, "RazonSocial");
                    txtDireccion.Text = ValorCol(r, "Direccion");
                    txtPersonaContacto.Text = ValorCol(r, "PersonaContacto");
                    txtTelefono.Text = ValorCol(r, "Telefono");
                    txtPaginaWeb.Text = ValorCol(r, "PaginaWeb");
                    txtEmail.Text = ValorCol(r, "Email");
                    SeleccionarCategoriaFiscal(ValorCol(r, "CategoriaFiscal"));
                    break;
                }
            }
            catch (Exception ex)
            {
                CustomMessageBox.Show($"Error al cargar el proveedor: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private static string ValorCol(DataRow row, string col)
        {
            if (row?.Table == null || !row.Table.Columns.Contains(col)) return "";
            var o = row[col];
            return o == null || o == DBNull.Value ? "" : o.ToString();
        }

        private void SeleccionarCategoriaFiscal(string valor)
        {
            foreach (ComboBoxItem item in cmbCategoriaFiscal.Items)
            {
                if (string.Equals(item.Content?.ToString(), valor, StringComparison.OrdinalIgnoreCase))
                {
                    cmbCategoriaFiscal.SelectedItem = item;
                    return;
                }
            }
            cmbCategoriaFiscal.SelectedIndex = 0;
        }

        private void btnGuardar_Click(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrWhiteSpace(txtCuit.Text) || string.IsNullOrWhiteSpace(txtRazonSocial.Text))
            {
                CustomMessageBox.Show("El CUIT y la Razón Social son obligatorios.", "Datos Incompletos", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            string categoriaFiscal = (cmbCategoriaFiscal.SelectedItem as ComboBoxItem)?.Content?.ToString() ?? "";

            bool exito = DatabaseService.GuardarProveedor(
                _proveedorId,
                txtCuit.Text.Trim(),
                txtRazonSocial.Text.Trim(),
                txtTelefono.Text.Trim(),
                txtEmail.Text.Trim(),
                txtDireccion.Text.Trim(),
                categoriaFiscal,
                txtPersonaContacto.Text.Trim(),
                txtPaginaWeb.Text.Trim());

            if (exito)
            {
                _onGuardado?.Invoke();
                DialogResult = true;
                Close();
            }
            else
            {
                CustomMessageBox.Show("Error al guardar el proveedor.", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void btnEliminar_Click(object sender, RoutedEventArgs e)
        {
            if (_proveedorId == 0) return;

            var confirmacion = CustomMessageBox.Show($"¿Está seguro de que desea eliminar al proveedor '{txtRazonSocial.Text}'?",
                "Confirmar eliminación", MessageBoxButton.YesNo, MessageBoxImage.Warning);

            if (confirmacion != MessageBoxResult.Yes) return;

            if (DatabaseService.EliminarProveedor(_proveedorId))
            {
                _onGuardado?.Invoke();
                DialogResult = true;
                Close();
            }
            else
            {
                CustomMessageBox.Show("No se pudo eliminar el proveedor. Puede tener compras o pagos asociados.", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void btnCancelar_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }
    }
}
