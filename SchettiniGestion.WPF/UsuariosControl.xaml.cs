using System;
using System.Collections.ObjectModel;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Controls;
using SchettiniGestion;

namespace SchettiniGestion.WPF
{
    public partial class UsuariosControl : UserControl
    {
        public class PermisoUI : INotifyPropertyChanged
        {
            private bool _habilitado;
            public string NombreModulo { get; set; }
            public bool Habilitado
            {
                get => _habilitado;
                set
                {
                    if (_habilitado == value) return;
                    _habilitado = value;
                    OnPropertyChanged();
                }
            }

            public event PropertyChangedEventHandler PropertyChanged;
            private void OnPropertyChanged([CallerMemberName] string prop = null)
                => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(prop));
        }

        private int _usuarioIdSeleccionado = 0;
        public ObservableCollection<PermisoUI> PermisosRolActual { get; } = new ObservableCollection<PermisoUI>();

        public UsuariosControl()
        {
            InitializeComponent();
            DataContext = this;
            CargarPermisosDesdeConstantes();
            lstPermisosDinamicos.ItemsSource = PermisosRolActual;
        }

        private void UsuariosControl_Loaded(object sender, RoutedEventArgs e)
        {
            CargarUsuarios();
            CargarRoles();
            Limpiar();
        }

        private void CargarPermisosDesdeConstantes()
        {
            PermisosRolActual.Clear();

            var permisos = typeof(DatabaseService)
                .GetFields(BindingFlags.Public | BindingFlags.Static | BindingFlags.FlattenHierarchy)
                .Where(f => f.IsLiteral && !f.IsInitOnly && f.FieldType == typeof(string) && f.Name.StartsWith("PERMISO_", StringComparison.Ordinal))
                .Select(f => f.GetRawConstantValue() as string)
                .Where(v => !string.IsNullOrWhiteSpace(v))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .OrderBy(v => v)
                .ToList();

            foreach (var permiso in permisos)
            {
                PermisosRolActual.Add(new PermisoUI
                {
                    NombreModulo = permiso,
                    Habilitado = false
                });
            }
        }

