using Microsoft.Win32;
using System;
using System.Data;
using System.Globalization;
using System.Windows;
using System.Windows.Controls;
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
                txtDireccion.Text = dr["Direccion"].ToString();
                txtTelefono.Text = dr["Telefono"].ToString();
                txtEmail.Text = dr["Email"].ToString();
                txtCertificadoPath.Text = dr["CertificadoPath"].ToString();
                if (dr.Table.Columns.Contains("LogoPath") && dr["LogoPath"] != DBNull.Value)
                    _logoPathActual = dr["LogoPath"].ToString();
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

                if (dr.Table.Columns.Contains("VisorPromoCarpeta") && dr["VisorPromoCarpeta"] != DBNull.Value)
                    txtVisorPromoCarpeta.Text = dr["VisorPromoCarpeta"].ToString();
                else if (txtVisorPromoCarpeta != null)
                    txtVisorPromoCarpeta.Text = "";

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
                    Title = "Elegir imagen de promoción",
                    Filter = "Imágenes|*.jpg;*.jpeg;*.png|JPEG (*.jpg;*.jpeg)|*.jpg;*.jpeg|PNG (*.png)|*.png"
                };
                if (dlg.ShowDialog() == true)
                {
                    txtRutaImagenPromocionImportada.Text = dlg.FileName;
                }
            }
            catch (Exception ex)
            {
                ModernMessageBox.Show(ex.Message, "Error", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
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

        private void btnGuardarPromoVisor_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (!int.TryParse(txtVisorPromoIntervalo?.Text?.Trim(), out int seg))
                    seg = 8;
                if (!DatabaseService.ActualizarVisorPromociones(txtVisorPromoCarpeta?.Text ?? "", seg))
                {
                    ModernMessageBox.Show("No se pudo guardar la configuración del visor.", "Error", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }
                ModernMessageBox.Show("Promociones del visor guardadas. Si la pantalla cliente está abierta, se actualizará al volver al inicio de venta.", "Listo", MessageBoxButton.OK, MessageBoxImage.Information);
                try { CustomerScreenService.RefrescarSegunConfiguracion(); } catch { }
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
                    conservarPasswordAfipSiContraseniaVacia: conservarPwdVacío);

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
            try
            {
                using (var dialog = new System.Windows.Forms.FolderBrowserDialog())
                {
                    dialog.Description = "Seleccione dónde guardar la Copia de Seguridad";

                    if (dialog.ShowDialog() == System.Windows.Forms.DialogResult.OK)
                    {
                        // Construir nombre de archivo con timestamp
                        string nombreArchivo = $"SchPOS_Backup_{DateTime.Now:yyyyMMdd_HHmmss}.bak";
                        string rutaArchivo   = System.IO.Path.Combine(dialog.SelectedPath, nombreArchivo);

                        this.Cursor = System.Windows.Input.Cursors.Wait;
                        bool ok = BackupService.RealizarBackup(rutaArchivo);
                        this.Cursor = System.Windows.Input.Cursors.Arrow;

                        if (ok)
                            ModernMessageBox.Show($"¡Copia de seguridad creada exitosamente!\n\nArchivo: {rutaArchivo}", "Éxito", MessageBoxButton.OK, MessageBoxImage.Information);
                        else
                            ModernMessageBox.Show("No se pudo crear la copia de seguridad.\n\nVerifique permisos de escritura y que el servicio SQL Server esté activo.", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                    }
                }
            }
            catch (Exception ex)
            {
                this.Cursor = System.Windows.Input.Cursors.Arrow;
                ModernMessageBox.Show("No se pudo crear el backup.\n\nDetalle: " + ex.Message, "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
    }
}
