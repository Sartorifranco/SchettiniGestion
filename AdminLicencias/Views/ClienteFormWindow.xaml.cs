using AdminLicencias.Models;
using System;
using System.Windows;
using System.Windows.Controls;

namespace AdminLicencias.Views
{
    public partial class ClienteFormWindow : Window
    {
        public Cliente ClienteResultado { get; private set; }
        private readonly Cliente _original;

        public ClienteFormWindow(Cliente clienteEditar)
        {
            InitializeComponent();
            _original = clienteEditar;

            if (clienteEditar != null)
            {
                lblTitulo.Text    = "Editar Cliente";
                txtRazon.Text     = clienteEditar.RazonSocial;
                txtCUIT.Text      = clienteEditar.CUIT;
                txtContacto.Text  = clienteEditar.Contacto;
                txtTelefono.Text  = clienteEditar.Telefono;
                txtEmail.Text     = clienteEditar.Email;
                txtCiudad.Text    = clienteEditar.Ciudad;
                txtProvincia.Text = clienteEditar.Provincia;
                txtIP.Text        = clienteEditar.IPServidor;
                txtPuerto.Text    = clienteEditar.PuertoServidor.ToString();
                txtPuestos.Text   = clienteEditar.CantidadPuestos.ToString();
                txtNotas.Text     = clienteEditar.Notas;

                foreach (ComboBoxItem item in cbCanal.Items)
                    if (item.Content?.ToString() == clienteEditar.CanalContacto)
                        item.IsSelected = true;
            }
        }

        private void Guardar_Click(object s, RoutedEventArgs e)
        {
            if (string.IsNullOrWhiteSpace(txtRazon.Text))
            {
                MessageBox.Show("La Razón Social es obligatoria.", "Validación",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            var c = _original ?? new Cliente { Id = Guid.NewGuid(), FechaAlta = DateTime.Today };
            c.RazonSocial     = txtRazon.Text.Trim();
            c.CUIT            = txtCUIT.Text.Trim();
            c.Contacto        = txtContacto.Text.Trim();
            c.Telefono        = txtTelefono.Text.Trim();
            c.Email           = txtEmail.Text.Trim();
            c.Ciudad          = txtCiudad.Text.Trim();
            c.Provincia       = txtProvincia.Text.Trim();
            c.IPServidor      = txtIP.Text.Trim();
            c.CanalContacto   = (cbCanal.SelectedItem as ComboBoxItem)?.Content?.ToString() ?? "WhatsApp";
            c.Notas           = txtNotas.Text.Trim();

            if (int.TryParse(txtPuerto.Text,   out int p))  c.PuertoServidor    = p;
            if (int.TryParse(txtPuestos.Text,  out int ps)) c.CantidadPuestos   = ps;

            ClienteResultado = c;
            DialogResult = true;
        }

        private void Cancelar_Click(object s, RoutedEventArgs e)
            => DialogResult = false;
    }
}
