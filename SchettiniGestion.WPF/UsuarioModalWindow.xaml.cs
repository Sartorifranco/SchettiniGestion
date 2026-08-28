using System;
using System.Collections.Generic;
using System.Windows;
using SchettiniGestion;

namespace SchettiniGestion.WPF
{
    public partial class UsuarioModalWindow : Window
    {
        private readonly int _usuarioId;

        public UsuarioModalWindow() : this(0, "", "", 0)
        {
        }

        public UsuarioModalWindow(int usuarioId, string nombreUsuario, string nombrePersonal, int rolId)
        {
            InitializeComponent();
            _usuarioId = usuarioId;

            bool esNuevo = usuarioId <= 0;
            txtTitulo.Text = esNuevo ? "Nuevo usuario" : "Editar usuario";
            Title = txtTitulo.Text;
            txtHintPassword.Text = esNuevo
                ? "Obligatoria para un usuario nuevo."
                : "Dejá vacío para no cambiar la contraseña.";

            CargarRoles(rolId);
            txtNombreUsuario.Text = nombreUsuario ?? "";
            txtNombrePersonal.Text = nombrePersonal ?? "";
            Loaded += (s, e) => txtNombreUsuario.Focus();
        }

        private void CargarRoles(int rolIdSeleccionado)
        {
            List<Rol> roles = DatabaseService.GetRoles();
            cmbRoles.ItemsSource = roles;
            if (rolIdSeleccionado > 0)
            {
                foreach (Rol r in roles)
                {
                    if (r.RolId == rolIdSeleccionado)
                    {
                        cmbRoles.SelectedItem = r;
                        return;
                    }
                }
            }
            cmbRoles.SelectedIndex = -1;
        }

        private void btnCancelar_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
        }

        private void btnGuardar_Click(object sender, RoutedEventArgs e)
        {
            bool esNuevo = _usuarioId <= 0;
            string usuario = (txtNombreUsuario.Text ?? "").Trim();
            string personal = (txtNombrePersonal.Text ?? "").Trim();

            if (string.IsNullOrWhiteSpace(usuario) || string.IsNullOrWhiteSpace(personal) || cmbRoles.SelectedItem == null)
            {
                CustomMessageBox.Show("Complete nombre de usuario, nombre del personal y rol.", "Aviso",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }
            if (esNuevo && string.IsNullOrWhiteSpace(txtPassword.Password))
            {
                CustomMessageBox.Show("Ingrese una contraseña para el nuevo usuario.", "Aviso",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            if (DatabaseService.EsNombreUsuarioReservado(usuario))
            {
                CustomMessageBox.Show("Ese nombre de usuario está reservado para el sistema.", "Aviso",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            try
            {
                var rol = (Rol)cmbRoles.SelectedItem;
                string hash = string.IsNullOrWhiteSpace(txtPassword.Password)
                    ? ""
                    : PasswordHasher.HashPassword(txtPassword.Password);

                bool exito = DatabaseService.GuardarUsuarioConHash(
                    _usuarioId, usuario, hash, rol.RolId, rol.Nombre, personal);

                if (!exito)
                {
                    CustomMessageBox.Show("Error al guardar en base de datos.", "Error",
                        MessageBoxButton.OK, MessageBoxImage.Error);
                    return;
                }

                DialogResult = true;
            }
            catch (Exception ex)
            {
                CustomMessageBox.Show("Error crítico: " + ex.Message, "Error",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
    }
}
