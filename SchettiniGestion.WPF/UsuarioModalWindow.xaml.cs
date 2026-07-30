using System;
using System.Collections.Generic;
using System.Data;
using System.Windows;
using SchettiniGestion;

namespace SchettiniGestion.WPF
{
    public partial class UsuarioModalWindow : Window
    {
        private readonly int _usuarioId;
        private readonly Action _onGuardado;
        private string _nombreUsuarioOriginal = "";

        public UsuarioModalWindow(int usuarioId, Action onGuardado)
        {
            InitializeComponent();
            _usuarioId = usuarioId;
            _onGuardado = onGuardado;
            Loaded += UsuarioModalWindow_Loaded;
        }

        private void UsuarioModalWindow_Loaded(object sender, RoutedEventArgs e)
        {
            List<Rol> roles = DatabaseService.GetRoles();
            cmbRol.ItemsSource = roles;

            if (_usuarioId > 0)
            {
                lblTitulo.Text = "Editar Usuario";
                lblPassword.Text = "Contraseña";
                lblPasswordHint.Visibility = Visibility.Visible;
                btnEliminar.Visibility = Visibility.Visible;
                CargarDatos(roles);
            }
            else
            {
                lblTitulo.Text = "Nuevo Usuario";
                lblPassword.Text = "Contraseña *";
                lblPasswordHint.Visibility = Visibility.Collapsed;
                btnEliminar.Visibility = Visibility.Collapsed;
                cmbRol.SelectedIndex = -1;
            }
            txtNombreUsuario.Focus();
        }

        private void CargarDatos(List<Rol> roles)
        {
            try
            {
                DataTable dt = DatabaseService.GetUsuarios();
                foreach (DataRow r in dt.Rows)
                {
                    if (Convert.ToInt32(r["UsuarioID"]) != _usuarioId) continue;

                    txtNombreUsuario.Text = r["NombreUsuario"]?.ToString() ?? "";
                    _nombreUsuarioOriginal = txtNombreUsuario.Text;
                    txtNombrePersonal.Text = (r.Table.Columns.Contains("NombrePersonal") && r["NombrePersonal"] != DBNull.Value)
                        ? r["NombrePersonal"].ToString() : "";

                    int rolId = r["RolID"] != DBNull.Value ? Convert.ToInt32(r["RolID"]) : 0;
                    foreach (var rol in roles)
                    {
                        if (rol.RolId == rolId) { cmbRol.SelectedItem = rol; break; }
                    }
                    break;
                }
            }
            catch (Exception ex)
            {
                CustomMessageBox.Show("Error al cargar el usuario: " + ex.Message, "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void btnGuardar_Click(object sender, RoutedEventArgs e)
        {
            bool esNuevo = _usuarioId == 0;

            if (string.IsNullOrWhiteSpace(txtNombreUsuario.Text) || string.IsNullOrWhiteSpace(txtNombrePersonal.Text) || cmbRol.SelectedItem == null)
            {
                CustomMessageBox.Show("Complete Nombre de Usuario, Nombre del Personal y Rol.", "Aviso", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }
            if (esNuevo && string.IsNullOrWhiteSpace(txtPassword.Password))
            {
                CustomMessageBox.Show("Ingrese una contraseña para el nuevo usuario.", "Aviso", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            try
            {
                Rol rolSeleccionado = (Rol)cmbRol.SelectedItem;
                string hash = string.IsNullOrWhiteSpace(txtPassword.Password) ? "" : PasswordHasher.HashPassword(txtPassword.Password);

                bool exito = DatabaseService.GuardarUsuarioConHash(
                    _usuarioId,
                    txtNombreUsuario.Text.Trim(),
                    hash,
                    rolSeleccionado.RolId,
                    rolSeleccionado.Nombre,
                    txtNombrePersonal.Text.Trim());

                if (exito)
                {
                    _onGuardado?.Invoke();
                    DialogResult = true;
                    Close();
                }
                else
                {
                    CustomMessageBox.Show("Error al guardar en base de datos.", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
            catch (Exception ex)
            {
                CustomMessageBox.Show("Error crítico: " + ex.Message, "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void btnEliminar_Click(object sender, RoutedEventArgs e)
        {
            if (_usuarioId == 0) return;

            if (_nombreUsuarioOriginal.ToLower() == "admin")
            {
                CustomMessageBox.Show("No se puede eliminar al super-administrador.", "Prohibido", MessageBoxButton.OK, MessageBoxImage.Stop);
                return;
            }

            if (CustomMessageBox.Show($"¿Eliminar el usuario '{_nombreUsuarioOriginal}'?", "Confirmar", MessageBoxButton.YesNo, MessageBoxImage.Warning) == MessageBoxResult.Yes)
            {
                if (DatabaseService.EliminarUsuario(_usuarioId))
                {
                    _onGuardado?.Invoke();
                    DialogResult = true;
                    Close();
                }
                else
                {
                    CustomMessageBox.Show("No se pudo eliminar el usuario.", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }

        private void btnCancelar_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }
    }
}
