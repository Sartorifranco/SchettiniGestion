using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
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

        private int _usuarioIdSeleccionado;
        private DataTable _rolesDataTable;
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

        private void CargarUsuarios()
        {
            try
            {
                DataTable dt = global::SchettiniGestion.DatabaseService.GetUsuarios();
                dgvUsuarios.ItemsSource = dt.DefaultView;
            }
            catch (Exception ex)
            {
                ModernMessageBox.Show("Error al cargar usuarios: " + ex.Message, "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void CargarRoles()
        {
            try
            {
                var roles = global::SchettiniGestion.DatabaseService.GetRoles();
                var dt = new DataTable();
                dt.Columns.Add("RolID", typeof(int));
                dt.Columns.Add("NombreRol", typeof(string));

                foreach (var rol in roles)
                {
                    dt.Rows.Add(rol.RolId, rol.Nombre);
                }

                _rolesDataTable = dt;
                cmbRolesUsuario.ItemsSource = _rolesDataTable.DefaultView;
                lstRolesPermisos.ItemsSource = _rolesDataTable.DefaultView;
                cmbRolesUsuario.SelectedIndex = -1;
                lstRolesPermisos.SelectedIndex = -1;
            }
            catch (Exception ex)
            {
                ModernMessageBox.Show("Error cargando roles: " + ex.Message, "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void CargarPermisosDesdeConstantes()
        {
            PermisosRolActual.Clear();

            var permisos = typeof(global::SchettiniGestion.DatabaseService)
                .GetFields(BindingFlags.Public | BindingFlags.Static | BindingFlags.FlattenHierarchy)
                .Where(f => f.IsLiteral && !f.IsInitOnly && f.FieldType == typeof(string) && f.Name.StartsWith("PERMISO_", StringComparison.Ordinal))
                .Select(f => f.GetRawConstantValue() as string)
                .Where(v => !string.IsNullOrWhiteSpace(v))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .OrderBy(v => v)
                .ToList();

            foreach (var permiso in permisos)
            {
                PermisosRolActual.Add(new PermisoUI { NombreModulo = permiso, Habilitado = false });
            }
        }

        private void CargarPermisosRolSeleccionado()
        {
            if (!(lstRolesPermisos.SelectedItem is DataRowView rolRow))
            {
                foreach (var p in PermisosRolActual) p.Habilitado = false;
                return;
            }

            int rolId = Convert.ToInt32(rolRow["RolID"]);
            HashSet<string> permisosGuardados = global::SchettiniGestion.DatabaseService.GetPermisosNombresPorRol(rolId);
            foreach (var p in PermisosRolActual)
            {
                p.Habilitado = permisosGuardados.Contains(p.NombreModulo);
            }
        }

        private void Limpiar()
        {
            _usuarioIdSeleccionado = 0;
            txtNombreUsuario.Text = string.Empty;
            txtPassword.Password = string.Empty;
            cmbRolesUsuario.SelectedIndex = -1;
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
                ModernMessageBox.Show("Complete Nombre de Usuario, Contraseña y Rol.", "Atención", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            try
            {
                var row = (DataRowView)cmbRolesUsuario.SelectedItem;
                int rolId = Convert.ToInt32(row["RolID"]);
                string nombreRol = Convert.ToString(row["NombreRol"]);
                string hash = global::SchettiniGestion.PasswordHasher.HashPassword(txtPassword.Password);

                bool exito = global::SchettiniGestion.DatabaseService.GuardarUsuarioConHash(
                    _usuarioIdSeleccionado,
                    txtNombreUsuario.Text.Trim(),
                    hash,
                    rolId,
                    nombreRol);

                if (exito)
                {
                    ModernMessageBox.Show("Usuario guardado correctamente.", "Exito", MessageBoxButton.OK, MessageBoxImage.Information);
                    Limpiar();
                    CargarUsuarios();
                }
                else
                {
                    ModernMessageBox.Show("Error al guardar en base de datos.", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
            catch (Exception ex)
            {
                ModernMessageBox.Show("Error critico al guardar usuario: " + ex.Message, "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void btnEliminar_Click(object sender, RoutedEventArgs e)
        {
            if (_usuarioIdSeleccionado == 0) return;

            if (txtNombreUsuario.Text.Trim().Equals("admin", StringComparison.OrdinalIgnoreCase))
            {
                ModernMessageBox.Show("No se puede eliminar al super-administrador.", "Prohibido", MessageBoxButton.OK, MessageBoxImage.Stop);
                return;
            }

            if (ModernMessageBox.Show("¿Eliminar el usuario '" + txtNombreUsuario.Text + "'?", "Confirmar", MessageBoxButton.YesNo, MessageBoxImage.Warning) != MessageBoxResult.Yes)
            {
                return;
            }

            bool ok = global::SchettiniGestion.DatabaseService.EliminarUsuario(_usuarioIdSeleccionado);
            if (ok)
            {
                CargarUsuarios();
                Limpiar();
            }
            else
            {
                ModernMessageBox.Show("No se pudo eliminar el usuario.", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void dgvUsuarios_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (!(dgvUsuarios.SelectedItem is DataRowView row)) return;

            _usuarioIdSeleccionado = Convert.ToInt32(row["UsuarioID"]);
            txtNombreUsuario.Text = Convert.ToString(row["NombreUsuario"]);
            txtPassword.Password = string.Empty;
            btnEliminar.IsEnabled = true;

            int rolId = row["RolID"] == DBNull.Value ? 0 : Convert.ToInt32(row["RolID"]);
            SeleccionarRolEnControles(rolId);
        }

        private void SeleccionarRolEnControles(int rolId)
        {
            if (_rolesDataTable == null) return;

            for (int i = 0; i < _rolesDataTable.Rows.Count; i++)
            {
                if (Convert.ToInt32(_rolesDataTable.Rows[i]["RolID"]) == rolId)
                {
                    cmbRolesUsuario.SelectedIndex = i;
                    lstRolesPermisos.SelectedIndex = i;
                    break;
                }
            }

            CargarPermisosRolSeleccionado();
        }

        private void lstRolesPermisos_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (!IsLoaded) return;
            CargarPermisosRolSeleccionado();
        }

        private void btnGuardarPermisos_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (lstRolesPermisos.SelectedItem == null)
                {
                    ModernMessageBox.Show("Por favor, seleccione un rol primero.", "Atencion", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                int idRolSeleccionado = Convert.ToInt32(((DataRowView)lstRolesPermisos.SelectedItem)["RolID"]);

                if (lstPermisosDinamicos.ItemsSource is IEnumerable<PermisoUI> permisos)
                {
                    var permisosSeleccionados = permisos.Where(p => p.Habilitado).Select(p => p.NombreModulo).ToList();
                    string stringPermisos = string.Join(",", permisosSeleccionados);

                    bool exito = global::SchettiniGestion.DatabaseService.ActualizarPermisosRol(idRolSeleccionado, stringPermisos);

                    if (exito)
                    {
                        ModernMessageBox.Show("Permisos guardados correctamente en la base de datos.", "Exito", MessageBoxButton.OK, MessageBoxImage.Information);
                    }
                    else
                    {
                        ModernMessageBox.Show("Fallo al actualizar la base de datos. DatabaseService retorno false.", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                    }
                }
            }
            catch (Exception ex)
            {
                ModernMessageBox.Show("Error critico al guardar: " + ex.Message, "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
    }
}