        private void CargarPermisosRolSeleccionado()
        {
            if (!(cmbRolesPermisos.SelectedItem is Rol rolSel))
            {
                foreach (var p in PermisosRolActual) p.Habilitado = false;
                return;
            }

            var permisosGuardados = DatabaseService.GetPermisosNombresPorRol(rolSel.RolId);
            foreach (var p in PermisosRolActual)
                p.Habilitado = permisosGuardados.Contains(p.NombreModulo);
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
                CustomMessageBox.Show("Error al cargar usuarios: " + ex.Message, "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void CargarRoles()
        {
            try
            {
                // Obtenemos la lista actualizada de roles (incluyendo los nuevos creados en GestiónPermisos)
                List<Rol> roles = DatabaseService.GetRoles();

                cmbRolesUsuario.ItemsSource = null;
                cmbRolesPermisos.ItemsSource = null;
                cmbRolesUsuario.ItemsSource = roles;
                cmbRolesPermisos.ItemsSource = roles;

                if (roles.Count > 0)
                {
                    cmbRolesUsuario.SelectedIndex = -1;
                    cmbRolesPermisos.SelectedIndex = -1;
                }
            }
            catch (Exception ex)
            {
                CustomMessageBox.Show("Error cargando roles: " + ex.Message, "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void Limpiar()
        {
            _usuarioIdSeleccionado = 0;
            txtNombreUsuario.Text = "";
            txtNombrePersonal.Text = "";
            txtPassword.Password = "";
            cmbRolesUsuario.SelectedIndex = -1;
            foreach (var p in PermisosRolActual) p.Habilitado = false;
            btnEliminar.IsEnabled = false;
            txtNombreUsuario.Focus();
        }

        private void btnNuevo_Click(object sender, RoutedEventArgs e)
        {
            Limpiar();
        }

        private void btnGuardarUsuario_Click(object sender, RoutedEventArgs e)
        {
            bool esNuevo = _usuarioIdSeleccionado == 0;

            if (string.IsNullOrWhiteSpace(txtNombreUsuario.Text) || string.IsNullOrWhiteSpace(txtNombrePersonal.Text) || cmbRolesUsuario.SelectedItem == null)
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
                Rol rolSeleccionado = (Rol)cmbRolesUsuario.SelectedItem;
                // Al editar: si la contraseña quedó en blanco, se conserva la existente
                string hash = string.IsNullOrWhiteSpace(txtPassword.Password) ? "" : PasswordHasher.HashPassword(txtPassword.Password);

                bool exito = DatabaseService.GuardarUsuarioConHash(
                    _usuarioIdSeleccionado,
                    txtNombreUsuario.Text.Trim(),
                    hash,
                    rolSeleccionado.RolId,
                    rolSeleccionado.Nombre,
                    txtNombrePersonal.Text.Trim());

                if (exito)
                {
                    CustomMessageBox.Show("Usuario guardado correctamente.", "Éxito", MessageBoxButton.OK, MessageBoxImage.Information);
                    CargarUsuarios();
                    Limpiar();
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
            if (_usuarioIdSeleccionado == 0) return;

            // Evitar que se borre a sí mismo o al admin principal si se llama 'admin'
            if (txtNombreUsuario.Text.ToLower() == "admin")
            {
                CustomMessageBox.Show("No se puede eliminar al super-administrador.", "Prohibido", MessageBoxButton.OK, MessageBoxImage.Stop);
                return;
            }

            if (CustomMessageBox.Show($"¿Eliminar el usuario '{txtNombreUsuario.Text}'?", "Confirmar", MessageBoxButton.YesNo, MessageBoxImage.Warning) == MessageBoxResult.Yes)
            {
                if (DatabaseService.EliminarUsuario(_usuarioIdSeleccionado))
                {
                    CargarUsuarios();
                    Limpiar();
                }
                else
                {
                    CustomMessageBox.Show("No se pudo eliminar el usuario.", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }

        private void btnNuevoRol_Click(object sender, RoutedEventArgs e)
        {
            var input = new ModernInputWindow("Crear nuevo rol", "Nombre del rol:")
            {
                Owner = Window.GetWindow(this)
            };
            if (input.ShowDialog() != true) return;

            string nombre = input.ResponseText?.Trim();
            if (string.IsNullOrWhiteSpace(nombre))
            {
                CustomMessageBox.Show("El nombre no puede estar vacío.", "Aviso", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            var (ok, error) = DatabaseService.GuardarRol(nombre);
            if (!ok)
            {
                CustomMessageBox.Show(error ?? "No se pudo crear el rol.", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            CargarRoles();
            foreach (Rol r in cmbRolesPermisos.Items)
            {
                if (string.Equals(r.Nombre, nombre, StringComparison.OrdinalIgnoreCase))
                {
                    cmbRolesPermisos.SelectedItem = r;
                    cmbRolesUsuario.SelectedItem = r;
                    break;
                }
            }
            CustomMessageBox.Show(
                $"Rol '{nombre}' creado correctamente.\nAsigne los permisos y guarde los cambios.",
                "Éxito", MessageBoxButton.OK, MessageBoxImage.Information);
        }

        private void dgvUsuarios_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (dgvUsuarios.SelectedItem is DataRowView row)
            {
                _usuarioIdSeleccionado = Convert.ToInt32(row["UsuarioID"]);
                txtNombreUsuario.Text = row["NombreUsuario"].ToString();
                if (row.Row.Table.Columns.Contains("NombrePersonal") && row["NombrePersonal"] != DBNull.Value)
                    txtNombrePersonal.Text = row["NombrePersonal"].ToString();
                else
                    txtNombrePersonal.Text = "";
                txtPassword.Password = ""; // Por seguridad, no traemos el Hash

                // Seleccionar el rol correspondiente en el ComboBox
                int rolId = 0;
                if (row["RolID"] != DBNull.Value)
                    rolId = Convert.ToInt32(row["RolID"]);

                foreach (Rol r in cmbRolesUsuario.Items)
                {
                    if (r.RolId == rolId)
                    {
                        cmbRolesUsuario.SelectedItem = r;
                        cmbRolesPermisos.SelectedItem = r;
                        CargarPermisosRolSeleccionado();
                        break;
                    }
                }

                btnEliminar.IsEnabled = true;
            }
        }

        private void cmbRolesPermisos_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (!IsLoaded) return;
            CargarPermisosRolSeleccionado();
        }

        private void btnGuardarPermisos_Click(object sender, RoutedEventArgs e)
        {
            if (!(cmbRolesPermisos.SelectedItem is Rol rolSel))
            {
                CustomMessageBox.Show("Seleccione un rol para guardar sus permisos.", "Aviso", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            try
            {
                var permisosActivos = PermisosRolActual
                    .Where(p => p.Habilitado)
                    .Select(p => p.NombreModulo)
                    .ToList();
                string permisosCsv = string.Join(",", permisosActivos);

                bool ok = DatabaseService.ActualizarPermisosParaRolPorNombre(rolSel.RolId, permisosActivos);
                if (ok)
                    CustomMessageBox.Show("Permisos actualizados correctamente.", "Éxito", MessageBoxButton.OK, MessageBoxImage.Information);
                else
                    CustomMessageBox.Show("No se pudieron guardar los permisos.", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            catch (Exception ex)
            {
                CustomMessageBox.Show("Error al guardar permisos: " + ex.Message, "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
    }
}