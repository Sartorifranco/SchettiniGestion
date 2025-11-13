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
using SchettiniGestion; // ¡Importamos nuestra lógica!

namespace SchettiniGestion.WPF
{
    public partial class PrincipalWindow : Window
    {
        public PrincipalWindow()
        {
            InitializeComponent();
            btnInicio_Click(null, null); // Cargamos la pantalla de "Inicio" por defecto
        }

        // --- LÓGICA DE PERMISOS ---
        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            AplicarPermisos();
        }

        private void AplicarPermisos()
        {
            // Ocultamos todo por defecto (excepto Inicio y Salir) y luego mostramos según permiso.

            // Sección Facturación
            if (!LicenseManager.IsModuleEnabled(DatabaseService.PERMISO_FACTURACION) ||
                !SesionUsuario.TienePermiso(DatabaseService.PERMISO_FACTURACION))
            {
                btnFacturacion.Visibility = Visibility.Collapsed;
            }

            // Sección Reportes
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
            if (!puedeVerReportes)
            {
                headerReportes.Visibility = Visibility.Collapsed;
            }


            // Sección Gestión
            bool puedeVerGestion = false;

            // ===== INICIO DE CÓDIGO NUEVO (PRECIOS) =====
            if (LicenseManager.IsModuleEnabled(DatabaseService.PERMISO_PRECIOS) &&
                SesionUsuario.TienePermiso(DatabaseService.PERMISO_PRECIOS))
            {
                puedeVerGestion = true;
            }
            else
            {
                btnPrecios.Visibility = Visibility.Collapsed;
            }
            // ===== FIN DE CÓDIGO NUEVO =====

            if (LicenseManager.IsModuleEnabled(DatabaseService.PERMISO_COMPRAS) &&
                SesionUsuario.TienePermiso(DatabaseService.PERMISO_COMPRAS))
            {
                puedeVerGestion = true;
            }
            else
            {
                btnCompras.Visibility = Visibility.Collapsed;
            }

            if (LicenseManager.IsModuleEnabled(DatabaseService.PERMISO_PROVEEDORES) &&
                SesionUsuario.TienePermiso(DatabaseService.PERMISO_PROVEEDORES))
            {
                puedeVerGestion = true;
            }
            else
            {
                btnProveedores.Visibility = Visibility.Collapsed;
            }

            if (LicenseManager.IsModuleEnabled(DatabaseService.PERMISO_STOCK) &&
                SesionUsuario.TienePermiso(DatabaseService.PERMISO_STOCK))
            {
                puedeVerGestion = true;
            }
            else
            {
                btnStock.Visibility = Visibility.Collapsed;
            }

            if (LicenseManager.IsModuleEnabled(DatabaseService.PERMISO_PRODUCTOS) &&
                SesionUsuario.TienePermiso(DatabaseService.PERMISO_PRODUCTOS))
            {
                puedeVerGestion = true;
            }
            else
            {
                btnProductos.Visibility = Visibility.Collapsed;
            }

            if (LicenseManager.IsModuleEnabled(DatabaseService.PERMISO_CLIENTES) &&
                SesionUsuario.TienePermiso(DatabaseService.PERMISO_CLIENTES))
            {
                puedeVerGestion = true;
            }
            else
            {
                btnClientes.Visibility = Visibility.Collapsed;
            }
            if (!puedeVerGestion)
            {
                headerGestion.Visibility = Visibility.Collapsed;
            }


            // Sección Administración
            bool puedeVerAdmin = false;
            if (LicenseManager.IsModuleEnabled(DatabaseService.PERMISO_USUARIOS) &&
                SesionUsuario.TienePermiso(DatabaseService.PERMISO_USUARIOS))
            {
                puedeVerAdmin = true;
            }
            else
            {
                btnUsuarios.Visibility = Visibility.Collapsed;
            }

            if (LicenseManager.IsModuleEnabled(DatabaseService.PERMISO_PERMISOS) &&
                SesionUsuario.TienePermiso(DatabaseService.PERMISO_PERMISOS))
            {
                puedeVerAdmin = true;
            }
            else
            {
                btnPermisos.Visibility = Visibility.Collapsed;
            }
            if (!puedeVerAdmin)
            {
                headerAdministracion.Visibility = Visibility.Collapsed;
            }
        }

