using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;
using System.Diagnostics;
using System.IO;
using SchettiniGestion;

namespace SchettiniGestion.WPF
{
    public partial class PrincipalWindow : Window
    {
        public PrincipalWindow()
        {
            InitializeComponent();

            // Configuración vital: La aplicación solo se cierra cuando cerramos ESTA ventana manualmente
            Application.Current.ShutdownMode = ShutdownMode.OnMainWindowClose;
            Application.Current.MainWindow = this;

            // Intentar iniciar pantalla secundaria (sin bloquear si falla)
            try
            {
                CustomerScreenService.Iniciar();
                CustomerScreenService.Resetear();
            }
            catch { /* Ignorar errores de pantalla secundaria */ }
        }

        private bool _volviendoALogin = false;

        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            ActualizarBotonTema();
            ActualizarHeaderUsuario();
            AplicarPermisos();
        }

        private void ActualizarHeaderUsuario()
        {
            if (txtUsuarioLogueado != null)
                txtUsuarioLogueado.Text = SesionUsuario.NombreUsuario ?? "Usuario";
            if (txtRolLogueado != null)
                txtRolLogueado.Text = SesionUsuario.NombreRol != null ? $"{SesionUsuario.NombreRol} • En línea" : "En línea";
        }

        private void AplicarPermisos()
        {
            try
            {
                // 1. Validamos Licencia / Conexión de forma segura
                bool licenciaValida = false;
                try
                {
                    licenciaValida = LicenseManager.ValidarLicencia();
                }
                catch
                {
                    licenciaValida = false; // Si explota SQL, asumimos inválida
                }

                // 2. Si falla (Sin red, IP mal configurada o Licencia Vencida)
                if (!licenciaValida)
                {
                    MessageBox.Show("No se detectó una licencia válida o no hay conexión con el Servidor.\n\n" +
                                    "El sistema entró en MODO MANTENIMIENTO.\n" +
                                    "Vaya a CONFIGURACIÓN > RED para corregir la IP o cargar la licencia.",
                                    "Aviso de Sistema", MessageBoxButton.OK, MessageBoxImage.Warning);

                    OcultarTodoPorFalloLicencia();

                    if (this.FindName("btnConfiguracion") != null) btnConfiguracion.Visibility = Visibility.Visible;
                    if (this.FindName("btnPermisos") != null) btnPermisos.Visibility = Visibility.Visible;

                    return;
                }

                // 3. Si llegamos acá, TODO ESTÁ BIEN -> Cargamos Inicio y Botones
                btnInicio_Click(null, null);

                // --- LÓGICA DE BOTONES SEGÚN MÓDULOS ---

                // FACTURACIÓN
                if (!LicenseManager.IsModuleEnabled(DatabaseService.PERMISO_FACTURACION) || !SesionUsuario.TienePermiso(DatabaseService.PERMISO_FACTURACION))
                    btnFacturacion.Visibility = Visibility.Collapsed;
                else
                    btnFacturacion.Visibility = Visibility.Visible;

                // REPORTES (Ventas) - sin Reportes avanzados (va a Informes)
                bool puedeVerReportes = false;
                if (LicenseManager.IsModuleEnabled(DatabaseService.PERMISO_VENTAS) && SesionUsuario.TienePermiso(DatabaseService.PERMISO_VENTAS))
                {
                    puedeVerReportes = true;
                    btnVentas.Visibility = Visibility.Visible;
                }
                else btnVentas.Visibility = Visibility.Collapsed;

                // PRESUPUESTOS
                if (LicenseManager.IsModuleEnabled(DatabaseService.PERMISO_PRESUPUESTOS) && SesionUsuario.TienePermiso(DatabaseService.PERMISO_PRESUPUESTOS))
                {
                    puedeVerReportes = true;
                    btnPresupuestos.Visibility = Visibility.Visible;
                    btnReportePresupuestos.Visibility = Visibility.Visible;
                }
                else
                {
                    btnPresupuestos.Visibility = Visibility.Collapsed;
                    btnReportePresupuestos.Visibility = Visibility.Collapsed;
                }

                if (!puedeVerReportes) headerReportes.Visibility = Visibility.Collapsed;

                // TESORERÍA
                bool puedeVerTesoreria = false;
                if (LicenseManager.IsModuleEnabled(DatabaseService.PERMISO_CAJA) && SesionUsuario.TienePermiso(DatabaseService.PERMISO_CAJA))
                {
                    puedeVerTesoreria = true;
                    if (this.FindName("btnCobranzas") != null) btnCobranzas.Visibility = Visibility.Visible;
                    if (this.FindName("btnIngresosEgresos") != null) btnIngresosEgresos.Visibility = Visibility.Visible;
                    if (this.FindName("btnMovimientos") != null) btnMovimientos.Visibility = Visibility.Visible;
                    if (this.FindName("btnCuponesTarjetas") != null) btnCuponesTarjetas.Visibility = Visibility.Visible;
                    if (this.FindName("btnAperturaCaja") != null) btnAperturaCaja.Visibility = Visibility.Visible;
                    if (this.FindName("btnCierreCaja") != null) btnCierreCaja.Visibility = Visibility.Visible;
                    if (this.FindName("btnConsultaCaja") != null) btnConsultaCaja.Visibility = Visibility.Visible;
                    if (this.FindName("btnPlanillaDiaria") != null) btnPlanillaDiaria.Visibility = Visibility.Visible;
                }
                else
                {
                    if (this.FindName("btnCobranzas") != null) btnCobranzas.Visibility = Visibility.Collapsed;
                    if (this.FindName("btnIngresosEgresos") != null) btnIngresosEgresos.Visibility = Visibility.Collapsed;
                    if (this.FindName("btnMovimientos") != null) btnMovimientos.Visibility = Visibility.Collapsed;
                    if (this.FindName("btnCuponesTarjetas") != null) btnCuponesTarjetas.Visibility = Visibility.Collapsed;
                    if (this.FindName("btnAperturaCaja") != null) btnAperturaCaja.Visibility = Visibility.Collapsed;
                    if (this.FindName("btnCierreCaja") != null) btnCierreCaja.Visibility = Visibility.Collapsed;
                    if (this.FindName("btnConsultaCaja") != null) btnConsultaCaja.Visibility = Visibility.Collapsed;
                    if (this.FindName("btnPlanillaDiaria") != null) btnPlanillaDiaria.Visibility = Visibility.Collapsed;
                }
                if (!puedeVerTesoreria && this.FindName("headerTesoreria") != null) headerTesoreria.Visibility = Visibility.Collapsed;

                // GESTIÓN (sin Caja, Cierre Caja, Ctas Corrientes - movidos a Tesorería e Informes)
                bool puedeVerGestion = false;

                // PRECIOS
                if (LicenseManager.IsModuleEnabled(DatabaseService.PERMISO_PRECIOS) && SesionUsuario.TienePermiso(DatabaseService.PERMISO_PRECIOS))
                {
                    puedeVerGestion = true;
                    btnPrecios.Visibility = Visibility.Visible;
                }
                else btnPrecios.Visibility = Visibility.Collapsed;

                // LISTAS DE PRECIOS
                if (LicenseManager.IsModuleEnabled(DatabaseService.PERMISO_LISTASPRECIOS) && SesionUsuario.TienePermiso(DatabaseService.PERMISO_LISTASPRECIOS))
                {
                    puedeVerGestion = true;
                    if (this.FindName("btnListasPrecios") != null) btnListasPrecios.Visibility = Visibility.Visible;
                }
                else if (this.FindName("btnListasPrecios") != null) btnListasPrecios.Visibility = Visibility.Collapsed;

                // COMPRAS
                if (LicenseManager.IsModuleEnabled(DatabaseService.PERMISO_COMPRAS) && SesionUsuario.TienePermiso(DatabaseService.PERMISO_COMPRAS))
                {
                    puedeVerGestion = true;
                    btnCompras.Visibility = Visibility.Visible;
                }
                else btnCompras.Visibility = Visibility.Collapsed;

                // PROVEEDORES
                if (LicenseManager.IsModuleEnabled(DatabaseService.PERMISO_PROVEEDORES) && SesionUsuario.TienePermiso(DatabaseService.PERMISO_PROVEEDORES))
                {
                    puedeVerGestion = true;
                    btnProveedores.Visibility = Visibility.Visible;
                }
                else btnProveedores.Visibility = Visibility.Collapsed;

                // STOCK
                if (LicenseManager.IsModuleEnabled(DatabaseService.PERMISO_STOCK) && SesionUsuario.TienePermiso(DatabaseService.PERMISO_STOCK))
                {
                    puedeVerGestion = true;
                    btnStock.Visibility = Visibility.Visible;
                }
                else btnStock.Visibility = Visibility.Collapsed;

                // PRODUCTOS
                if (LicenseManager.IsModuleEnabled(DatabaseService.PERMISO_PRODUCTOS) && SesionUsuario.TienePermiso(DatabaseService.PERMISO_PRODUCTOS))
                {
                    puedeVerGestion = true;
                    btnProductos.Visibility = Visibility.Visible;
                }
                else btnProductos.Visibility = Visibility.Collapsed;

                // CLIENTES
                if (LicenseManager.IsModuleEnabled(DatabaseService.PERMISO_CLIENTES) && SesionUsuario.TienePermiso(DatabaseService.PERMISO_CLIENTES))
                {
                    puedeVerGestion = true;
                    btnClientes.Visibility = Visibility.Visible;
                }
                else btnClientes.Visibility = Visibility.Collapsed;

                if (!puedeVerGestion) headerGestion.Visibility = Visibility.Collapsed;

                // ADMINISTRACIÓN
                bool puedeVerAdmin = false;

                // INFORMES - visible si tiene Ventas, Ctas Corrientes o Permisos (admin)
                if ((LicenseManager.IsModuleEnabled(DatabaseService.PERMISO_VENTAS) && SesionUsuario.TienePermiso(DatabaseService.PERMISO_VENTAS)) ||
                    (LicenseManager.IsModuleEnabled(DatabaseService.PERMISO_CUENTASCORRIENTES) && SesionUsuario.TienePermiso(DatabaseService.PERMISO_CUENTASCORRIENTES)) ||
                    (LicenseManager.IsModuleEnabled(DatabaseService.PERMISO_PERMISOS) && SesionUsuario.TienePermiso(DatabaseService.PERMISO_PERMISOS)))
                {
                    puedeVerAdmin = true;
                    if (this.FindName("btnInformes") != null) btnInformes.Visibility = Visibility.Visible;
                }
                else if (this.FindName("btnInformes") != null) btnInformes.Visibility = Visibility.Collapsed;

                // USUARIOS
                if (LicenseManager.IsModuleEnabled(DatabaseService.PERMISO_USUARIOS) && SesionUsuario.TienePermiso(DatabaseService.PERMISO_USUARIOS))
                {
                    puedeVerAdmin = true;
                    btnUsuarios.Visibility = Visibility.Visible;
                }
                else btnUsuarios.Visibility = Visibility.Collapsed;

                // PERMISOS
                if (LicenseManager.IsModuleEnabled(DatabaseService.PERMISO_PERMISOS) && SesionUsuario.TienePermiso(DatabaseService.PERMISO_PERMISOS))
                {
                    puedeVerAdmin = true;
                    btnPermisos.Visibility = Visibility.Visible;
                    if (this.FindName("btnConfiguracion") != null) btnConfiguracion.Visibility = Visibility.Visible;
                }
                else
                {
                    btnPermisos.Visibility = Visibility.Collapsed;
                    if (this.FindName("btnConfiguracion") != null) btnConfiguracion.Visibility = Visibility.Collapsed;
                }

                if (!puedeVerAdmin) headerAdministracion.Visibility = Visibility.Collapsed;
            }
            catch (Exception ex)
            {
                MessageBox.Show("Error al iniciar interfaz: " + ex.Message);
            }
        }

