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

            AppCulture.Initialize();
            ThemeManager.LoadSavedTheme();

            this.DispatcherUnhandledException += App_DispatcherUnhandledException;
            AppDomain.CurrentDomain.UnhandledException += CurrentDomain_UnhandledException;

            // Intentar conectar y crear BD. Si falla, mostrar asistente de primer uso.
            if (!IntentarInicializarConexion())
            {
                var setup = new PrimerUsoWindow();
                bool? ok = setup.ShowDialog();
                if (ok != true)
                {
                    Shutdown();
                    return;
                }
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

        /// <summary>
        /// Intenta conectar con la cadena actual y, si falla, prueba fallbacks automáticos.
        /// Retorna true si logra inicializar la BD correctamente.
        /// </summary>
        private bool IntentarInicializarConexion()
        {
            // Cadenas a probar en orden
            string[] candidatos = new string[]
            {
                DatabaseService.ConnectionString,           // 1. conexion.cfg o App.config
                DatabaseService.CS_LOCALDB,                 // 2. LocalDB (instalación nueva)
                DatabaseService.CS_SQLEXPRESS,              // 3. SQL Server Express local
            };

            foreach (string cs in candidatos)
            {
                try
                {
                    InicializarBaseDeDatosCompleta(cs);
                    // Si llegamos aquí, funcionó. Guardamos si era diferente al actual.
                    if (cs != DatabaseService.ConnectionString)
                        DatabaseService.ActualizarConexion(cs);
                    DatabaseService.InitializeDatabase();
                    DatabaseService.AsegurarUsuarioAdminInicial();
                    return true;
                }
                catch { /* probar siguiente */ }
            }
            return false;
        }

        private void InicializarBaseDeDatosCompleta(string connectionString = null)
        {
            connectionString = connectionString ?? DatabaseService.ConnectionString;
            var builder = new SqlConnectionStringBuilder(connectionString);
            string targetDb = builder.InitialCatalog;

            // Crear la BD si no existe (conectando a master del mismo servidor)
            builder.InitialCatalog = "master";
            using (var conn = new SqlConnection(builder.ConnectionString))
            {
                conn.Open();
                using (var cmd = new SqlCommand($"SELECT db_id(N'{targetDb}')", conn))
                {
                    if (cmd.ExecuteScalar() == DBNull.Value)
                    {
                        using (var cmdCreate = new SqlCommand($"CREATE DATABASE [{targetDb}]", conn))
                            cmdCreate.ExecuteNonQuery();
                        System.Threading.Thread.Sleep(1500);
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

                // Tablas adicionales: se crean solo si no existen (para BDs ya inicializadas)
                string[] scriptsAdicionales = new string[]
                {
                    @"IF NOT EXISTS (SELECT * FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_NAME='MediosPago')
                      CREATE TABLE MediosPago (
                          MedioID INT PRIMARY KEY IDENTITY(1,1),
                          Nombre NVARCHAR(100) NOT NULL,
                          Activo BIT DEFAULT 1,
                          Orden INT DEFAULT 0
                      );
                      IF NOT EXISTS (SELECT * FROM MediosPago WHERE Nombre='Efectivo')
                          INSERT INTO MediosPago (Nombre,Activo,Orden) VALUES ('Efectivo',1,1);
                      IF NOT EXISTS (SELECT * FROM MediosPago WHERE Nombre='Tarjeta Débito')
                          INSERT INTO MediosPago (Nombre,Activo,Orden) VALUES ('Tarjeta Débito',1,2);
                      IF NOT EXISTS (SELECT * FROM MediosPago WHERE Nombre='Tarjeta Crédito')
                          INSERT INTO MediosPago (Nombre,Activo,Orden) VALUES ('Tarjeta Crédito',1,3);
                      IF NOT EXISTS (SELECT * FROM MediosPago WHERE Nombre='Transferencia')
                          INSERT INTO MediosPago (Nombre,Activo,Orden) VALUES ('Transferencia',1,4);
                      IF NOT EXISTS (SELECT * FROM MediosPago WHERE Nombre='Cheque')
                          INSERT INTO MediosPago (Nombre,Activo,Orden) VALUES ('Cheque',1,5);",

                    @"IF NOT EXISTS (SELECT * FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_NAME='RecepcionesCompra')
                      CREATE TABLE RecepcionesCompra (
                          RecepcionID INT PRIMARY KEY IDENTITY(1,1),
                          CompraID INT NULL,
                          ProveedorID INT,
                          Fecha DATETIME,
                          Observaciones NVARCHAR(500),
                          Estado NVARCHAR(50) DEFAULT 'Recibido'
                      );",

                    @"IF NOT EXISTS (SELECT * FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_NAME='RecepcionCompraDetalle')
                      CREATE TABLE RecepcionCompraDetalle (
                          DetalleID INT PRIMARY KEY IDENTITY(1,1),
                          RecepcionID INT,
                          ProductoID INT,
                          CantidadEsperada INT,
                          CantidadRecibida INT,
                          PrecioCosto DECIMAL(18,2)
                      );",

                    @"IF NOT EXISTS (SELECT * FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_NAME='NotasCreditoDebitoCompras')
                      CREATE TABLE NotasCreditoDebitoCompras (
                          NotaID INT PRIMARY KEY IDENTITY(1,1),
                          ProveedorID INT,
                          Tipo NVARCHAR(10),
                          Fecha DATETIME,
                          Monto DECIMAL(18,2),
                          Descripcion NVARCHAR(500),
                          NumeroComprobante NVARCHAR(50)
                      );",

                    @"IF NOT EXISTS (SELECT * FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_NAME='GastosRapidos')
                      CREATE TABLE GastosRapidos (
                          GastoID INT PRIMARY KEY IDENTITY(1,1),
                          Fecha DATETIME,
                          Concepto NVARCHAR(200),
                          Categoria NVARCHAR(100),
                          Monto DECIMAL(18,2),
                          MedioPago NVARCHAR(50),
                          Usuario NVARCHAR(50)
                      );",

                    @"IF NOT EXISTS (SELECT * FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_NAME='PagosProveedores')
                      CREATE TABLE PagosProveedores (
                          PagoID INT PRIMARY KEY IDENTITY(1,1),
                          ProveedorID INT,
                          Fecha DATETIME,
                          Monto DECIMAL(18,2),
                          MedioPago NVARCHAR(50),
                          Concepto NVARCHAR(200),
                          NumeroComprobante NVARCHAR(50)
                      );",

                    @"IF NOT EXISTS (SELECT * FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_NAME='OrdenCompra')
                      CREATE TABLE OrdenCompra (
                          OrdenID INT PRIMARY KEY IDENTITY(1,1),
                          ProveedorID INT,
                          Fecha DATETIME,
                          FechaEntrega DATETIME NULL,
                          Estado NVARCHAR(50) DEFAULT 'Pendiente',
                          Observaciones NVARCHAR(500),
                          Total DECIMAL(18,2) DEFAULT 0
                      );",

                    @"IF NOT EXISTS (SELECT * FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_NAME='OrdenCompraDetalle')
                      CREATE TABLE OrdenCompraDetalle (
                          DetalleID INT PRIMARY KEY IDENTITY(1,1),
                          OrdenID INT,
                          ProductoID INT,
                          Cantidad INT,
                          PrecioCosto DECIMAL(18,2)
                      );",

                    @"IF NOT EXISTS (SELECT * FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_NAME='ProductosListas')
                      CREATE TABLE ProductosListas (
                          ProductoID INT,
                          ListaID INT,
                          PRIMARY KEY (ProductoID, ListaID)
                      );",

                    @"IF NOT EXISTS (SELECT * FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_NAME='ProductoComboDetalle')
                      CREATE TABLE ProductoComboDetalle (
                          ComboID INT PRIMARY KEY IDENTITY(1,1),
                          ProductoPadreID INT,
                          ProductoHijoID INT,
                          Cantidad INT DEFAULT 1
                      );",

                    @"IF NOT EXISTS (SELECT * FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_NAME='ReservasStock')
                      CREATE TABLE ReservasStock (
                          ReservaID INT PRIMARY KEY IDENTITY(1,1),
                          ProductoID INT,
                          ClienteID INT NULL,
                          Fecha DATETIME,
                          FechaVencimiento DATETIME NULL,
                          Cantidad INT,
                          Motivo NVARCHAR(200),
                          Estado NVARCHAR(50) DEFAULT 'Activa',
                          Usuario NVARCHAR(50)
                      );",

                    @"IF NOT EXISTS (SELECT * FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_NAME='CierresCaja')
                      CREATE TABLE CierresCaja (
                          CierreID INT PRIMARY KEY IDENTITY(1,1),
                          Fecha DATETIME,
                          SaldoApertura DECIMAL(18,2),
                          TotalIngresos DECIMAL(18,2),
                          TotalEgresos DECIMAL(18,2),
                          SaldoCierre DECIMAL(18,2),
                          TotalEfectivo DECIMAL(18,2),
                          TotalTarjeta DECIMAL(18,2),
                          TotalTransferencia DECIMAL(18,2),
                          Observaciones NVARCHAR(500),
                          Usuario NVARCHAR(50)
                      );",

                    @"IF NOT EXISTS (SELECT * FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_NAME='FacturasCobranza')
                      CREATE TABLE FacturasCobranza (
                          CobranzaID INT PRIMARY KEY IDENTITY(1,1),
                          FacturaID INT,
                          MedioPagoID INT,
                          NombreMedio NVARCHAR(100),
                          Monto DECIMAL(18,2),
                          NroTarjeta NVARCHAR(20),
                          NroCuotas INT DEFAULT 1
                      );",

                    @"IF NOT EXISTS (SELECT * FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_NAME='Remitos')
                      CREATE TABLE Remitos (
                          RemitoID INT PRIMARY KEY IDENTITY(1,1),
                          ClienteID INT,
                          FacturaID INT NULL,
                          Fecha DATETIME,
                          Estado NVARCHAR(50) DEFAULT 'Emitido',
                          Observaciones NVARCHAR(500)
                      );
                      IF NOT EXISTS (SELECT * FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_NAME='RemitoDetalle')
                      CREATE TABLE RemitoDetalle (
                          DetalleID INT PRIMARY KEY IDENTITY(1,1),
                          RemitoID INT,
                          ProductoID INT,
                          Cantidad INT,
                          PrecioUnitario DECIMAL(18,2)
                      );",

                    @"IF NOT EXISTS (SELECT * FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_NAME='Pedidos')
                      CREATE TABLE Pedidos (
                          PedidoID INT PRIMARY KEY IDENTITY(1,1),
                          ClienteID INT,
                          Fecha DATETIME,
                          FechaEntrega DATETIME NULL,
                          Estado NVARCHAR(50) DEFAULT 'Pendiente',
                          Total DECIMAL(18,2),
                          Observaciones NVARCHAR(500)
                      );
                      IF NOT EXISTS (SELECT * FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_NAME='PedidoDetalle')
                      CREATE TABLE PedidoDetalle (
                          DetalleID INT PRIMARY KEY IDENTITY(1,1),
                          PedidoID INT,
                          ProductoID INT,
                          Cantidad INT,
                          PrecioUnitario DECIMAL(18,2)
                      );",

                    @"IF NOT EXISTS (SELECT * FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_NAME='NotasCreditoDebitoVentas')
                      CREATE TABLE NotasCreditoDebitoVentas (
                          NotaID INT PRIMARY KEY IDENTITY(1,1),
                          ClienteID INT,
                          FacturaID INT NULL,
                          Tipo NVARCHAR(10),
                          Fecha DATETIME,
                          Monto DECIMAL(18,2),
                          Descripcion NVARCHAR(500),
                          NumeroComprobante NVARCHAR(50)
                      );",

                    // Columnas que pueden faltar en tablas existentes
                    @"IF NOT EXISTS (SELECT * FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME='Clientes' AND COLUMN_NAME='SaldoDeuda')
                      ALTER TABLE Clientes ADD SaldoDeuda DECIMAL(18,2) NOT NULL DEFAULT 0;",

                    @"IF NOT EXISTS (SELECT * FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME='Clientes' AND COLUMN_NAME='PermiteCuentaCorriente')
                      ALTER TABLE Clientes ADD PermiteCuentaCorriente BIT NOT NULL DEFAULT 0;",

                    @"IF NOT EXISTS (SELECT * FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME='Clientes' AND COLUMN_NAME='MontoLimiteCtaCte')
                      ALTER TABLE Clientes ADD MontoLimiteCtaCte DECIMAL(18,2) NULL;",

                    @"IF NOT EXISTS (SELECT * FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME='Proveedores' AND COLUMN_NAME='SaldoDeuda')
                      ALTER TABLE Proveedores ADD SaldoDeuda DECIMAL(18,2) NOT NULL DEFAULT 0;",

                    @"IF NOT EXISTS (SELECT * FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME='Facturas' AND COLUMN_NAME='CAE')
                      ALTER TABLE Facturas ADD CAE NVARCHAR(50) NULL;
                      IF NOT EXISTS (SELECT * FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME='Facturas' AND COLUMN_NAME='VencimientoCAE')
                      ALTER TABLE Facturas ADD VencimientoCAE NVARCHAR(20) NULL;
                      IF NOT EXISTS (SELECT * FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME='Facturas' AND COLUMN_NAME='NumeroComprobanteAFIP')
                      ALTER TABLE Facturas ADD NumeroComprobanteAFIP INT NULL;",

                    // Tablas base que pueden no existir en DBs antiguas
                    @"IF NOT EXISTS (SELECT * FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_NAME='MovimientosCaja')
                      CREATE TABLE MovimientosCaja (
                          MovimientoID INT PRIMARY KEY IDENTITY(1,1),
                          Fecha DATETIME,
                          Concepto NVARCHAR(200),
                          Tipo NVARCHAR(20),
                          Monto DECIMAL(18,2),
                          Usuario NVARCHAR(50)
                      );",

                    @"IF NOT EXISTS (SELECT * FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_NAME='MovimientosStock')
                      CREATE TABLE MovimientosStock (
                          MovimientoID INT PRIMARY KEY IDENTITY(1,1),
                          ProductoID INT,
                          FacturaID INT NULL,
                          CompraID INT NULL,
                          Fecha DATETIME,
                          TipoMovimiento NVARCHAR(50),
                          Cantidad INT
                      );",

                    @"IF NOT EXISTS (SELECT * FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_NAME='MovimientosCuentaCorriente')
                      CREATE TABLE MovimientosCuentaCorriente (
                          MovimientoID INT PRIMARY KEY IDENTITY(1,1),
                          ClienteID INT NULL,
                          ProveedorID INT NULL,
                          Fecha DATETIME,
                          Descripcion NVARCHAR(200),
                          Monto DECIMAL(18,2),
                          SaldoHistorico DECIMAL(18,2)
                      );",

                    @"IF NOT EXISTS (SELECT * FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_NAME='Presupuestos')
                      CREATE TABLE Presupuestos (
                          PresupuestoID INT PRIMARY KEY IDENTITY(1,1),
                          ClienteID INT,
                          Fecha DATETIME,
                          Total DECIMAL(18,2),
                          Estado NVARCHAR(50)
                      );
                      IF NOT EXISTS (SELECT * FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_NAME='PresupuestoDetalle')
                      CREATE TABLE PresupuestoDetalle (
                          DetalleID INT PRIMARY KEY IDENTITY(1,1),
                          PresupuestoID INT,
                          ProductoID INT,
                          Cantidad INT,
                          PrecioUnitario DECIMAL(18,2)
                      );",

                    @"IF NOT EXISTS (SELECT * FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_NAME='Compras')
                      CREATE TABLE Compras (
                          CompraID INT PRIMARY KEY IDENTITY(1,1),
                          ProveedorID INT,
                          Fecha DATETIME,
                          Total DECIMAL(18,2),
                          TipoComprobante NVARCHAR(50)
                      );
                      IF NOT EXISTS (SELECT * FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_NAME='CompraDetalle')
                      CREATE TABLE CompraDetalle (
                          DetalleID INT PRIMARY KEY IDENTITY(1,1),
                          CompraID INT,
                          ProductoID INT,
                          Cantidad INT,
                          PrecioCosto DECIMAL(18,2)
                      );",

                    @"IF NOT EXISTS (SELECT * FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_NAME='ListasPrecios')
                      CREATE TABLE ListasPrecios (
                          ListaID INT PRIMARY KEY IDENTITY(1,1),
                          Nombre NVARCHAR(100),
                          Porcentaje DECIMAL(18,2)
                      );",

                    // Datos iniciales: Roles (si la tabla está vacía)
                    @"IF NOT EXISTS (SELECT 1 FROM Roles)
                      INSERT INTO Roles (NombreRol) VALUES ('Administrador'),('Vendedor'),('Cajero');",

                    @"IF NOT EXISTS (SELECT 1 FROM Permisos)
                      INSERT INTO Permisos (NombrePermiso) VALUES
                          ('ACCESO_FACTURACION'),('ACCESO_VENTAS'),('ACCESO_PRODUCTOS'),('ACCESO_CLIENTES'),
                          ('ACCESO_STOCK'),('ACCESO_COMPRAS'),('ACCESO_PROVEEDORES'),('ACCESO_PRECIOS'),
                          ('ACCESO_LISTASPRECIOS'),('ACCESO_CAJA'),('ACCESO_PRESUPUESTOS'),
                          ('ACCESO_CUENTASCORRIENTES'),('ACCESO_USUARIOS'),('ACCESO_PERMISOS'),('ACCESO_TOTAL');",

                    @"IF NOT EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME='Configuracion' AND COLUMN_NAME='VisorPromoCarpeta')
                      ALTER TABLE Configuracion ADD VisorPromoCarpeta NVARCHAR(500) NULL;
                      IF NOT EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME='Configuracion' AND COLUMN_NAME='VisorPromoIntervaloSeg')
                      ALTER TABLE Configuracion ADD VisorPromoIntervaloSeg INT NULL;",

                    @"IF NOT EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME='Configuracion' AND COLUMN_NAME='TipoCambioUSD')
                      ALTER TABLE Configuracion ADD TipoCambioUSD DECIMAL(18,4) NULL;",

                    @"IF NOT EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME='Configuracion' AND COLUMN_NAME='AfipProduccion')
                      ALTER TABLE Configuracion ADD AfipProduccion BIT NOT NULL DEFAULT 0;",

                    @"IF NOT EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME='Facturas' AND COLUMN_NAME='ListaID')
                      ALTER TABLE Facturas ADD ListaID INT NULL;"
                };

                foreach (string script in scriptsAdicionales)
                {
                    try
                    {
                        using (var cmdAd = new SqlCommand(script, conn))
                            cmdAd.ExecuteNonQuery();
                    }
                    catch { }
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