        // --- LÓGICA DEL MENÚ ---
        private void salirMenuItem_Click(object sender, RoutedEventArgs e)
        {
            this.Close();
        }

        private void usuariosMenuItem_Click(object sender, RoutedEventArgs e)
        {
            if (!LicenseManager.IsModuleEnabled(DatabaseService.PERMISO_USUARIOS) ||
                !SesionUsuario.TienePermiso(DatabaseService.PERMISO_USUARIOS)) return;

            UsuariosControl controlUsuarios = new UsuariosControl();
            mainContentArea.Content = controlUsuarios;
        }

        private void clientesMenuItem_Click(object sender, RoutedEventArgs e)
        {
            if (!LicenseManager.IsModuleEnabled(DatabaseService.PERMISO_CLIENTES) ||
                !SesionUsuario.TienePermiso(DatabaseService.PERMISO_CLIENTES)) return;

            ClientesControl controlClientes = new ClientesControl();
            mainContentArea.Content = controlClientes;
        }

        private void productosMenuItem_Click(object sender, RoutedEventArgs e)
        {
            if (!LicenseManager.IsModuleEnabled(DatabaseService.PERMISO_PRODUCTOS) ||
                !SesionUsuario.TienePermiso(DatabaseService.PERMISO_PRODUCTOS)) return;

            ProductosControl controlProductos = new ProductosControl();
            mainContentArea.Content = controlProductos;
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
            if (!LicenseManager.IsModuleEnabled(DatabaseService.PERMISO_FACTURACION) ||
                !SesionUsuario.TienePermiso(DatabaseService.PERMISO_FACTURACION)) return;

            FacturacionControl controlFacturacion = new FacturacionControl();
            mainContentArea.Content = controlFacturacion;
        }

        private void btnVentas_Click(object sender, RoutedEventArgs e)
        {
            if (!LicenseManager.IsModuleEnabled(DatabaseService.PERMISO_VENTAS) ||
                !SesionUsuario.TienePermiso(DatabaseService.PERMISO_VENTAS)) return;

            VentasControl controlVentas = new VentasControl();
            mainContentArea.Content = controlVentas;
        }


        private void btnStock_Click(object sender, RoutedEventArgs e)
        {
            if (!LicenseManager.IsModuleEnabled(DatabaseService.PERMISO_STOCK) ||
                !SesionUsuario.TienePermiso(DatabaseService.PERMISO_STOCK)) return;

            StockControl controlStock = new StockControl();
            mainContentArea.Content = controlStock;
        }

        private void btnProveedores_Click(object sender, RoutedEventArgs e)
        {
            if (!LicenseManager.IsModuleEnabled(DatabaseService.PERMISO_PROVEEDORES) ||
                !SesionUsuario.TienePermiso(DatabaseService.PERMISO_PROVEEDORES)) return;

            ProveedoresControl controlProveedores = new ProveedoresControl();
            mainContentArea.Content = controlProveedores;
        }

        private void btnCompras_Click(object sender, RoutedEventArgs e)
        {
            if (!LicenseManager.IsModuleEnabled(DatabaseService.PERMISO_COMPRAS) ||
                !SesionUsuario.TienePermiso(DatabaseService.PERMISO_COMPRAS)) return;

            ComprasControl controlCompras = new ComprasControl();
            mainContentArea.Content = controlCompras;
        }

        // ===== INICIO DE CÓDIGO NUEVO (PRECIOS) =====
        private void btnPrecios_Click(object sender, RoutedEventArgs e)
        {
            // Doble Guardia de seguridad
            if (!LicenseManager.IsModuleEnabled(DatabaseService.PERMISO_PRECIOS) ||
                !SesionUsuario.TienePermiso(DatabaseService.PERMISO_PRECIOS)) return;

            PreciosControl controlPrecios = new PreciosControl();
            mainContentArea.Content = controlPrecios;
        }
        // ===== FIN DE CÓDIGO NUEVO =====

        private void btnPermisos_Click(object sender, RoutedEventArgs e)
        {
            if (!LicenseManager.IsModuleEnabled(DatabaseService.PERMISO_PERMISOS) ||
                !SesionUsuario.TienePermiso(DatabaseService.PERMISO_PERMISOS)) return;

            GestionPermisos controlPermisos = new GestionPermisos();
            mainContentArea.Content = controlPermisos;
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