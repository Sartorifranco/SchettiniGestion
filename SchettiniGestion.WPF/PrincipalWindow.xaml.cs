using System;
using System.Collections.Generic;
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
        private Button _navActivo;
        private bool _menuColapsado;
        private readonly Dictionary<Button, string> _textosNavCompletos = new Dictionary<Button, string>();
        private readonly Dictionary<Type, UIElement> _modulosCargados = new Dictionary<Type, UIElement>();

        public PrincipalWindow()
        {
            _themeChangedHandler = (s, e) => Dispatcher.BeginInvoke(new Action(() =>
            {
                ActualizarBotonTema();
                RefrescarContenidoPorTema();
            }));
            ThemeManager.ThemeChanged += _themeChangedHandler;

            InitializeComponent();
            try
            {
                DatabaseService.InicializarPermisosBaseDatos();
            }
            catch (Exception ex)
            {
                MessageBox.Show("Error en el Seeder de Base de Datos: " + ex.Message, "Error de Arranque", MessageBoxButton.OK, MessageBoxImage.Warning);
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

            // Doble-chequeo de licencia en tiempo de ejecución (defensa ante manipulación post-inicio)
            if (!LicenseManager.ValidarLicencia())
            {
                OcultarTodoPorFalloLicencia();
                MessageBox.Show(
                    "La licencia no es válida o ha expirado.\nEl acceso a los módulos ha sido desactivado.",
                    "Licencia inválida",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);
                return;
            }

            // Recargar permisos tras asegurar catálogo en BD (la sesión se creó en el login, antes del seeder).
            if (!string.IsNullOrWhiteSpace(SesionUsuario.NombreUsuario))
                DatabaseService.CargarSesionUsuario(SesionUsuario.NombreUsuario);

            AplicarPermisosLite();
            AplicarExtrasLicencia();
            SincronizarModoPantallaDesdeBase();
            ActualizarBtnTeclado();
            InicializarMenuLateral();
            AplicarLayoutResponsivoVentana();
            AfipColaService.Iniciar();

            RedSyncWatcher.Cambio -= Principal_CambioRed;
            RedSyncWatcher.Cambio += Principal_CambioRed;
            RedSyncWatcher.Iniciar();

            // Pantalla de bienvenida por defecto al abrir
            try { MostrarModulo(() => new InicioControl()); }
            catch { /* InicioControl es opcional; no bloquear el arranque */ }
        }

        private void Window_Closing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            RedSyncWatcher.Cambio -= Principal_CambioRed;
            RedSyncWatcher.Detener();
            AfipColaService.Detener();
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
                txtUsuarioLogueado.Text = SesionUsuario.NombreParaRegistro() ?? "Usuario";
            if (txtRolLogueado != null)
            {
                string puesto = PuestoLocalService.Nombre;
                string rol = SesionUsuario.NombreRol;
                if (!string.IsNullOrWhiteSpace(puesto) && !string.IsNullOrWhiteSpace(rol))
                    txtRolLogueado.Text = puesto + " · " + rol;
                else if (!string.IsNullOrWhiteSpace(puesto))
                    txtRolLogueado.Text = puesto + " · En línea";
                else
                    txtRolLogueado.Text = rol != null ? rol + " · En línea" : "En línea";
            }
        }

        public void RefrescarNombrePuestoHeader()
        {
            ActualizarHeaderUsuario();
        }

        private void Principal_CambioRed(string entidad)
        {
            foreach (var modulo in _modulosCargados.Values)
            {
                if (modulo is ISincronizableEnRed sync)
                {
                    try { sync.AplicarCambioRed(entidad); }
                    catch { }
                }
            }
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

        /// <summary>Informes tabulares: ventas, compras o estadísticas licenciadas.</summary>
        private static bool PuedeInformes()
        {
            return PuedeModulo(DatabaseService.PERMISO_VENTAS)
                || PuedeModulo(DatabaseService.PERMISO_FACTURACION)
                || PuedeModulo(DatabaseService.PERMISO_COMPRAS)
                || PuedeModulo(DatabaseService.PERMISO_ESTADISTICAS);
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
            if (modoDosPantallas && !LicenseManager.TieneVisorCliente())
            {
                _sincronizandoModoPantalla = true;
                try
                {
                    cmbModoPantalla.SelectedIndex = 0;
                }
                finally
                {
                    _sincronizandoModoPantalla = false;
                }
                MessageBox.Show(
                    "La pantalla cliente (segundo monitor) no está incluida en su licencia.\n\n" +
                    "Solicite el extra «Pantalla cliente» para habilitar el visor.",
                    "Extra no habilitado",
                    MessageBoxButton.OK,
                    MessageBoxImage.Information);
                return;
            }

            if (!DatabaseService.ActualizarUsaVisorCliente(modoDosPantallas))
            {
                MessageBox.Show(
                    "No se pudo guardar el modo de pantalla. Probá de nuevo o revisá la conexión a la base.",
                    "Pantalla cliente",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);
                return;
            }

            string aviso = CustomerScreenService.AplicarModo(modoDosPantallas);
            if (!string.IsNullOrEmpty(aviso))
            {
                MessageBox.Show(aviso, "Pantalla cliente", MessageBoxButton.OK, MessageBoxImage.Information);
            }
        }

        private void AplicarExtrasLicencia()
        {
            if (cmbModoPantalla == null) return;

            bool visorOk = LicenseManager.TieneVisorCliente();
            if (cmbModoPantalla.Items.Count > 1 && cmbModoPantalla.Items[1] is ComboBoxItem itemDosPantallas)
                itemDosPantallas.Visibility = visorOk ? Visibility.Visible : Visibility.Collapsed;

            if (!visorOk && cmbModoPantalla.SelectedIndex == 1)
            {
                _sincronizandoModoPantalla = true;
                try
                {
                    cmbModoPantalla.SelectedIndex = 0;
                    DatabaseService.ActualizarUsaVisorCliente(false);
                    CustomerScreenService.Cerrar();
                }
                finally
                {
                    _sincronizandoModoPantalla = false;
                }
            }
        }

        private void AplicarPermisosLite()
        {
            try
            {
                // PuedeModulo = LicenseManager.IsModuleEnabled AND SesionUsuario.TienePermiso
                // Garantiza que los botones principales también respeten la licencia activa.
                btnVentasFacturacion.Visibility = PuedeModulo(DatabaseService.PERMISO_VENTAS) ? Visibility.Visible : Visibility.Collapsed;
                btnProductos.Visibility         = PuedeModulo(DatabaseService.PERMISO_PRODUCTOS) ? Visibility.Visible : Visibility.Collapsed;
                if (btnEtiquetas != null)
                    btnEtiquetas.Visibility = (LicenseManager.TieneEtiquetas() && PuedeModulo(DatabaseService.PERMISO_PRODUCTOS))
                        ? Visibility.Visible : Visibility.Collapsed;
                btnGestionStock.Visibility      = PuedeModulo(DatabaseService.PERMISO_STOCK)  ? Visibility.Visible : Visibility.Collapsed;
                btnListasPrecios.Visibility     = PuedeModulo(DatabaseService.PERMISO_LISTASPRECIOS) ? Visibility.Visible : Visibility.Collapsed;
                if (btnPromociones != null)
                    btnPromociones.Visibility = PuedeModulo(DatabaseService.PERMISO_PRODUCTOS) ? Visibility.Visible : Visibility.Collapsed;
                btnClientes.Visibility          = PuedeModulo(DatabaseService.PERMISO_CLIENTES) ? Visibility.Visible : Visibility.Collapsed;
                if (btnProveedores != null)
                    btnProveedores.Visibility = PuedeModulo(DatabaseService.PERMISO_PROVEEDORES) ? Visibility.Visible : Visibility.Collapsed;
                if (btnCompras != null)
                    btnCompras.Visibility = PuedeModulo(DatabaseService.PERMISO_COMPRAS) ? Visibility.Visible : Visibility.Collapsed;
                btnCaja.Visibility              = PuedeModulo(DatabaseService.PERMISO_CAJA)   ? Visibility.Visible : Visibility.Collapsed;
                if (btnInformes != null)
                    btnInformes.Visibility = PuedeInformes() ? Visibility.Visible : Visibility.Collapsed;
                // Gráficos unificados en pestaña Informes → ocultar entrada duplicada
                if (btnEstadisticas != null)
                    btnEstadisticas.Visibility = Visibility.Collapsed;
                btnUsuariosPermisos.Visibility  = PuedeModulo(DatabaseService.PERMISO_USUARIOS) ? Visibility.Visible : Visibility.Collapsed;
                btnConfiguracion.Visibility     = PuedeModulo(DatabaseService.PERMISO_CONFIGURACION) ? Visibility.Visible : Visibility.Collapsed;

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
            if (btnEtiquetas != null) btnEtiquetas.Visibility = Visibility.Collapsed;
            if (btnGestionStock != null) btnGestionStock.Visibility = Visibility.Collapsed;
            if (btnListasPrecios != null) btnListasPrecios.Visibility = Visibility.Collapsed;
            if (btnPromociones != null) btnPromociones.Visibility = Visibility.Collapsed;
            if (btnClientes != null) btnClientes.Visibility = Visibility.Collapsed;
            if (btnProveedores != null) btnProveedores.Visibility = Visibility.Collapsed;
            if (btnCompras != null) btnCompras.Visibility = Visibility.Collapsed;
            if (btnCaja != null) btnCaja.Visibility = Visibility.Collapsed;
            if (btnInformes != null) btnInformes.Visibility = Visibility.Collapsed;
            if (btnEstadisticas != null) btnEstadisticas.Visibility = Visibility.Collapsed;
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
            if (MessageBox.Show("¿Cerrar completamente el sistema?", "Salir", MessageBoxButton.YesNo, MessageBoxImage.Question) == MessageBoxResult.Yes)
            {
                _volviendoALogin = false;
                SesionUsuario.Cerrar();
                CustomerScreenService.Cerrar();
                Application.Current.Shutdown();
            }
        }

        private void SetModuloActivo(Button boton)
        {
            if (_navActivo != null)
                _navActivo.Style = (Style)FindResource("LiteNavButton");
            _navActivo = boton;
            if (boton != null)
                boton.Style = (Style)FindResource("LiteNavButtonActive");
        }

        /// <summary>
        /// Conserva cada módulo en memoria, pero solo uno vive en el árbol visual.
        /// Así la navegación no mide/compone Facturación+Productos+Informes a la vez.
        /// Los Loaded de cada módulo deben protegerse con un flag (_inicializado) para
        /// no repetir consultas SQL al volver.
        /// </summary>
        private void MostrarModulo<T>(Func<T> crear) where T : UIElement
        {
            if (!_modulosCargados.TryGetValue(typeof(T), out UIElement modulo))
            {
                modulo = crear();
                _modulosCargados[typeof(T)] = modulo;
            }

            if (ReferenceEquals(mainContentArea.Content, modulo))
                return;

            mainContentArea.Content = modulo;
        }

        private void btnInicio_Click(object sender, RoutedEventArgs e)
        {
            SetModuloActivo(btnInicio);
            MostrarModulo(() => new InicioControl());
        }

        private void btnVentasFacturacion_Click(object sender, RoutedEventArgs e)
        {
            if (!PuedeModulo(DatabaseService.PERMISO_VENTAS))
            {
                MessageBox.Show("No tiene permiso para acceder a ventas o facturación.", "Acceso", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            SetModuloActivo(btnVentasFacturacion);
            MostrarModulo(() => new FacturacionControl());
        }

        private void productosMenuItem_Click(object sender, RoutedEventArgs e) => btnProductos_Click(sender, e);

        private void btnEtiquetas_Click(object sender, RoutedEventArgs e)
        {
            if (!LicenseManager.TieneEtiquetas() || !PuedeModulo(DatabaseService.PERMISO_PRODUCTOS))
            {
                MessageBox.Show(
                    "La impresión de etiquetas no está incluida en su licencia.\n\nSolicite el extra «Impresión de etiquetas».",
                    "Acceso", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }
            SetModuloActivo(btnEtiquetas);
            MostrarModulo(() => new EtiquetasControl());
        }

        private void btnProductos_Click(object sender, RoutedEventArgs e)
        {
            if (!PuedeModulo(DatabaseService.PERMISO_PRODUCTOS))
            {
                MessageBox.Show("No tiene permiso para acceder al módulo de productos.", "Acceso", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }
            SetModuloActivo(btnProductos);
            MostrarModulo(() => new ProductosControl());
        }

        private void btnGestionStock_Click(object sender, RoutedEventArgs e)
        {
            if (!PuedeModulo(DatabaseService.PERMISO_STOCK))
            {
                MessageBox.Show("No tiene permiso para acceder al stock.", "Acceso", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }
            SetModuloActivo(btnGestionStock);
            MostrarModulo(() => new StockControl());
        }

        private void clientesMenuItem_Click(object sender, RoutedEventArgs e)
        {
            if (!PuedeModulo(DatabaseService.PERMISO_CLIENTES))
            {
                MessageBox.Show("No tiene permiso para acceder a clientes.", "Acceso", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }
            SetModuloActivo(btnClientes);
            MostrarModulo(() => new ClientesControl());
        }

        private void btnProveedores_Click(object sender, RoutedEventArgs e)
        {
            if (!PuedeModulo(DatabaseService.PERMISO_PROVEEDORES)) { MensajeSinPermiso(); return; }
            SetModuloActivo(btnProveedores);
            MostrarModulo(() => new ProveedoresControl());
        }

        private void btnCompras_Click(object sender, RoutedEventArgs e)
        {
            if (!PuedeModulo(DatabaseService.PERMISO_COMPRAS)) { MensajeSinPermiso(); return; }
            SetModuloActivo(btnCompras);
            MostrarModulo(() => new ComprasControl());
        }

        private void btnPresupuestos_Click(object sender, RoutedEventArgs e)
        {
            if (!PuedeModulo(DatabaseService.PERMISO_PRESUPUESTOS)) { MensajeSinPermiso(); return; }
            MostrarModulo(() => new PresupuestosControl());
        }

        private void btnCuentasCorrientes_Click(object sender, RoutedEventArgs e)
        {
            if (!PuedeModulo(DatabaseService.PERMISO_CUENTASCORRIENTES)) { MensajeSinPermiso(); return; }
            MostrarModulo(() => new CuentasCorrientesControl());
        }

        private void btnListasPrecios_Click(object sender, RoutedEventArgs e)
        {
            if (!PuedeModulo(DatabaseService.PERMISO_LISTASPRECIOS)) { MensajeSinPermiso(); return; }
            SetModuloActivo(btnListasPrecios);
            MostrarModulo(() => new ListasPreciosControl());
        }

        private void btnPromociones_Click(object sender, RoutedEventArgs e)
        {
            if (!PuedeModulo(DatabaseService.PERMISO_PRODUCTOS)) { MensajeSinPermiso(); return; }
            SetModuloActivo(btnPromociones);
            MostrarModulo(() => new PromocionesControl());
        }

        private void btnPreciosActualizar_Click(object sender, RoutedEventArgs e)
        {
            if (!PuedeModulo(DatabaseService.PERMISO_PRECIOS)) { MensajeSinPermiso(); return; }
            MostrarModulo(() => new PreciosControl());
        }

        private void btnInformes_Click(object sender, RoutedEventArgs e)
        {
            if (!PuedeInformes()) { MensajeSinPermiso(); return; }
            SetModuloActivo(btnInformes);
            MostrarModulo(() => new InformesControl());
        }

        private void btnReportesVentas_Click(object sender, RoutedEventArgs e)
        {
            if (!PuedeModulo(DatabaseService.PERMISO_VENTAS) && !PuedeModulo(DatabaseService.PERMISO_FACTURACION))
            {
                MensajeSinPermiso();
                return;
            }
            MostrarModulo(() => new ReportesControl());
        }

        private void btnEstadisticas_Click(object sender, RoutedEventArgs e)
        {
            if (!PuedeModulo(DatabaseService.PERMISO_VENTAS) && !PuedeModulo(DatabaseService.PERMISO_FACTURACION))
            {
                MensajeSinPermiso();
                return;
            }
            SetModuloActivo(btnEstadisticas);
            MostrarModulo(() => new EstadisticasControl());
        }

        private static void MensajeSinPermiso()
        {
            MessageBox.Show("No tiene permiso para acceder a este módulo.", "Acceso", MessageBoxButton.OK, MessageBoxImage.Information);
        }

        private void btnCaja_Click(object sender, RoutedEventArgs e)
        {
            if (!PuedeModulo(DatabaseService.PERMISO_CAJA))
            {
                MensajeSinPermiso();
                return;
            }

            SetModuloActivo(btnCaja);
            MostrarModulo(() => new CajaModuloControl());
        }

        private void btnUsuariosPermisos_Click(object sender, RoutedEventArgs e)
        {
            if (!PuedeModulo(DatabaseService.PERMISO_USUARIOS))
            {
                MessageBox.Show("No tiene permiso para acceder a usuarios.", "Acceso", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }
            SetModuloActivo(btnUsuariosPermisos);
            MostrarModulo(() => new UsuariosControl());
        }

        private void btnConfiguracion_Click(object sender, RoutedEventArgs e)
        {
            if (!PuedeModulo(DatabaseService.PERMISO_CONFIGURACION))
            {
                MessageBox.Show("No tiene permiso para acceder a la configuración.", "Acceso", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            SetModuloActivo(btnConfiguracion);
            MostrarModulo(() => new ConfiguracionControl());
        }

        private void btnTeclado_Click(object sender, RoutedEventArgs e)
        {
            KeyboardService.Toggle();
            ActualizarBtnTeclado();
        }

        private void ActualizarBtnTeclado()
        {
            if (btnTeclado == null) return;
            bool on = KeyboardService.IsEnabled;
            var iconBlock  = btnTeclado.FindName("txtIconoTeclado") as System.Windows.Controls.TextBlock;
            var labelBlock = btnTeclado.FindName("txtLabelTeclado") as System.Windows.Controls.TextBlock;
            if (iconBlock  != null) iconBlock.Text  = "⌨";
            if (labelBlock != null) labelBlock.Text  = on ? "Teclado ON" : "Teclado OFF";

            btnTeclado.BorderBrush = on
                ? (System.Windows.Media.Brush)FindResource("VKAccentBar")
                : (System.Windows.Media.Brush)FindResource("BorderColor");
            btnTeclado.Opacity = on ? 1.0 : 0.55;
            btnTeclado.ToolTip = on
                ? "Teclado táctil activo. Se recuerda al reiniciar."
                : "Teclado táctil apagado. Tocá para activarlo (se recuerda al reiniciar).";
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

        private void RefrescarContenidoPorTema()
        {
            // Los módulos usan DynamicResource: no hace falta destruirlos y volverlos a
            // crear (eso repetía consultas y además perdía filtros/selecciones).
            mainContentArea?.InvalidateVisual();
        }

        private void InicializarMenuLateral()
        {
            RegistrarTextoNav(btnInicio);
            RegistrarTextoNav(btnVentasFacturacion);
            RegistrarTextoNav(btnProductos);
            RegistrarTextoNav(btnEtiquetas);
            RegistrarTextoNav(btnGestionStock);
            RegistrarTextoNav(btnListasPrecios);
            RegistrarTextoNav(btnPromociones);
            RegistrarTextoNav(btnClientes);
            RegistrarTextoNav(btnProveedores);
            RegistrarTextoNav(btnCompras);
            RegistrarTextoNav(btnCaja);
            RegistrarTextoNav(btnInformes);
            RegistrarTextoNav(btnEstadisticas);
            RegistrarTextoNav(btnUsuariosPermisos);
            RegistrarTextoNav(btnConfiguracion);
            RegistrarTextoNav(btnCerrarSesion);
            RegistrarTextoNav(btnSalirSistema);
            AplicarMenuColapsado(DatabaseService.ObtenerMenuLateralColapsado(), persistir: false);
        }

        private void RegistrarTextoNav(Button btn)
        {
            if (btn == null) return;
            _textosNavCompletos[btn] = btn.Content?.ToString() ?? "";
        }

        private void btnToggleMenuLateral_Click(object sender, RoutedEventArgs e)
        {
            AplicarMenuColapsado(!_menuColapsado, persistir: true);
        }

        private void Window_SizeChanged(object sender, SizeChangedEventArgs e)
        {
            AplicarLayoutResponsivoVentana();
        }

        private void AplicarLayoutResponsivoVentana()
        {
            if (UiScaleHelper.IsCompactWidth(ActualWidth) && !_menuColapsado)
                AplicarMenuColapsado(true, persistir: false);
            else if (UiScaleHelper.IsSmallScreen() && !_menuColapsado)
                AplicarMenuColapsado(true, persistir: false);

            bool compactoAncho = UiScaleHelper.IsCompactWidth(ActualWidth);
            bool compactoAlto = UiScaleHelper.IsCompactHeight(ActualHeight);

            if (bdrContenidoPrincipal != null)
                bdrContenidoPrincipal.Padding = UiScaleHelper.ModulePadding(compactoAncho);

            if (bdrHeaderPrincipal != null)
                bdrHeaderPrincipal.Padding = UiScaleHelper.HeaderPadding(compactoAlto);

            if (txtBreadcrumbHeader != null)
                txtBreadcrumbHeader.Visibility = compactoAlto ? Visibility.Collapsed : Visibility.Visible;

            if (rowBreadcrumb != null)
                rowBreadcrumb.Height = compactoAlto ? new GridLength(0) : GridLength.Auto;

            if (txtRolLogueado != null)
                txtRolLogueado.Visibility = compactoAlto ? Visibility.Collapsed : Visibility.Visible;

            double alturaBoton = compactoAlto ? 36 : 44;
            if (btnTema != null) btnTema.MinHeight = alturaBoton;
            if (btnTeclado != null) btnTeclado.MinHeight = alturaBoton;
            if (cmbModoPantalla != null) cmbModoPantalla.MinHeight = alturaBoton;
        }

        private void AplicarMenuColapsado(bool colapsado, bool persistir)
        {
            _menuColapsado = colapsado;
            if (colMenuLateral != null)
                colMenuLateral.Width = colapsado ? new GridLength(72) : new GridLength(290);

            if (btnToggleMenuLateral != null)
                btnToggleMenuLateral.Content = colapsado ? "▶" : "◀";

            var visTitulos = colapsado ? Visibility.Collapsed : Visibility.Visible;
            if (txtMenuTitulo != null) txtMenuTitulo.Visibility = visTitulos;
            if (txtVersionFooter != null) txtVersionFooter.Visibility = visTitulos;
            if (pnlLogoMenu != null) pnlLogoMenu.Visibility = colapsado ? Visibility.Collapsed : Visibility.Visible;

            foreach (var kv in _textosNavCompletos)
            {
                var btn = kv.Key;
                var full = kv.Value ?? "";
                if (colapsado)
                {
                    int sp = full.IndexOf(' ');
                    btn.Content = sp > 0 ? full.Substring(0, sp) : full;
                    btn.ToolTip = full;
                    btn.HorizontalContentAlignment = HorizontalAlignment.Center;
                    btn.Padding = new Thickness(8, 12, 8, 12);
                }
                else
                {
                    btn.Content = full;
                    btn.ToolTip = null;
                    btn.HorizontalContentAlignment = HorizontalAlignment.Left;
                    btn.Padding = new Thickness(16, 12, 16, 12);
                }
            }

            if (persistir)
                DatabaseService.GuardarMenuLateralColapsado(colapsado);
        }
    }
}
