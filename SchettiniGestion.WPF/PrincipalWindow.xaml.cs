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
                        "No se detectó una licencia válida o no hay conexión con el servidor.\n\n" +
                        "El sistema entró en modo mantenimiento.\n" +
                        "Use Configuración para corregir la red o la licencia.",
                        "Aviso de sistema", MessageBoxButton.OK, MessageBoxImage.Warning);

                    OcultarTodoPorFalloLicencia();
                    if (btnConfiguracion != null) btnConfiguracion.Visibility = Visibility.Visible;
                    return;
                }

                btnInicio_Click(null, null);

                if (btnInicio != null) btnInicio.Visibility = Visibility.Visible;

                bool ventasFact = (LicenseManager.IsModuleEnabled(DatabaseService.PERMISO_FACTURACION) && SesionUsuario.TienePermiso(DatabaseService.PERMISO_FACTURACION))
                    || (LicenseManager.IsModuleEnabled(DatabaseService.PERMISO_VENTAS) && SesionUsuario.TienePermiso(DatabaseService.PERMISO_VENTAS));
                if (btnVentasFacturacion != null) btnVentasFacturacion.Visibility = Vis(ventasFact);

                if (btnProductos != null)
                    btnProductos.Visibility = Vis(LicenseManager.IsModuleEnabled(DatabaseService.PERMISO_PRODUCTOS) && SesionUsuario.TienePermiso(DatabaseService.PERMISO_PRODUCTOS));

                if (btnClientes != null)
                    btnClientes.Visibility = Vis(LicenseManager.IsModuleEnabled(DatabaseService.PERMISO_CLIENTES) && SesionUsuario.TienePermiso(DatabaseService.PERMISO_CLIENTES));

                if (btnCaja != null)
                    btnCaja.Visibility = Vis(LicenseManager.IsModuleEnabled(DatabaseService.PERMISO_CAJA) && SesionUsuario.TienePermiso(DatabaseService.PERMISO_CAJA));

                bool usuariosPermisos = (LicenseManager.IsModuleEnabled(DatabaseService.PERMISO_USUARIOS) && SesionUsuario.TienePermiso(DatabaseService.PERMISO_USUARIOS))
                    || (LicenseManager.IsModuleEnabled(DatabaseService.PERMISO_PERMISOS) && SesionUsuario.TienePermiso(DatabaseService.PERMISO_PERMISOS));
                if (btnUsuariosPermisos != null) btnUsuariosPermisos.Visibility = Vis(usuariosPermisos);

                if (btnConfiguracion != null)
                    btnConfiguracion.Visibility = Vis(LicenseManager.IsModuleEnabled(DatabaseService.PERMISO_PERMISOS) && SesionUsuario.TienePermiso(DatabaseService.PERMISO_PERMISOS));
            }
            catch (Exception ex)
            {
                MessageBox.Show("Error al iniciar interfaz: " + ex.Message);
            }
        }

        private void OcultarTodoPorFalloLicencia()
        {
            if (btnVentasFacturacion != null) btnVentasFacturacion.Visibility = Visibility.Collapsed;
            if (btnProductos != null) btnProductos.Visibility = Visibility.Collapsed;
            if (btnClientes != null) btnClientes.Visibility = Visibility.Collapsed;
            if (btnCaja != null) btnCaja.Visibility = Visibility.Collapsed;
            if (btnUsuariosPermisos != null) btnUsuariosPermisos.Visibility = Visibility.Collapsed;
            if (btnInicio != null) btnInicio.Visibility = Visibility.Visible;
        }

        private void btnConfiguracion_Click(object sender, RoutedEventArgs e)
        {
            if (mainContentArea.Content is ConfiguracionControl) return;
            mainContentArea.Content = new ConfiguracionControl();
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
            if (LicenseManager.IsModuleEnabled(DatabaseService.PERMISO_PRODUCTOS) && SesionUsuario.TienePermiso(DatabaseService.PERMISO_PRODUCTOS))
                mainContentArea.Content = new ProductosControl();
        }

        private void clientesMenuItem_Click(object sender, RoutedEventArgs e)
        {
            if (LicenseManager.IsModuleEnabled(DatabaseService.PERMISO_CLIENTES) && SesionUsuario.TienePermiso(DatabaseService.PERMISO_CLIENTES))
                mainContentArea.Content = new ClientesControl();
        }

        private void btnCaja_Click(object sender, RoutedEventArgs e)
        {
            if (LicenseManager.IsModuleEnabled(DatabaseService.PERMISO_CAJA) && SesionUsuario.TienePermiso(DatabaseService.PERMISO_CAJA))
                mainContentArea.Content = new CajaControl();
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
            KeyboardHelper.ShowOnScreenKeyboard();
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
