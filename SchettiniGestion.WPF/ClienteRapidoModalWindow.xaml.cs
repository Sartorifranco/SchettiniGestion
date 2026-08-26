using System;
using System.Collections.Generic;
using System.Data;
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
            Loaded += (s, e) => CargarComboListas();
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

        private void CargarComboListas()
        {
            if (cmbListaPrecio == null) return;
            var items = new List<ComboLookupItem>
            {
                new ComboLookupItem { Id = 0, Nombre = "(Usar lista del POS)" }
            };
            try
            {
                var dt = DatabaseService.GetListasPrecios();
                if (dt != null)
                {
                    foreach (DataRow r in dt.Rows)
                    {
                        items.Add(new ComboLookupItem
                        {
                            Id = Convert.ToInt32(r["ListaID"]),
                            Nombre = r["Nombre"]?.ToString() ?? ""
                        });
                    }
                }
            }
            catch { }
            cmbListaPrecio.ItemsSource = items;
            cmbListaPrecio.SelectedValue = 0;
        }

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

            // Validar CUIT solo si fue ingresado (en cliente rápido puede ser opcional)
            if (!string.IsNullOrEmpty(cuit) && !EsCuitValido(cuit))
            {
                CustomMessageBox.Show(
                    "El CUIT ingresado no es válido. Verificá el dígito verificador.\n" +
                    "Si no tenés el CUIT, dejá el campo vacío o usá 00-00000000-0 para consumidor final.",
                    "CUIT inválido", MessageBoxButton.OK, MessageBoxImage.Warning);
                txtCuit.Focus();
                return;
            }

            string condIva = (cmbCondicionIVA.SelectedItem as System.Windows.Controls.ComboBoxItem)?.Content?.ToString() ?? "Consumidor Final";
            string telefono = txtTelefono.Text.Trim();
            string email = txtEmail.Text.Trim();
            string direccion = txtDireccion.Text.Trim();
            int? listaPrecioId = null;
            if (cmbListaPrecio?.SelectedValue != null)
            {
                int lid = Convert.ToInt32(cmbListaPrecio.SelectedValue);
                if (lid > 0) listaPrecioId = lid;
            }

            bool ok = DatabaseService.GuardarCliente(0, cuit, razonSocial, condIva, direccion, telefono, email, false, null, listaPrecioId);
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
    }
}
