using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.Data;
using System.Drawing.Printing;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using SchettiniGestion;

namespace SchettiniGestion.WPF
{
    public partial class ConfiguracionControl : UserControl
    {
        /// <summary>Valores válidos para la columna Tipo del DataGrid de medios de pago.</summary>
        public string[] TiposMedioPago { get; } = { "Efectivo", "Tarjeta", "Transferencia" };

        private bool _hayPasswordAfipGuardadaEnBd;
        private bool _passwordAfipTocadoPorUsuario;
        private bool _suprimirEventoPasswordAfip;
        private string _logoPathActual = "";

        public ConfiguracionControl()
        {
            InitializeComponent();
        }

        private void ConfiguracionControl_Loaded(object sender, RoutedEventArgs e)
        {
            CargarDatosNegocio();
            CargarDatosConexion();
            CargarDatosLicencia();
            CargarMediosPago();
            AplicarVisibilidadCredencialesSQL();
            AplicarVisibilidadSegunLicencia();
            CargarImpresoras();
        }

        private void AplicarVisibilidadSegunLicencia()
        {
            bool afip = LicenseManager.TieneAfip();
            bool mp = LicenseManager.TieneMercadoPagoQr();
            bool visor = LicenseManager.TieneVisorCliente();
            bool red = LicenseManager.TieneConexionRed();

            if (panelSeccionAfip != null)
                panelSeccionAfip.Visibility = afip ? Visibility.Visible : Visibility.Collapsed;
            if (panelSeccionMercadoPago != null)
                panelSeccionMercadoPago.Visibility = mp ? Visibility.Visible : Visibility.Collapsed;
            if (panelSeccionVisorCliente != null)
                panelSeccionVisorCliente.Visibility = visor ? Visibility.Visible : Visibility.Collapsed;
            if (tabItemRedServidor != null)
                tabItemRedServidor.Visibility = red ? Visibility.Visible : Visibility.Collapsed;
        }

        // --- LOGO ---
        private void MostrarPreviewLogo(string ruta)
        {
            if (!string.IsNullOrWhiteSpace(ruta) && System.IO.File.Exists(ruta))
            {
                try
                {
                    var bi = new System.Windows.Media.Imaging.BitmapImage();
                    bi.BeginInit();
                    bi.UriSource = new Uri(ruta, UriKind.Absolute);
                    bi.CacheOption = System.Windows.Media.Imaging.BitmapCacheOption.OnLoad;
                    bi.EndInit();
                    imgLogoPreview.Source = bi;
                    imgLogoPreview.Visibility = Visibility.Visible;
                    lblLogoPlaceholder.Visibility = Visibility.Collapsed;
                    lblLogoRuta.Text = ruta;
                    return;
                }
                catch { }
            }
            imgLogoPreview.Source = null;
            imgLogoPreview.Visibility = Visibility.Collapsed;
            lblLogoPlaceholder.Visibility = Visibility.Visible;
            lblLogoRuta.Text = "";
        }

        private void btnCargarLogo_Click(object sender, RoutedEventArgs e)
        {
            var ofd = new Microsoft.Win32.OpenFileDialog
            {
                Title  = "Seleccionar logo del negocio",
                Filter = "Imágenes (*.png;*.jpg;*.jpeg;*.bmp)|*.png;*.jpg;*.jpeg;*.bmp"
            };
            if (ofd.ShowDialog() != true) return;

            // Copiar el logo a ProgramData\SCHPOS (escribible sin ser administrador)
            string ext = System.IO.Path.GetExtension(ofd.FileName).ToLower();
            string destino = System.IO.Path.Combine(DatabaseService.AsegurarCarpetaDatosSchpos(), "logo_empresa" + ext);
            try
            {
                System.IO.File.Copy(ofd.FileName, destino, overwrite: true);
                _logoPathActual = destino;
                MostrarPreviewLogo(destino);
            }
            catch (Exception ex)
            {
                ModernMessageBox.Show("No se pudo copiar el logo: " + ex.Message);
            }
        }

        private void btnQuitarLogo_Click(object sender, RoutedEventArgs e)
        {
            _logoPathActual = "";
            MostrarPreviewLogo("");
        }

