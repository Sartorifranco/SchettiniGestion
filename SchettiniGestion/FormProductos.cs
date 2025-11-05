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
    public partial class FormProductos : Form
    {
        private int _productoIDSeleccionado = 0;

        public FormProductos()
        {
            InitializeComponent();
        }

        private void FormProductos_Load(object sender, EventArgs e)
        {
            ConfigurarControlesNumericos();
            CargarProductos();
            ConfigurarGrilla();
            LimpiarCampos();
        }

        private void ConfigurarControlesNumericos()
        {
            numPrecioVenta.DecimalPlaces = 2;
            numPrecioVenta.Maximum = 10000000;
            numPrecioVenta.Minimum = 0;
            numPrecioVenta.ThousandsSeparator = true;

            numStockActual.DecimalPlaces = 0;
            numStockActual.Maximum = 100000;
            numStockActual.Minimum = 0;
            numStockActual.ThousandsSeparator = true;
        }

        private void CargarProductos()
        {
            DataTable dt = DatabaseService.GetProductos();
            // --- ¡CAMBIO IMPORTANTE! ---
            // Usamos tu nombre de control "dvgProductos"
            dvgProductos.DataSource = dt;
        }

        private void ConfigurarGrilla()
        {
            // --- ¡CAMBIO IMPORTANTE! ---
            // Usamos tu nombre de control "dvgProductos"
            dvgProductos.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill;
            dvgProductos.ReadOnly = true;
            dvgProductos.MultiSelect = false;
            dvgProductos.SelectionMode = DataGridViewSelectionMode.FullRowSelect;

            dvgProductos.Columns["ProductoID"].HeaderText = "ID";
            dvgProductos.Columns["Codigo"].HeaderText = "Código";
            dvgProductos.Columns["Descripcion"].HeaderText = "Descripción";
            dvgProductos.Columns["PrecioVenta"].HeaderText = "Precio Venta";
            dvgProductos.Columns["StockActual"].HeaderText = "Stock";

            dvgProductos.Columns["ProductoID"].Visible = false;

            dvgProductos.Columns["Descripcion"].FillWeight = 150;
            dvgProductos.Columns["Codigo"].FillWeight = 80;
            dvgProductos.Columns["PrecioVenta"].FillWeight = 80;
            dvgProductos.Columns["StockActual"].FillWeight = 70;

            dvgProductos.Columns["PrecioVenta"].DefaultCellStyle.Format = "C2";
        }

        private void LimpiarCampos()
        {
            _productoIDSeleccionado = 0;
            txtCodigo.Text = "";
            txtDescripcion.Text = "";
            numPrecioVenta.Value = 0;
            numStockActual.Value = 0;

            // --- ¡CAMBIO IMPORTANTE! ---
            // Usamos tu nombre de control "dvgProductos"
            dvgProductos.ClearSelection();
        }

        private void dgvProductos_SelectionChanged(object sender, EventArgs e)
        {
            // --- ¡CAMBIO IMPORTANTE! ---
            // Usamos tu nombre de control "dvgProductos"
            if (dvgProductos.SelectedRows.Count > 0)
            {
                DataGridViewRow filaSeleccionada = dvgProductos.SelectedRows[0];

                object idValue = filaSeleccionada.Cells["ProductoID"].Value;
                if (idValue == null || idValue == DBNull.Value)
                {
                    LimpiarCampos();
                    return;
                }

                _productoIDSeleccionado = Convert.ToInt32(idValue);
                txtCodigo.Text = filaSeleccionada.Cells["Codigo"].Value.ToString();
                txtDescripcion.Text = filaSeleccionada.Cells["Descripcion"].Value.ToString();

                numPrecioVenta.Value = Convert.ToDecimal(filaSeleccionada.Cells["PrecioVenta"].Value);
                numStockActual.Value = Convert.ToInt32(filaSeleccionada.Cells["StockActual"].Value);
            }
        }

        private void btnNuevo_Click(object sender, EventArgs e)
        {
            LimpiarCampos();
        }

        private void btnGuardar_Click(object sender, EventArgs e)
        {
            if (string.IsNullOrWhiteSpace(txtDescripcion.Text))
            {
                MessageBox.Show("La descripción del producto no puede estar vacía.", "Error", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            string codigo = txtCodigo.Text.Trim();
            string descripcion = txtDescripcion.Text.Trim();
            decimal precio = numPrecioVenta.Value;
            int stock = (int)numStockActual.Value;

            bool exito = DatabaseService.GuardarProducto(_productoIDSeleccionado, codigo, descripcion, precio, stock);

            if (exito)
            {
                MessageBox.Show("Producto guardado exitosamente.", "Éxito", MessageBoxButtons.OK, MessageBoxIcon.Information);

                CargarProductos();
                LimpiarCampos();
            }
        }

        // --- ¡INICIO DEL CÓDIGO NUEVO (PASO 36)! ---
        // Este es el evento del botón "Eliminar" que conectaste en el Paso 1
        private void btnEliminar_Click(object sender, EventArgs e)
        {
            // 1. Validar que haya un producto seleccionado
            if (_productoIDSeleccionado == 0)
            {
                MessageBox.Show("Por favor, seleccione un producto de la grilla para eliminar.", "Error", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            // 2. Pedir confirmación
            DialogResult confirmacion = MessageBox.Show($"¿Está seguro de que desea eliminar el producto '{txtDescripcion.Text}'?",
                                                      "Confirmar eliminación",
                                                      MessageBoxButtons.YesNo,
                                                      MessageBoxIcon.Warning);

            if (confirmacion == DialogResult.Yes)
            {
                // 3. Llamar al servicio para eliminar
                bool exito = DatabaseService.EliminarProducto(_productoIDSeleccionado);

                if (exito)
                {
                    MessageBox.Show("Producto eliminado exitosamente.", "Éxito", MessageBoxButtons.OK, MessageBoxIcon.Information);

                    // 4. Recargar y limpiar
                    CargarProductos();
                    LimpiarCampos();
                }
            }
        }
        // --- ¡FIN DEL CÓDIGO NUEVO (PASO 36)! ---
    }
}

