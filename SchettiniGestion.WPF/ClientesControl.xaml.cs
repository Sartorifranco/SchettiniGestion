using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using SchettiniGestion;

namespace SchettiniGestion.WPF
{
    public partial class ClientesControl : UserControl
    {
        private int _clienteIdSeleccionado = 0;
        private List<ClienteListadoItem> _clientesTodos = new List<ClienteListadoItem>();

        public ClientesControl()
        {
            InitializeComponent();
        }

        private void ClientesControl_Loaded(object sender, RoutedEventArgs e)
        {
            CargarClientes();
            LimpiarCampos();
        }

        private void CargarClientes()
        {
            try
            {
                _clientesTodos = DatabaseService.GetClientesLista() ?? new List<ClienteListadoItem>();
                AplicarFiltro();
            }
            catch (Exception ex)
            {
                ModernMessageBox.Show("Error al cargar clientes: " + ex.Message);
            }
        }

        private void AplicarFiltro()
        {
            if (_clientesTodos == null)
            {
                dgvClientes.ItemsSource = null;
                return;
            }

            string t = (txtFiltroClientes?.Text ?? "").Trim();
            IEnumerable<ClienteListadoItem> q = _clientesTodos;
            if (!string.IsNullOrEmpty(t))
            {
                q = q.Where(c =>
                    (c.RazonSocial ?? "").IndexOf(t, StringComparison.OrdinalIgnoreCase) >= 0
                    || (c.CUIT ?? "").IndexOf(t, StringComparison.OrdinalIgnoreCase) >= 0);
            }

            dgvClientes.ItemsSource = q.ToList();
        }

        private void txtFiltroClientes_TextChanged(object sender, TextChangedEventArgs e)
        {
            AplicarFiltro();
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
            btnNuevo.IsEnabled = false;
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
                    ModernMessageBox.Show(persona.Error ?? "No se encontró el CUIT en AFIP. Ingrese los datos manualmente.", "AFIP", MessageBoxButton.OK, MessageBoxImage.Warning);
                }
            }
            catch (Exception ex)
            {
                ModernMessageBox.Show("Error al consultar AFIP: " + ex.Message + "\n\nIngrese los datos manualmente.", "AFIP", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
            finally
            {
                btnGuardar.IsEnabled = true;
                btnNuevo.IsEnabled = true;
            }
        }

        private void LimpiarCampos()
        {
            _clienteIdSeleccionado = 0;
            txtCuit.Text = "";
            txtRazonSocial.Text = "";
            cmbCondicionIVA.SelectedIndex = 0;
            txtTelefono.Text = "";
            txtDireccion.Text = "";
            txtEmail.Text = "";
            chkPermiteCtaCte.IsChecked = false;
            txtMontoLimiteCtaCte.Text = "";
            txtMontoLimiteCtaCte.IsEnabled = false;
            btnEliminar.IsEnabled = false;
            txtCuit.Focus();
        }

        private void btnNuevo_Click(object sender, RoutedEventArgs e)
        {
            LimpiarCampos();
        }

        private void btnGuardar_Click(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrWhiteSpace(txtCuit.Text) || string.IsNullOrWhiteSpace(txtRazonSocial.Text))
            {
                ModernMessageBox.Show("Complete CUIT y Razón Social.");
                return;
            }

            string condIva = (cmbCondicionIVA.SelectedItem as ComboBoxItem)?.Content?.ToString() ?? "";
            bool permiteCtaCte = chkPermiteCtaCte.IsChecked == true;
            decimal? montoLimite = null;
            if (permiteCtaCte && decimal.TryParse(txtMontoLimiteCtaCte.Text?.Replace(",", "."), System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out decimal ml))
                montoLimite = ml;

            if (DatabaseService.GuardarCliente(_clienteIdSeleccionado, txtCuit.Text.Trim(), txtRazonSocial.Text.Trim(), condIva, txtDireccion.Text.Trim(), txtTelefono.Text.Trim(), txtEmail.Text.Trim(), permiteCtaCte, montoLimite))
            {
                ModernMessageBox.Show("Cliente guardado.");
                CargarClientes();
                LimpiarCampos();
            }
            else
            {
                ModernMessageBox.Show("Error al guardar.");
            }
        }

        private void btnEliminar_Click(object sender, RoutedEventArgs e)
        {
            if (_clienteIdSeleccionado != 0 && ModernMessageBox.Show("¿Eliminar?", "Confirmar", MessageBoxButton.YesNo) == MessageBoxResult.Yes)
            {
                DatabaseService.EliminarCliente(_clienteIdSeleccionado);
                CargarClientes();
                LimpiarCampos();
            }
        }

        private void dgvClientes_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (!(dgvClientes.SelectedItem is ClienteListadoItem item))
                return;

            _clienteIdSeleccionado = item.ClienteID;
            DataRow row = DatabaseService.BuscarClientePorID(item.ClienteID);
            if (row == null)
            {
                txtCuit.Text = item.CUIT ?? "";
                txtRazonSocial.Text = item.RazonSocial ?? "";
                EstablecerCondicionIVA(item.CondicionIVA ?? "");
                txtTelefono.Text = item.Telefono ?? "";
                txtDireccion.Text = "";
                txtEmail.Text = item.Email ?? "";
                chkPermiteCtaCte.IsChecked = false;
                txtMontoLimiteCtaCte.Text = "";
                txtMontoLimiteCtaCte.IsEnabled = false;
                btnEliminar.IsEnabled = true;
                return;
            }

            txtCuit.Text = ValorCol(row, "CUIT");
            txtRazonSocial.Text = ValorCol(row, "RazonSocial");
            EstablecerCondicionIVA(ValorCol(row, "CondicionIVA"));
            txtTelefono.Text = ValorCol(row, "Telefono");
            txtDireccion.Text = ValorCol(row, "Direccion");
            txtEmail.Text = ValorCol(row, "Email");
            chkPermiteCtaCte.IsChecked = row.Table.Columns.Contains("PermiteCuentaCorriente") && row["PermiteCuentaCorriente"] != DBNull.Value && Convert.ToBoolean(row["PermiteCuentaCorriente"]);
            txtMontoLimiteCtaCte.Text = ValorCol(row, "MontoLimiteCtaCte");
            txtMontoLimiteCtaCte.IsEnabled = chkPermiteCtaCte.IsChecked == true;
            btnEliminar.IsEnabled = true;
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
    }
}
