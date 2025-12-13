using System;
using System.Data;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input; // Necesario para el evento KeyDown
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
                DataTable dt = DatabaseService.GetClientes();
                dgvClientes.ItemsSource = dt.DefaultView;
            }
            catch (Exception ex)
            {
                CustomMessageBox.Show($"Error al cargar clientes: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void ConfigurarGrilla()
        {
            // La grilla se autogenera, aquí podrías ajustar anchos si quisieras.
        }

        // --- MÉTODOS DE LA INTERFAZ ---

        private void LimpiarCampos()
        {
            _clienteIDSeleccionado = 0;
            txtCuit.Text = "";
            txtRazonSocial.Text = "";
            cmbCondicionIVA.SelectedIndex = 2; // "Consumidor Final"
            dgvClientes.SelectedItem = null;
            txtCuit.Focus(); // Pone el foco en el CUIT para empezar rápido
        }

        private void dgvClientes_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (dgvClientes.SelectedItem is DataRowView filaSeleccionada)
            {
                _clienteIDSeleccionado = Convert.ToInt32(filaSeleccionada["ClienteID"]);
                txtCuit.Text = filaSeleccionada["CUIT"].ToString();
                txtRazonSocial.Text = filaSeleccionada["RazonSocial"].ToString();
                cmbCondicionIVA.Text = filaSeleccionada["CondicionIVA"].ToString();
            }
        }

        // --- MAGIA DE AFIP: AUTOCOMPLETAR ---
        // Asegúrate de que en tu XAML el TextBox txtCuit tenga el evento: KeyDown="txtCuit_KeyDown"
        private async void txtCuit_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter)
            {
                string cuitTexto = txtCuit.Text.Replace("-", "").Trim();

                // Validación simple de longitud
                if (long.TryParse(cuitTexto, out long cuit))
                {
                    // Feedback visual: Ponemos el cursor en espera
                    this.Cursor = Cursors.Wait;

                    try
                    {
                        // Llamamos al servicio (Simulado o Real)
                        var datos = await AfipService.ObtenerDatosPersonaAsync(cuit);

                        if (datos.Exito)
                        {
                            // ¡Éxito! Llenamos los campos
                            txtRazonSocial.Text = datos.RazonSocial.ToUpper();

                            // Intentamos seleccionar la condición de IVA en el combo
                            // (Debe coincidir el texto exacto, ej: "Responsable Inscripto")
                            cmbCondicionIVA.Text = datos.CondicionIVA;

                            // Si tuvieras campo Dirección en la DB, iría aquí:
                            // txtDireccion.Text = datos.Domicilio; 

                            // Pasamos el foco al siguiente campo para que confirme
                            txtRazonSocial.Focus();
                        }
                        else
                        {
                            CustomMessageBox.Show("No se encontraron datos en AFIP o hubo un error: " + datos.Error, "Aviso AFIP", MessageBoxButton.OK, MessageBoxImage.Warning);
                        }
                    }
                    catch (Exception ex)
                    {
                        CustomMessageBox.Show("Error de conexión: " + ex.Message, "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                    }
                    finally
                    {
                        // Restauramos el cursor
                        this.Cursor = Cursors.Arrow;
                    }
                }
                else
                {
                    CustomMessageBox.Show("El CUIT ingresado no es válido (solo números).", "Formato Inválido", MessageBoxButton.OK, MessageBoxImage.Warning);
                }
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
                CustomMessageBox.Show("El CUIT y la Razón Social no pueden estar vacíos.", "Error", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }
            if (cmbCondicionIVA.SelectedItem == null)
            {
                CustomMessageBox.Show("Por favor, seleccione una Condición de IVA.", "Error", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            // 2. Obtener valores
            string cuit = txtCuit.Text.Trim();
            string razonSocial = txtRazonSocial.Text.Trim();
            string condicionIVA = cmbCondicionIVA.Text;

            // 3. Guardar en la DB
            bool exito = DatabaseService.GuardarCliente(_clienteIDSeleccionado, cuit, razonSocial, condicionIVA);

            if (exito)
            {
                CustomMessageBox.Show("Cliente guardado exitosamente.", "Éxito", MessageBoxButton.OK, MessageBoxImage.Information);
                CargarClientes();
                LimpiarCampos();
            }
        }

        private void btnEliminar_Click(object sender, RoutedEventArgs e)
        {
            if (_clienteIDSeleccionado == 0)
            {
                CustomMessageBox.Show("Por favor, seleccione un cliente de la grilla para eliminar.", "Error", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            MessageBoxResult confirmacion = CustomMessageBox.Show($"¿Está seguro de que desea eliminar al cliente '{txtRazonSocial.Text}'?",
                                                                "Confirmar eliminación",
                                                                MessageBoxButton.YesNo,
                                                                MessageBoxImage.Warning);

            if (confirmacion == MessageBoxResult.Yes)
            {
                bool exito = DatabaseService.EliminarCliente(_clienteIDSeleccionado);

                if (exito)
                {
                    CustomMessageBox.Show("Cliente eliminado exitosamente.", "Éxito", MessageBoxButton.OK, MessageBoxImage.Information);
                    CargarClientes();
                    LimpiarCampos();
                }
            }
        }
    }
}