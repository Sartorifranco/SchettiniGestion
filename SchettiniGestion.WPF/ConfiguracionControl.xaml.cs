using Microsoft.Win32;
using System;
using System.Data;
using System.Windows;
using System.Windows.Controls;
using SchettiniGestion;

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
                txtCuit.Text = dr["CUIT"].ToString();
                txtDireccion.Text = dr["Direccion"].ToString();
                txtTelefono.Text = dr["Telefono"].ToString();
                txtEmail.Text = dr["Email"].ToString();
                txtCertificadoPath.Text = dr["CertificadoPath"].ToString();
                txtPasswordAfip.Password = dr["PasswordAfip"].ToString();
                txtPuntoVenta.Text = dr["PuntoVenta"].ToString();

                if (dr.Table.Columns.Contains("MPAccessToken"))
                    txtMPToken.Text = dr["MPAccessToken"].ToString();

                if (dr.Table.Columns.Contains("MPUserId"))
                    txtMPUserId.Text = dr["MPUserId"].ToString();

                if (dr.Table.Columns.Contains("MPPosId"))
                    txtMPPosId.Text = dr["MPPosId"].ToString();

                if (dr.Table.Columns.Contains("TipoCambioUSD") && dr["TipoCambioUSD"] != DBNull.Value && dr["TipoCambioUSD"] != null)
                    txtTipoCambioUSD.Text = dr["TipoCambioUSD"].ToString();
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

        // ----------------------------------------------------
        // BOTÓN 1: BUSCAR DATOS Y CAJAS (Lupa)
        // ----------------------------------------------------
        private async void btnBuscarCajas_Click(object sender, RoutedEventArgs e)
        {
            string token = txtMPToken.Text.Trim();

            if (string.IsNullOrEmpty(token))
            {
                MessageBox.Show("Primero pegue su Access Token.", "Falta Token", MessageBoxButton.OK, MessageBoxImage.Warning);
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
                MessageBox.Show("Error: " + ex.Message);
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
                MessageBox.Show("Primero ingrese su Access Token.", "Falta Token", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            if (MessageBox.Show("¿Desea crear automáticamente una caja llamada 'Caja SchTec Principal' y asignarle el ID 'SCH01'?", "Creación Automática", MessageBoxButton.YesNo, MessageBoxImage.Question) == MessageBoxResult.No)
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

                    MessageBox.Show("¡Éxito! Caja creada y vinculada.\n\nID Asignado: " + idNuevo + "\n\nAhora GUARDE los cambios.", "Listo", MessageBoxButton.OK, MessageBoxImage.Information);
                }
                else if (resultado.StartsWith("ERROR"))
                {
                    MessageBox.Show(resultado, "Error Mercado Pago", MessageBoxButton.OK, MessageBoxImage.Error);
                }
                else
                {
                    MessageBox.Show("Ocurrió un error: " + resultado);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show("Error crítico: " + ex.Message);
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

                bool exito = DatabaseService.GuardarConfiguracion(
                    txtNombreFantasia.Text,
                    txtRazonSocial.Text,
                    txtCuit.Text,
                    txtDireccion.Text,
                    txtTelefono.Text,
                    txtEmail.Text,
                    "",
                    txtCertificadoPath.Text,
                    txtPasswordAfip.Password,
                    pto,
                    txtMPToken.Text.Trim(),
                    txtMPUserId.Text.Trim(),
                    txtMPPosId.Text.Trim(),
                    true,
                    tc
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
                MessageBox.Show("Por favor, ingrese una Dirección IP válida.");
                return;
            }

            bool usarIntegrado = chkUsarWindowsAuth.IsChecked == true;
            if (!usarIntegrado && string.IsNullOrWhiteSpace(txtUsuarioSQL.Text))
            {
                MessageBox.Show("Si no usa autenticación Windows, debe ingresar Usuario SQL.");
                return;
            }

            if (MessageBox.Show("Al guardar la configuración de red, el sistema se cerrará para aplicar los cambios.\n\n¿Desea continuar?", "Confirmar Reinicio", MessageBoxButton.YesNo, MessageBoxImage.Warning) == MessageBoxResult.Yes)
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
                    MessageBox.Show("Configuración guardada.\nEl sistema se cerrará ahora.", "Éxito", MessageBoxButton.OK, MessageBoxImage.Information);
                    Application.Current.Shutdown();
                }
                else
                {
                    MessageBox.Show("Hubo un error al intentar guardar en App.config. Verifique permisos.", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
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
                    MessageBox.Show("¡Licencia activada correctamente!\n\nPor favor, reinicie el sistema para aplicar los cambios en los módulos.", "Activación Exitosa", MessageBoxButton.OK, MessageBoxImage.Information);
                    Application.Current.Shutdown();
                }
                else
                {
                    MessageBox.Show("La licencia se guardó pero parece ser INVÁLIDA o está vencida.", "Advertencia", MessageBoxButton.OK, MessageBoxImage.Warning);
                    CargarDatosLicencia();
                }
            }
            else
            {
                MessageBox.Show("Error al guardar la licencia en la base de datos.", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
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
                    int id = r["MedioPagoID"] != DBNull.Value ? Convert.ToInt32(r["MedioPagoID"]) : 0;
                    string nombre = r["Nombre"]?.ToString() ?? "";
                    bool activo = r["Activo"] != DBNull.Value && Convert.ToBoolean(r["Activo"]);
                    int orden = r["Orden"] != DBNull.Value ? Convert.ToInt32(r["Orden"]) : 0;
                    if (DatabaseService.GuardarMedioPago(id, nombre, activo, orden))
                    {
                        MessageBox.Show("Medio de pago guardado.");
                        CargarMediosPago();
                    }
                }
            }
            catch (Exception ex) { MessageBox.Show(ex.Message); }
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
                newRow["MedioPagoID"] = 0;
                newRow["Nombre"] = "Nuevo";
                newRow["Activo"] = true;
                newRow["Orden"] = maxOrden + 1;
                dt.Rows.Add(newRow);
                dgvMediosPago.ItemsSource = dt.DefaultView;
            }
            catch (Exception ex) { MessageBox.Show(ex.Message); }
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
                        string ruta = dialog.SelectedPath;
                        this.Cursor = System.Windows.Input.Cursors.Wait;
                        BackupService.RealizarBackup(ruta);
                        this.Cursor = System.Windows.Input.Cursors.Arrow;
                        MessageBox.Show("¡Copia de seguridad creada exitosamente!\n\nArchivo guardado en: " + ruta, "Éxito", MessageBoxButton.OK, MessageBoxImage.Information);
                    }
                }
            }
            catch (Exception ex)
            {
                this.Cursor = System.Windows.Input.Cursors.Arrow;
                MessageBox.Show("No se pudo crear el backup.\n\nDetalle: " + ex.Message, "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
    }
}
