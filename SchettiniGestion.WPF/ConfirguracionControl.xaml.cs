using SchettiniGestion;
using System;
using System.Data;
using System.IO; // Para manejar archivos
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media.Imaging;
using Microsoft.Win32;

namespace SchettiniGestion.WPF
{
    public partial class ConfiguracionControl : UserControl
    {
        private string _logoPath = "";
        private string _certificadoPath = ""; // Ruta final segura

        public ConfiguracionControl()
        {
            InitializeComponent();
        }

        private void UserControl_Loaded(object sender, RoutedEventArgs e)
        {
            CargarDatos();
        }

        private void CargarDatos()
        {
            DataRow row = DatabaseService.GetConfiguracion();
            if (row != null)
            {
                // Datos Generales
                txtNombre.Text = row["NombreFantasia"].ToString();
                txtRazonSocial.Text = row["RazonSocial"].ToString();
                txtCuit.Text = row["CUIT"].ToString();
                txtDireccion.Text = row["Direccion"].ToString();
                txtTelefono.Text = row["Telefono"].ToString();

                // Logo
                string pathLogo = row["LogoPath"].ToString();
                if (!string.IsNullOrEmpty(pathLogo))
                {
                    _logoPath = pathLogo;
                    try { imgLogo.Source = new BitmapImage(new Uri(pathLogo)); } catch { }
                }

                // Datos Fiscales (Verificar si existen las columnas por si la BD es vieja)
                if (row.Table.Columns.Contains("PuntoVenta") && row["PuntoVenta"] != DBNull.Value)
                    numPuntoVenta.Value = Convert.ToInt32(row["PuntoVenta"]);

                if (row.Table.Columns.Contains("CertificadoPath"))
                {
                    _certificadoPath = row["CertificadoPath"].ToString();
                    txtCertificadoPath.Text = _certificadoPath;
                }

                if (row.Table.Columns.Contains("PasswordAfip"))
                    txtPasswordAfip.Password = row["PasswordAfip"].ToString();
            }
        }

        private void btnSeleccionarLogo_Click(object sender, RoutedEventArgs e)
        {
            OpenFileDialog dlg = new OpenFileDialog { Filter = "Imágenes|*.jpg;*.png;*.jpeg" };
            if (dlg.ShowDialog() == true)
            {
                _logoPath = dlg.FileName;
                imgLogo.Source = new BitmapImage(new Uri(_logoPath));
            }
        }

        // --- LÓGICA SEGURA DE CERTIFICADO ---
        private void btnBuscarCertificado_Click(object sender, RoutedEventArgs e)
        {
            OpenFileDialog dlg = new OpenFileDialog { Filter = "Certificado Digital|*.pfx;*.p12" };

            if (dlg.ShowDialog() == true)
            {
                try
                {
                    string origen = dlg.FileName;
                    string appData = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "SchettiniGestion_NUEVO", "Certificados");

                    if (!Directory.Exists(appData)) Directory.CreateDirectory(appData);

                    string destino = Path.Combine(appData, "certificado_afip.pfx");

                    // Copiamos el archivo a nuestra carpeta segura
                    File.Copy(origen, destino, true);

                    _certificadoPath = destino;
                    txtCertificadoPath.Text = destino;

                    CustomMessageBox.Show("Certificado copiado al sistema correctamente.", "Seguridad", MessageBoxButton.OK, MessageBoxImage.Information);
                }
                catch (Exception ex)
                {
                    CustomMessageBox.Show($"Error al copiar certificado: {ex.Message}");
                }
            }
        }

        private void btnGuardar_Click(object sender, RoutedEventArgs e)
        {
            // Validar datos mínimos si queremos facturar
            // (Aquí podrías validar CUIT)

            bool exito = DatabaseService.GuardarConfiguracion(
                txtNombre.Text,
                txtRazonSocial.Text,
                txtCuit.Text,
                txtDireccion.Text,
                txtTelefono.Text,
                "", // Email
                _logoPath,
                _certificadoPath,       // Nuevo
                txtPasswordAfip.Password, // Nuevo
                numPuntoVenta.Value ?? 1  // Nuevo
            );

            if (exito)
            {
                CustomMessageBox.Show("Configuración guardada correctamente.", "Éxito", MessageBoxButton.OK, MessageBoxImage.Information);
            }
        }
    }
}