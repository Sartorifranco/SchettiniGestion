using SchettiniGestion;
using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using SqlConnection = System.Data.SqlClient.SqlConnection;
using SqlCommand = System.Data.SqlClient.SqlCommand;
using System.Windows;
using System.Windows.Controls;

namespace SchettiniGestion.WPF
{
    public partial class GestionPermisos : UserControl
    {
        private List<Rol> roles;
        private List<Permiso> todosLosPermisos;
        private Dictionary<int, List<int>> permisosPorRol;

        public GestionPermisos()
        {
            InitializeComponent();
            this.Loaded += GestionPermisos_Loaded;
        }

        private void GestionPermisos_Loaded(object sender, RoutedEventArgs e)
        {
            CargarDatosIniciales();
        }

        private void CargarDatosIniciales()
        {
            try
            {
                // 1. Obtener datos frescos de la BD
                roles = DatabaseService.GetRoles();
                todosLosPermisos = DatabaseService.GetPermisos();
                permisosPorRol = DatabaseService.GetPermisosPorRol();

                // 2. Llenar la lista de Roles
                RolesListBox.ItemsSource = null;
                RolesListBox.ItemsSource = roles;
                RolesListBox.DisplayMemberPath = "Nombre";
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error al cargar datos: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void RolesListBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (RolesListBox.SelectedItem == null)
            {
                // Ocultar panel si no hay selección
                PermisosHelpText.Visibility = Visibility.Visible;
                PermisosStackPanel.Visibility = Visibility.Collapsed;
                GuardarButton.IsEnabled = false;
                btnEliminarRol.IsEnabled = false;
                return;
            }

            // Mostrar panel
            PermisosHelpText.Visibility = Visibility.Collapsed;
            PermisosStackPanel.Visibility = Visibility.Visible;
            GuardarButton.IsEnabled = true;
            btnEliminarRol.IsEnabled = true;

            // Limpiar visualmente los checkboxes
            PermisosStackPanel.Children.Clear();

            Rol rolSeleccionado = (Rol)RolesListBox.SelectedItem;

            // Obtener permisos actuales de este rol
            List<int> permisosDelRol = new List<int>();
            if (permisosPorRol.ContainsKey(rolSeleccionado.RolId))
            {
                permisosDelRol = permisosPorRol[rolSeleccionado.RolId];
            }

            // Generar CheckBox por cada permiso disponible
            foreach (var permiso in todosLosPermisos)
            {
                // Limpiamos el nombre para que se vea bonito (Ej: ACCESO_VENTAS -> VENTAS)
                string nombreLimpio = permiso.Nombre.Replace("ACCESO_", "").Replace("_", " ");

                CheckBox cb = new CheckBox
                {
                    Content = nombreLimpio,
                    Tag = permiso.PermisoId, // Guardamos el ID oculto
                    Foreground = System.Windows.Media.Brushes.White,
                    FontSize = 14,
                    Margin = new Thickness(0, 5, 0, 5),
                    IsChecked = permisosDelRol.Contains(permiso.PermisoId) // Marcar si ya lo tiene
                };
                PermisosStackPanel.Children.Add(cb);
            }
        }

        private void btnNuevoRol_Click(object sender, RoutedEventArgs e)
        {
            // 1. Abrimos la ventanita para pedir el nombre
            InputWindow input = new InputWindow("Crear Nuevo Rol");
            if (input.ShowDialog() == true)
            {
                string nuevoNombre = input.ResponseText.Trim();

                if (string.IsNullOrEmpty(nuevoNombre))
                {
                    MessageBox.Show("El nombre no puede estar vacío.");
                    return;
                }

                // 2. Insertamos el rol en la Base de Datos
                try
                {
                    using (var conn = new SqlConnection(DatabaseService.ConnectionString))
                    {
                        conn.Open();
                        // Evitar roles duplicados (insensible a mayúsculas/espacios)
                        var cmdCheck = new SqlCommand("SELECT COUNT(*) FROM Roles WHERE LOWER(LTRIM(RTRIM(NombreRol))) = LOWER(LTRIM(RTRIM(@n)))", conn);
                        cmdCheck.Parameters.AddWithValue("@n", nuevoNombre);
                        if (Convert.ToInt32(cmdCheck.ExecuteScalar()) > 0)
                        {
                            MessageBox.Show($"Ya existe un rol con el nombre '{nuevoNombre}'.\nElija otro nombre.", "Rol duplicado", MessageBoxButton.OK, MessageBoxImage.Warning);
                            return;
                        }

                        string sql = "INSERT INTO Roles (NombreRol) VALUES (@n)";
                        using (var cmd = new SqlCommand(sql, conn))
                        {
                            cmd.Parameters.AddWithValue("@n", nuevoNombre);
                            cmd.ExecuteNonQuery();
                        }
                    }

                    // 3. Recargamos la lista para que aparezca el nuevo rol
                    CargarDatosIniciales();
                    MessageBox.Show($"Rol '{nuevoNombre}' creado exitosamente.\nAhora selecciónelo y asígnele permisos.", "Éxito");
                }
                catch (Exception ex)
                {
                    MessageBox.Show("Error al crear rol: " + ex.Message);
                }
            }
        }

        private void GuardarButton_Click(object sender, RoutedEventArgs e)
        {
            if (RolesListBox.SelectedItem == null) return;

            Rol rolSeleccionado = (Rol)RolesListBox.SelectedItem;
            List<int> nuevosPermisos = new List<int>();

            // Recorrer los checkboxes marcados
            foreach (CheckBox cb in PermisosStackPanel.Children.OfType<CheckBox>())
            {
                if (cb.IsChecked == true)
                {
                    nuevosPermisos.Add((int)cb.Tag);
                }
            }

            try
            {
                // Guardar en BD
                DatabaseService.ActualizarPermisosParaRol(rolSeleccionado.RolId, nuevosPermisos);

                // Actualizar memoria local
                permisosPorRol[rolSeleccionado.RolId] = nuevosPermisos;

                MessageBox.Show("Permisos guardados correctamente.", "Guardado", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error al guardar: {ex.Message}", "Error");
            }
        }

        private void btnEliminarRol_Click(object sender, RoutedEventArgs e)
        {
            if (RolesListBox.SelectedItem == null) return;
            Rol rol = (Rol)RolesListBox.SelectedItem;

            // Seguridad: No borrar al Admin
            if (rol.Nombre.ToUpper().Contains("ADMIN"))
            {
                MessageBox.Show("No se puede eliminar el rol de Administrador.", "Bloqueado", MessageBoxButton.OK, MessageBoxImage.Stop);
                return;
            }

            if (MessageBox.Show($"¿Está seguro de eliminar el rol '{rol.Nombre}'?\nLos usuarios con este rol perderán sus accesos.", "Confirmar Eliminación", MessageBoxButton.YesNo, MessageBoxImage.Warning) == MessageBoxResult.Yes)
            {
                try
                {
                    using (var conn = new SqlConnection(DatabaseService.ConnectionString))
                    {
                        conn.Open();
                        // Primero borrar los permisos asociados
                        new SqlCommand($"DELETE FROM Roles_Permisos WHERE RolID={rol.RolId}", conn).ExecuteNonQuery();
                        // Luego borrar el rol
                        new SqlCommand($"DELETE FROM Roles WHERE RolID={rol.RolId}", conn).ExecuteNonQuery();

                        // Opcional: Dejar usuarios huérfanos con rol NULL o por defecto (no implementado aquí para simplificar)
                    }
                    CargarDatosIniciales();
                }
                catch (Exception ex)
                {
                    MessageBox.Show("Error eliminando rol: " + ex.Message);
                }
            }
        }
    }
}