        private void OcultarTodoPorFalloLicencia()
        {
            btnFacturacion.Visibility = Visibility.Collapsed;
            btnVentas.Visibility = Visibility.Collapsed;
            btnPresupuestos.Visibility = Visibility.Collapsed;
            btnReportePresupuestos.Visibility = Visibility.Collapsed;
            if (this.FindName("btnCobranzas") != null) btnCobranzas.Visibility = Visibility.Collapsed;
            if (this.FindName("btnIngresosEgresos") != null) btnIngresosEgresos.Visibility = Visibility.Collapsed;
            if (this.FindName("btnMovimientos") != null) btnMovimientos.Visibility = Visibility.Collapsed;
            if (this.FindName("btnCuponesTarjetas") != null) btnCuponesTarjetas.Visibility = Visibility.Collapsed;
            if (this.FindName("btnAperturaCaja") != null) btnAperturaCaja.Visibility = Visibility.Collapsed;
            if (this.FindName("btnCierreCaja") != null) btnCierreCaja.Visibility = Visibility.Collapsed;
            if (this.FindName("btnConsultaCaja") != null) btnConsultaCaja.Visibility = Visibility.Collapsed;
            if (this.FindName("btnPlanillaDiaria") != null) btnPlanillaDiaria.Visibility = Visibility.Collapsed;
            if (this.FindName("btnInformes") != null) btnInformes.Visibility = Visibility.Collapsed;
            btnPrecios.Visibility = Visibility.Collapsed;
            if (this.FindName("btnListasPrecios") != null) btnListasPrecios.Visibility = Visibility.Collapsed;
            btnCompras.Visibility = Visibility.Collapsed;
            btnProveedores.Visibility = Visibility.Collapsed;
            btnStock.Visibility = Visibility.Collapsed;
            btnProductos.Visibility = Visibility.Collapsed;
            btnClientes.Visibility = Visibility.Collapsed;
            btnUsuarios.Visibility = Visibility.Collapsed;

            headerReportes.Visibility = Visibility.Collapsed;
            if (this.FindName("headerTesoreria") != null) headerTesoreria.Visibility = Visibility.Collapsed;
            headerGestion.Visibility = Visibility.Collapsed;
        }

