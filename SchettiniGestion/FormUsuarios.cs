using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace SchettiniGestion
{
    public partial class FormUsuarios : Form
    {
        private int _usuarioIDSeleccionado = 0;

        public FormUsuarios()
        {
            InitializeComponent();
        }

        private void FormUsuarios_Load(object sender, EventArgs e)
        {
            CargarRoles();
            CargarUsuarios();
            ConfigurarGrilla();
            LimpiarCampos();
        }

        private void CargarRoles()
        {
            cmbRol.Items.Clear();
            cmbRol.Items.Add("Admin");
            cmbRol.Items.Add("Vendedor");
            cmbRol.SelectedIndex = 1;
        }

        private void CargarUsuarios()
        {
            DataTable dt = DatabaseService.GetUsuarios();
            dgvUsuarios.DataSource = dt;
        }

        private void ConfigurarGrilla()
        {
            dgvUsuarios.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill;
            dgvUsuarios.ReadOnly = true;
            dgvUsuarios.MultiSelect = false;
            dgvUsuarios.SelectionMode = DataGridViewSelectionMode.FullRowSelect;

            dgvUsuarios.Columns["UsuarioID"].HeaderText = "ID";
            dgvUsuarios.Columns["NombreUsuario"].HeaderText = "Nombre de Usuario";
            dgvUsuarios.Columns["Rol"].HeaderText = "Rol";
            dgvUsuarios.Columns["UsuarioID"].Visible = false;
        }

        private void dgvUsuarios_SelectionChanged(object sender, EventArgs e)
        {
            if (dgvUsuarios.SelectedRows.Count > 0)
            {
                DataGridViewRow filaSeleccionada = dgvUsuarios.SelectedRows[0];

                object idValue = filaSeleccionada.Cells["UsuarioID"].Value;

                if (idValue == null || idValue == DBNull.Value)
                {
                    _usuarioIDSeleccionado = 0;
                    txtUsuario.Text = "";
                    txtPassword.Text = "";
                    cmbRol.SelectedIndex = 1;
                    return;
                }

                _usuarioIDSeleccionado = Convert.ToInt32(idValue);

                txtUsuario.Text = filaSeleccionada.Cells["NombreUsuario"].Value.ToString();
                cmbRol.Text = filaSeleccionada.Cells["Rol"].Value.ToString();

                txtPassword.Text = "";
            }
        }

        private void btnNuevo_Click(object sender, EventArgs e)
        {
            LimpiarCampos();
        }

        private void btnGuardar_Click(object sender, EventArgs e)
        {
            if (string.IsNullOrWhiteSpace(txtUsuario.Text))
            {
                MessageBox.Show("El nombre de usuario no puede estar vacío.", "Error", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            string usuario = txtUsuario.Text.Trim();
            string password = txtPassword.Text;
            string rol = cmbRol.Text;

            bool exito = DatabaseService.GuardarUsuario(_usuarioIDSeleccionado, usuario, password, rol);

            if (exito)
            {
                MessageBox.Show("Usuario guardado exitosamente.", "Éxito", MessageBoxButtons.OK, MessageBoxIcon.Information);

                CargarUsuarios();
                LimpiarCampos();
            }
        }

        private void LimpiarCampos()
        {
            _usuarioIDSeleccionado = 0;
            txtUsuario.Text = "";
            txtPassword.Text = "";
            cmbRol.SelectedIndex = 1;

            dgvUsuarios.ClearSelection();
        }

        // --- ¡INICIO DEL CÓDIGO NUEVO (PASO 27)! ---
        private void btnEliminar_Click(object sender, EventArgs e)
        {
            // 1. Validar que haya un usuario seleccionado
            if (_usuarioIDSeleccionado == 0)
            {
                MessageBox.Show("Por favor, seleccione un usuario de la grilla para eliminar.", "Error", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            // 2. Validar que no se intente borrar al admin principal
            // Comparamos usando el nombre de usuario que es más seguro que el ID
            if (txtUsuario.Text.ToLower() == "admin")
            {
                MessageBox.Show("No se puede eliminar al usuario 'admin' principal.", "Acción no permitida", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }

            // 3. Pedir confirmación
            DialogResult confirmacion = MessageBox.Show($"¿Está seguro de que desea eliminar al usuario '{txtUsuario.Text}'?",
                                                      "Confirmar eliminación",
                                                      MessageBoxButtons.YesNo,
                                                      MessageBoxIcon.Warning);

            if (confirmacion == DialogResult.Yes)
            {
                // 4. Llamar al servicio para eliminar
                bool exito = DatabaseService.EliminarUsuario(_usuarioIDSeleccionado);

                if (exito)
                {
                    MessageBox.Show("Usuario eliminado exitosamente.", "Éxito", MessageBoxButtons.OK, MessageBoxIcon.Information);

                    // 5. Recargar y limpiar
                    CargarUsuarios();
                    LimpiarCampos();
                }
            }
        }
        // --- ¡FIN DEL CÓDIGO NUEVO (PASO 27)! ---
    }
}