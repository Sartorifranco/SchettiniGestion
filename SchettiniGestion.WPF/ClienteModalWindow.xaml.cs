using System;
using System.Data;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using SchettiniGestion;

namespace SchettiniGestion.WPF
{
    public partial class ClienteModalWindow : Window
    {
        private readonly int _clienteId;
        private readonly Action _onGuardado;

        public ClienteModalWindow(int clienteId, Action onGuardado)
        {
            InitializeComponent();
            _clienteId = clienteId;
            _onGuardado = onGuardado;
            Loaded += ClienteModalWindow_Loaded;
        }

        private void ClienteModalWindow_Loaded(object sender, RoutedEventArgs e)
        {
            txtMontoLimiteCtaCte.IsEnabled = false;
            if (_clienteId > 0)
            {
                lblTitulo.Text = "Editar Cliente";
                btnEliminar.Visibility = Visibility.Visible;
                CargarDatos();
            }
            else
            {
                lblTitulo.Text = "Nuevo Cliente";
                btnEliminar.Visibility = Visibility.Collapsed;
            }
            txtCuit.Focus();
        }

        private void CargarDatos()
        {
            DataRow row = DatabaseService.BuscarClientePorID(_clienteId);
            if (row == null) return;

            txtCuit.Text = ValorCol(row, "CUIT");
            txtRazonSocial.Text = ValorCol(row, "RazonSocial");
            EstablecerCondicionIVA(ValorCol(row, "CondicionIVA"));
            txtTelefono.Text = ValorCol(row, "Telefono");
            txtDireccion.Text = ValorCol(row, "Direccion");
            txtEmail.Text = ValorCol(row, "Email");
            chkPermiteCtaCte.IsChecked = row.Table.Columns.Contains("PermiteCuentaCorriente") && row["PermiteCuentaCorriente"] != DBNull.Value && Convert.ToBoolean(row["PermiteCuentaCorriente"]);
            txtMontoLimiteCtaCte.Text = ValorCol(row, "MontoLimiteCtaCte");
            txtMontoLimiteCtaCte.IsEnabled = chkPermiteCtaCte.IsChecked == true;
        }

        private static string ValorCol(DataRow row, string col)
        {
            if (row?.Table == null || !row.Table.Columns.Contains(col)) return "";
            var o = row[col];
            return o == null || o == DBNull.Value ? "" : o.ToString();
        }

        private void EstablecerCondicionIVA(string valor)
        {
            if (string.IsNullOrWhiteSpace(valor)) { cmbCondicionIVA.SelectedIndex = 0; return; }
            var valorTrim = valor.Trim();
            for (int i = 0; i < cmbCondicionIVA.Items.Count; i++)
            {
                var item = cmbCondicionIVA.Items[i] as ComboBoxItem;
                if (item?.Content?.ToString()?.Equals(valorTrim, StringComparison.OrdinalIgnoreCase) == true)
                {
                    cmbCondicionIVA.SelectedIndex = i;
                    return;
                }
            }
            cmbCondicionIVA.Items.Add(new ComboBoxItem { Content = valorTrim });
            cmbCondicionIVA.SelectedIndex = cmbCondicionIVA.Items.Count - 1;
        }

        private void chkPermiteCtaCte_Changed(object sender, RoutedEventArgs e)
        {
            txtMontoLimiteCtaCte.IsEnabled = chkPermiteCtaCte.IsChecked == true;
        }

        private async void txtCuit_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key != Key.Enter || string.IsNullOrWhiteSpace(txtCuit.Text)) return;
            e.Handled = true;

            btnGuardar.IsEnabled = false;
            try
            {
                var persona = await AfipService.ObtenerPersonaPorCuitAsync(txtCuit.Text);
                if (persona.Exito)
                {
                    txtRazonSocial.Text = persona.RazonSocial;
                    EstablecerCondicionIVA(persona.CondicionIVA);
                    txtRazonSocial.Focus();
                }
                else
                {
                    CustomMessageBox.Show(persona.Error ?? "No se encontró el CUIT en ARCA. Ingrese los datos manualmente.", "ARCA", MessageBoxButton.OK, MessageBoxImage.Warning);
                }
            }
            catch (Exception ex)
            {
                CustomMessageBox.Show("Error al consultar ARCA: " + ex.Message + "\n\nIngrese los datos manualmente.", "ARCA", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
            finally
            {
                btnGuardar.IsEnabled = true;
            }
        }

        /// <summary>
        /// Valida el CUIT/CUIL argentino usando el algoritmo módulo-11 de ARCA.
        /// Acepta formatos con o sin guiones: 20-12345678-9 / 20123456789.
        /// </summary>
        private static bool EsCuitValido(string cuit)
        {
            string solo = System.Text.RegularExpressions.Regex.Replace(cuit ?? "", "[^0-9]", "");
            if (solo.Length != 11) return false;

            int[] pesos = { 5, 4, 3, 2, 7, 6, 5, 4, 3, 2 };
            int suma = 0;
            for (int i = 0; i < 10; i++)
                suma += (solo[i] - '0') * pesos[i];

            int resto = suma % 11;
            int digitoEsperado = resto == 0 ? 0 : resto == 1 ? 9 : 11 - resto;
            return (solo[10] - '0') == digitoEsperado;
        }

        private void btnGuardar_Click(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrWhiteSpace(txtCuit.Text) || string.IsNullOrWhiteSpace(txtRazonSocial.Text))
            {
                CustomMessageBox.Show("Complete CUIT y Razón Social.");
                return;
            }

            if (!EsCuitValido(txtCuit.Text))
            {
                CustomMessageBox.Show(
                    "El CUIT ingresado no es válido.\n\n" +
                    "Verificá que tenga 11 dígitos y que el dígito verificador sea correcto.\n" +
                    "Ejemplo: 20-12345678-9\n\n" +
                    "Si el cliente no tiene CUIT, usá 00-00000000-0 para consumidor final.",
                    "CUIT inválido", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            string condIva = (cmbCondicionIVA.SelectedItem as ComboBoxItem)?.Content?.ToString() ?? "";
            bool permiteCtaCte = chkPermiteCtaCte.IsChecked == true;
            decimal? montoLimite = null;
            if (permiteCtaCte && decimal.TryParse(txtMontoLimiteCtaCte.Text?.Replace(",", "."), System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out decimal ml))
                montoLimite = ml;

            if (DatabaseService.GuardarCliente(_clienteId, txtCuit.Text.Trim(), txtRazonSocial.Text.Trim(), condIva, txtDireccion.Text.Trim(), txtTelefono.Text.Trim(), txtEmail.Text.Trim(), permiteCtaCte, montoLimite))
            {
                _onGuardado?.Invoke();
                DialogResult = true;
                Close();
            }
            else
            {
                CustomMessageBox.Show("Error al guardar.");
            }
        }

        private void btnEliminar_Click(object sender, RoutedEventArgs e)
        {
            if (_clienteId == 0) return;
            if (CustomMessageBox.Show("¿Eliminar este cliente?", "Confirmar", MessageBoxButton.YesNo) == MessageBoxResult.Yes)
            {
                if (DatabaseService.EliminarCliente(_clienteId))
                {
                    _onGuardado?.Invoke();
                    DialogResult = true;
                    Close();
                }
                else
                {
                    CustomMessageBox.Show("No se pudo eliminar el cliente. Puede tener facturas o movimientos asociados.", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }

        private void btnCancelar_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }
    }
}
