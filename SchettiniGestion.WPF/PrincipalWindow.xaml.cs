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
            btnInicio_Click(null, null);
        }

        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            AplicarPermisos();
        }

        private void AplicarPermisos()
        {
            // Facturación
            if (!LicenseManager.IsModuleEnabled(DatabaseService.PERMISO_FACTURACION) ||
                !SesionUsuario.TienePermiso(DatabaseService.PERMISO_FACTURACION))
            {
                btnFacturacion.Visibility = Visibility.Collapsed;
            }

            // Reportes (Ventas y Presupuestos)
            bool puedeVerReportes = false;
            if (LicenseManager.IsModuleEnabled(DatabaseService.PERMISO_VENTAS) &&
                SesionUsuario.TienePermiso(DatabaseService.PERMISO_VENTAS))
            {
                puedeVerReportes = true;
            }
            else
            {
                btnVentas.Visibility = Visibility.Collapsed;
            }

            if (LicenseManager.IsModuleEnabled(DatabaseService.PERMISO_PRESUPUESTOS) &&
                SesionUsuario.TienePermiso(DatabaseService.PERMISO_PRESUPUESTOS))
            {
                puedeVerReportes = true;
            }
            else
            {
                btnPresupuestos.Visibility = Visibility.Collapsed;
            }

            if (!puedeVerReportes) headerReportes.Visibility = Visibility.Collapsed;


            // Gestión (Caja, Precios, Compras, etc.)
            bool puedeVerGestion = false;

            if (LicenseManager.IsModuleEnabled(DatabaseService.PERMISO_CAJA) &&
                SesionUsuario.TienePermiso(DatabaseService.PERMISO_CAJA))
            {
                puedeVerGestion = true;
            }
            else { btnCaja.Visibility = Visibility.Collapsed; }

            // ===== CÓDIGO NUEVO (CUENTAS CORRIENTES) =====
            if (LicenseManager.IsModuleEnabled(DatabaseService.PERMISO_CUENTASCORRIENTES) &&
                SesionUsuario.TienePermiso(DatabaseService.PERMISO_CUENTASCORRIENTES))
            {
                puedeVerGestion = true;
                btnCtaCte.Visibility = Visibility.Visible;
            }
            else { btnCtaCte.Visibility = Visibility.Collapsed; }
            // ============================================

            if (LicenseManager.IsModuleEnabled(DatabaseService.PERMISO_PRECIOS) &&
                SesionUsuario.TienePermiso(DatabaseService.PERMISO_PRECIOS))
            {
                puedeVerGestion = true;
            }
            else { btnPrecios.Visibility = Visibility.Collapsed; }

            if (LicenseManager.IsModuleEnabled(DatabaseService.PERMISO_COMPRAS) &&
                SesionUsuario.TienePermiso(DatabaseService.PERMISO_COMPRAS))
            {
                puedeVerGestion = true;
            }
            else { btnCompras.Visibility = Visibility.Collapsed; }

            if (LicenseManager.IsModuleEnabled(DatabaseService.PERMISO_PROVEEDORES) &&
                SesionUsuario.TienePermiso(DatabaseService.PERMISO_PROVEEDORES))
            {
                puedeVerGestion = true;
            }
            else { btnProveedores.Visibility = Visibility.Collapsed; }

            if (LicenseManager.IsModuleEnabled(DatabaseService.PERMISO_STOCK) &&
                SesionUsuario.TienePermiso(DatabaseService.PERMISO_STOCK))
            {
                puedeVerGestion = true;
            }
            else { btnStock.Visibility = Visibility.Collapsed; }

            if (LicenseManager.IsModuleEnabled(DatabaseService.PERMISO_PRODUCTOS) &&
                SesionUsuario.TienePermiso(DatabaseService.PERMISO_PRODUCTOS))
            {
                puedeVerGestion = true;
            }
            else { btnProductos.Visibility = Visibility.Collapsed; }

            if (LicenseManager.IsModuleEnabled(DatabaseService.PERMISO_CLIENTES) &&
                SesionUsuario.TienePermiso(DatabaseService.PERMISO_CLIENTES))
            {
                puedeVerGestion = true;
            }
            else { btnClientes.Visibility = Visibility.Collapsed; }

            if (!puedeVerGestion) headerGestion.Visibility = Visibility.Collapsed;


            // Administración
            bool puedeVerAdmin = false;
            if (LicenseManager.IsModuleEnabled(DatabaseService.PERMISO_USUARIOS) &&
                SesionUsuario.TienePermiso(DatabaseService.PERMISO_USUARIOS))
            {
                puedeVerAdmin = true;
            }
            else { btnUsuarios.Visibility = Visibility.Collapsed; }

            if (LicenseManager.IsModuleEnabled(DatabaseService.PERMISO_PERMISOS) &&
                SesionUsuario.TienePermiso(DatabaseService.PERMISO_PERMISOS))
            {
                puedeVerAdmin = true;
            }
            else { btnPermisos.Visibility = Visibility.Collapsed; }

            if (!puedeVerAdmin) headerAdministracion.Visibility = Visibility.Collapsed;
        }

        // --- CLICS ---
        private void salirMenuItem_Click(object sender, RoutedEventArgs e) { this.Close(); }

        private void usuariosMenuItem_Click(object sender, RoutedEventArgs e)
        {
            if (!LicenseManager.IsModuleEnabled(DatabaseService.PERMISO_USUARIOS) || !SesionUsuario.TienePermiso(DatabaseService.PERMISO_USUARIOS)) return;
            mainContentArea.Content = new UsuariosControl();
        }

        private void clientesMenuItem_Click(object sender, RoutedEventArgs e)
        {
            if (!LicenseManager.IsModuleEnabled(DatabaseService.PERMISO_CLIENTES) || !SesionUsuario.TienePermiso(DatabaseService.PERMISO_CLIENTES)) return;
            mainContentArea.Content = new ClientesControl();
        }

        private void productosMenuItem_Click(object sender, RoutedEventArgs e)
        {
            if (!LicenseManager.IsModuleEnabled(DatabaseService.PERMISO_PRODUCTOS) || !SesionUsuario.TienePermiso(DatabaseService.PERMISO_PRODUCTOS)) return;
            mainContentArea.Content = new ProductosControl();
        }

        private void btnInicio_Click(object sender, RoutedEventArgs e)
        {
            mainContentArea.Content = new TextBlock
            {
                Text = $"¡Bienvenido, {SesionUsuario.NombreUsuario}!",
                FontSize = 48,
                FontWeight = FontWeights.Bold,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center,
                Foreground = (SolidColorBrush)FindResource("BodyForegroundBrush")
            };
        }

        private void btnFacturacion_Click(object sender, RoutedEventArgs e)
        {
            if (!LicenseManager.IsModuleEnabled(DatabaseService.PERMISO_FACTURACION) || !SesionUsuario.TienePermiso(DatabaseService.PERMISO_FACTURACION)) return;
            mainContentArea.Content = new FacturacionControl();
        }

        private void btnVentas_Click(object sender, RoutedEventArgs e)
        {
            if (!LicenseManager.IsModuleEnabled(DatabaseService.PERMISO_VENTAS) || !SesionUsuario.TienePermiso(DatabaseService.PERMISO_VENTAS)) return;
            mainContentArea.Content = new VentasControl();
        }

        private void btnPresupuestos_Click(object sender, RoutedEventArgs e)
        {
            if (!LicenseManager.IsModuleEnabled(DatabaseService.PERMISO_PRESUPUESTOS) || !SesionUsuario.TienePermiso(DatabaseService.PERMISO_PRESUPUESTOS)) return;
            mainContentArea.Content = new PresupuestosControl();
        }

        private void btnReportePresupuestos_Click(object sender, RoutedEventArgs e)
        {
            if (!LicenseManager.IsModuleEnabled(DatabaseService.PERMISO_PRESUPUESTOS) || !SesionUsuario.TienePermiso(DatabaseService.PERMISO_PRESUPUESTOS)) return;
            ReportePresupuestosControl control = new ReportePresupuestosControl();
            mainContentArea.Content = control;
        }

        private void btnStock_Click(object sender, RoutedEventArgs e)
        {
            if (!LicenseManager.IsModuleEnabled(DatabaseService.PERMISO_STOCK) || !SesionUsuario.TienePermiso(DatabaseService.PERMISO_STOCK)) return;
            mainContentArea.Content = new StockControl();
        }

        private void btnProveedores_Click(object sender, RoutedEventArgs e)
        {
            if (!LicenseManager.IsModuleEnabled(DatabaseService.PERMISO_PROVEEDORES) || !SesionUsuario.TienePermiso(DatabaseService.PERMISO_PROVEEDORES)) return;
            mainContentArea.Content = new ProveedoresControl();
        }

        private void btnCompras_Click(object sender, RoutedEventArgs e)
        {
            if (!LicenseManager.IsModuleEnabled(DatabaseService.PERMISO_COMPRAS) || !SesionUsuario.TienePermiso(DatabaseService.PERMISO_COMPRAS)) return;
            mainContentArea.Content = new ComprasControl();
        }

        private void btnPrecios_Click(object sender, RoutedEventArgs e)
        {
            if (!LicenseManager.IsModuleEnabled(DatabaseService.PERMISO_PRECIOS) || !SesionUsuario.TienePermiso(DatabaseService.PERMISO_PRECIOS)) return;
            mainContentArea.Content = new PreciosControl();
        }

        private void btnCaja_Click(object sender, RoutedEventArgs e)
        {
            if (!LicenseManager.IsModuleEnabled(DatabaseService.PERMISO_CAJA) || !SesionUsuario.TienePermiso(DatabaseService.PERMISO_CAJA)) return;
            mainContentArea.Content = new CajaControl();
        }

        // ===== CÓDIGO NUEVO (CUENTAS CORRIENTES) =====
        private void btnCtaCte_Click(object sender, RoutedEventArgs e)
        {
            if (!LicenseManager.IsModuleEnabled(DatabaseService.PERMISO_CUENTASCORRIENTES) || !SesionUsuario.TienePermiso(DatabaseService.PERMISO_CUENTASCORRIENTES)) return;
            mainContentArea.Content = new CuentasCorrientesControl();
        }
        // ============================================

        private void btnPermisos_Click(object sender, RoutedEventArgs e)
        {
            if (!LicenseManager.IsModuleEnabled(DatabaseService.PERMISO_PERMISOS) || !SesionUsuario.TienePermiso(DatabaseService.PERMISO_PERMISOS)) return;
            mainContentArea.Content = new GestionPermisos();
        }

        private void Window_Closing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            SesionUsuario.Cerrar();
            Application.Current.Shutdown();
        }

        private void btnTeclado_Click(object sender, RoutedEventArgs e)
        {
            KeyboardHelper.ShowOnScreenKeyboard();
        }
    }
}