using SchettiniGestion; // <-- ¡Nuestro Cerebro!
using System;
using System.Data; // <-- Para usar DataTable
using System.Windows;
using System.Windows.Controls;
using System.Collections.Generic; // <-- ¡Importante para List<Rol>!

namespace SchettiniGestion.WPF
{
    /// <summary>
    /// Lógica de interacción para UsuariosControl.xaml
    /// </summary>
    public partial class UsuariosControl : UserControl
    {
        private int _usuarioIDSeleccionado = 0;

        public UsuariosControl()
        {
            InitializeComponent();
        }

        // --- 1. MÉTODOS DE CARGA (MODIFICADOS) ---

        private void UsuariosControl_Loaded(object sender, RoutedEventArgs e)
        {
            CargarRoles(); // ¡Llama al nuevo método!
            CargarUsuarios();
            LimpiarCampos();
        }

        // Método de Carga de Roles (corregido para usar List<Rol>)
        private void CargarRoles()
        {
            try
            {
                List<Rol> listaRoles = DatabaseService.GetRoles();
                cmbRol.ItemsSource = listaRoles;
                cmbRol.DisplayMemberPath = "Nombre";
                cmbRol.SelectedValuePath = "RolId";
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error al cargar roles: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
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
                MessageBox.Show($"Error al cargar usuarios: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        // --- 2. LÓGICA DE LOS BOTONES (MODIFICADA) ---

        private void btnNuevo_Click(object sender, RoutedEventArgs e)
        {
            LimpiarCampos();
        }

        // --- ¡INICIO DE CÓDIGO MODIFICADO (ERROR NOT NULL)! ---
        private void btnGuardar_Click(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrWhiteSpace(txtNombreUsuario.Text))
            {
                MessageBox.Show("El nombre de usuario no puede estar vacío.", "Error", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }
            if (cmbRol.SelectedItem == null)
            {
                MessageBox.Show("Debe seleccionar un rol para el usuario.", "Error", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            string usuario = txtNombreUsuario.Text.Trim();
            string password = txtPassword.Password;

            // Obtenemos el ID del rol (ej: 1, 2)
            int rolID = (int)cmbRol.SelectedValue;

            // ¡NUEVO! Obtenemos también el TEXTO del rol (ej: "Admin")
            string rolTexto = (cmbRol.SelectedItem as Rol).Nombre;

            // Llamamos al servicio de base de datos (¡el método modificado!)
            bool exito = DatabaseService.GuardarUsuario(_usuarioIDSeleccionado, usuario, password, rolID, rolTexto);

            if (exito)
            {
                MessageBox.Show("Usuario guardado exitosamente.", "Éxito", MessageBoxButton.OK, MessageBoxImage.Information);
                CargarUsuarios();
                LimpiarCampos();
            }
        }
        // --- ¡FIN DE CÓDIGO MODIFICADO (ERROR NOT NULL)! ---

        private void btnEliminar_Click(object sender, RoutedEventArgs e)
        {
            if (_usuarioIDSeleccionado == 0)
            {
                MessageBox.Show("Por favor, seleccione un usuario de la grilla para eliminar.", "Error", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            if (txtNombreUsuario.Text.ToLower() == "admin")
            {
                MessageBox.Show("No se puede eliminar al usuario 'admin' principal.", "Acción no permitida", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            MessageBoxResult confirmacion = MessageBox.Show($"¿Está seguro de que desea eliminar al usuario '{txtNombreUsuario.Text}'?",
                                                  "Confirmar eliminación",
                                                  MessageBoxButton.YesNo,
                                                  MessageBoxImage.Warning);

            if (confirmacion == MessageBoxResult.Yes)
            {
                bool exito = DatabaseService.EliminarUsuario(_usuarioIDSeleccionado);

                if (exito)
                {
                    MessageBox.Show("Usuario eliminado exitosamente.", "Éxito", MessageBoxButton.OK, MessageBoxImage.Information);
                    CargarUsuarios();
                    LimpiarCampos();
                }
            }
        }

        // --- 3. MÉTODOS AYUDANTES (MODIFICADOS) ---

        private void LimpiarCampos()
        {
            _usuarioIDSeleccionado = 0;
            txtNombreUsuario.Text = "";
            txtPassword.Password = "";
            cmbRol.SelectedIndex = -1;
            dgvUsuarios.UnselectAll();
        }


        private void dgvUsuarios_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (dgvUsuarios.SelectedItem is DataRowView filaSeleccionada)
            {
                _usuarioIDSeleccionado = Convert.ToInt32(filaSeleccionada["UsuarioID"]);
                txtNombreUsuario.Text = filaSeleccionada["NombreUsuario"].ToString();

                // Asignamos el RolID (ej: 1) al ComboBox
                cmbRol.SelectedValue = filaSeleccionada["RolID"];

                txtPassword.Password = "";
            }
        }
    }
}