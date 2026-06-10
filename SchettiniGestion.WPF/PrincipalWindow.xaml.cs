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
            try
            {
                DatabaseService.InicializarPermisosBaseDatos();
            }
            catch (Exception ex)
            {
                ModernMessageBox.Show("Error en el Seeder de Base de Datos: " + ex.Message, "Error de Arranque", MessageBoxButton.OK, MessageBoxImage.Warning);
            }

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
            if (SesionUsuario.TienePermiso(DatabaseService.PERMISO_VENTAS))
                AbrirPuntoDeVenta();
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
            if (string.Equals(SesionUsuario.NombreUsuario, "admin", StringComparison.OrdinalIgnoreCase))
                return true;
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
                ModernMessageBox.Show(
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
                btnPuntoDeVenta.Visibility = SesionUsuario.TienePermiso(DatabaseService.PERMISO_VENTAS) ? Visibility.Visible : Visibility.Collapsed;
                btnHistorialVentas.Visibility = SesionUsuario.TienePermiso(DatabaseService.PERMISO_VENTAS) ? Visibility.Visible : Visibility.Collapsed;
                btnGestionStock.Visibility = SesionUsuario.TienePermiso(DatabaseService.PERMISO_STOCK) ? Visibility.Visible : Visibility.Collapsed;
                btnClientes.Visibility = SesionUsuario.TienePermiso(DatabaseService.PERMISO_CLIENTES) ? Visibility.Visible : Visibility.Collapsed;
                btnCaja.Visibility = SesionUsuario.TienePermiso(DatabaseService.PERMISO_CAJA) ? Visibility.Visible : Visibility.Collapsed;
                btnUsuariosPermisos.Visibility = SesionUsuario.TienePermiso(DatabaseService.PERMISO_USUARIOS) ? Visibility.Visible : Visibility.Collapsed;
                btnConfiguracion.Visibility = SesionUsuario.TienePermiso(DatabaseService.PERMISO_CONFIGURACION) ? Visibility.Visible : Visibility.Collapsed;

            }
            catch (Exception ex)
            {
                ModernMessageBox.Show("Error al iniciar interfaz: " + ex.Message);
            }
        }

        private void OcultarTodoPorFalloLicencia()
        {
            if (btnPuntoDeVenta != null) btnPuntoDeVenta.Visibility = Visibility.Collapsed;
            if (btnHistorialVentas != null) btnHistorialVentas.Visibility = Visibility.Collapsed;
            if (btnGestionStock != null) btnGestionStock.Visibility = Visibility.Collapsed;
            if (btnClientes != null) btnClientes.Visibility = Visibility.Collapsed;
            if (btnCaja != null) btnCaja.Visibility = Visibility.Collapsed;
            if (btnUsuariosPermisos != null) btnUsuariosPermisos.Visibility = Visibility.Collapsed;
            if (btnConfiguracion != null) btnConfiguracion.Visibility = Visibility.Collapsed;
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
            if (ModernMessageBox.Show("¿Cerrar completamente el sistema?", "Salir", MessageBoxButton.YesNo, MessageBoxImage.Question) == MessageBoxResult.Yes)
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

        private void AbrirPuntoDeVenta()
        {
            if (!SesionUsuario.TienePermiso(DatabaseService.PERMISO_VENTAS))
                return;
            mainContentArea.Content = new FacturacionControl();
        }

        /// <summary>Pantalla de caja POS: código de barras, buscador de productos, carrito y facturación/cobro.</summary>
        private void btnPuntoDeVenta_Click(object sender, RoutedEventArgs e)
        {
            if (!SesionUsuario.TienePermiso(DatabaseService.PERMISO_VENTAS))
            {
                ModernMessageBox.Show("No tiene permiso para acceder al punto de venta.", "Acceso", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            AbrirPuntoDeVenta();
        }

        /// <summary>Listado histórico de comprobantes (no es cobro en tiempo real).</summary>
        private void btnHistorialVentas_Click(object sender, RoutedEventArgs e)
        {
            if (!SesionUsuario.TienePermiso(DatabaseService.PERMISO_VENTAS))
            {
                ModernMessageBox.Show("No tiene permiso para ver ventas.", "Acceso", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            mainContentArea.Content = new VentasControl();
        }

        private void productosMenuItem_Click(object sender, RoutedEventArgs e)
        {
            if (!PuedeModulo(DatabaseService.PERMISO_PRODUCTOS))
            {
                ModernMessageBox.Show("No tiene permiso para acceder al módulo de productos.", "Acceso", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }
            mainContentArea.Content = new ProductosControl();
        }

        private void btnGestionStock_Click(object sender, RoutedEventArgs e)
        {
            if (!SesionUsuario.TienePermiso(DatabaseService.PERMISO_STOCK))
            {
                ModernMessageBox.Show("No tiene permiso para acceder al stock/productos.", "Acceso", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }
            mainContentArea.Content = new ProductosControl();
        }

        private void clientesMenuItem_Click(object sender, RoutedEventArgs e)
        {
            if (!SesionUsuario.TienePermiso(DatabaseService.PERMISO_CLIENTES))
            {
                ModernMessageBox.Show("No tiene permiso para acceder a clientes.", "Acceso", MessageBoxButton.OK, MessageBoxImage.Information);
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
            ModernMessageBox.Show("No tiene permiso para acceder a este módulo.", "Acceso", MessageBoxButton.OK, MessageBoxImage.Information);
        }

        private void btnCaja_Click(object sender, RoutedEventArgs e)
        {
            if (!SesionUsuario.TienePermiso(DatabaseService.PERMISO_CAJA))
            {
                MensajeSinPermiso();
                return;
            }

            mainContentArea.Content = new CajaModuloControl();
        }

        private void btnUsuariosPermisos_Click(object sender, RoutedEventArgs e)
        {
            if (!SesionUsuario.TienePermiso(DatabaseService.PERMISO_USUARIOS))
            {
                ModernMessageBox.Show("No tiene permiso para acceder a usuarios.", "Acceso", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }
            mainContentArea.Content = new UsuariosControl();
        }

        private void btnConfiguracion_Click(object sender, RoutedEventArgs e)
        {
            if (!SesionUsuario.TienePermiso(DatabaseService.PERMISO_CONFIGURACION))
            {
                ModernMessageBox.Show("No tiene permiso para acceder a la configuración.", "Acceso", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            mainContentArea.Content = new ConfiguracionControl();
        }

        private void btnTeclado_Click(object sender, RoutedEventArgs e)
            => KeyboardHelper.ShowOnScreenKeyboard();

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
