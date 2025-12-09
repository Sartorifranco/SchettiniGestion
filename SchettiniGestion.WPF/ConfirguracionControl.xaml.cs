using Microsoft.Win32;
using System;
using System.Data;
using System.Windows;
using System.Windows.Controls;
using SchettiniGestion; // Importante para acceder a LicenseManager y DatabaseService

namespace SchettiniGestion.WPF
{
    public partial class ConfiguracionControl : UserControl
    {
        public ConfiguracionControl()
        {
            InitializeComponent();
        }

        private void ConfiguracionControl_Loaded(object sender, RoutedEventArgs e)
        {
            CargarDatosNegocio();
            CargarDatosConexion();
            CargarDatosLicencia(); // Cargar la nueva pestaña
        }

        // --- PESTAÑA 1: NEGOCIO ---
        private void CargarDatosNegocio()
        {
            DataRow dr = DatabaseService.GetConfiguracion();
            if (dr != null)
            {
                txtNombreFantasia.Text = dr["NombreFantasia"].ToString();
                txtRazonSocial.Text = dr["RazonSocial"].ToString();
                txtCuit.Text = dr["CUIT"].ToString();
                txtDireccion.Text = dr["Direccion"].ToString();
                txtTelefono.Text = dr["Telefono"].ToString();
                txtEmail.Text = dr["Email"].ToString();
                txtCertificadoPath.Text = dr["CertificadoPath"].ToString();
                txtPasswordAfip.Password = dr["PasswordAfip"].ToString();
                txtPuntoVenta.Text = dr["PuntoVenta"].ToString();

                txtMPToken.Text = dr["MPAccessToken"].ToString();
                txtMPUserId.Text = dr["MPUserId"].ToString();
                txtMPPosId.Text = dr["MPPosId"].ToString();
            }
        }

        private void btnBuscarCertificado_Click(object sender, RoutedEventArgs e)
        {
            OpenFileDialog ofd = new OpenFileDialog();
            ofd.Filter = "Certificados PFX|*.pfx";
            if (ofd.ShowDialog() == true)
            {
                txtCertificadoPath.Text = ofd.FileName;
            }
        }

        private void btnGuardar_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                int pto = 0;
                int.TryParse(txtPuntoVenta.Text, out pto);

                bool exito = DatabaseService.GuardarConfiguracion(
                    txtNombreFantasia.Text,
                    txtRazonSocial.Text,
                    txtCuit.Text,
                    txtDireccion.Text,
                    txtTelefono.Text,
                    txtEmail.Text,
                    "", // LogoPath (pendiente)
                    txtCertificadoPath.Text,
                    txtPasswordAfip.Password,
                    pto,
                    txtMPToken.Text,
                    txtMPUserId.Text,
                    txtMPPosId.Text,
                    true // UsaVisor
                );

                if (exito) MessageBox.Show("¡Datos del negocio guardados correctamente!");
                else MessageBox.Show("Error al guardar datos.");
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message);
            }
        }

        // --- PESTAÑA 2: RED / SERVIDOR ---
        private void CargarDatosConexion()
        {
            try
            {
                var datos = DatabaseService.GetDatosConexionActual();

                txtIpServidor.Text = datos["Servidor"];
                txtPuertoServidor.Text = datos["Puerto"];
                txtUsuarioSQL.Text = datos["Usuario"];
                txtPasswordSQL.Password = datos["Password"];

                string ip = txtIpServidor.Text.Trim().ToLower();
                if (ip == "." || ip == "127.0.0.1" || ip == "localhost")
                {
                    cmbModoPC.SelectedIndex = 0; // Servidor
                }
                else
                {
                    cmbModoPC.SelectedIndex = 1; // Cliente
                }
            }
            catch { }
        }

        private void cmbModoPC_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (txtIpServidor == null) return;

            if (cmbModoPC.SelectedIndex == 0) // Servidor
            {
                txtIpServidor.Text = "127.0.0.1";
                txtIpServidor.IsEnabled = false;
            }
            else // Cliente
            {
                if (txtIpServidor.Text == "127.0.0.1") txtIpServidor.Text = "";
                txtIpServidor.IsEnabled = true;
                txtIpServidor.Focus();
            }
        }

        private void btnGuardarConexion_Click(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrWhiteSpace(txtIpServidor.Text))
            {
                MessageBox.Show("Por favor, ingrese una Dirección IP válida.");
                return;
            }

            if (MessageBox.Show("Al guardar la configuración de red, el sistema se cerrará para aplicar los cambios.\n\n¿Desea continuar?", "Confirmar Reinicio", MessageBoxButton.YesNo, MessageBoxImage.Warning) == MessageBoxResult.Yes)
            {
                bool exito = DatabaseService.GuardarNuevaConexion(
                    txtIpServidor.Text.Trim(),
                    txtPuertoServidor.Text.Trim(),
                    txtUsuarioSQL.Text.Trim(),
                    txtPasswordSQL.Password
                );

                if (exito)
                {
                    MessageBox.Show("Configuración guardada.\nEl sistema se cerrará ahora.", "Éxito", MessageBoxButton.OK, MessageBoxImage.Information);
                    Application.Current.Shutdown();
                }
                else
                {
                    MessageBox.Show("Hubo un error al intentar guardar en App.config. Verifique permisos.", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }

        // --- PESTAÑA 3: LICENCIA (NUEVA) ---
        private void CargarDatosLicencia()
        {
            try
            {
                string keyActual = DatabaseService.ObtenerStringLicencia();
                txtLicenciaKey.Text = keyActual;

                if (LicenseManager.ValidarLicencia())
                {
                    lblEstadoLicencia.Text = "Licencia Válida y Activa";
                    lblEstadoLicencia.Foreground = System.Windows.Media.Brushes.LimeGreen;
                    lblVencimiento.Text = "Vence: " + LicenseManager.ObtenerFechaVencimiento();
                }
                else
                {
                    lblEstadoLicencia.Text = "Licencia Inválida o Expirada";
                    lblEstadoLicencia.Foreground = System.Windows.Media.Brushes.Red;
                    lblVencimiento.Text = "-";
                }
            }
            catch { }
        }

        private void btnActivarLicencia_Click(object sender, RoutedEventArgs e)
        {
            string nuevaKey = txtLicenciaKey.Text.Trim();
            if (string.IsNullOrEmpty(nuevaKey)) return;

            // Guardar en DB
            if (DatabaseService.GuardarNuevaLicencia(nuevaKey))
            {
                // Validar inmediatamente
                if (LicenseManager.ValidarLicencia())
                {
                    MessageBox.Show("¡Licencia activada correctamente!\n\nPor favor, reinicie el sistema para aplicar los cambios en los módulos.", "Activación Exitosa", MessageBoxButton.OK, MessageBoxImage.Information);
                    Application.Current.Shutdown();
                }
                else
                {
                    MessageBox.Show("La licencia se guardó pero parece ser INVÁLIDA o está vencida.", "Advertencia", MessageBoxButton.OK, MessageBoxImage.Warning);
                    CargarDatosLicencia(); // Refrescar visualmente para mostrar el error
                }
            }
            else
            {
                MessageBox.Show("Error al guardar la licencia en la base de datos.", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
    }
}