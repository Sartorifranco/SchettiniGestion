using System;
using System.Collections.Generic;
using System.Configuration;
using System.Data;
using System.Diagnostics;
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

            if (EsModoBootstrap(e.Args))
            {
                ShutdownMode = ShutdownMode.OnExplicitShutdown;
                AppCulture.Initialize();
                AsegurarArchivoConexionPorDefecto();
                bool ok = IntentarInicializarConexion();
                Shutdown(ok ? 0 : 1);
                return;
            }

            AppCulture.Initialize();
            AppIconHelper.ApplyToAllWindows();
            ThemeManager.LoadSavedTheme();
            ResponsiveWindowService.Initialize();
            ResponsiveModuleService.Initialize();

            // Registrar teclado virtual inteligente (responde a cualquier TextBox/PasswordBox).
            KeyboardService.Initialize();
            WindowEscapeService.Initialize();

            this.DispatcherUnhandledException += App_DispatcherUnhandledException;
            AppDomain.CurrentDomain.UnhandledException += CurrentDomain_UnhandledException;

            // Despertar LocalDB antes de cualquier intento de conexión (resuelve Error 26).
            DespertarLocalDB();

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

                // PrimerUsoWindow guardó la nueva cadena de conexión.
                // Hay que crear el esquema completo AHORA, antes de validar la licencia.
                if (!IntentarInicializarConexion())
                {
                    MessageBox.Show(
                        "No se pudo crear la base de datos con la configuración ingresada.\nVerifique los datos de conexión e intente nuevamente.",
                        "Error de inicialización", MessageBoxButton.OK, MessageBoxImage.Error);
                    Shutdown();
                    return;
                }
            }

            // Garantía absoluta: forzar creación del esquema completo (BD + tablas)
            // con la conexión activa antes de cualquier operación de licencia.
            try { InicializarBaseDeDatosCompleta(DatabaseService.ConnectionString); }
            catch (Exception exInit)
            {
                MessageBox.Show(
                    "Error al inicializar la base de datos:\n\n" + exInit.Message,
                    "Error de inicialización", MessageBoxButton.OK, MessageBoxImage.Error);
                Shutdown();
                return;
            }

            // Licencia: si no hay clave válida, asistente de activación (pegar texto o cargar archivo)
            if (!LicenseManager.ValidarLicencia())
            {
                var activation = new ActivationWindow();
                if (activation.ShowDialog() != true || !LicenseManager.ValidarLicencia())
                {
                    Shutdown();
                    return;
                }
            }

            AdvertirConexionRedSinLicencia();

            var login = new LoginWindow();
            MainWindow = login;
            login.Show();
        }

        private static void AdvertirConexionRedSinLicencia()
        {
            try
            {
                string cs = DatabaseService.ConnectionString ?? "";
                if (!EsConexionPersonalizada(cs))
                    return;
                if (LicenseManager.TieneConexionRed())
                    return;

                MessageBox.Show(
                    "Este equipo está configurado para usar una base de datos en red, " +
                    "pero la licencia activa no incluye el extra «Conexión en RED».\n\n" +
                    "Solicite la habilitación al proveedor o use LocalDB (una sola PC) desde Configuración.",
                    "Conexión en red no habilitada",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);
            }
            catch { }
        }

        private static bool EsModoBootstrap(string[] args)
        {
            if (args == null) return false;
            foreach (string a in args)
            {
                if (string.Equals(a, "/bootstrap", StringComparison.OrdinalIgnoreCase)
                    || string.Equals(a, "-bootstrap", StringComparison.OrdinalIgnoreCase))
                    return true;
            }
            return false;
        }

        private static void AsegurarArchivoConexionPorDefecto()
        {
            try
            {
                string dir = System.IO.Path.GetDirectoryName(DatabaseService.RutaConexionCfg);
                if (!string.IsNullOrEmpty(dir) && !System.IO.Directory.Exists(dir))
                    System.IO.Directory.CreateDirectory(dir);
                if (!System.IO.File.Exists(DatabaseService.RutaConexionCfg))
                    System.IO.File.WriteAllText(DatabaseService.RutaConexionCfg, DatabaseService.CS_LOCALDB);
            }
            catch { }
        }

        /// <summary>
        /// Intenta conectar con la cadena actual y, si falla, prueba fallbacks automáticos.
        /// Retorna true si logra inicializar la BD correctamente.
        /// IMPORTANTE: si el usuario configuró manualmente un servidor de red en conexion.cfg,
        /// NO se hace fallback a LocalDB para evitar revertir silenciosamente a una BD vacía local.
        /// </summary>
        private bool IntentarInicializarConexion()
        {
            string csConfigurado = DatabaseService.ConnectionString;

            // Detectar si la conexión guardada fue configurada manualmente (no es LocalDB por defecto).
            bool esConexionPersonalizada = EsConexionPersonalizada(csConfigurado);

            // Si hay conexión personalizada, intentar solo con esa cadena (sin fallbacks que borrarían la config).
            // Si es la default (LocalDB), intentar también Express como alternativa local.
            string[] candidatos = esConexionPersonalizada
                ? new[] { csConfigurado }
                : new[] { csConfigurado, DatabaseService.CS_LOCALDB, DatabaseService.CS_SQLEXPRESS };

            const int MaxReintentos = 3;
            const int EsperaEntreReintentos = 2000; // ms

            string ultimoError = "";
            foreach (string cs in candidatos)
            {
                // Política de reintentos por candidato (cubre arranque lento de LocalDB).
                for (int intento = 1; intento <= MaxReintentos; intento++)
                {
                    try
                    {
                        InicializarBaseDeDatosCompleta(cs);
                        // Solo actualizar conexion.cfg si usamos un candidato distinto al configurado
                        // (fallback automático a LocalDB en primera instalación).
                        if (cs != csConfigurado)
                            DatabaseService.ActualizarConexion(cs);
                        DatabaseService.InitializeDatabase();
                        DatabaseService.MigrarNombresPermisosConGuionBajo();
                        DatabaseService.InicializarPermisosBaseDatos();
                        DatabaseService.AsegurarUsuarioAdminInicial();
                        return true;
                    }
                    catch (Exception ex)
                    {
                        ultimoError = ex.Message;

                        bool esErrorConexion =
                            ex.Message.Contains("No se puede abrir")  ||
                            ex.Message.Contains("Cannot open")        ||
                            ex.Message.Contains("network-related")    ||
                            ex.Message.Contains("A network")          ||
                            ex.Message.Contains("Error 26")           ||
                            ex.Message.Contains("error 26")           ||
                            ex.Message.Contains("login failed")       ||
                            ex.Message.Contains("Login failed");

                        if (!esErrorConexion)
                        {
                            // Error de DDL: no tiene sentido reintentar ni probar otro candidato.
                            throw new Exception($"Error al inicializar la base de datos:\n{ex.Message}", ex);
                        }

                        if (intento < MaxReintentos)
                        {
                            // Error de conexión: esperar y reintentar con el mismo candidato.
                            System.Threading.Thread.Sleep(EsperaEntreReintentos);
                        }
                        // Si agotó los reintentos, el bucle for termina y se prueba el siguiente candidato.
                    }
                }
            }

            // Si la conexión personalizada falló, mostrar error claro en lugar de silencio.
            if (esConexionPersonalizada && !string.IsNullOrEmpty(ultimoError))
            {
                MessageBox.Show(
                    "No se pudo conectar al servidor de base de datos configurado.\n\n" +
                    "Servidor: " + csConfigurado.Split(';')[0] + "\n" +
                    "Error: " + ultimoError + "\n\n" +
                    "Verifique que el servidor SQL esté encendido y accesible en la red.\n" +
                    "Para cambiar la configuración de red, utilice el asistente de primer uso.",
                    "Error de conexión al servidor",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
            }

            _ = ultimoError;
            return false;
        }

        /// <summary>
        /// Devuelve true si el connection string guardado en conexion.cfg fue configurado
        /// manualmente por el usuario (apunta a servidor remoto o SQL Express personalizado)
        /// y no debe ser sobreescrito con LocalDB en caso de fallo temporal.
        /// </summary>
        private static bool EsConexionPersonalizada(string cs)
        {
            if (string.IsNullOrWhiteSpace(cs)) return false;
            try
            {
                var b = new SqlConnectionStringBuilder(cs);
                string src = (b.DataSource ?? "").ToLowerInvariant().Trim();
                // LocalDB o localhost sin instancia nombrada = default, no personalizada.
                if (src.Contains("(localdb)")) return false;
                if (src == "." || src == "localhost" || src == "(local)") return false;
                if (src.StartsWith(".\\") || src.StartsWith("localhost\\") || src.StartsWith("(local)\\"))
                {
                    // SQL Express local con instancia nombrada: no sobreescribir, pero tampoco es "remoto".
                    // Solo hacer fallback si es la primera instalación (conexion.cfg no existe aún).
                    return System.IO.File.Exists(DatabaseService.RutaConexionCfg);
                }
                // Cualquier otra cosa (IP, nombre de servidor remoto) = personalizada.
                return true;
            }
            catch { return false; }
        }

        private void InicializarBaseDeDatosCompleta(string connectionString = null)
        {
            connectionString = connectionString ?? DatabaseService.ConnectionString;
            var builder = new SqlConnectionStringBuilder(connectionString);
            string targetDb = builder.InitialCatalog;

            // Siempre conectar a master primero.
            // Nunca se abre una conexión directa a Database=SchPosDB antes de confirmar
            // que la BD existe: evita "Cannot open database requested by the login"
            // en instalaciones limpias.
            builder.InitialCatalog = "master";
            string masterCs = builder.ConnectionString;

            using (var conn = new SqlConnection(masterCs))
            {
                conn.Open();

                // ── Paso 1: crear la BD si no existe ──────────────────────────────
                using (var cmd = new SqlCommand($"SELECT db_id(N'{targetDb}')", conn))
                {
                    if (cmd.ExecuteScalar() == DBNull.Value)
                    {
                        string createSql;

                        // En LocalDB: especificamos rutas para que los archivos queden en el perfil del usuario
                        // (requerido por LocalDB que no tiene carpeta de datos propia accesible).
                        // En SQL Server remoto o Express: NO especificamos FILENAME; el motor usa sus propias
                        // rutas por defecto en el servidor. Intentar escribir rutas locales del cliente causaría
                        // un error "The path is not valid" en el servidor remoto.
                        bool esLocalDb = builder.DataSource.IndexOf("(localdb)", StringComparison.OrdinalIgnoreCase) >= 0;

                        if (esLocalDb)
                        {
                            string userProfile = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
                            string mdfPath = System.IO.Path.Combine(userProfile, targetDb + ".mdf");
                            string ldfPath = System.IO.Path.Combine(userProfile, targetDb + "_log.ldf");
                            createSql =
                                $"CREATE DATABASE [{targetDb}] " +
                                $"ON  PRIMARY (NAME = N'{targetDb}', FILENAME = N'{mdfPath.Replace("'", "''")}', SIZE = 8MB, FILEGROWTH = 65536KB) " +
                                $"LOG ON      (NAME = N'{targetDb}_log', FILENAME = N'{ldfPath.Replace("'", "''")}', SIZE = 8MB, FILEGROWTH = 65536KB)";
                        }
                        else
                        {
                            // SQL Server Express/Standard/Developer remoto:
                            // dejar que el motor elija las rutas en el servidor.
                            createSql = $"CREATE DATABASE [{targetDb}]";
                        }

                        new SqlCommand(createSql, conn).ExecuteNonQuery();
                        // Breve pausa para que LocalDB registre la BD recién creada (no necesaria en SQL Server).
                        if (esLocalDb)
                            System.Threading.Thread.Sleep(2000);
                    }
                }

                // ── Paso 2: cambiar al contexto de la BD objetivo SIN abrir nueva conexión ──
                // ChangeDatabase reutiliza la conexión ya establecida con el servidor,
                // evitando el error "Cannot open database" en una conexión nueva.
                conn.ChangeDatabase(targetDb);

                // Verificar si ya existe el esquema base
                int existeConfiguracion;
                using (var cmdCheck = new SqlCommand(
                    "SELECT COUNT(*) FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_NAME='Configuracion'", conn))
                    existeConfiguracion = Convert.ToInt32(cmdCheck.ExecuteScalar());

                if (existeConfiguracion == 0)
                {
                    // Cada DDL en su propio SqlCommand: si uno falla el mensaje identifica cuál.
                    var sentencias = new[]
                    {
                        @"CREATE TABLE Configuracion (
                            ID INT PRIMARY KEY IDENTITY(1,1),
                            LicenciaPayload NVARCHAR(MAX) NULL,
                            NombreFantasia  NVARCHAR(200) NULL, RazonSocial  NVARCHAR(200) NULL, CUIT NVARCHAR(50) NULL,
                            Direccion       NVARCHAR(200) NULL, Telefono     NVARCHAR(50)  NULL, Email NVARCHAR(100) NULL,
                            LogoPath        NVARCHAR(MAX) NULL, CertificadoPath NVARCHAR(MAX) NULL, PasswordAfip NVARCHAR(MAX) NULL,
                            PuntoVenta      INT           NULL, MPAccessToken   NVARCHAR(MAX) NULL,
                            MPUserId        NVARCHAR(MAX) NULL, MPPosId         NVARCHAR(MAX) NULL,
                            AfipProduccion  BIT NOT NULL DEFAULT 0, UsaVisorCliente BIT NOT NULL DEFAULT 0
                        )",
                        "INSERT INTO Configuracion (NombreFantasia) VALUES ('Mi Negocio')",
                        @"CREATE TABLE Roles (RolID INT PRIMARY KEY IDENTITY(1,1), NombreRol NVARCHAR(50))",
                        @"CREATE TABLE Permisos (PermisoID INT PRIMARY KEY IDENTITY(1,1), NombrePermiso NVARCHAR(100))",
                        @"CREATE TABLE Roles_Permisos (RolID INT, PermisoID INT, PRIMARY KEY (RolID, PermisoID))",
                        @"CREATE TABLE Usuarios (UsuarioID INT PRIMARY KEY IDENTITY(1,1), NombreUsuario NVARCHAR(50), PasswordHash NVARCHAR(MAX), RolID INT, Rol NVARCHAR(50))",
                        "INSERT INTO Roles   (NombreRol)     VALUES ('Administrador')",
                        "INSERT INTO Permisos(NombrePermiso) VALUES ('ACCESO_TOTAL')",
                        @"CREATE TABLE Clientes (
                            ClienteID INT PRIMARY KEY IDENTITY(1,1), CUIT NVARCHAR(50), RazonSocial NVARCHAR(200),
                            CondicionIVA NVARCHAR(50), Direccion NVARCHAR(200), Telefono NVARCHAR(50), Email NVARCHAR(100),
                            PermiteCuentaCorriente BIT DEFAULT 0, MontoLimiteCtaCte DECIMAL(18,2) NULL, SaldoDeuda DECIMAL(18,2) DEFAULT 0
                        )",
                        @"CREATE TABLE Productos (
                            ProductoID INT PRIMARY KEY IDENTITY(1,1), Codigo NVARCHAR(50), CodigoBarra NVARCHAR(50),
                            Descripcion NVARCHAR(200), Categoria NVARCHAR(50), SubRubro NVARCHAR(100), Marca NVARCHAR(100), Proveedor NVARCHAR(100),
                            TipoIVA NVARCHAR(20), PrecioCosto DECIMAL(18,2), Ganancia DECIMAL(18,2), ImpuestoInterno DECIMAL(18,2),
                            PrecioVenta DECIMAL(18,2), StockActual INT, ImagenPath NVARCHAR(MAX)
                        )",
                        @"CREATE TABLE Proveedores (
                            ProveedorID INT PRIMARY KEY IDENTITY(1,1), CUIT NVARCHAR(50), RazonSocial NVARCHAR(200),
                            Direccion NVARCHAR(200), CategoriaFiscal NVARCHAR(100), PersonaContacto NVARCHAR(100),
                            Telefono NVARCHAR(50), PaginaWeb NVARCHAR(300), Email NVARCHAR(100), SaldoDeuda DECIMAL(18,2) DEFAULT 0
                        )",
                        "CREATE TABLE ListasPrecios (ListaID INT PRIMARY KEY IDENTITY(1,1), Nombre NVARCHAR(100), Porcentaje DECIMAL(18,2))",
                        @"CREATE TABLE Facturas (
                            FacturaID INT PRIMARY KEY IDENTITY(1,1), ClienteID INT, Fecha DATETIME, Total DECIMAL(18,2),
                            TipoComprobante NVARCHAR(50), CondicionVenta NVARCHAR(100), CAE NVARCHAR(50),
                            VencimientoCAE NVARCHAR(20), NumeroComprobanteAFIP INT
                        )",
                        "CREATE TABLE FacturaDetalle (DetalleID INT PRIMARY KEY IDENTITY(1,1), FacturaID INT, ProductoID INT, Cantidad INT, PrecioUnitario DECIMAL(18,2))",
                        @"CREATE TABLE MovimientosCaja (
                            MovimientoID INT PRIMARY KEY IDENTITY(1,1), Fecha DATETIME, Concepto NVARCHAR(200),
                            Tipo NVARCHAR(20), Monto DECIMAL(18,2), Usuario NVARCHAR(50)
                        )",
                        @"CREATE TABLE MovimientosStock (
                            MovimientoID INT PRIMARY KEY IDENTITY(1,1), ProductoID INT, FacturaID INT NULL,
                            CompraID INT NULL, Fecha DATETIME, TipoMovimiento NVARCHAR(50), Cantidad INT
                        )",
                        @"CREATE TABLE MovimientosCuentaCorriente (
                            MovimientoID INT PRIMARY KEY IDENTITY(1,1), ClienteID INT NULL, ProveedorID INT NULL,
                            Fecha DATETIME, Descripcion NVARCHAR(200), Monto DECIMAL(18,2), SaldoHistorico DECIMAL(18,2)
                        )",
                        "CREATE TABLE Presupuestos (PresupuestoID INT PRIMARY KEY IDENTITY(1,1), ClienteID INT, Fecha DATETIME, Total DECIMAL(18,2), Estado NVARCHAR(50))",
                        "CREATE TABLE PresupuestoDetalle (DetalleID INT PRIMARY KEY IDENTITY(1,1), PresupuestoID INT, ProductoID INT, Cantidad INT, PrecioUnitario DECIMAL(18,2))",
                        "CREATE TABLE Compras (CompraID INT PRIMARY KEY IDENTITY(1,1), ProveedorID INT, Fecha DATETIME, Total DECIMAL(18,2), TipoComprobante NVARCHAR(50))",
                        "CREATE TABLE CompraDetalle (DetalleID INT PRIMARY KEY IDENTITY(1,1), CompraID INT, ProductoID INT, Cantidad INT, PrecioCosto DECIMAL(18,2))",
                    };

                    foreach (string sql in sentencias)
                    {
                        try
                        {
                            new SqlCommand(sql, conn).ExecuteNonQuery();
                        }
                        catch (Exception ex)
                        {
                            throw new Exception(
                                $"Error creando esquema en '{targetDb}'.\n" +
                                $"Sentencia: {sql.TrimStart().Substring(0, Math.Min(80, sql.TrimStart().Length))}...\n" +
                                $"SQL Server: {ex.Message}", ex);
                        }
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

                    @"IF NOT EXISTS (SELECT * FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_NAME='AperturasCaja')
                      CREATE TABLE AperturasCaja (
                          AperturaID INT PRIMARY KEY IDENTITY(1,1),
                          Fecha DATETIME,
                          MontoFondoFijo DECIMAL(18,2),
                          Observaciones NVARCHAR(500),
                          Usuario NVARCHAR(50),
                          MovimientoID INT NULL
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
                      INSERT INTO Roles (NombreRol) VALUES ('Administrador'),('Vendedor'),('Encargado / Supervisor'),('Cajero');",

                    @"IF NOT EXISTS (SELECT 1 FROM Permisos)
                      INSERT INTO Permisos (NombrePermiso) VALUES
                          ('ACCESO_FACTURACION'),('ACCESO_VENTAS'),('ACCESO_PRODUCTOS'),('ACCESO_CLIENTES'),
                          ('ACCESO_STOCK'),('ACCESO_COMPRAS'),('ACCESO_PROVEEDORES'),('ACCESO_PRECIOS'),
                          ('ACCESO_LISTASPRECIOS'),('ACCESO_CAJA'),('ACCESO_PRESUPUESTOS'),
                          ('ACCESO_CUENTASCORRIENTES'),('ACCESO_USUARIOS'),('ACCESO_PERMISOS'),('ACCESO_CONFIGURACION'),('ACCESO_TOTAL');",

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

        /// <summary>
        /// Crea e inicia la instancia MSSQLLocalDB de forma silenciosa.
        /// Garantiza que el motor esté corriendo antes de intentar cualquier conexión,
        /// evitando el Error 26 ("no se encontró la instancia del servidor").
        /// Defensa 1: busca sqllocaldb.exe por ruta absoluta en versiones conocidas de SQL Server.
        /// Defensa 2: espera 3 segundos tras WaitForExit para que Windows levante los servicios internos.
        /// </summary>
        private static void DespertarLocalDB()
        {
            try
            {
                // Defensa 1: buscar sqllocaldb.exe por ruta absoluta en versiones 160→120.
                string sqlLocalDbExe = null;
                string[] versions = { "160", "150", "140", "130", "120" };
                string programFiles = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles);
                string programFilesX86 = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86);
                foreach (string ver in versions)
                {
                    foreach (string root in new[] { programFiles, programFilesX86 })
                    {
                        string candidate = System.IO.Path.Combine(
                            root, "Microsoft SQL Server", ver, "Tools", "Binn", "sqllocaldb.exe");
                        if (System.IO.File.Exists(candidate))
                        {
                            sqlLocalDbExe = candidate;
                            break;
                        }
                    }
                    if (sqlLocalDbExe != null) break;
                }

                // Si no se encontró por ruta absoluta, confiar en el PATH del sistema.
                if (sqlLocalDbExe == null)
                    sqlLocalDbExe = "sqllocaldb";

                // Crear la instancia (sin efecto si ya existe) y luego arrancarla.
                foreach (string args in new[] { "create MSSQLLocalDB", "start MSSQLLocalDB" })
                {
                    using (var p = new Process())
                    {
                        p.StartInfo = new ProcessStartInfo
                        {
                            FileName        = sqlLocalDbExe,
                            Arguments       = args,
                            CreateNoWindow  = true,
                            UseShellExecute = false,
                        };
                        p.Start();
                        p.WaitForExit();
                    }
                }

                // Defensa 2: tiempo de gracia para que Windows levante los servicios internos de SQL.
                System.Threading.Thread.Sleep(3000);
            }
            catch { /* Si LocalDB no está instalado el error se surfaceará al conectar */ }
        }
    }
}
