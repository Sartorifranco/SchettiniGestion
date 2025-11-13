using SchettiniGestion; // ¡Importante!
using System;
using System.Data; // ¡Importante!
using System.Windows;
using System.Windows.Controls;

namespace SchettiniGestion.WPF
{
    public partial class ProveedoresControl : UserControl
    {
        private int _proveedorIDSeleccionado = 0;

        public ProveedoresControl()
        {
            InitializeComponent();
        }

        private void ProveedoresControl_Loaded(object sender, RoutedEventArgs e)
        {
            CargarProveedores();
            LimpiarCampos();
        }

        private void CargarProveedores()
        {
            try
            {
                // Llamamos al nuevo método del DatabaseService
                DataTable dt = DatabaseService.GetProveedores();
                dgvProveedores.ItemsSource = dt.DefaultView;
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error al cargar proveedores: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void LimpiarCampos()
        {
            _proveedorIDSeleccionado = 0; // Indica "Nuevo"
            txtCuit.Text = "";
            txtRazonSocial.Text = "";
            txtTelefono.Text = "";
            txtEmail.Text = "";
            txtDireccion.Text = "";

            btnGuardar.Content = "💾 Guardar";
            btnEliminar.IsEnabled = false;
            dgvProveedores.UnselectAll();
            txtCuit.Focus();
        }

        private void dgvProveedores_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (dgvProveedores.SelectedItem is DataRowView filaSeleccionada)
            {
                // Guardamos el ID
                _proveedorIDSeleccionado = Convert.ToInt32(filaSeleccionada["ProveedorID"]);

                // Cargamos los datos en los campos
                txtCuit.Text = filaSeleccionada["CUIT"].ToString();
                txtRazonSocial.Text = filaSeleccionada["RazonSocial"].ToString();
                txtTelefono.Text = filaSeleccionada["Telefono"].ToString();
                txtEmail.Text = filaSeleccionada["Email"].ToString();
                txtDireccion.Text = filaSeleccionada["Direccion"].ToString();

                // Actualizamos botones
                btnGuardar.Content = "Modificar";
                btnEliminar.IsEnabled = true;
            }
        }

        private void btnNuevo_Click(object sender, RoutedEventArgs e)
        {
            LimpiarCampos();
        }

        private void btnGuardar_Click(object sender, RoutedEventArgs e)
        {
            // Validaciones básicas
            if (string.IsNullOrWhiteSpace(txtCuit.Text) || string.IsNullOrWhiteSpace(txtRazonSocial.Text))
            {
                MessageBox.Show("El CUIT y la Razón Social son obligatorios.", "Datos Incompletos", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            // Llamamos al método GuardarProveedor de la base de datos
            bool exito = DatabaseService.GuardarProveedor(
                _proveedorIDSeleccionado,
                txtCuit.Text,
                txtRazonSocial.Text,
                txtTelefono.Text,
                txtEmail.Text,
                txtDireccion.Text
            );

            if (exito)
            {
                MessageBox.Show("Proveedor guardado exitosamente.", "Éxito", MessageBoxButton.OK, MessageBoxImage.Information);
                CargarProveedores();
                LimpiarCampos();
            }
        }

        private void btnEliminar_Click(object sender, RoutedEventArgs e)
        {
            if (_proveedorIDSeleccionado == 0) return;

            // Confirmación
            MessageBoxResult confirmacion = MessageBox.Show($"¿Está seguro de que desea eliminar al proveedor '{txtRazonSocial.Text}'?",
                                                  "Confirmar eliminación",
                                                  MessageBoxButton.YesNo,
                                                  MessageBoxImage.Warning);

            if (confirmacion == MessageBoxResult.Yes)
            {
                // Llamamos al método EliminarProveedor
                bool exito = DatabaseService.EliminarProveedor(_proveedorIDSeleccionado);

                if (exito)
                {
                    MessageBox.Show("Proveedor eliminado exitosamente.", "Éxito", MessageBoxButton.OK, MessageBoxImage.Information);
                    CargarProveedores();
                    LimpiarCampos();
                }
            }
        }
    }
}