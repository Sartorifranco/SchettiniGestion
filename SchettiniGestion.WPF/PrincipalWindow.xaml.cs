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

            // NOTA: NO llamamos a btnInicio_Click aquí para evitar que explote si no hay DB conectada.
            // Lo llamaremos en AplicarPermisos solo si todo está bien.
        }

        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            AplicarPermisos();
        }

        private void AplicarPermisos()
        {
            try
            {
                // 1. Validamos Licencia / Conexión de forma segura
                // Usamos un try-catch interno por si la DB no responde (IP Incorrecta)
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
                    // Mostramos aviso pero NO cerramos la app
                    MessageBox.Show("No se detectó una licencia válida o no hay conexión con el Servidor.\n\n" +
                                    "El sistema entró en MODO MANTENIMIENTO.\n" +
                                    "Vaya a CONFIGURACIÓN > RED para corregir la IP o cargar la licencia.",
                                    "Aviso de Sistema", MessageBoxButton.OK, MessageBoxImage.Warning);

                    // Ocultamos TODO excepto Configuración
                    OcultarTodoPorFalloLicencia();

                    // Forzamos visible el botón de Configuración para que puedas arreglarlo
                    if (this.FindName("btnConfiguracion") != null) btnConfiguracion.Visibility = Visibility.Visible;
                    if (this.FindName("btnPermisos") != null) btnPermisos.Visibility = Visibility.Visible;

                    // IMPORTANTE: Return aquí para NO cargar el Dashboard y evitar el crash
                    return;
                }

                // 3. Si llegamos acá, TODO ESTÁ BIEN -> Cargamos Dashboard y Botones
                btnInicio_Click(null, null);

                // --- LÓGICA DE BOTONES SEGÚN MÓDULOS ---

                // FACTURACIÓN
                if (!LicenseManager.IsModuleEnabled(DatabaseService.PERMISO_FACTURACION) || !SesionUsuario.TienePermiso(DatabaseService.PERMISO_FACTURACION))
                    btnFacturacion.Visibility = Visibility.Collapsed;
                else
                    btnFacturacion.Visibility = Visibility.Visible;

                // REPORTES (Ventas)
                bool puedeVerReportes = false;
                if (LicenseManager.IsModuleEnabled(DatabaseService.PERMISO_VENTAS) && SesionUsuario.TienePermiso(DatabaseService.PERMISO_VENTAS))
                {
                    puedeVerReportes = true;
                    btnVentas.Visibility = Visibility.Visible;
                    if (this.FindName("btnReportesAvanzados") != null) btnReportesAvanzados.Visibility = Visibility.Visible;
                }
                else
                {
                    btnVentas.Visibility = Visibility.Collapsed;
                    if (this.FindName("btnReportesAvanzados") != null) btnReportesAvanzados.Visibility = Visibility.Collapsed;
                }

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

                // GESTIÓN
                bool puedeVerGestion = false;

                // CAJA
                if (LicenseManager.IsModuleEnabled(DatabaseService.PERMISO_CAJA) && SesionUsuario.TienePermiso(DatabaseService.PERMISO_CAJA))
                {
                    puedeVerGestion = true;
                    btnCaja.Visibility = Visibility.Visible;
                }
                else btnCaja.Visibility = Visibility.Collapsed;

                // CUENTAS CORRIENTES
                if (LicenseManager.IsModuleEnabled(DatabaseService.PERMISO_CUENTASCORRIENTES) && SesionUsuario.TienePermiso(DatabaseService.PERMISO_CUENTASCORRIENTES))
                {
                    puedeVerGestion = true;
                    btnCtaCte.Visibility = Visibility.Visible;
                }
                else btnCtaCte.Visibility = Visibility.Collapsed;

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
                    // Configuración la dejamos visible si falla la licencia, pero aquí (flujo normal) depende del permiso
                    if (this.FindName("btnConfiguracion") != null) btnConfiguracion.Visibility = Visibility.Collapsed;
                }

                if (!puedeVerAdmin) headerAdministracion.Visibility = Visibility.Collapsed;
            }
            catch (Exception ex)
            {
                // Si falla algo inesperado, mostramos error pero NO cerramos la app
                MessageBox.Show("Error al iniciar interfaz: " + ex.Message);
            }
        }

        private void OcultarTodoPorFalloLicencia()
        {
            // Método para limpiar la pantalla en caso de error
            btnFacturacion.Visibility = Visibility.Collapsed;
            btnVentas.Visibility = Visibility.Collapsed;
            btnPresupuestos.Visibility = Visibility.Collapsed;
            btnReportePresupuestos.Visibility = Visibility.Collapsed;
            if (this.FindName("btnReportesAvanzados") != null) btnReportesAvanzados.Visibility = Visibility.Collapsed;
            btnCaja.Visibility = Visibility.Collapsed;
            btnCtaCte.Visibility = Visibility.Collapsed;
            btnPrecios.Visibility = Visibility.Collapsed;
            if (this.FindName("btnListasPrecios") != null) btnListasPrecios.Visibility = Visibility.Collapsed;
            btnCompras.Visibility = Visibility.Collapsed;
            btnProveedores.Visibility = Visibility.Collapsed;
            btnStock.Visibility = Visibility.Collapsed;
            btnProductos.Visibility = Visibility.Collapsed;
            btnClientes.Visibility = Visibility.Collapsed;
            btnUsuarios.Visibility = Visibility.Collapsed;

            headerReportes.Visibility = Visibility.Collapsed;
            headerGestion.Visibility = Visibility.Collapsed;
            // headerAdministracion queda visible para acceder a Configuración
        }

        // --- EVENTOS CLIC ---
        private void btnConfiguracion_Click(object sender, RoutedEventArgs e) { if (mainContentArea.Content is ConfiguracionControl) return; mainContentArea.Content = new ConfiguracionControl(); }
        private void salirMenuItem_Click(object sender, RoutedEventArgs e) { this.Close(); }

        // Dashboard se carga aquí
        private void btnInicio_Click(object sender, RoutedEventArgs e) { mainContentArea.Content = new DashboardControl(); }

        private void Window_Closing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            CustomerScreenService.Cerrar();
            SesionUsuario.Cerrar();
            Application.Current.Shutdown();
        }

        private void btnTeclado_Click(object sender, RoutedEventArgs e) { KeyboardHelper.ShowOnScreenKeyboard(); }

        // Eventos de botones del menú
        private void usuariosMenuItem_Click(object sender, RoutedEventArgs e) { if (LicenseManager.IsModuleEnabled(DatabaseService.PERMISO_USUARIOS)) mainContentArea.Content = new UsuariosControl(); }
        private void clientesMenuItem_Click(object sender, RoutedEventArgs e) { if (LicenseManager.IsModuleEnabled(DatabaseService.PERMISO_CLIENTES)) mainContentArea.Content = new ClientesControl(); }
        private void productosMenuItem_Click(object sender, RoutedEventArgs e) { if (LicenseManager.IsModuleEnabled(DatabaseService.PERMISO_PRODUCTOS)) mainContentArea.Content = new ProductosControl(); }
        private void btnFacturacion_Click(object sender, RoutedEventArgs e) { if (LicenseManager.IsModuleEnabled(DatabaseService.PERMISO_FACTURACION)) mainContentArea.Content = new FacturacionControl(); }
        private void btnVentas_Click(object sender, RoutedEventArgs e) { if (LicenseManager.IsModuleEnabled(DatabaseService.PERMISO_VENTAS)) mainContentArea.Content = new VentasControl(); }
        private void btnPresupuestos_Click(object sender, RoutedEventArgs e) { if (LicenseManager.IsModuleEnabled(DatabaseService.PERMISO_PRESUPUESTOS)) mainContentArea.Content = new PresupuestosControl(); }
        private void btnReportePresupuestos_Click(object sender, RoutedEventArgs e) { if (LicenseManager.IsModuleEnabled(DatabaseService.PERMISO_PRESUPUESTOS)) { ReportePresupuestosControl control = new ReportePresupuestosControl(); mainContentArea.Content = control; } }
        private void btnReportesAvanzados_Click(object sender, RoutedEventArgs e) { if (LicenseManager.IsModuleEnabled(DatabaseService.PERMISO_VENTAS)) mainContentArea.Content = new ReportesControl(); }
        private void btnStock_Click(object sender, RoutedEventArgs e) { if (LicenseManager.IsModuleEnabled(DatabaseService.PERMISO_STOCK)) mainContentArea.Content = new StockControl(); }
        private void btnProveedores_Click(object sender, RoutedEventArgs e) { if (LicenseManager.IsModuleEnabled(DatabaseService.PERMISO_PROVEEDORES)) mainContentArea.Content = new ProveedoresControl(); }
        private void btnCompras_Click(object sender, RoutedEventArgs e) { if (LicenseManager.IsModuleEnabled(DatabaseService.PERMISO_COMPRAS)) mainContentArea.Content = new ComprasControl(); }
        private void btnPrecios_Click(object sender, RoutedEventArgs e) { if (LicenseManager.IsModuleEnabled(DatabaseService.PERMISO_PRECIOS)) mainContentArea.Content = new PreciosControl(); }
        private void btnListasPrecios_Click(object sender, RoutedEventArgs e) { if (LicenseManager.IsModuleEnabled(DatabaseService.PERMISO_LISTASPRECIOS)) mainContentArea.Content = new ListasPreciosControl(); }
        private void btnCaja_Click(object sender, RoutedEventArgs e) { if (LicenseManager.IsModuleEnabled(DatabaseService.PERMISO_CAJA)) mainContentArea.Content = new CajaControl(); }
        private void btnCtaCte_Click(object sender, RoutedEventArgs e) { if (LicenseManager.IsModuleEnabled(DatabaseService.PERMISO_CUENTASCORRIENTES)) mainContentArea.Content = new CuentasCorrientesControl(); }
        private void btnPermisos_Click(object sender, RoutedEventArgs e) { if (LicenseManager.IsModuleEnabled(DatabaseService.PERMISO_PERMISOS)) mainContentArea.Content = new GestionPermisos(); }
    }
}