        // --- EVENTOS CLIC ---
        private void btnConfiguracion_Click(object sender, RoutedEventArgs e) { if (mainContentArea.Content is ConfiguracionControl) return; mainContentArea.Content = new ConfiguracionControl(); }

        private void btnCerrarSesion_Click(object sender, RoutedEventArgs e)
        {
            _volviendoALogin = true;
            SesionUsuario.Cerrar();
            CustomerScreenService.Cerrar();
            var login = new MainWindow();
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
        private void Window_Closing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            CustomerScreenService.Cerrar();
            if (!_volviendoALogin)
            {
                SesionUsuario.Cerrar();
                Application.Current.Shutdown();
            }
        }

        private void btnTeclado_Click(object sender, RoutedEventArgs e) { KeyboardHelper.ShowOnScreenKeyboard(); }

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

        // Eventos de botones del menú
        private void usuariosMenuItem_Click(object sender, RoutedEventArgs e) { if (LicenseManager.IsModuleEnabled(DatabaseService.PERMISO_USUARIOS)) mainContentArea.Content = new UsuariosControl(); }
        private void clientesMenuItem_Click(object sender, RoutedEventArgs e) { if (LicenseManager.IsModuleEnabled(DatabaseService.PERMISO_CLIENTES)) mainContentArea.Content = new ClientesControl(); }
        private void productosMenuItem_Click(object sender, RoutedEventArgs e) { if (LicenseManager.IsModuleEnabled(DatabaseService.PERMISO_PRODUCTOS)) mainContentArea.Content = new ProductosControl(); }
        private void btnFacturacion_Click(object sender, RoutedEventArgs e) { if (LicenseManager.IsModuleEnabled(DatabaseService.PERMISO_FACTURACION)) mainContentArea.Content = new FacturacionControl(); }
        private void btnVentas_Click(object sender, RoutedEventArgs e) { if (LicenseManager.IsModuleEnabled(DatabaseService.PERMISO_VENTAS)) mainContentArea.Content = new VentasControl(); }
        private void btnPresupuestos_Click(object sender, RoutedEventArgs e) { if (LicenseManager.IsModuleEnabled(DatabaseService.PERMISO_PRESUPUESTOS)) mainContentArea.Content = new PresupuestosControl(); }
        private void btnReportePresupuestos_Click(object sender, RoutedEventArgs e) { if (LicenseManager.IsModuleEnabled(DatabaseService.PERMISO_PRESUPUESTOS)) { ReportePresupuestosControl control = new ReportePresupuestosControl(); mainContentArea.Content = control; } }
        private void btnStock_Click(object sender, RoutedEventArgs e) { if (LicenseManager.IsModuleEnabled(DatabaseService.PERMISO_STOCK)) mainContentArea.Content = new StockControl(); }
        private void btnProveedores_Click(object sender, RoutedEventArgs e) { if (LicenseManager.IsModuleEnabled(DatabaseService.PERMISO_PROVEEDORES)) mainContentArea.Content = new ProveedoresControl(); }
        private void btnCompras_Click(object sender, RoutedEventArgs e) { if (LicenseManager.IsModuleEnabled(DatabaseService.PERMISO_COMPRAS)) mainContentArea.Content = new ComprasControl(); }
        private void btnPrecios_Click(object sender, RoutedEventArgs e) { if (LicenseManager.IsModuleEnabled(DatabaseService.PERMISO_PRECIOS)) mainContentArea.Content = new ListasPreciosControl(1); }
        private void btnListasPrecios_Click(object sender, RoutedEventArgs e) { if (LicenseManager.IsModuleEnabled(DatabaseService.PERMISO_LISTASPRECIOS)) mainContentArea.Content = new ListasPreciosControl(0); }

