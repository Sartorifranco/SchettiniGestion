using System;
using System.Collections.Generic;
using System.Configuration;
using System.Data;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Threading;
using System.Data.SqlClient;
using SchettiniGestion;

namespace SchettiniGestion.WPF
{
    public partial class App : Application
    {
        protected override void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);

            this.DispatcherUnhandledException += App_DispatcherUnhandledException;
            AppDomain.CurrentDomain.UnhandledException += CurrentDomain_UnhandledException;

            try
            {
                // Auto-creación de BD y tablas si no existen
                InicializarBaseDeDatosCompleta();

                // Inicialización del servicio de BD
                DatabaseService.InitializeDatabase();
            }
            catch (Exception ex)
            {
                string mensaje = "Error al conectar con la base de datos:\n\n" + ex.Message +
                    "\n\n--- SOLUCIÓN ---\n" +
                    "1. Verifique que SQL Server Express esté instalado\n" +
                    "2. Vaya a Configuración > Red y Servidor y revise la conexión.\n" +
                    "3. Para PC única, use 'Autenticación Windows' con .\\SQLEXPRESS o 127.0.0.1";
                MessageBox.Show(mensaje, "Error de Conexión - SchettiniGestion", MessageBoxButton.OK, MessageBoxImage.Error);
                Shutdown();
                return;
            }

