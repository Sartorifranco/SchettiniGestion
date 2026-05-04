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
                MessageBox.Show("Error al cargar usuarios: " + ex.Message);
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
                MessageBox.Show("Error cargando roles: " + ex.Message);
            }
        }

        private void Limpiar()
        {
            _usuarioIdSeleccionado = 0;
            txtNombreUsuario.Text = "";
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
            if (string.IsNullOrWhiteSpace(txtNombreUsuario.Text)
                || string.IsNullOrWhiteSpace(txtPassword.Password)
                || cmbRolesUsuario.SelectedItem == null)
            {
                MessageBox.Show("Complete Nombre de Usuario, Contraseña y Rol.", "Aviso", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            try
            {
                Rol rolSeleccionado = (Rol)cmbRolesUsuario.SelectedItem;
                string hash = PasswordHasher.HashPassword(txtPassword.Password);

                bool exito = DatabaseService.GuardarUsuarioConHash(
                    _usuarioIdSeleccionado,
                    txtNombreUsuario.Text.Trim(),
                    hash,
                    rolSeleccionado.RolId,
                    rolSeleccionado.Nombre);

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
                MessageBox.Show("Seleccione un rol para guardar sus permisos.", "Aviso", MessageBoxButton.OK, MessageBoxImage.Warning);
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
                    MessageBox.Show("Permisos actualizados correctamente.", "Éxito", MessageBoxButton.OK, MessageBoxImage.Information);
                else
                    MessageBox.Show("No se pudieron guardar los permisos.", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            catch (Exception ex)
            {
                MessageBox.Show("Error al guardar permisos: " + ex.Message, "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
    }
}