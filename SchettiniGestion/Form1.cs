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
    public partial class Form1 : Form
    {
        public Form1()
        {
            InitializeComponent();
        }

        private void button1_Click(object sender, EventArgs e)
        {
            // ESTO ES LO QUE TENÉS QUE PEGAR ACÁ DENTRO:

            bool tienePermiso = LicenseManager.IsModuleEnabled("PRESUPUESTOS");

            if (tienePermiso)
            {
                MessageBox.Show("¡PERMISO CONCEDIDO! Abriendo presupuestos...");
            }
            else
            {
                MessageBox.Show("ACCESO DENEGADO. Este módulo no está incluido en su licencia.", "Sin Permiso", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
        }

        private void salirToolStripMenuItem_Click(object sender, EventArgs e)
        {
            // Preguntamos al usuario si realmente quiere salir
            DialogResult dialogo = MessageBox.Show("¿Está seguro de que desea salir?",
                                                   "Confirmar salida",
                                                   MessageBoxButtons.YesNo,
                                                   MessageBoxIcon.Question);

            if (dialogo == DialogResult.Yes)
            {
                Application.Exit(); // Cierra toda la aplicación
            }
        }

        private void Form1_FormClosing(object sender, FormClosingEventArgs e)
        {
            // Si el Form1 se está cerrando, cerramos toda la aplicación.
            // Hacemos esto para evitar que el FormLogin quede "oculto"
            // si el usuario cierra la ventana principal con la X.

            // Validamos si el cierre fue por Application.Exit() para evitar bucles
            if (e.CloseReason == CloseReason.UserClosing)
            {
                // Simulamos un clic en nuestro botón Salir (que ya tiene la confirmación)
                salirToolStripMenuItem_Click(sender, e);

                // Cancelamos este cierre inmediato, ya que el botón Salir se encargará
                e.Cancel = true;
            }
        }

        private void Form1_Load(object sender, EventArgs e)
        {

        }

        private void administracionToolStripMenuItem_Click(object sender, EventArgs e)
        {

        }

        private void usuariosToolStripMenuItem_Click(object sender, EventArgs e)
        {
            // Creamos una instancia del formulario de usuarios
            FormUsuarios formUsuarios = new FormUsuarios();

            // Le decimos que su "Padre" MDI es este formulario (Form1)
            formUsuarios.MdiParent = this;

            // Mostramos el formulario
            formUsuarios.Show();
        }

        private void clientesToolStripMenuItem_Click(object sender, EventArgs e)
        {
            // Creamos una instancia del formulario de clientes
            FormClientes formClientes = new FormClientes();

            // Le decimos que su "Padre" MDI es este formulario (Form1)
            formClientes.MdiParent = this;

            // Mostramos el formulario
            formClientes.Show();
        }
    }
}
