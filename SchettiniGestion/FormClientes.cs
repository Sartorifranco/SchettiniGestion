using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data; // <-- ¡IMPORTANTE AGREGAR ESTE!
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace SchettiniGestion
{
    public partial class FormClientes : Form
    {
        private int _clienteIDSeleccionado = 0;

        public FormClientes()
        {
            InitializeComponent();
        }

        private void FormClientes_Load(object sender, EventArgs e)
        {
            CargarCondicionIVA();
            CargarClientes();
            ConfigurarGrilla();
            LimpiarCampos();
        }

        private void CargarCondicionIVA()
        {
            cmbCondicionIVA.Items.Clear();
            cmbCondicionIVA.Items.Add("Responsable Inscripto");
            cmbCondicionIVA.Items.Add("Monotributo");
            cmbCondicionIVA.Items.Add("Consumidor Final");
            cmbCondicionIVA.Items.Add("Exento");
            cmbCondicionIVA.SelectedIndex = 2; // Default "Consumidor Final"
        }

        private void CargarClientes()
        {
            DataTable dt = DatabaseService.GetClientes();
            dgvClientes.DataSource = dt;
        }

        private void ConfigurarGrilla()
        {
            dgvClientes.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill;
            dgvClientes.ReadOnly = true;
            dgvClientes.MultiSelect = false;
            dgvClientes.SelectionMode = DataGridViewSelectionMode.FullRowSelect;

            dgvClientes.Columns["ClienteID"].HeaderText = "ID";
            dgvClientes.Columns["CUIT"].HeaderText = "CUIT";
            dgvClientes.Columns["RazonSocial"].HeaderText = "Razón Social";
            dgvClientes.Columns["CondicionIVA"].HeaderText = "Condición IVA";
            dgvClientes.Columns["ClienteID"].Visible = false;
            dgvClientes.Columns["RazonSocial"].FillWeight = 200;
        }

        private void dgvClientes_SelectionChanged(object sender, EventArgs e)
        {
            if (dgvClientes.SelectedRows.Count > 0)
            {
                DataGridViewRow filaSeleccionada = dgvClientes.SelectedRows[0];

                object idValue = filaSeleccionada.Cells["ClienteID"].Value;

                if (idValue == null || idValue == DBNull.Value)
                {
                    _clienteIDSeleccionado = 0;
                    txtCuit.Text = "";
                    txtRazonSocial.Text = "";
                    cmbCondicionIVA.SelectedIndex = 2;
                    return;
                }

                _clienteIDSeleccionado = Convert.ToInt32(idValue);

                txtCuit.Text = filaSeleccionada.Cells["CUIT"].Value.ToString();
                txtRazonSocial.Text = filaSeleccionada.Cells["RazonSocial"].Value.ToString();
                cmbCondicionIVA.Text = filaSeleccionada.Cells["CondicionIVA"].Value.ToString();
            }
        }

        private void LimpiarCampos()
        {
            _clienteIDSeleccionado = 0;
            txtCuit.Text = "";
            txtRazonSocial.Text = "";
            cmbCondicionIVA.SelectedIndex = 2;

            dgvClientes.ClearSelection();
        }

        private void btnNuevo_Click(object sender, EventArgs e)
        {
            LimpiarCampos();
        }

        private void btnGuardar_Click(object sender, EventArgs e)
        {
            if (string.IsNullOrWhiteSpace(txtCuit.Text) || string.IsNullOrWhiteSpace(txtRazonSocial.Text))
            {
                MessageBox.Show("El CUIT y la Razón Social no pueden estar vacíos.", "Error", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            string cuit = txtCuit.Text.Trim();
            string razonSocial = txtRazonSocial.Text.Trim();
            string condicionIVA = cmbCondicionIVA.Text;

            bool exito = DatabaseService.GuardarCliente(_clienteIDSeleccionado, cuit, razonSocial, condicionIVA);

            if (exito)
            {
                MessageBox.Show("Cliente guardado exitosamente.", "Éxito", MessageBoxButtons.OK, MessageBoxIcon.Information);

                CargarClientes();
                LimpiarCampos();
            }
        }

        private void btnEliminar_Click(object sender, EventArgs e)
        {
            // 1. Validar que haya un cliente seleccionado
            if (_clienteIDSeleccionado == 0)
            {
                MessageBox.Show("Por favor, seleccione un cliente de la grilla para eliminar.", "Error", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            // 2. Pedir confirmación
            DialogResult confirmacion = MessageBox.Show($"¿Está seguro de que desea eliminar al cliente '{txtRazonSocial.Text}'?",
                                                      "Confirmar eliminación",
                                                      MessageBoxButtons.YesNo,
                                                      MessageBoxIcon.Warning);

            if (confirmacion == DialogResult.Yes)
            {
                // 3. Llamar al servicio para eliminar
                bool exito = DatabaseService.EliminarCliente(_clienteIDSeleccionado);

                if (exito)
                {
                    MessageBox.Show("Cliente eliminado exitosamente.", "Éxito", MessageBoxButtons.OK, MessageBoxIcon.Information);

                    // 4. Recargar y limpiar
                    CargarClientes();
                    LimpiarCampos();
                }
            }
        }
    }
}