            // Validar licencia del sistema
            bool licenciaValida = LicenseManager.ValidarLicencia();
            if (!licenciaValida)
            {
                string mensaje = SchettiniGestion.LicenseManager.UltimoMensajeError ?? "Error de licencia. La aplicación se cerrará.";
                CustomMessageBox.Show(mensaje, "Error de licencia", MessageBoxButton.OK, MessageBoxImage.Error);
                Application.Current.Shutdown();
            }
        }

        private void InicializarBaseDeDatosCompleta()
        {
            string connectionString = DatabaseService.ConnectionString;
            var builder = new SqlConnectionStringBuilder(connectionString);
            string targetDb = builder.InitialCatalog;

            // Crear la BD si no existe (conectando a master)
            builder.InitialCatalog = "master";
            using (var conn = new SqlConnection(builder.ConnectionString))
            {
                conn.Open();
                using (var cmd = new SqlCommand($"SELECT db_id('{targetDb}')", conn))
                {
                    if (cmd.ExecuteScalar() == DBNull.Value)
                    {
                        using (var cmdCreate = new SqlCommand($"CREATE DATABASE [{targetDb}]", conn))
                            cmdCreate.ExecuteNonQuery();
                        System.Threading.Thread.Sleep(2000);
                    }
                }
            }

            // Crear tablas si no existen
            using (var conn = new SqlConnection(connectionString))
            {
                conn.Open();
                using (var cmdCheck = new SqlCommand("SELECT COUNT(*) FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_NAME = 'Configuracion'", conn))
                {
                    if (Convert.ToInt32(cmdCheck.ExecuteScalar()) == 0)
                    {
                        string scriptTablas = @"
                            CREATE TABLE Configuracion (
                                ID INT PRIMARY KEY IDENTITY(1,1),
                                LicenciaPayload NVARCHAR(MAX),
                                NombreFantasia NVARCHAR(200), RazonSocial NVARCHAR(200), CUIT NVARCHAR(50),
                                Direccion NVARCHAR(200), Telefono NVARCHAR(50), Email NVARCHAR(100),
                                LogoPath NVARCHAR(MAX), CertificadoPath NVARCHAR(MAX), PasswordAfip NVARCHAR(MAX),
                                PuntoVenta INT, MPAccessToken NVARCHAR(MAX), MPUserId NVARCHAR(MAX), MPPosId NVARCHAR(MAX),
                                UsaVisorCliente BIT DEFAULT 0
                            );
                            INSERT INTO Configuracion (NombreFantasia) VALUES ('Mi Negocio');
                            CREATE TABLE Roles (RolID INT PRIMARY KEY IDENTITY(1,1), NombreRol NVARCHAR(50));
                            CREATE TABLE Permisos (PermisoID INT PRIMARY KEY IDENTITY(1,1), NombrePermiso NVARCHAR(100));
                            CREATE TABLE Roles_Permisos (RolID INT, PermisoID INT, PRIMARY KEY (RolID, PermisoID));
                            CREATE TABLE Usuarios (UsuarioID INT PRIMARY KEY IDENTITY(1,1), NombreUsuario NVARCHAR(50), PasswordHash NVARCHAR(MAX), RolID INT, Rol NVARCHAR(50));
                            INSERT INTO Roles (NombreRol) VALUES ('Administrador');
                            INSERT INTO Permisos (NombrePermiso) VALUES ('ACCESO_TOTAL');
                            CREATE TABLE Clientes (
                                ClienteID INT PRIMARY KEY IDENTITY(1,1), CUIT NVARCHAR(50), RazonSocial NVARCHAR(200),
                                CondicionIVA NVARCHAR(50), Direccion NVARCHAR(200), Telefono NVARCHAR(50), Email NVARCHAR(100),
                                PermiteCuentaCorriente BIT DEFAULT 0, MontoLimiteCtaCte DECIMAL(18,2) NULL,
                                SaldoDeuda DECIMAL(18,2) DEFAULT 0
                            );
                            CREATE TABLE Productos (
                                ProductoID INT PRIMARY KEY IDENTITY(1,1), Codigo NVARCHAR(50), CodigoBarra NVARCHAR(50),
                                Descripcion NVARCHAR(200), Categoria NVARCHAR(50), SubRubro NVARCHAR(100), Marca NVARCHAR(100), Proveedor NVARCHAR(100),
                                TipoIVA NVARCHAR(20), PrecioCosto DECIMAL(18,2), Ganancia DECIMAL(18,2), ImpuestoInterno DECIMAL(18,2),
                                PrecioVenta DECIMAL(18,2), StockActual INT, ImagenPath NVARCHAR(MAX)
                            );
                            CREATE TABLE Proveedores (
                                ProveedorID INT PRIMARY KEY IDENTITY(1,1), CUIT NVARCHAR(50), RazonSocial NVARCHAR(200),
                                Direccion NVARCHAR(200), CategoriaFiscal NVARCHAR(100), PersonaContacto NVARCHAR(100),
                                Telefono NVARCHAR(50), PaginaWeb NVARCHAR(300), Email NVARCHAR(100), SaldoDeuda DECIMAL(18,2) DEFAULT 0
                            );
                            CREATE TABLE ListasPrecios (ListaID INT PRIMARY KEY IDENTITY(1,1), Nombre NVARCHAR(100), Porcentaje DECIMAL(18,2));
                            CREATE TABLE Facturas (
                                FacturaID INT PRIMARY KEY IDENTITY(1,1), ClienteID INT, Fecha DATETIME, Total DECIMAL(18,2),
                                TipoComprobante NVARCHAR(50), CondicionVenta NVARCHAR(100), CAE NVARCHAR(50),
                                VencimientoCAE NVARCHAR(20), NumeroComprobanteAFIP INT
                            );
                            CREATE TABLE FacturaDetalle (
                                DetalleID INT PRIMARY KEY IDENTITY(1,1), FacturaID INT, ProductoID INT,
                                Cantidad INT, PrecioUnitario DECIMAL(18,2)
                            );
                            CREATE TABLE MovimientosCaja (
                                MovimientoID INT PRIMARY KEY IDENTITY(1,1), Fecha DATETIME, Concepto NVARCHAR(200),
                                Tipo NVARCHAR(20), Monto DECIMAL(18,2), Usuario NVARCHAR(50)
                            );
                            CREATE TABLE MovimientosStock (
                                MovimientoID INT PRIMARY KEY IDENTITY(1,1), ProductoID INT, FacturaID INT NULL,
                                CompraID INT NULL, Fecha DATETIME, TipoMovimiento NVARCHAR(50), Cantidad INT
                            );
                            CREATE TABLE MovimientosCuentaCorriente (
                                MovimientoID INT PRIMARY KEY IDENTITY(1,1), ClienteID INT NULL, ProveedorID INT NULL,
                                Fecha DATETIME, Descripcion NVARCHAR(200), Monto DECIMAL(18,2), SaldoHistorico DECIMAL(18,2)
                            );
                            CREATE TABLE Presupuestos (PresupuestoID INT PRIMARY KEY IDENTITY(1,1), ClienteID INT, Fecha DATETIME, Total DECIMAL(18,2), Estado NVARCHAR(50));
                            CREATE TABLE PresupuestoDetalle (DetalleID INT PRIMARY KEY IDENTITY(1,1), PresupuestoID INT, ProductoID INT, Cantidad INT, PrecioUnitario DECIMAL(18,2));
                            CREATE TABLE Compras (CompraID INT PRIMARY KEY IDENTITY(1,1), ProveedorID INT, Fecha DATETIME, Total DECIMAL(18,2), TipoComprobante NVARCHAR(50));
                            CREATE TABLE CompraDetalle (DetalleID INT PRIMARY KEY IDENTITY(1,1), CompraID INT, ProductoID INT, Cantidad INT, PrecioCosto DECIMAL(18,2));
                        ";
                        using (var cmdScript = new SqlCommand(scriptTablas, conn))
                            cmdScript.ExecuteNonQuery();
                    }
                }
            }
        }

        private void App_DispatcherUnhandledException(object sender, DispatcherUnhandledExceptionEventArgs e)
        {
            string detalle = e.Exception.Message;
            if (e.Exception.StackTrace != null)
                detalle += "\n\nUbicación:\n" + e.Exception.StackTrace.Split(new[] { '\r', '\n' })[0];
            MessageBox.Show("Error UI: " + detalle);
            e.Handled = true;
        }

        private void CurrentDomain_UnhandledException(object sender, UnhandledExceptionEventArgs e)
        {
            MessageBox.Show("Error Fatal: " + (e.ExceptionObject as Exception)?.Message);
        }
    }
}
