using System;
using System.Collections.Generic;
using System.Data;
using System.Windows;
using System.Windows.Controls;
using SchettiniGestion;

namespace SchettiniGestion.WPF
{
    public partial class UsuariosControl : UserControl
    {
        private int _usuarioIdSeleccionado = 0;

        public UsuariosControl()
        {
            InitializeComponent();
        }

        private void UsuariosControl_Loaded(object sender, RoutedEventArgs e)
        {
            CargarUsuarios();
            CargarRoles();
            Limpiar();
        }

        private void CargarUsuarios()
        {
            try
            {
                DataTable dt = DatabaseService.GetUsuarios();
                dgvUsuarios.ItemsSource = dt.DefaultView;
            }
            catch (Exception ex)
            {
                MessageBox.Show("Error al cargar usuarios: " + ex.Message);
            }
        }

        private void CargarRoles()
        {
            try
            {
                // Obtenemos la lista actualizada de roles (incluyendo los nuevos creados en GestiónPermisos)
                List<Rol> roles = DatabaseService.GetRoles();

                cmbRoles.ItemsSource = null; // Limpiar para refrescar
                cmbRoles.ItemsSource = roles;

                // Si la lista tiene elementos, seleccionar el primero por defecto (opcional)
                if (roles.Count > 0) cmbRoles.SelectedIndex = -1;
            }
            catch (Exception ex)
            {
                MessageBox.Show("Error cargando roles: " + ex.Message);
            }
        }

        private void Limpiar()
        {
            _usuarioIdSeleccionado = 0;
            txtNombreUsuario.Text = "";
            txtPassword.Password = "";
            cmbRoles.SelectedIndex = -1;
            btnEliminar.IsEnabled = false;
            txtNombreUsuario.Focus();
        }

        private void btnNuevo_Click(object sender, RoutedEventArgs e)
        {
            Limpiar();
        }

        private void btnGuardar_Click(object sender, RoutedEventArgs e)
        {
            // Validaciones básicas
            if (string.IsNullOrWhiteSpace(txtNombreUsuario.Text) || cmbRoles.SelectedItem == null)
            {
                MessageBox.Show("Complete el nombre de usuario y seleccione un rol.", "Aviso", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            // Si es nuevo, la contraseña es obligatoria
            if (_usuarioIdSeleccionado == 0 && string.IsNullOrWhiteSpace(txtPassword.Password))
            {
                MessageBox.Show("La contraseña es obligatoria para nuevos usuarios.", "Aviso", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            try
            {
                Rol rolSeleccionado = (Rol)cmbRoles.SelectedItem;

                bool exito = DatabaseService.GuardarUsuario(
                    _usuarioIdSeleccionado,
                    txtNombreUsuario.Text.Trim(),
                    txtPassword.Password, // Se envía tal cual, DatabaseService se encarga del Hash
                    rolSeleccionado.RolId,
                    rolSeleccionado.Nombre
                );

                if (exito)
                {
                    MessageBox.Show("Usuario guardado correctamente.", "Éxito", MessageBoxButton.OK, MessageBoxImage.Information);
                    CargarUsuarios();
                    Limpiar();
                }
                else
                {
                    MessageBox.Show("Error al guardar en base de datos.", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show("Error crítico: " + ex.Message);
            }
        }

        private void btnEliminar_Click(object sender, RoutedEventArgs e)
        {
            if (_usuarioIdSeleccionado == 0) return;

            // Evitar que se borre a sí mismo o al admin principal si se llama 'admin'
            if (txtNombreUsuario.Text.ToLower() == "admin")
            {
                MessageBox.Show("No se puede eliminar al super-administrador.", "Prohibido", MessageBoxButton.OK, MessageBoxImage.Stop);
                return;
            }

            if (MessageBox.Show($"¿Eliminar el usuario '{txtNombreUsuario.Text}'?", "Confirmar", MessageBoxButton.YesNo, MessageBoxImage.Warning) == MessageBoxResult.Yes)
            {
                if (DatabaseService.EliminarUsuario(_usuarioIdSeleccionado))
                {
                    CargarUsuarios();
                    Limpiar();
                }
                else
                {
                    MessageBox.Show("No se pudo eliminar el usuario.", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }

        private void dgvUsuarios_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (dgvUsuarios.SelectedItem is DataRowView row)
            {
                _usuarioIdSeleccionado = Convert.ToInt32(row["UsuarioID"]);
                txtNombreUsuario.Text = row["NombreUsuario"].ToString();
                txtPassword.Password = ""; // Por seguridad, no traemos el Hash

                // Seleccionar el rol correspondiente en el ComboBox
                int rolId = 0;
                if (row["RolID"] != DBNull.Value)
                    rolId = Convert.ToInt32(row["RolID"]);

                foreach (Rol r in cmbRoles.Items)
                {
                    if (r.RolId == rolId)
                    {
                        cmbRoles.SelectedItem = r;
                        break;
                    }
                }

                btnEliminar.IsEnabled = true;
            }
        }
    }
}