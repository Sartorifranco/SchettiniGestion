using System.Windows;
using SchettiniGestion;

namespace SchettiniGestion.WPF
{
    public partial class ClienteRapidoModalWindow : Window
    {
        public int ClienteID { get; private set; } = 0;

        public ClienteRapidoModalWindow()
        {
            InitializeComponent();
        }

        public ClienteRapidoModalWindow(string textoInicial) : this()
        {
            Loaded += (s, e) =>
            {
                txtRazonSocial.Text = textoInicial ?? "";
                txtRazonSocial.CaretIndex = txtRazonSocial.Text.Length;
                txtRazonSocial.Focus();
            };
        }

        public ClienteRapidoModalWindow(object param) : this()
        {
            if (param is string s)
            {
                Loaded += (sender, e) => { txtRazonSocial.Text = s; };
            }
        }

        public ClienteRapidoModalWindow(object p1, object p2) : this() { }

        private void btnGuardar_Click(object sender, RoutedEventArgs e)
        {
            string razonSocial = txtRazonSocial.Text.Trim();
            if (string.IsNullOrWhiteSpace(razonSocial))
            {
                CustomMessageBox.Show("La razón social es obligatoria.", "Campo requerido", MessageBoxButton.OK, MessageBoxImage.Warning);
                txtRazonSocial.Focus();
                return;
            }

            string cuit = txtCuit.Text.Trim();
            string condIva = (cmbCondicionIVA.SelectedItem as System.Windows.Controls.ComboBoxItem)?.Content?.ToString() ?? "Consumidor Final";
            string telefono = txtTelefono.Text.Trim();
            string email = txtEmail.Text.Trim();
            string direccion = txtDireccion.Text.Trim();

            bool ok = DatabaseService.GuardarCliente(0, cuit, razonSocial, condIva, direccion, telefono, email, false, null);
            if (!ok)
            {
                CustomMessageBox.Show("Error al guardar el cliente. Verifique los datos.", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            // Recuperar el ID del cliente recién creado
            var row = DatabaseService.BuscarCliente(razonSocial);
            if (row != null)
                ClienteID = System.Convert.ToInt32(row["ClienteID"]);

            DialogResult = true;
            Close();
        }

        private void btnCancelar_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }
    }
}
