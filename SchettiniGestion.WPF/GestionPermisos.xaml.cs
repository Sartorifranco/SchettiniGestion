using SchettiniGestion; // Para usar DatabaseService y las clases
using System;
using System.Collections.Generic;
using System.Data;
using System.Data.Entity.Core.Common.CommandTrees.ExpressionBuilder;
using System.Linq;
using System.Windows;
using System.Windows.Controls;

namespace SchettiniGestion.WPF
{
    public partial class GestionPermisos : UserControl
    {
        // Guardamos los datos cargados para usarlos fácilmente
        private List<Rol> roles;
        private List<Permiso> todosLosPermisos;
        private Dictionary<int, List<int>> permisosPorRol; // RolID -> Lista de PermisoID

        public GestionPermisos()
        {
            InitializeComponent();
            // Carga los datos cuando el control esté listo
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
                // 1. Obtener todos los datos de la BD
                roles = DatabaseService.GetRoles();
                todosLosPermisos = DatabaseService.GetPermisos();
                permisosPorRol = DatabaseService.GetPermisosPorRol();

                // 2. Llenar la lista de Roles (Columna 1)
                RolesListBox.ItemsSource = roles;
                RolesListBox.DisplayMemberPath = "Nombre"; // Muestra la propiedad "Nombre" del objeto Rol
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error al cargar datos de permisos: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void RolesListBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (RolesListBox.SelectedItem == null)
            {
                // Si no hay nada seleccionado, ocultar todo
                PermisosHelpText.Visibility = Visibility.Visible;
                PermisosStackPanel.Visibility = Visibility.Collapsed;
                GuardarButton.IsEnabled = false;
                return;
            }

            // Mostrar el panel de checkboxes y ocultar el texto de ayuda
            PermisosHelpText.Visibility = Visibility.Collapsed;
            PermisosStackPanel.Visibility = Visibility.Visible;
            GuardarButton.IsEnabled = true;

            // Limpiar checkboxes anteriores
            PermisosStackPanel.Children.Clear();

            // 1. Obtener el Rol seleccionado
            Rol rolSeleccionado = (Rol)RolesListBox.SelectedItem;

            // 2. Obtener la lista de permisos que ESTE rol ya tiene
            List<int> permisosDelRol = new List<int>();
            if (permisosPorRol.ContainsKey(rolSeleccionado.RolId))
            {
                permisosDelRol = permisosPorRol[rolSeleccionado.RolId];
            }

            // 3. Crear un CheckBox por CADA permiso que existe en el sistema
            foreach (var permiso in todosLosPermisos)
            {
                CheckBox cb = new CheckBox
                {
                    Content = permiso.Nombre, // "ACCESO_USUARIOS"
                    Tag = permiso.PermisoId,  // Guardamos el ID en el Tag
                    Foreground = (System.Windows.Media.SolidColorBrush)FindResource("BodyForegroundBrush"), // Estilo
                    FontSize = 14,
                    Margin = new Thickness(0, 8, 0, 8),
                    // Marcar el checkbox si el rol tiene este permiso
                    IsChecked = permisosDelRol.Contains(permiso.PermisoId)
                };

                PermisosStackPanel.Children.Add(cb);
            }
        }

        private void GuardarButton_Click(object sender, RoutedEventArgs e)
        {
            if (RolesListBox.SelectedItem == null) return;

            Rol rolSeleccionado = (Rol)RolesListBox.SelectedItem;

            // 1. Crear la nueva lista de permisos para este rol
            List<int> nuevosPermisosParaEsteRol = new List<int>();

            // 2. Recorrer todos los checkboxes que creamos
            foreach (CheckBox cb in PermisosStackPanel.Children.OfType<CheckBox>())
            {
                // Si está marcado...
                if (cb.IsChecked == true)
                {
                    // ...agregamos el ID (que guardamos en el Tag) a la lista
                    nuevosPermisosParaEsteRol.Add((int)cb.Tag);
                }
            }

            try
            {
                // 3. Enviar la lista completa a la BD
                DatabaseService.ActualizarPermisosParaRol(rolSeleccionado.RolId, nuevosPermisosParaEsteRol);

                // 4. Actualizar nuestro "caché" local para que la próxima selección sea correcta
                permisosPorRol[rolSeleccionado.RolId] = nuevosPermisosParaEsteRol;

                MessageBox.Show("Permisos actualizados correctamente.", "Éxito", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error al guardar los permisos: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
    }
}