        // --- PESTAÑA 1: NEGOCIO ---
        private void CargarDatosNegocio()
        {
            DataRow dr = DatabaseService.GetConfiguracion();
            if (dr != null)
            {
                txtNombreFantasia.Text = dr["NombreFantasia"].ToString();
                txtRazonSocial.Text = dr["RazonSocial"].ToString();
                txtCuit.Text = DatabaseService.ObtenerCuitEmpresaTextoBruto(dr);
                EstablecerCondicionIVAEmpresa(dr.Table.Columns.Contains("CondicionIVAEmpresa") ? dr["CondicionIVAEmpresa"]?.ToString() : "");
                txtDireccion.Text = dr["Direccion"].ToString();
                txtTelefono.Text = dr["Telefono"].ToString();
                txtEmail.Text = dr["Email"].ToString();
                txtCertificadoPath.Text = dr["CertificadoPath"].ToString();
                ActualizarEstadoActivacionAfip(dr);
                if (dr.Table.Columns.Contains("AfipClavePrivadaPath") && dr["AfipClavePrivadaPath"] != DBNull.Value)
                {
                    string certPath = dr["CertificadoPath"]?.ToString() ?? "";
                    if (certPath.EndsWith(".crt", StringComparison.OrdinalIgnoreCase)
                        || certPath.EndsWith(".cer", StringComparison.OrdinalIgnoreCase))
                        txtCertificadoCrtPath.Text = certPath;
                }
                if (dr.Table.Columns.Contains("LogoPath") && dr["LogoPath"] != DBNull.Value)
                    _logoPathActual = dr["LogoPath"].ToString();
                MostrarPreviewLogo(_logoPathActual);

                if (dr.Table.Columns.Contains("LogoEnA4"))
                    chkLogoEnA4.IsChecked = dr["LogoEnA4"] != DBNull.Value && Convert.ToBoolean(dr["LogoEnA4"]);
                else chkLogoEnA4.IsChecked = true;

                if (dr.Table.Columns.Contains("LogoEnTicket"))
                    chkLogoEnTicket.IsChecked = dr["LogoEnTicket"] != DBNull.Value && Convert.ToBoolean(dr["LogoEnTicket"]);
                else chkLogoEnTicket.IsChecked = true;

                _hayPasswordAfipGuardadaEnBd = DatabaseService.TienePasswordAfipPersistida(dr);
                _passwordAfipTocadoPorUsuario = false;
                _suprimirEventoPasswordAfip = true;
                try { txtPasswordAfip.Password = ""; }
                finally { _suprimirEventoPasswordAfip = false; }
                ActualizarAyudaContraseñaAfip();
                txtPuntoVenta.Text = dr["PuntoVenta"].ToString();

                if (dr.Table.Columns.Contains("MPAccessToken"))
                    txtMPToken.Text = dr["MPAccessToken"].ToString();

                if (dr.Table.Columns.Contains("MPUserId"))
                    txtMPUserId.Text = dr["MPUserId"].ToString();

                if (dr.Table.Columns.Contains("MPPosId"))
                    txtMPPosId.Text = dr["MPPosId"].ToString();

                if (dr.Table.Columns.Contains("TipoCambioUSD") && dr["TipoCambioUSD"] != DBNull.Value && dr["TipoCambioUSD"] != null)
                    txtTipoCambioUSD.Text = dr["TipoCambioUSD"].ToString();

                if (dr.Table.Columns.Contains("AfipProduccion") && dr["AfipProduccion"] != DBNull.Value && chkAfipProduccion != null)
                    chkAfipProduccion.IsChecked = Convert.ToBoolean(dr["AfipProduccion"]);
                else if (chkAfipProduccion != null)
                    chkAfipProduccion.IsChecked = false;

                if (chkUsaAperturaCaja != null)
                {
                    if (dr.Table.Columns.Contains("UsaAperturaCaja") && dr["UsaAperturaCaja"] != DBNull.Value)
                        chkUsaAperturaCaja.IsChecked = Convert.ToBoolean(dr["UsaAperturaCaja"]);
                    else
                        chkUsaAperturaCaja.IsChecked = false;
                }

                if (dr.Table.Columns.Contains("VisorPromoCarpeta") && dr["VisorPromoCarpeta"] != DBNull.Value
                    && !string.IsNullOrWhiteSpace(dr["VisorPromoCarpeta"].ToString()))
                    txtVisorPromoCarpeta.Text = dr["VisorPromoCarpeta"].ToString();
                else if (txtVisorPromoCarpeta != null)
                    txtVisorPromoCarpeta.Text = DatabaseService.CarpetaPublicidadesCliente;

                if (dr.Table.Columns.Contains("VisorPromoIntervaloSeg") && dr["VisorPromoIntervaloSeg"] != DBNull.Value)
                    txtVisorPromoIntervalo.Text = dr["VisorPromoIntervaloSeg"].ToString();
                else if (txtVisorPromoIntervalo != null)
                    txtVisorPromoIntervalo.Text = "8";
            }
        }