        private void btnInformes_Click(object sender, RoutedEventArgs e) { mainContentArea.Content = new InformesControl(); }

        private void btnCobranzas_Click(object sender, RoutedEventArgs e) { if (LicenseManager.IsModuleEnabled(DatabaseService.PERMISO_CAJA)) mainContentArea.Content = new CobranzasPendientesControl(); }
        private void btnIngresosEgresos_Click(object sender, RoutedEventArgs e) { if (LicenseManager.IsModuleEnabled(DatabaseService.PERMISO_CAJA)) mainContentArea.Content = new CajaControl(); }
        private void btnMovimientos_Click(object sender, RoutedEventArgs e) { if (LicenseManager.IsModuleEnabled(DatabaseService.PERMISO_CAJA)) mainContentArea.Content = new MovimientosCajaControl(); }
        private void btnCuponesTarjetas_Click(object sender, RoutedEventArgs e) { if (LicenseManager.IsModuleEnabled(DatabaseService.PERMISO_CAJA)) mainContentArea.Content = new CuponesTarjetasControl(); }
        private void btnAperturaCaja_Click(object sender, RoutedEventArgs e) { if (LicenseManager.IsModuleEnabled(DatabaseService.PERMISO_CAJA)) mainContentArea.Content = new CajaControl(); }
        private void btnCierreCaja_Click(object sender, RoutedEventArgs e) { if (LicenseManager.IsModuleEnabled(DatabaseService.PERMISO_CAJA)) mainContentArea.Content = new CierreCajaControl(); }
        private void btnConsultaCaja_Click(object sender, RoutedEventArgs e) { if (LicenseManager.IsModuleEnabled(DatabaseService.PERMISO_CAJA)) mainContentArea.Content = new ConsultaCajaControl(); }
        private void btnPlanillaDiaria_Click(object sender, RoutedEventArgs e) { if (LicenseManager.IsModuleEnabled(DatabaseService.PERMISO_CAJA)) mainContentArea.Content = new PlanillaDiariaControl(); }
        private void btnPermisos_Click(object sender, RoutedEventArgs e) { if (LicenseManager.IsModuleEnabled(DatabaseService.PERMISO_PERMISOS)) mainContentArea.Content = new GestionPermisos(); }
    }
}