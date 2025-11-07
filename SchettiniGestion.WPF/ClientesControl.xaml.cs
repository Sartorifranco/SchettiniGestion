using System;
using System.Data;
using System.Windows;
using System.Windows.Controls;
// --- ¡IMPORTANTE! Importamos la lógica de nuestro proyecto viejo ---
using SchettiniGestion;

namespace SchettiniGestion.WPF
{
    /// <summary>
    /// Lógica de interacción para ClientesControl.xaml
    /// </summary>
    public partial class ClientesControl : UserControl
    {
        // Guardaremos el ID del cliente seleccionado
        private int _clienteIDSeleccionado = 0;

        public ClientesControl()
        {
            InitializeComponent();
        }

        // --- MÉTODOS DE CARGA ---

        private void ClientesControl_Loaded(object sender, RoutedEventArgs e)
        {
            // Este evento se dispara cuando el control se carga
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
            cmbCondicionIVA.SelectedIndex = 2; // "Consumidor Final" por defecto
        }

        private void CargarClientes()
        {
            try
            {
                // Llamamos al DatabaseService (¡que ya funciona!)
                DataTable dt = DatabaseService.GetClientes();
                dgvClientes.ItemsSource = dt.DefaultView;
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error al cargar clientes: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void ConfigurarGrilla()
        {
            // Ocultamos el ID, ya no lo hacemos en XAML sino aquí
            // (La columna se genera sola por el ItemsSource, pero podemos acceder a ella)
            // Nota: Esto se maneja mejor con propiedades de binding, pero así es más simple
        }

        // --- MÉTODOS DE LA INTERFAZ ---

        private void LimpiarCampos()
        {
            _clienteIDSeleccionado = 0;
            txtCuit.Text = "";
            txtRazonSocial.Text = "";
            cmbCondicionIVA.SelectedIndex = 2; // "Consumidor Final"

            dgvClientes.SelectedItem = null;
        }

        private void dgvClientes_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            // Verificamos que haya una fila seleccionada
            if (dgvClientes.SelectedItem is DataRowView filaSeleccionada)
            {
                // Obtenemos los valores de la fila
                // IMPORTANTE: Accedemos a la columna por su NOMBRE en la base de datos
                _clienteIDSeleccionado = Convert.ToInt32(filaSeleccionada["ClienteID"]);
                txtCuit.Text = filaSeleccionada["CUIT"].ToString();
                txtRazonSocial.Text = filaSeleccionada["RazonSocial"].ToString();
                cmbCondicionIVA.Text = filaSeleccionada["CondicionIVA"].ToString();
            }
        }

        // --- LÓGICA DE BOTONES (ABM) ---

        private void btnNuevo_Click(object sender, RoutedEventArgs e)
        {
            LimpiarCampos();
        }

        private void btnGuardar_Click(object sender, RoutedEventArgs e)
        {
            // 1. Validaciones
            if (string.IsNullOrWhiteSpace(txtCuit.Text) || string.IsNullOrWhiteSpace(txtRazonSocial.Text))
            {
                MessageBox.Show("El CUIT y la Razón Social no pueden estar vacíos.", "Error", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }
            if (cmbCondicionIVA.SelectedItem == null)
            {
                MessageBox.Show("Por favor, seleccione una Condición de IVA.", "Error", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            // 2. Obtener valores
            string cuit = txtCuit.Text.Trim();
            string razonSocial = txtRazonSocial.Text.Trim();
            string condicionIVA = cmbCondicionIVA.Text;

            // 3. Guardar en la DB (usando nuestro DatabaseService)
            bool exito = DatabaseService.GuardarCliente(_clienteIDSeleccionado, cuit, razonSocial, condicionIVA);

            if (exito)
            {
                MessageBox.Show("Cliente guardado exitosamente.", "Éxito", MessageBoxButton.OK, MessageBoxImage.Information);
                CargarClientes();
                LimpiarCampos();
            }
            // (El DatabaseService ya muestra el MessageBox de error si falla)
        }

        private void btnEliminar_Click(object sender, RoutedEventArgs e)
        {
            // 1. Validar que haya un cliente seleccionado
            if (_clienteIDSeleccionado == 0)
            {
                MessageBox.Show("Por favor, seleccione un cliente de la grilla para eliminar.", "Error", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            // 2. Pedir confirmación
            MessageBoxResult confirmacion = MessageBox.Show($"¿Está seguro de que desea eliminar al cliente '{txtRazonSocial.Text}'?",
                                                            "Confirmar eliminación",
                                                            MessageBoxButton.YesNo,
                                                            MessageBoxImage.Warning);

            if (confirmacion == MessageBoxResult.Yes)
            {
                // 3. Eliminar de la DB (usando nuestro DatabaseService)
                bool exito = DatabaseService.EliminarCliente(_clienteIDSeleccionado);

                if (exito)
                {
                    MessageBox.Show("Cliente eliminado exitosamente.", "Éxito", MessageBoxButton.OK, MessageBoxImage.Information);
                    CargarClientes();
                    LimpiarCampos();
                }
            }
        }
    }
}