        private void btnImportarImagen_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var dlg = new OpenFileDialog
                {
                    Title = "Elegir publicidad para la pantalla del cliente",
                    Filter = "Publicidades|*.jpg;*.jpeg;*.png;*.gif;*.bmp;*.mp4;*.avi|" +
                               "Imágenes|*.jpg;*.jpeg;*.png;*.gif;*.bmp|" +
                               "Videos|*.mp4;*.avi|" +
                               "Todos|*.*"
                };
                if (dlg.ShowDialog() == true)
                    txtRutaImagenPromocionImportada.Text = dlg.FileName;
            }
            catch (Exception ex)
            {
                ModernMessageBox.Show(ex.Message, "Error", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }

        private string ResolverCarpetaPromoVisor()
        {
            string carpeta = txtVisorPromoCarpeta?.Text?.Trim();
            if (string.IsNullOrWhiteSpace(carpeta))
                carpeta = DatabaseService.AsegurarCarpetaPublicidadesCliente();
            else if (!Directory.Exists(carpeta))
                Directory.CreateDirectory(carpeta);
            return carpeta;
        }

        private void CopiarArchivoPromoSeleccionado(string carpetaDestino)
        {
            string origen = txtRutaImagenPromocionImportada?.Text?.Trim();
            if (string.IsNullOrWhiteSpace(origen) || !File.Exists(origen))
                return;

            string ext = Path.GetExtension(origen);
            if (!DatabaseService.EsExtensionPromoImagenCliente(ext) && !DatabaseService.EsExtensionPromoVideoCliente(ext))
            {
                ModernMessageBox.Show("Formato no soportado. Use JPG, PNG, GIF, BMP, MP4 o AVI.", "Archivo no válido",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            string nombre = Path.GetFileName(origen);
            string destino = Path.Combine(carpetaDestino, nombre);
            if (File.Exists(destino))
            {
                string baseName = Path.GetFileNameWithoutExtension(nombre);
                destino = Path.Combine(carpetaDestino, $"{baseName}_{DateTime.Now:yyyyMMdd_HHmmss}{ext}");
            }

            File.Copy(origen, destino, overwrite: false);
            txtRutaImagenPromocionImportada.Text = destino;
        }

        private void btnBuscarCarpetaPromo_Click(object sender, RoutedEventArgs e)
        {
            using (var dlg = new System.Windows.Forms.FolderBrowserDialog())
            {
                dlg.Description = "Carpeta con imágenes o videos de promoción para la pantalla del cliente";
                if (!string.IsNullOrWhiteSpace(txtVisorPromoCarpeta?.Text))
                    dlg.SelectedPath = txtVisorPromoCarpeta.Text.Trim();
                if (dlg.ShowDialog() == System.Windows.Forms.DialogResult.OK)
                    txtVisorPromoCarpeta.Text = dlg.SelectedPath;
            }
        }

        private void btnVistaPreviaVisor_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                string carpeta = ResolverCarpetaPromoVisor();
                if (!int.TryParse(txtVisorPromoIntervalo?.Text?.Trim(), out int seg))
                    seg = 8;

                var rutasExtra = new List<string>();
                string pendiente = txtRutaImagenPromocionImportada?.Text?.Trim();
                if (!string.IsNullOrWhiteSpace(pendiente) && File.Exists(pendiente))
                    rutasExtra.Add(pendiente);

                var preview = new VisorClienteWindow(
                    modoVistaPrevia: true,
                    carpetaPromoOverride: carpeta,
                    intervaloSegundosOverride: seg,
                    rutasPromoExtra: rutasExtra);

                preview.ShowDialog();
            }
            catch (Exception ex)
            {
                ModernMessageBox.Show(ex.Message, "Vista previa", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }

        private void btnGuardarPromoVisor_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (!int.TryParse(txtVisorPromoIntervalo?.Text?.Trim(), out int seg))
                    seg = 8;

                string carpeta = ResolverCarpetaPromoVisor();
                txtVisorPromoCarpeta.Text = carpeta;
                CopiarArchivoPromoSeleccionado(carpeta);

                if (!DatabaseService.ActualizarVisorPromociones(carpeta, seg))
                {
                    ModernMessageBox.Show("No se pudo guardar la configuración del visor.", "Error", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                ModernMessageBox.Show(
                    "Promociones del visor guardadas. Si la pantalla del cliente está abierta, el carrusel se actualizará al instante.",
                    "Listo", MessageBoxButton.OK, MessageBoxImage.Information);

                try { CustomerScreenService.RecargarPublicidades(); } catch { }
            }
            catch (Exception ex)
            {
                ModernMessageBox.Show(ex.Message, "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void txtPasswordAfip_PasswordChanged(object sender, RoutedEventArgs e)
        {
            if (_suprimirEventoPasswordAfip) return;
            _passwordAfipTocadoPorUsuario = true;
        }

        private void ActualizarAyudaContraseñaAfip()
        {
            if (lblHintPasswordAfip == null) return;
            lblHintPasswordAfip.Text = _hayPasswordAfipGuardadaEnBd
                ? "Hay una contraseña guardada (no se muestra). Deje el campo vacío para conservarla; escriba solo si cambió el archivo o la clave."
                : "Vacío hasta que cargue por primera vez su certificado.";
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

        private void ActualizarEstadoActivacionAfip(DataRow dr = null)
        {
            if (lblEstadoActivacionAfip == null) return;
            dr = dr ?? DatabaseService.GetConfiguracion();
            lblEstadoActivacionAfip.Text = AfipActivacionFiscalService.ObtenerEstadoActivacion(dr);
            lblEstadoActivacionAfip.Foreground = (Brush)FindResource("SuccessColor");
        }

        private void EstablecerEstadoActivacionAfip(string mensaje, bool exito)
        {
            if (lblEstadoActivacionAfip == null) return;
            lblEstadoActivacionAfip.Text = mensaje;
            lblEstadoActivacionAfip.Foreground = (Brush)FindResource(exito ? "SuccessColor" : "DangerColor");
        }

        private async void btnProbarConexionAfip_Click(object sender, RoutedEventArgs e)
        {
            if (btnProbarConexionAfip == null) return;

            btnProbarConexionAfip.IsEnabled = false;
            if (btnGenerarCsrAfip != null) btnGenerarCsrAfip.IsEnabled = false;
            if (btnSubirCertificadoAfip != null) btnSubirCertificadoAfip.IsEnabled = false;
            if (pbProbarConexionAfip != null) pbProbarConexionAfip.Visibility = Visibility.Visible;

            EstablecerEstadoActivacionAfip("Probando conexión con WSAA de AFIP…", true);
            if (lblEstadoActivacionAfip != null)
                lblEstadoActivacionAfip.Foreground = (Brush)FindResource("TextSecondary");

            try
            {
                ResultadoPruebaWsaa resultado = await AfipService.ProbarConexionWsaaAsync();
                EstablecerEstadoActivacionAfip(resultado.Mensaje, resultado.Exito);

                if (resultado.Exito)
                {
                    ModernMessageBox.Show(
                        resultado.Mensaje,
                        "Conexión AFIP",
                        MessageBoxButton.OK,
                        MessageBoxImage.Information);
                }
                else
                {
                    ModernMessageBox.Show(
                        resultado.Mensaje,
                        "Error de conexión AFIP",
                        MessageBoxButton.OK,
                        MessageBoxImage.Warning);
                }
            }
            catch (Exception ex)
            {
                string msg = "Error inesperado al probar la conexión: " + ex.Message;
                EstablecerEstadoActivacionAfip(msg, false);
                ModernMessageBox.Show(msg, "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                btnProbarConexionAfip.IsEnabled = true;
                if (btnGenerarCsrAfip != null) btnGenerarCsrAfip.IsEnabled = true;
                if (btnSubirCertificadoAfip != null) btnSubirCertificadoAfip.IsEnabled = true;
                if (pbProbarConexionAfip != null) pbProbarConexionAfip.Visibility = Visibility.Collapsed;
            }
        }

        private void btnGenerarCsrAfip_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                string cuit = txtCuit.Text?.Trim() ?? "";
                string razonSocial = txtRazonSocial.Text?.Trim() ?? "";
                string nombreFantasia = txtNombreFantasia.Text?.Trim() ?? "";

                if (ModernMessageBox.Show(
                    "Se generará una nueva clave privada RSA y un CSR. Si ya tiene un certificado activo, deberá solicitar uno nuevo en AFIP/ARCA.\n\n¿Continuar?",
                    "Generar CSR AFIP",
                    MessageBoxButton.YesNo,
                    MessageBoxImage.Question) != MessageBoxResult.Yes)
                    return;

                var resultado = AfipActivacionFiscalService.GenerarCsr(cuit, razonSocial, nombreFantasia);
                if (!resultado.Exito)
                {
                    ModernMessageBox.Show(resultado.Error ?? "No se pudo generar el CSR.", "Error", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                var sfd = new SaveFileDialog
                {
                    Title = "Guardar pedido de certificado (CSR)",
                    Filter = "Pedido de certificado (*.csr)|*.csr|Todos (*.*)|*.*",
                    FileName = resultado.NombreArchivoCsr,
                    OverwritePrompt = true
                };

                if (sfd.ShowDialog() == true)
                    File.WriteAllText(sfd.FileName, resultado.ContenidoCsr, System.Text.Encoding.ASCII);

                ActualizarEstadoActivacionAfip();

                ModernMessageBox.Show(
                    "CSR generado correctamente.\n\n" +
                    "1. Suba el archivo .csr en AFIP/ARCA (Administrador de Relaciones de Clave Fiscal).\n" +
                    "2. Cuando reciba el certificado .crt, impórtelo con el botón «Subir .crt».\n\n" +
                    "La clave privada quedó guardada en:\n" + resultado.RutaClavePrivada,
                    "CSR listo",
                    MessageBoxButton.OK,
                    MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                ModernMessageBox.Show(ex.Message, "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void btnSubirCertificadoAfip_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var ofd = new OpenFileDialog
                {
                    Title = "Seleccionar certificado AFIP",
                    Filter = "Certificado AFIP (*.crt;*.cer)|*.crt;*.cer"
                };
                if (ofd.ShowDialog() != true) return;

                var resultado = AfipActivacionFiscalService.GuardarCertificadoAfip(ofd.FileName);
                if (!resultado.Exito)
                {
                    ModernMessageBox.Show(resultado.Error ?? "No se pudo guardar el certificado.", "Error", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                txtCertificadoCrtPath.Text = resultado.RutaCertificado;
                txtCertificadoPath.Text = resultado.RutaCertificado;
                ActualizarEstadoActivacionAfip();

                ModernMessageBox.Show(
                    "Certificado AFIP importado correctamente.\n\nEl sistema ya puede conectarse al WebService de Facturación Electrónica usando el par .key + .crt.\n\nRecuerde autorizar el servicio wsfe en AFIP/ARCA y registrar la IP si corresponde.",
                    "Certificado listo",
                    MessageBoxButton.OK,
                    MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                ModernMessageBox.Show(ex.Message, "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        // ----------------------------------------------------
        // BOTÓN 1: BUSCAR DATOS Y CAJAS (Lupa)
        // ----------------------------------------------------
        private async void btnBuscarCajas_Click(object sender, RoutedEventArgs e)
        {
            string token = txtMPToken.Text.Trim();

            if (string.IsNullOrEmpty(token))
            {
                ModernMessageBox.Show("Primero pegue su Access Token.", "Falta Token", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            btnBuscarCajas.IsEnabled = false;
            btnBuscarCajas.Content = "⏳";

            try
            {
                // 1. Obtener y llenar el User ID automáticamente
                string userId = await MercadoPagoService.ObtenerUserId(token);
                if (!string.IsNullOrEmpty(userId))
                {
                    txtMPUserId.Text = userId;
                }

                // 2. Buscar Cajas
                await MercadoPagoService.DescubrirCajas(token);
            }
            catch (Exception ex)
            {
                ModernMessageBox.Show("Error: " + ex.Message);
            }
            finally
            {
                btnBuscarCajas.IsEnabled = true;
                btnBuscarCajas.Content = "🔍";
            }
        }

        // ----------------------------------------------------
        // BOTÓN 2: CREAR CAJA AUTOMÁTICA (Magia)
        // ----------------------------------------------------
        private async void btnCrearCaja_Click(object sender, RoutedEventArgs e)
        {
            string token = txtMPToken.Text.Trim();

            if (string.IsNullOrEmpty(token))
            {
                ModernMessageBox.Show("Primero ingrese su Access Token.", "Falta Token", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            if (ModernMessageBox.Show("¿Desea crear automáticamente una caja llamada 'Caja SchTec Principal' y asignarle el ID 'SCH01'?", "Creación Automática", MessageBoxButton.YesNo, MessageBoxImage.Question) == MessageBoxResult.No)
            {
                return;
            }

            btnCrearCaja.IsEnabled = false;
            btnCrearCaja.Content = "⏳";

            try
            {
                // Primero intentamos llenar el User ID por si acaso
                string userId = await MercadoPagoService.ObtenerUserId(token);
                if (!string.IsNullOrEmpty(userId)) txtMPUserId.Text = userId;

                // Llamamos a la creación
                string resultado = await MercadoPagoService.CrearCajaPorDefecto(token);

                if (resultado.StartsWith("OK:"))
                {
                    string idNuevo = resultado.Split(':')[1];
                    txtMPPosId.Text = idNuevo; // Llenamos el campo automáticamente

                    ModernMessageBox.Show("¡Éxito! Caja creada y vinculada.\n\nID Asignado: " + idNuevo + "\n\nAhora GUARDE los cambios.", "Listo", MessageBoxButton.OK, MessageBoxImage.Information);
                }
                else if (resultado.StartsWith("ERROR"))
                {
                    ModernMessageBox.Show(resultado, "Error Mercado Pago", MessageBoxButton.OK, MessageBoxImage.Error);
                }
                else
                {
                    ModernMessageBox.Show("Ocurrió un error: " + resultado);
                }
            }
            catch (Exception ex)
            {
                ModernMessageBox.Show("Error crítico: " + ex.Message);
            }
            finally
            {
                btnCrearCaja.IsEnabled = true;
                btnCrearCaja.Content = "🪄 Crear";
            }
        }

        private void btnGuardar_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                int pto = 0;
                int.TryParse(txtPuntoVenta.Text, out pto);

                string cuit = txtCuit.Text?.Trim() ?? "";
                if (!string.IsNullOrWhiteSpace(cuit))
                {
                    string cuitDigits = cuit.Replace("-", "").Replace(" ", "");
                    if (cuitDigits.Length != 11 || !cuitDigits.All(char.IsDigit))
                    {
                        ModernMessageBox.Show("CUIT inválido. Debe tener 11 dígitos (ej: 20-12345678-9).");
                        return;
                    }
                }

                if (pto <= 0 || pto > 99999)
                {
                    ModernMessageBox.Show("Punto de venta AFIP inválido. Debe ser un número entre 1 y 99999.");
                    return;
                }

                decimal? tc = null;
                if (decimal.TryParse(txtTipoCambioUSD?.Text?.Replace(",", "."), System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out decimal t))
                    tc = t;

                string pwdAfip = txtPasswordAfip.Password ?? "";
                bool conservarPwdVacío = string.IsNullOrWhiteSpace(pwdAfip) && _hayPasswordAfipGuardadaEnBd
                    && !_passwordAfipTocadoPorUsuario;

                bool exito = DatabaseService.GuardarConfiguracion(
                    txtNombreFantasia.Text,
                    txtRazonSocial.Text,
                    txtCuit.Text,
                    txtDireccion.Text,
                    txtTelefono.Text,
                    txtEmail.Text,
                    _logoPathActual,
                    txtCertificadoPath.Text,
                    pwdAfip,
                    pto,
                    txtMPToken.Text.Trim(),
                    txtMPUserId.Text.Trim(),
                    txtMPPosId.Text.Trim(),
                    true,
                    tc,
                    chkAfipProduccion?.IsChecked == true,
                    conservarPasswordAfipSiContraseniaVacia: conservarPwdVacío,
                    logoEnTicket: chkLogoEnTicket?.IsChecked == true,
                    logoEnA4: chkLogoEnA4?.IsChecked == true,
                    usaAperturaCaja: chkUsaAperturaCaja?.IsChecked == true,
                    condicionIVAEmpresa: ObtenerCondicionIVAEmpresaSeleccionada());

                if (exito)
                {
                    if (!conservarPwdVacío && !string.IsNullOrWhiteSpace(pwdAfip))
                        _hayPasswordAfipGuardadaEnBd = true;
                    else if (!conservarPwdVacío && string.IsNullOrWhiteSpace(pwdAfip))
                        _hayPasswordAfipGuardadaEnBd = false;
                    _suprimirEventoPasswordAfip = true;
                    try { txtPasswordAfip.Password = ""; }
                    finally { _suprimirEventoPasswordAfip = false; }
                    _passwordAfipTocadoPorUsuario = false;
                    ActualizarAyudaContraseñaAfip();
                }

                if (exito) ModernMessageBox.Show("¡Datos del negocio guardados correctamente!");
                else ModernMessageBox.Show("Error al guardar datos.");
            }
            catch (Exception ex)
            {
                ModernMessageBox.Show(ex.Message);
            }
        }

        // --- PESTAÑA 2: RED / SERVIDOR ---
        private void CargarDatosConexion()
        {
            try
            {
                var datos = DatabaseService.GetDatosConexionActual();

                // Mostrar el servidor completo (con instancia si aplica, ej: .\SQLEXPRESS o 192.168.1.5\SQLEXPRESS)
                txtIpServidor.Text = datos["Servidor"];
                txtPuertoServidor.Text = datos["Puerto"];
                chkUsarWindowsAuth.IsChecked = datos.ContainsKey("UsaIntegrado") && datos["UsaIntegrado"] == "1";
                txtUsuarioSQL.Text = string.IsNullOrEmpty(datos["Usuario"]) ? "Sistema" : datos["Usuario"];
                txtPasswordSQL.Password = datos["Password"] ?? "";

                string ip = txtIpServidor.Text.Trim().ToLower();
                if (ip == "." || ip == "127.0.0.1" || ip == "localhost" || ip.StartsWith(".\\"))
                {
                    cmbModoPC.SelectedIndex = 0; // Servidor
                }
                else
                {
                    cmbModoPC.SelectedIndex = 1; // Cliente
                }

                chkUsarWindowsAuth_Changed(null, null);
            }
            catch { }
        }

        private void chkUsarWindowsAuth_Changed(object sender, RoutedEventArgs e)
        {
            if (pnlCredencialesSQL == null) return;
            bool usarWindows = chkUsarWindowsAuth.IsChecked == true;
            pnlCredencialesSQL.Visibility = (!usarWindows && EsAdministrador()) ? Visibility.Visible : Visibility.Collapsed;
        }

        private bool EsAdministrador() => SesionUsuario.RolID == 1;

        private void AplicarVisibilidadCredencialesSQL()
        {
            if (pnlCredencialesSQL == null) return;
            chkUsarWindowsAuth_Changed(null, null);
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
            if (!LicenseManager.TieneConexionRed())
            {
                ModernMessageBox.Show(
                    "La conexión en red no está incluida en su licencia.\n\n" +
                    "Solicite el extra «Conexión en RED» para usar SQL Server en red o varias PCs.",
                    "Extra no habilitado", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            if (SesionUsuario.RolID != 1)
            {
                ModernMessageBox.Show("Solo un administrador puede cambiar la conexión a la base de datos.", "Acceso denegado",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            if (string.IsNullOrWhiteSpace(txtIpServidor.Text))
            {
                ModernMessageBox.Show("Por favor, ingrese una Dirección IP válida.");
                return;
            }

            bool usarIntegrado = chkUsarWindowsAuth.IsChecked == true;
            if (!usarIntegrado && string.IsNullOrWhiteSpace(txtUsuarioSQL.Text))
            {
                ModernMessageBox.Show("Si no usa autenticación Windows, debe ingresar Usuario SQL.");
                return;
            }

            if (ModernMessageBox.Show("Al guardar la configuración de red, el sistema se cerrará para aplicar los cambios.\n\n¿Desea continuar?", "Confirmar Reinicio", MessageBoxButton.YesNo, MessageBoxImage.Warning) == MessageBoxResult.Yes)
            {
                bool exito = DatabaseService.GuardarNuevaConexion(
                    txtIpServidor.Text.Trim(),
                    txtPuertoServidor.Text.Trim(),
                    usarIntegrado,
                    txtUsuarioSQL.Text.Trim(),
                    txtPasswordSQL.Password
                );

                if (exito)
                {
                    ModernMessageBox.Show(
                        "Configuración de red guardada correctamente.\n\n" +
                        "El sistema se cerrará ahora. Al volver a abrirlo, se conectará al servidor configurado.\n\n" +
                        "Si la conexión falla al reiniciar, verifique que el servidor SQL esté activo y accesible en la red.",
                        "Configuración guardada",
                        MessageBoxButton.OK,
                        MessageBoxImage.Information);
                    Application.Current.Shutdown();
                }
                else
                {
                    ModernMessageBox.Show("No se pudo guardar la configuración de red.\nVerifique permisos de escritura en:\n" + SchettiniGestion.DatabaseService.RutaConexionCfg, "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }

        // --- PESTAÑA 3: LICENCIA ---
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

            if (DatabaseService.GuardarNuevaLicencia(nuevaKey))
            {
                if (LicenseManager.ValidarLicencia())
                {
                    ModernMessageBox.Show("¡Licencia activada correctamente!\n\nPor favor, reinicie el sistema para aplicar los cambios en los módulos.", "Activación Exitosa", MessageBoxButton.OK, MessageBoxImage.Information);
                    Application.Current.Shutdown();
                }
                else
                {
                    ModernMessageBox.Show("La licencia se guardó pero parece ser INVÁLIDA o está vencida.", "Advertencia", MessageBoxButton.OK, MessageBoxImage.Warning);
                    CargarDatosLicencia();
                }
            }
            else
            {
                ModernMessageBox.Show("Error al guardar la licencia en la base de datos.", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        // --- PESTAÑA 4: MANTENIMIENTO (BACKUPS) ---
        private void CargarMediosPago()
        {
            try
            {
                if (dgvMediosPago != null)
                {
                    var dt = DatabaseService.GetMediosPagoCompleto();
                    if (dt != null) dgvMediosPago.ItemsSource = dt.DefaultView;
                }
            }
            catch { }
        }

        private void btnGuardarMedioPago_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (dgvMediosPago != null && (sender as System.Windows.Controls.Button)?.DataContext is DataRowView drv)
                {
                    var r = drv.Row;
                    int id = r["MedioID"] != DBNull.Value ? Convert.ToInt32(r["MedioID"]) : 0;
                    string nombre = r["Nombre"]?.ToString() ?? "";
                    bool activo = r["Activo"] != DBNull.Value && Convert.ToBoolean(r["Activo"]);
                    int orden = r["Orden"] != DBNull.Value ? Convert.ToInt32(r["Orden"]) : 0;
                    string tipo = "Efectivo";
                    if (r.Table.Columns.Contains("Tipo") && r["Tipo"] != DBNull.Value && !string.IsNullOrWhiteSpace(r["Tipo"]?.ToString()))
                        tipo = r["Tipo"].ToString().Trim();

                    decimal recargoPct = 0m;
                    if (r.Table.Columns.Contains("RecargoDescuentoPct") && r["RecargoDescuentoPct"] != DBNull.Value)
                    {
                        var s = Convert.ToString(r["RecargoDescuentoPct"], CultureInfo.InvariantCulture)
                            ?? r["RecargoDescuentoPct"].ToString();
                        if (!decimal.TryParse(s.Replace(",", "."), NumberStyles.Any, CultureInfo.InvariantCulture, out recargoPct))
                            recargoPct = 0m;
                    }

                    if (DatabaseService.GuardarMedioPago(id, nombre, activo, orden, tipo, recargoPct))
                    {
                        ModernMessageBox.Show("Medio de pago guardado.");
                        CargarMediosPago();
                    }
                }
            }
            catch (Exception ex) { ModernMessageBox.Show(ex.Message); }
        }

        private void btnNuevoMedioPago_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var dt = DatabaseService.GetMediosPagoCompleto();
                var maxOrden = 0;
                foreach (DataRow r in dt.Rows)
                    if (r["Orden"] != DBNull.Value) maxOrden = Math.Max(maxOrden, Convert.ToInt32(r["Orden"]));
                var newRow = dt.NewRow();
                newRow["MedioID"] = 0;
                newRow["Nombre"] = "Nuevo";
                newRow["Activo"] = true;
                newRow["Orden"] = maxOrden + 1;
                if (dt.Columns.Contains("Tipo")) newRow["Tipo"] = "Efectivo";
                if (dt.Columns.Contains("RecargoDescuentoPct")) newRow["RecargoDescuentoPct"] = 0m;
                dt.Rows.Add(newRow);
                dgvMediosPago.ItemsSource = dt.DefaultView;
            }
            catch (Exception ex) { ModernMessageBox.Show(ex.Message); }
        }

        private void btnGenerarBackup_Click(object sender, RoutedEventArgs e)
        {
            var sfd = new Microsoft.Win32.SaveFileDialog
            {
                Title      = "Guardar copia de seguridad",
                Filter     = "Backup SQL Server (*.bak)|*.bak",
                FileName   = $"SchPOS_Backup_{DateTime.Now:yyyyMMdd_HHmmss}.bak",
                DefaultExt = ".bak"
            };
            if (sfd.ShowDialog() != true) return;

            this.Cursor = System.Windows.Input.Cursors.Wait;
            string error = BackupService.RealizarBackup(sfd.FileName);
            this.Cursor = System.Windows.Input.Cursors.Arrow;

            if (error == null)
                ModernMessageBox.Show(
                    $"¡Copia de seguridad creada exitosamente!\n\nArchivo: {sfd.FileName}",
                    "Copia guardada", MessageBoxButton.OK, MessageBoxImage.Information);
            else
                ModernMessageBox.Show(
                    "No se pudo crear la copia de seguridad.\n\n" +
                    "Detalle del error:\n" + error + "\n\n" +
                    "Tip: El servicio SQL Server debe tener permiso de escritura en la carpeta elegida.\n" +
                    "Probá guardarlo en C:\\Backups o en el escritorio del servidor.",
                    "Error al hacer backup", MessageBoxButton.OK, MessageBoxImage.Error);
        }

        private void btnRestaurarBackup_Click(object sender, RoutedEventArgs e)
        {
            if (SesionUsuario.RolID != 1)
            {
                ModernMessageBox.Show("Solo un administrador puede restaurar copias de seguridad.", "Acceso denegado",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            if (ModernMessageBox.Show(
                    "⚠ ATENCIÓN: Esta acción reemplazará TODOS los datos actuales (ventas, clientes, productos) con los del archivo de backup seleccionado.\n\n" +
                    "Esta operación NO se puede deshacer.\n\n" +
                    "¿Está seguro que desea continuar?",
                    "Confirmar restauración", MessageBoxButton.YesNo, MessageBoxImage.Warning) != MessageBoxResult.Yes)
                return;

            var ofd = new Microsoft.Win32.OpenFileDialog
            {
                Title  = "Seleccionar archivo de backup",
                Filter = "Backup SQL Server (*.bak)|*.bak|Todos los archivos (*.*)|*.*"
            };
            if (ofd.ShowDialog() != true) return;

            // Segunda confirmación
            if (ModernMessageBox.Show(
                    $"Vas a restaurar desde:\n{ofd.FileName}\n\n" +
                    "Se perderán TODOS los datos actuales. ¿Confirmar?",
                    "Última confirmación", MessageBoxButton.YesNo, MessageBoxImage.Warning) != MessageBoxResult.Yes)
                return;

            this.Cursor = System.Windows.Input.Cursors.Wait;
            string error = BackupService.RestaurarBackup(ofd.FileName);
            this.Cursor = System.Windows.Input.Cursors.Arrow;

            if (error == null)
            {
                ModernMessageBox.Show(
                    "✔ Base de datos restaurada exitosamente.\n\nEl sistema se cerrará para reconectar.",
                    "Restauración exitosa", MessageBoxButton.OK, MessageBoxImage.Information);
                System.Windows.Application.Current.Shutdown();
            }
            else
            {
                ModernMessageBox.Show(
                    "No se pudo restaurar la base de datos.\n\n" +
                    "Detalle del error:\n" + error + "\n\n" +
                    "Asegurate de que el archivo .bak fue generado desde este mismo sistema y que el servicio SQL Server tiene acceso al archivo.",
                    "Error al restaurar", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void EstablecerCondicionIVAEmpresa(string valor)
        {
            if (cmbCondicionIVAEmpresa == null) return;
            if (string.IsNullOrWhiteSpace(valor))
            {
                cmbCondicionIVAEmpresa.SelectedIndex = 0;
                return;
            }
            string valorTrim = valor.Trim();
            for (int i = 0; i < cmbCondicionIVAEmpresa.Items.Count; i++)
            {
                if ((cmbCondicionIVAEmpresa.Items[i] as ComboBoxItem)?.Content?.ToString() == valorTrim)
                {
                    cmbCondicionIVAEmpresa.SelectedIndex = i;
                    return;
                }
            }
            cmbCondicionIVAEmpresa.Items.Add(new ComboBoxItem { Content = valorTrim });
            cmbCondicionIVAEmpresa.SelectedIndex = cmbCondicionIVAEmpresa.Items.Count - 1;
        }

        private string ObtenerCondicionIVAEmpresaSeleccionada()
        {
            return (cmbCondicionIVAEmpresa?.SelectedItem as ComboBoxItem)?.Content?.ToString()?.Trim() ?? "";
        }

        // --- PESTAÑA IMPRESORAS ---
        private void CargarImpresoras()
        {
            try
            {
                var impresoras = PrinterSettings.InstalledPrinters
                    .Cast<string>()
                    .OrderBy(x => x)
                    .ToList();

                // Agregar opción vacía al inicio (= pedir cada vez)
                impresoras.Insert(0, "(Preguntar cada vez)");

                cmbImpresoraTicket.ItemsSource = impresoras;
                cmbImpresoraA4.ItemsSource = new System.Collections.Generic.List<string>(impresoras);

                var (ticket, a4) = DatabaseService.GetImpresoras();
                cmbImpresoraTicket.SelectedItem = string.IsNullOrWhiteSpace(ticket) ? "(Preguntar cada vez)" : ticket;
                cmbImpresoraA4.SelectedItem     = string.IsNullOrWhiteSpace(a4)     ? "(Preguntar cada vez)" : a4;

                if (chkPreguntarAntesImprimir != null)
                    chkPreguntarAntesImprimir.IsChecked = DatabaseService.GetPreguntarAntesImprimir();

                if (cmbDestinoImpresionVenta != null)
                {
                    cmbDestinoImpresionVenta.ItemsSource = new[]
                    {
                        new { Valor = "Ticket", Texto = "Impresora térmica (formato ticket)" },
                        new { Valor = "A4", Texto = "Impresora A4 (formato documento)" },
                        new { Valor = "Archivo", Texto = "Guardar como PDF (para WhatsApp / email)" },
                        new { Valor = "Preguntar", Texto = "Preguntar al cobrar" }
                    };
                    cmbDestinoImpresionVenta.DisplayMemberPath = "Texto";
                    cmbDestinoImpresionVenta.SelectedValuePath = "Valor";
                    string destino = DatabaseService.GetDestinoImpresionVenta();
                    foreach (dynamic item in cmbDestinoImpresionVenta.Items)
                        if (item.Valor == destino) { cmbDestinoImpresionVenta.SelectedItem = item; break; }
                    if (cmbDestinoImpresionVenta.SelectedItem == null && cmbDestinoImpresionVenta.Items.Count > 0)
                        cmbDestinoImpresionVenta.SelectedIndex = 0;
                }

                if (cmbAnchoTicketMm != null)
                {
                    cmbAnchoTicketMm.ItemsSource = new[] { "80 mm", "58 mm" };
                    var op = DatabaseService.GetOpcionesImpresionTicket();
                    cmbAnchoTicketMm.SelectedItem = op.AnchoMm == 58 ? "58 mm" : "80 mm";
                }

                if (txtCarpetaArchivosComprobantes != null)
                    txtCarpetaArchivosComprobantes.Text = DatabaseService.GetCarpetaArchivosComprobantes() ?? "";

                CargarOpcionesTicketEnUi();
            }
            catch (Exception ex)
            {
                ModernMessageBox.Show("Error al listar impresoras: " + ex.Message);
            }
        }

        private void CargarOpcionesTicketEnUi()
        {
            var op = DatabaseService.GetOpcionesImpresionTicket();
            if (chkTicketLogo != null) chkTicketLogo.IsChecked = op.MostrarLogo;
            if (chkTicketDireccion != null) chkTicketDireccion.IsChecked = op.MostrarDireccion;
            if (chkTicketTelefono != null) chkTicketTelefono.IsChecked = op.MostrarTelefono;
            if (chkTicketCuit != null) chkTicketCuit.IsChecked = op.MostrarCuit;
            if (chkTicketCliente != null) chkTicketCliente.IsChecked = op.MostrarCliente;
            if (chkTicketCodigo != null) chkTicketCodigo.IsChecked = op.MostrarCodigo;
            if (chkTicketFormaPago != null) chkTicketFormaPago.IsChecked = op.MostrarFormaPago;
            if (chkTicketPieFiscal != null) chkTicketPieFiscal.IsChecked = op.MostrarPieFiscal;
            if (chkTicketGracias != null) chkTicketGracias.IsChecked = op.MostrarGracias;
            if (chkTicketPuntoVenta != null) chkTicketPuntoVenta.IsChecked = op.MostrarPuntoVenta;
            if (chkTicketVendedor != null) chkTicketVendedor.IsChecked = op.MostrarVendedor;
        }

        private OpcionesImpresionTicket LeerOpcionesTicketDesdeUi()
        {
            return new OpcionesImpresionTicket
            {
                AnchoMm = cmbAnchoTicketMm?.SelectedItem?.ToString()?.StartsWith("58") == true ? 58 : 80,
                MostrarLogo = chkTicketLogo?.IsChecked != false,
                MostrarDireccion = chkTicketDireccion?.IsChecked != false,
                MostrarTelefono = chkTicketTelefono?.IsChecked != false,
                MostrarCuit = chkTicketCuit?.IsChecked != false,
                MostrarCliente = chkTicketCliente?.IsChecked != false,
                MostrarCodigo = chkTicketCodigo?.IsChecked == true,
                MostrarFormaPago = chkTicketFormaPago?.IsChecked != false,
                MostrarPieFiscal = chkTicketPieFiscal?.IsChecked != false,
                MostrarGracias = chkTicketGracias?.IsChecked != false,
                MostrarPuntoVenta = chkTicketPuntoVenta?.IsChecked != false,
                MostrarVendedor = chkTicketVendedor?.IsChecked == true
            };
        }

        private void btnElegirCarpetaComprobantes_Click(object sender, RoutedEventArgs e)
        {
            using (var dlg = new System.Windows.Forms.FolderBrowserDialog())
            {
                dlg.Description = "Carpeta para guardar comprobantes digitales";
                if (!string.IsNullOrWhiteSpace(txtCarpetaArchivosComprobantes?.Text) && Directory.Exists(txtCarpetaArchivosComprobantes.Text))
                    dlg.SelectedPath = txtCarpetaArchivosComprobantes.Text;
                if (dlg.ShowDialog() == System.Windows.Forms.DialogResult.OK)
                    txtCarpetaArchivosComprobantes.Text = dlg.SelectedPath;
            }
        }

        private void btnGuardarImpresoras_Click(object sender, RoutedEventArgs e)
        {
            string ticket = cmbImpresoraTicket.SelectedItem?.ToString();
            string a4     = cmbImpresoraA4.SelectedItem?.ToString();

            if (ticket == "(Preguntar cada vez)") ticket = null;
            if (a4     == "(Preguntar cada vez)") a4     = null;

            string destino = cmbDestinoImpresionVenta?.SelectedValue?.ToString() ?? "Ticket";
            var opciones = LeerOpcionesTicketDesdeUi();

            if (DatabaseService.GuardarConfiguracionImpresoras(
                ticket, a4, chkPreguntarAntesImprimir?.IsChecked != false, opciones,
                destinoImpresionVenta: destino,
                carpetaArchivos: txtCarpetaArchivosComprobantes?.Text?.Trim(),
                anchoTicketMm: opciones.AnchoMm,
                logoEnTicket: opciones.MostrarLogo))
                ModernMessageBox.Show("Configuración de impresoras guardada correctamente.", "Guardado", MessageBoxButton.OK, MessageBoxImage.Information);
            else
                ModernMessageBox.Show("No se pudo guardar la configuración.", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
        }

        private void btnTestTicket_Click(object sender, RoutedEventArgs e)
        {
            string nombre = cmbImpresoraTicket.SelectedItem?.ToString();
            if (nombre == "(Preguntar cada vez)" || string.IsNullOrWhiteSpace(nombre))
            {
                ModernMessageBox.Show("Seleccioná una impresora primero.");
                return;
            }
            PrintService.ImprimirPaginaDePrueba(nombre, "Ticket");
        }

        private void btnTestA4_Click(object sender, RoutedEventArgs e)
        {
            string nombre = cmbImpresoraA4.SelectedItem?.ToString();
            if (nombre == "(Preguntar cada vez)" || string.IsNullOrWhiteSpace(nombre))
            {
                ModernMessageBox.Show("Seleccioná una impresora primero.");
                return;
            }
            PrintService.ImprimirPaginaDePrueba(nombre, "A4");
        }
    }
}
