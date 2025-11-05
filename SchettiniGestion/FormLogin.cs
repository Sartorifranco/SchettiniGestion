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
    public partial class FormLogin : Form
    {
        public FormLogin()
        {
            InitializeComponent();
        }

        private void label1_Click(object sender, EventArgs e)
        {

        }

        private void btnSalir_Click(object sender, EventArgs e)
        {
            this.Close();
        }

        private void btnIngresar_Click(object sender, EventArgs e)
        {
            // 1. Obtenemos el texto de los TextBox
            string usuario = txtUsuario.Text;
            string contrasena = txtContrasena.Text;

            // 2. Validación simple de campos vacíos
            if (string.IsNullOrEmpty(usuario) || string.IsNullOrEmpty(contrasena))
            {
                MessageBox.Show("Por favor, ingrese usuario y contraseña.", "Error", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
                    }

            // --- ¡NUEVA LÓGICA DE VALIDACIÓN! ---
            // 3. Llamamos al DatabaseService para que verifique en la DB
            bool esValido = DatabaseService.ValidarUsuario(usuario, contrasena);

            if (esValido)
            {
                // ¡Login exitoso!
                // Ahora sí, abrimos el Formulario Principal
                Form1 formPrincipal = new Form1();
                formPrincipal.Show(); // Muestra el Form1

                this.Hide(); // Esconde este formulario de Login
            }
            else
            {
                // Login fallido
                MessageBox.Show("Usuario o contraseña incorrectos.", "Error de Login", MessageBoxButtons.OK, MessageBoxIcon.Error);
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
    }
}
