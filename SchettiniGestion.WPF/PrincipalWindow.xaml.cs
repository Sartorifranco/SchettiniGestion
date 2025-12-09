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

            // INICIAR PANTALLA SECUNDARIA AL ARRANCAR
            CustomerScreenService.Iniciar();
            CustomerScreenService.Resetear(); // Muestra el logo de bienvenida

            btnInicio_Click(null, null);
        }

        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            AplicarPermisos();
        }

        private void AplicarPermisos()
        {
            // --- VALIDACIÓN DE LICENCIA ---
            LicenseManager.ValidarLicencia();

            // FACTURACIÓN
            if (!LicenseManager.IsModuleEnabled(DatabaseService.PERMISO_FACTURACION) || !SesionUsuario.TienePermiso(DatabaseService.PERMISO_FACTURACION))
            {
                btnFacturacion.Visibility = Visibility.Collapsed;
            }
            else
            {
                btnFacturacion.Visibility = Visibility.Visible;
            }

            // REPORTES
            bool puedeVerReportes = false;

            // Ventas Realizadas
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

            // Presupuestos
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

            // Caja
            if (LicenseManager.IsModuleEnabled(DatabaseService.PERMISO_CAJA) && SesionUsuario.TienePermiso(DatabaseService.PERMISO_CAJA))
            {
                puedeVerGestion = true;
                btnCaja.Visibility = Visibility.Visible;
            }
            else { btnCaja.Visibility = Visibility.Collapsed; }

            // Cuentas Corrientes
            if (LicenseManager.IsModuleEnabled(DatabaseService.PERMISO_CUENTASCORRIENTES) && SesionUsuario.TienePermiso(DatabaseService.PERMISO_CUENTASCORRIENTES))
            {
                puedeVerGestion = true;
                btnCtaCte.Visibility = Visibility.Visible;
            }
            else { btnCtaCte.Visibility = Visibility.Collapsed; }

            // Precios
            if (LicenseManager.IsModuleEnabled(DatabaseService.PERMISO_PRECIOS) && SesionUsuario.TienePermiso(DatabaseService.PERMISO_PRECIOS))
            {
                puedeVerGestion = true;
                btnPrecios.Visibility = Visibility.Visible;
            }
            else { btnPrecios.Visibility = Visibility.Collapsed; }

            // Listas de Precios
            if (LicenseManager.IsModuleEnabled(DatabaseService.PERMISO_LISTASPRECIOS) && SesionUsuario.TienePermiso(DatabaseService.PERMISO_LISTASPRECIOS))
            {
                puedeVerGestion = true;
                if (this.FindName("btnListasPrecios") != null) btnListasPrecios.Visibility = Visibility.Visible;
            }
            else
            {
                if (this.FindName("btnListasPrecios") != null) btnListasPrecios.Visibility = Visibility.Collapsed;
            }

            // Compras
            if (LicenseManager.IsModuleEnabled(DatabaseService.PERMISO_COMPRAS) && SesionUsuario.TienePermiso(DatabaseService.PERMISO_COMPRAS))
            {
                puedeVerGestion = true;
                btnCompras.Visibility = Visibility.Visible;
            }
            else { btnCompras.Visibility = Visibility.Collapsed; }

            // Proveedores
            if (LicenseManager.IsModuleEnabled(DatabaseService.PERMISO_PROVEEDORES) && SesionUsuario.TienePermiso(DatabaseService.PERMISO_PROVEEDORES))
            {
                puedeVerGestion = true;
                btnProveedores.Visibility = Visibility.Visible;
            }
            else { btnProveedores.Visibility = Visibility.Collapsed; }

            // Stock
            if (LicenseManager.IsModuleEnabled(DatabaseService.PERMISO_STOCK) && SesionUsuario.TienePermiso(DatabaseService.PERMISO_STOCK))
            {
                puedeVerGestion = true;
                btnStock.Visibility = Visibility.Visible;
            }
            else { btnStock.Visibility = Visibility.Collapsed; }

            // Productos
            if (LicenseManager.IsModuleEnabled(DatabaseService.PERMISO_PRODUCTOS) && SesionUsuario.TienePermiso(DatabaseService.PERMISO_PRODUCTOS))
            {
                puedeVerGestion = true;
                btnProductos.Visibility = Visibility.Visible;
            }
            else { btnProductos.Visibility = Visibility.Collapsed; }

            // Clientes
            if (LicenseManager.IsModuleEnabled(DatabaseService.PERMISO_CLIENTES) && SesionUsuario.TienePermiso(DatabaseService.PERMISO_CLIENTES))
            {
                puedeVerGestion = true;
                btnClientes.Visibility = Visibility.Visible;
            }
            else { btnClientes.Visibility = Visibility.Collapsed; }

            if (!puedeVerGestion) headerGestion.Visibility = Visibility.Collapsed;


            // ADMINISTRACIÓN
            bool puedeVerAdmin = false;

            // Usuarios
            if (LicenseManager.IsModuleEnabled(DatabaseService.PERMISO_USUARIOS) && SesionUsuario.TienePermiso(DatabaseService.PERMISO_USUARIOS))
            {
                puedeVerAdmin = true;
                btnUsuarios.Visibility = Visibility.Visible;
            }
            else { btnUsuarios.Visibility = Visibility.Collapsed; }

            // Permisos y Config
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

        // --- CLICS (Eventos del Menú) ---
        private void salirMenuItem_Click(object sender, RoutedEventArgs e) { this.Close(); }

        private void usuariosMenuItem_Click(object sender, RoutedEventArgs e)
        {
            if (!LicenseManager.IsModuleEnabled(DatabaseService.PERMISO_USUARIOS)) return;
            if (mainContentArea.Content is UsuariosControl) return;
            mainContentArea.Content = new UsuariosControl();
        }

        private void clientesMenuItem_Click(object sender, RoutedEventArgs e)
        {
            if (!LicenseManager.IsModuleEnabled(DatabaseService.PERMISO_CLIENTES)) return;
            if (mainContentArea.Content is ClientesControl) return;
            mainContentArea.Content = new ClientesControl();
        }

        // --- CORRECCIÓN AQUÍ: Renombrado para coincidir con tu XAML ---
        private void productosMenuItem_Click(object sender, RoutedEventArgs e)
        {
            if (!LicenseManager.IsModuleEnabled(DatabaseService.PERMISO_PRODUCTOS)) return;
            if (mainContentArea.Content is ProductosControl) return;
            mainContentArea.Content = new ProductosControl();
        }
        // -------------------------------------------------------------

        private void btnInicio_Click(object sender, RoutedEventArgs e)
        {
            if (mainContentArea.Content is DashboardControl) return;
            mainContentArea.Content = new DashboardControl();
        }

        private void btnFacturacion_Click(object sender, RoutedEventArgs e)
        {
            if (!LicenseManager.IsModuleEnabled(DatabaseService.PERMISO_FACTURACION)) return;
            if (mainContentArea.Content is FacturacionControl) return;
            mainContentArea.Content = new FacturacionControl();
        }

        private void btnVentas_Click(object sender, RoutedEventArgs e)
        {
            if (!LicenseManager.IsModuleEnabled(DatabaseService.PERMISO_VENTAS)) return;
            if (mainContentArea.Content is VentasControl) return;
            mainContentArea.Content = new VentasControl();
        }

        private void btnPresupuestos_Click(object sender, RoutedEventArgs e)
        {
            if (!LicenseManager.IsModuleEnabled(DatabaseService.PERMISO_PRESUPUESTOS)) return;
            if (mainContentArea.Content is PresupuestosControl) return;
            mainContentArea.Content = new PresupuestosControl();
        }

        private void btnReportePresupuestos_Click(object sender, RoutedEventArgs e)
        {
            if (!LicenseManager.IsModuleEnabled(DatabaseService.PERMISO_PRESUPUESTOS)) return;
            if (mainContentArea.Content is ReportePresupuestosControl) return;
            ReportePresupuestosControl control = new ReportePresupuestosControl();
            mainContentArea.Content = control;
        }

        private void btnReportesAvanzados_Click(object sender, RoutedEventArgs e)
        {
            if (!LicenseManager.IsModuleEnabled(DatabaseService.PERMISO_VENTAS)) return;
            if (mainContentArea.Content is ReportesControl) return;
            mainContentArea.Content = new ReportesControl();
        }

        private void btnStock_Click(object sender, RoutedEventArgs e)
        {
            if (!LicenseManager.IsModuleEnabled(DatabaseService.PERMISO_STOCK)) return;
            if (mainContentArea.Content is StockControl) return;
            mainContentArea.Content = new StockControl();
        }

        private void btnProveedores_Click(object sender, RoutedEventArgs e)
        {
            if (!LicenseManager.IsModuleEnabled(DatabaseService.PERMISO_PROVEEDORES)) return;
            if (mainContentArea.Content is ProveedoresControl) return;
            mainContentArea.Content = new ProveedoresControl();
        }

        private void btnCompras_Click(object sender, RoutedEventArgs e)
        {
            if (!LicenseManager.IsModuleEnabled(DatabaseService.PERMISO_COMPRAS)) return;
            if (mainContentArea.Content is ComprasControl) return;
            mainContentArea.Content = new ComprasControl();
        }

        private void btnPrecios_Click(object sender, RoutedEventArgs e)
        {
            if (!LicenseManager.IsModuleEnabled(DatabaseService.PERMISO_PRECIOS)) return;
            if (mainContentArea.Content is PreciosControl) return;
            mainContentArea.Content = new PreciosControl();
        }

        private void btnListasPrecios_Click(object sender, RoutedEventArgs e)
        {
            if (!LicenseManager.IsModuleEnabled(DatabaseService.PERMISO_LISTASPRECIOS)) return;
            if (mainContentArea.Content is ListasPreciosControl) return;
            mainContentArea.Content = new ListasPreciosControl();
        }

        private void btnCaja_Click(object sender, RoutedEventArgs e)
        {
            if (!LicenseManager.IsModuleEnabled(DatabaseService.PERMISO_CAJA)) return;
            if (mainContentArea.Content is CajaControl) return;
            mainContentArea.Content = new CajaControl();
        }

        private void btnCtaCte_Click(object sender, RoutedEventArgs e)
        {
            if (!LicenseManager.IsModuleEnabled(DatabaseService.PERMISO_CUENTASCORRIENTES)) return;
            if (mainContentArea.Content is CuentasCorrientesControl) return;
            mainContentArea.Content = new CuentasCorrientesControl();
        }

        private void btnPermisos_Click(object sender, RoutedEventArgs e)
        {
            if (!LicenseManager.IsModuleEnabled(DatabaseService.PERMISO_PERMISOS)) return;
            if (mainContentArea.Content is GestionPermisos) return;
            mainContentArea.Content = new GestionPermisos();
        }

        private void btnConfiguracion_Click(object sender, RoutedEventArgs e)
        {
            if (!LicenseManager.IsModuleEnabled(DatabaseService.PERMISO_PERMISOS)) return;
            if (mainContentArea.Content is ConfiguracionControl) return;
            mainContentArea.Content = new ConfiguracionControl();
        }

        private void Window_Closing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            CustomerScreenService.Cerrar();
            SesionUsuario.Cerrar();
            Application.Current.Shutdown();
        }

        private void btnTeclado_Click(object sender, RoutedEventArgs e)
        {
            KeyboardHelper.ShowOnScreenKeyboard();
        }
    }
}