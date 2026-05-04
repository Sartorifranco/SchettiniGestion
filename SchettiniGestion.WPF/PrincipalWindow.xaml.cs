using System;
using System.Data;
using System.Windows;
using System.Windows.Controls;
using SchettiniGestion;

namespace SchettiniGestion.WPF
{
    public partial class PrincipalWindow : Window
    {
        private readonly EventHandler _themeChangedHandler;
        private bool _sincronizandoModoPantalla;

        public PrincipalWindow()
        {
            _themeChangedHandler = (s, e) => Dispatcher.BeginInvoke(new Action(ActualizarBotonTema));
            ThemeManager.ThemeChanged += _themeChangedHandler;

            InitializeComponent();

            Application.Current.ShutdownMode = ShutdownMode.OnMainWindowClose;
            Application.Current.MainWindow = this;

            try
            {
                CustomerScreenService.Iniciar();
                CustomerScreenService.Resetear();
            }
            catch { /* pantalla secundaria opcional */ }
        }

        private bool _volviendoALogin = false;

        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            ActualizarBotonTema();
            ActualizarHeaderUsuario();
            AplicarPermisosLite();
            SincronizarModoPantallaDesdeBase();
        }

        private void Window_Closing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            ThemeManager.ThemeChanged -= _themeChangedHandler;
            CustomerScreenService.Cerrar();
            if (!_volviendoALogin)
            {
                SesionUsuario.Cerrar();
                Application.Current.Shutdown();
            }
        }

        private void ActualizarHeaderUsuario()
        {
            if (txtUsuarioLogueado != null)
                txtUsuarioLogueado.Text = SesionUsuario.NombreUsuario ?? "Usuario";
            if (txtRolLogueado != null)
                txtRolLogueado.Text = SesionUsuario.NombreRol != null ? $"{SesionUsuario.NombreRol} · En línea" : "En línea";
        }

        private static Visibility Vis(bool mostrar)
        {
            return mostrar ? Visibility.Visible : Visibility.Collapsed;
        }

        /// <summary>Módulo habilitado en licencia Y permiso del rol.</summary>
        private static bool PuedeModulo(string nombrePermiso)
        {
            return LicenseManager.IsModuleEnabled(nombrePermiso) && SesionUsuario.TienePermiso(nombrePermiso);
        }

        private void SincronizarModoPantallaDesdeBase()
        {
            if (cmbModoPantalla == null) return;
            _sincronizandoModoPantalla = true;
            try
            {
                DataRow cfg = DatabaseService.GetConfiguracion();
                bool dual = cfg != null
                    && cfg.Table.Columns.Contains("UsaVisorCliente")
                    && cfg["UsaVisorCliente"] != DBNull.Value
                    && Convert.ToBoolean(cfg["UsaVisorCliente"]);
                cmbModoPantalla.SelectedIndex = dual ? 1 : 0;
            }
            catch
            {
                cmbModoPantalla.SelectedIndex = 0;
            }
            finally
            {
                _sincronizandoModoPantalla = false;
            }
        }

        private void cmbModoPantalla_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (_sincronizandoModoPantalla || cmbModoPantalla == null || cmbModoPantalla.SelectedIndex < 0)
                return;

            bool modoDosPantallas = cmbModoPantalla.SelectedIndex == 1;
            if (!DatabaseService.ActualizarUsaVisorCliente(modoDosPantallas))
                return;

            CustomerScreenService.RefrescarSegunConfiguracion();

            if (modoDosPantallas && System.Windows.Forms.Screen.AllScreens.Length < 2)
            {
                MessageBox.Show(
                    "Modo «dos pantallas» activado: el visor para el cliente se abrirá automáticamente cuando haya un segundo monitor conectado.\n\n" +
                    "Con un solo monitor, el cajero trabaja solo en esta ventana.",
                    "Pantalla cliente",
                    MessageBoxButton.OK,
                    MessageBoxImage.Information);
            }
        }

        private void AplicarPermisosLite()
        {
            try
            {
                bool licenciaValida = false;
                try
                {
                    licenciaValida = LicenseManager.ValidarLicencia();
                }
                catch
                {
                    licenciaValida = false;
                }

                if (!licenciaValida)
                {
                    MessageBox.Show(
                        "No se detectó una licencia válida o no hay conexión con el servidor.",
                        "Aviso de sistema", MessageBoxButton.OK, MessageBoxImage.Warning);

                    OcultarTodoPorFalloLicencia();
                    return;
                }

                btnVentasFacturacion_Click(null, null);

                bool ventasFact = PuedeModulo(DatabaseService.PERMISO_FACTURACION) || PuedeModulo(DatabaseService.PERMISO_VENTAS);
                if (btnVentasFacturacion != null) btnVentasFacturacion.Visibility = Vis(ventasFact);

                if (btnGestionStock != null) btnGestionStock.Visibility = Vis(PuedeModulo(DatabaseService.PERMISO_STOCK) || PuedeModulo(DatabaseService.PERMISO_PRODUCTOS));
                if (btnClientes != null) btnClientes.Visibility = Vis(PuedeModulo(DatabaseService.PERMISO_CLIENTES));
                if (btnCaja != null) btnCaja.Visibility = Vis(PuedeModulo(DatabaseService.PERMISO_CAJA));

                bool usuariosPermisos = PuedeModulo(DatabaseService.PERMISO_USUARIOS) || PuedeModulo(DatabaseService.PERMISO_PERMISOS);
                if (btnUsuariosPermisos != null) btnUsuariosPermisos.Visibility = Vis(usuariosPermisos);

            }
            catch (Exception ex)
            {
                MessageBox.Show("Error al iniciar interfaz: " + ex.Message);
            }
        }

        private void OcultarTodoPorFalloLicencia()
        {
            if (btnVentasFacturacion != null) btnVentasFacturacion.Visibility = Visibility.Collapsed;
            if (btnGestionStock != null) btnGestionStock.Visibility = Visibility.Collapsed;
            if (btnClientes != null) btnClientes.Visibility = Visibility.Collapsed;
            if (btnCaja != null) btnCaja.Visibility = Visibility.Collapsed;
            if (btnUsuariosPermisos != null) btnUsuariosPermisos.Visibility = Visibility.Collapsed;
        }

        private void btnCerrarSesion_Click(object sender, RoutedEventArgs e)
        {
            _volviendoALogin = true;
            SesionUsuario.Cerrar();
            CustomerScreenService.Cerrar();
            var login = new LoginWindow();
            Application.Current.MainWindow = login;
            login.Show();
            this.Close();
        }

        private void btnSalirSistema_Click(object sender, RoutedEventArgs e)
        {
            if (MessageBox.Show("¿Cerrar completamente el sistema?", "Salir", MessageBoxButton.YesNo, MessageBoxImage.Question) == MessageBoxResult.Yes)
            {
                _volviendoALogin = false;
                SesionUsuario.Cerrar();
                CustomerScreenService.Cerrar();
                Application.Current.Shutdown();
            }
        }

        private void btnInicio_Click(object sender, RoutedEventArgs e)
        {
            mainContentArea.Content = new InicioControl();
        }

        private void btnVentasFacturacion_Click(object sender, RoutedEventArgs e)
        {
            if (LicenseManager.IsModuleEnabled(DatabaseService.PERMISO_FACTURACION) && SesionUsuario.TienePermiso(DatabaseService.PERMISO_FACTURACION))
            {
                mainContentArea.Content = new FacturacionControl();
                return;
            }
            if (LicenseManager.IsModuleEnabled(DatabaseService.PERMISO_VENTAS) && SesionUsuario.TienePermiso(DatabaseService.PERMISO_VENTAS))
            {
                mainContentArea.Content = new VentasControl();
                return;
            }
            MessageBox.Show("No tiene permiso para acceder a ventas o facturación.", "Acceso", MessageBoxButton.OK, MessageBoxImage.Information);
        }

        private void productosMenuItem_Click(object sender, RoutedEventArgs e)
        {
            if (!PuedeModulo(DatabaseService.PERMISO_PRODUCTOS))
            {
                MessageBox.Show("No tiene permiso para acceder al módulo de productos.", "Acceso", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }
            mainContentArea.Content = new ProductosControl();
        }

        private void btnGestionStock_Click(object sender, RoutedEventArgs e)
        {
            if (!PuedeModulo(DatabaseService.PERMISO_STOCK) && !PuedeModulo(DatabaseService.PERMISO_PRODUCTOS))
            {
                MessageBox.Show("No tiene permiso para acceder al stock/productos.", "Acceso", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }
            mainContentArea.Content = new ProductosControl();
        }

        private void clientesMenuItem_Click(object sender, RoutedEventArgs e)
        {
            if (!PuedeModulo(DatabaseService.PERMISO_CLIENTES))
            {
                MessageBox.Show("No tiene permiso para acceder a clientes.", "Acceso", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }
            mainContentArea.Content = new ClientesControl();
        }

        private void btnProveedores_Click(object sender, RoutedEventArgs e)
        {
            if (!PuedeModulo(DatabaseService.PERMISO_PROVEEDORES)) { MensajeSinPermiso(); return; }
            mainContentArea.Content = new ProveedoresControl();
        }

        private void btnCompras_Click(object sender, RoutedEventArgs e)
        {
            if (!PuedeModulo(DatabaseService.PERMISO_COMPRAS)) { MensajeSinPermiso(); return; }
            mainContentArea.Content = new ComprasControl();
        }

        private void btnPresupuestos_Click(object sender, RoutedEventArgs e)
        {
            if (!PuedeModulo(DatabaseService.PERMISO_PRESUPUESTOS)) { MensajeSinPermiso(); return; }
            mainContentArea.Content = new PresupuestosControl();
        }

        private void btnCuentasCorrientes_Click(object sender, RoutedEventArgs e)
        {
            if (!PuedeModulo(DatabaseService.PERMISO_CUENTASCORRIENTES)) { MensajeSinPermiso(); return; }
            mainContentArea.Content = new CuentasCorrientesControl();
        }

        private void btnListasPrecios_Click(object sender, RoutedEventArgs e)
        {
            if (!PuedeModulo(DatabaseService.PERMISO_LISTASPRECIOS)) { MensajeSinPermiso(); return; }
            mainContentArea.Content = new ListasPreciosControl();
        }

        private void btnPreciosActualizar_Click(object sender, RoutedEventArgs e)
        {
            if (!PuedeModulo(DatabaseService.PERMISO_PRECIOS)) { MensajeSinPermiso(); return; }
            mainContentArea.Content = new PreciosControl();
        }

        private void btnInformes_Click(object sender, RoutedEventArgs e)
        {
            if (!PuedeModulo(DatabaseService.PERMISO_FACTURACION) && !PuedeModulo(DatabaseService.PERMISO_VENTAS) && !PuedeModulo(DatabaseService.PERMISO_COMPRAS))
            {
                MensajeSinPermiso();
                return;
            }
            mainContentArea.Content = new InformesControl();
        }

        private void btnReportesVentas_Click(object sender, RoutedEventArgs e)
        {
            if (!PuedeModulo(DatabaseService.PERMISO_VENTAS) && !PuedeModulo(DatabaseService.PERMISO_FACTURACION))
            {
                MensajeSinPermiso();
                return;
            }
            mainContentArea.Content = new ReportesControl();
        }

        private static void MensajeSinPermiso()
        {
            MessageBox.Show("No tiene permiso para acceder a este módulo.", "Acceso", MessageBoxButton.OK, MessageBoxImage.Information);
        }

        private void btnCaja_Click(object sender, RoutedEventArgs e)
        {
            if (!PuedeModulo(DatabaseService.PERMISO_CAJA)) { MensajeSinPermiso(); return; }

            var tabs = new TabControl { Margin = new Thickness(0) };
            tabs.Items.Add(new TabItem { Header = "Resumen del día", Content = new CajaControl() });
            tabs.Items.Add(new TabItem { Header = "Movimientos", Content = new MovimientosCajaControl() });
            tabs.Items.Add(new TabItem { Header = "Consulta caja", Content = new ConsultaCajaControl() });
            tabs.Items.Add(new TabItem { Header = "Cierre de caja", Content = new CierreCajaControl() });
            tabs.Items.Add(new TabItem { Header = "Planilla diaria", Content = new PlanillaDiariaControl() });
            tabs.Items.Add(new TabItem { Header = "Cobranzas pendientes", Content = new CobranzasPendientesControl() });
            tabs.Items.Add(new TabItem { Header = "Cupones tarjetas", Content = new CuponesTarjetasControl() });
            mainContentArea.Content = tabs;
        }

        private void btnUsuariosPermisos_Click(object sender, RoutedEventArgs e)
        {
            bool puedeUsuarios = LicenseManager.IsModuleEnabled(DatabaseService.PERMISO_USUARIOS) && SesionUsuario.TienePermiso(DatabaseService.PERMISO_USUARIOS);
            bool puedePermisos = LicenseManager.IsModuleEnabled(DatabaseService.PERMISO_PERMISOS) && SesionUsuario.TienePermiso(DatabaseService.PERMISO_PERMISOS);

            if (!puedeUsuarios && !puedePermisos)
            {
                MessageBox.Show("No tiene permiso para acceder a usuarios ni permisos.", "Acceso", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            var tabs = new System.Windows.Controls.TabControl { Margin = new Thickness(0) };
            if (puedeUsuarios)
                tabs.Items.Add(new System.Windows.Controls.TabItem { Header = "Usuarios", Content = new UsuariosControl() });
            if (puedePermisos)
                tabs.Items.Add(new System.Windows.Controls.TabItem { Header = "Permisos", Content = new GestionPermisos() });

            mainContentArea.Content = tabs;
        }

        private void btnTeclado_Click(object sender, RoutedEventArgs e)
        {
            string windir = Environment.GetFolderPath(Environment.SpecialFolder.Windows);
            string oskPath64 = System.IO.Path.Combine(windir, "System32", "osk.exe");
            string oskPath32 = System.IO.Path.Combine(windir, "sysnative", "osk.exe");
            string targetPath = System.IO.File.Exists(oskPath64) ? oskPath64 : oskPath32;

            try
            {
                System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo { FileName = targetPath, UseShellExecute = true });
            }
            catch (Exception ex)
            {
                MessageBox.Show("No se pudo iniciar el teclado.\n\nError: " + ex.Message, "Error de teclado");
            }
        }

        private void btnTema_Click(object sender, RoutedEventArgs e)
        {
            ThemeManager.ToggleTheme();
            ActualizarBotonTema();
        }

        private void ActualizarBotonTema()
        {
            try
            {
                if (txtIconoTema != null) txtIconoTema.Text = ThemeManager.IsDark ? "\u263E" : "\u263C";
                if (txtTema != null) txtTema.Text = ThemeManager.IsDark ? "Tema oscuro" : "Tema claro";
            }
            catch { }
        }
    }
}
