using SchettiniGestion; // <-- ¡Nuestro Cerebro!
using System;
using System.Data; // <-- Para usar DataTable
using System.Windows;
using System.Windows.Controls;

namespace SchettiniGestion.WPF
{
    /// <summary>
    /// Lógica de interacción para UsuariosControl.xaml
    /// </summary>
    public partial class UsuariosControl : UserControl
    {
        // Guardaremos el ID del usuario que seleccionemos en la grilla
        private int _usuarioIDSeleccionado = 0;

        public UsuariosControl()
        {
            InitializeComponent();
        }

        // --- 1. MÉTODOS DE CARGA ---

        // Este evento se dispara cuando el control se carga en la pantalla
        // --- ¡INICIO DE LA CORRECCIÓN! ---
        // Renombramos el método para que coincida con el XAML
        private void UsuariosControl_Loaded(object sender, RoutedEventArgs e)
        // --- ¡FIN DE LA CORRECCIÓN! ---
        {
            CargarRoles();
            CargarUsuarios();
            LimpiarCampos();
        }

        private void CargarRoles()
        {
            cmbRol.Items.Clear();
            cmbRol.Items.Add("Admin");
            cmbRol.Items.Add("Vendedor");
            cmbRol.SelectedIndex = 1; // Default "Vendedor"
        }

        private void CargarUsuarios()
        {
            try
            {
                // Obtenemos los datos de la lógica de negocio
                DataTable dt = DatabaseService.GetUsuarios();
                // Asignamos los datos a la grilla de WPF
                dgvUsuarios.ItemsSource = dt.DefaultView;
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error al cargar usuarios: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        // --- 2. LÓGICA DE LOS BOTONES ---

        private void btnNuevo_Click(object sender, RoutedEventArgs e)
        {
            LimpiarCampos();
        }

        private void btnGuardar_Click(object sender, RoutedEventArgs e)
        {
            // 1. Validar que el nombre de usuario no esté vacío
            if (string.IsNullOrWhiteSpace(txtUsuario.Text))
            {
                MessageBox.Show("El nombre de usuario no puede estar vacío.", "Error", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            // 2. Obtener los valores de los campos
            string usuario = txtUsuario.Text.Trim();
            string password = txtPassword.Password; // Se usa .Password en WPF
            string rol = cmbRol.Text;

            // 3. Llamar al servicio de base de datos para guardar
            // _usuarioIDSeleccionado es 0 si es nuevo, o > 0 si estamos editando
            bool exito = DatabaseService.GuardarUsuario(_usuarioIDSeleccionado, usuario, password, rol);

            if (exito)
            {
                MessageBox.Show("Usuario guardado exitosamente.", "Éxito", MessageBoxButton.OK, MessageBoxImage.Information);

                // 4. Recargar la grilla y limpiar los campos
                CargarUsuarios();
                LimpiarCampos();
            }
        }

        private void btnEliminar_Click(object sender, RoutedEventArgs e)
        {
            // 1. Validar que haya un usuario seleccionado
            if (_usuarioIDSeleccionado == 0)
            {
                MessageBox.Show("Por favor, seleccione un usuario de la grilla para eliminar.", "Error", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            // 2. Validar que no se intente borrar al admin principal
            if (txtUsuario.Text.ToLower() == "admin")
            {
                MessageBox.Show("No se puede eliminar al usuario 'admin' principal.", "Acción no permitida", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            // 3. Pedir confirmación
            MessageBoxResult confirmacion = MessageBox.Show($"¿Está seguro de que desea eliminar al usuario '{txtUsuario.Text}'?",
                                                      "Confirmar eliminación",
                                                      MessageBoxButton.YesNo,
                                                      MessageBoxImage.Warning);

            if (confirmacion == MessageBoxResult.Yes)
            {
                // 4. Llamar al servicio para eliminar
                bool exito = DatabaseService.EliminarUsuario(_usuarioIDSeleccionado);

                if (exito)
                {
                    MessageBox.Show("Usuario eliminado exitosamente.", "Éxito", MessageBoxButton.OK, MessageBoxImage.Information);

                    // 5. Recargar y limpiar
                    CargarUsuarios();
                    LimpiarCampos();
                }
            }
        }

        // --- 3. MÉTODOS AYUDANTES ---

        private void LimpiarCampos()
        {
            _usuarioIDSeleccionado = 0; // Esto es CLAVE. Indica que es un "Nuevo" usuario
            txtUsuario.Text = "";
            txtPassword.Password = "";
            cmbRol.SelectedIndex = 1; // Volver a "Vendedor"

            // Quitar la selección de la grilla
            dgvUsuarios.UnselectAll();
        }

        private void dgvUsuarios_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            // Verificamos que haya un ítem seleccionado
            if (dgvUsuarios.SelectedItem is DataRowView filaSeleccionada)
            {
                // --- 1. Guardamos el ID del usuario ---
                // Leemos el valor de la fila
                _usuarioIDSeleccionado = Convert.ToInt32(filaSeleccionada["UsuarioID"]);

                // --- 2. Cargamos los datos en los campos de texto ---
                txtUsuario.Text = filaSeleccionada["NombreUsuario"].ToString();
                cmbRol.Text = filaSeleccionada["Rol"].ToString();

                // --- 3. Limpiamos el campo de contraseña ---
                txtPassword.Password = "";
            }
        }
    }
}