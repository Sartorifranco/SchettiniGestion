using System;
using System.Collections.Generic;
using System.Configuration;
using System.Data;
using System.Globalization;
using System.Text.RegularExpressions;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Linq;
using System.Reflection;
using SqlConnection = Microsoft.Data.SqlClient.SqlConnection;
using SqlCommand = Microsoft.Data.SqlClient.SqlCommand;
using SqlDataAdapter = Microsoft.Data.SqlClient.SqlDataAdapter;
using SqlException = Microsoft.Data.SqlClient.SqlException;
using SqlTransaction = Microsoft.Data.SqlClient.SqlTransaction;
using SqlConnectionStringBuilder = Microsoft.Data.SqlClient.SqlConnectionStringBuilder;

namespace SchettiniGestion
{
    // ==========================================
    // CLASES DE AYUDA
    // ==========================================
    public class FacturaItem : System.ComponentModel.INotifyPropertyChanged
    {
        private int _productoId;
        private string _codigo;
        private string _descripcion;
        private int _cantidad;
        private decimal _precioUnitario;
        private decimal _descuentoPorcentaje;
        private decimal _recargoPorcentaje;
        private string _promoNombre;
        private bool _descuentoPromocionAutomatica;
        private decimal _alicuotaIvaPct = 21m;
        private bool _permiteModificarPrecioVenta;
        private string _imagenPath;

        public event System.ComponentModel.PropertyChangedEventHandler PropertyChanged;
        private void OnPropertyChanged(string name) =>
            PropertyChanged?.Invoke(this, new System.ComponentModel.PropertyChangedEventArgs(name));

        public int ProductoID
        {
            get => _productoId;
            set { if (_productoId == value) return; _productoId = value; OnPropertyChanged(nameof(ProductoID)); }
        }
        public string Codigo
        {
            get => _codigo;
            set { if (_codigo == value) return; _codigo = value; OnPropertyChanged(nameof(Codigo)); }
        }
        public string Descripcion
        {
            get => _descripcion;
            set { if (_descripcion == value) return; _descripcion = value; OnPropertyChanged(nameof(Descripcion)); }
        }
        public int Cantidad
        {
            get => _cantidad;
            set
            {
                if (_cantidad == value) return;
                _cantidad = value;
                OnPropertyChanged(nameof(Cantidad));
                OnPropertyChanged(nameof(Subtotal));
                OnPropertyChanged(nameof(EsValido));
            }
        }
        public decimal PrecioUnitario
        {
            get => _precioUnitario;
            set
            {
                if (_precioUnitario == value) return;
                _precioUnitario = value;
                OnPropertyChanged(nameof(PrecioUnitario));
                OnPropertyChanged(nameof(Subtotal));
            }
        }
        public decimal DescuentoPorcentaje
        {
            get => _descuentoPorcentaje;
            set
            {
                if (_descuentoPorcentaje == value) return;
                _descuentoPorcentaje = value;
                OnPropertyChanged(nameof(DescuentoPorcentaje));
                OnPropertyChanged(nameof(Subtotal));
                OnPropertyChanged(nameof(AjusteLineaTexto));
            }
        }
        public decimal RecargoPorcentaje
        {
            get => _recargoPorcentaje;
            set
            {
                if (_recargoPorcentaje == value) return;
                _recargoPorcentaje = value;
                OnPropertyChanged(nameof(RecargoPorcentaje));
                OnPropertyChanged(nameof(Subtotal));
                OnPropertyChanged(nameof(AjusteLineaTexto));
            }
        }
        /// <summary>Nombre de la promoción automática aplicada (si hubo).</summary>
        public string PromoNombre
        {
            get => _promoNombre;
            set
            {
                if (_promoNombre == value) return;
                _promoNombre = value;
                OnPropertyChanged(nameof(PromoNombre));
                OnPropertyChanged(nameof(AjusteLineaTexto));
            }
        }
        /// <summary>True cuando el descuento fue recalculado por promociones automáticas del POS.</summary>
        public bool DescuentoPromocionAutomatica
        {
            get => _descuentoPromocionAutomatica;
            set
            {
                if (_descuentoPromocionAutomatica == value) return;
                _descuentoPromocionAutomatica = value;
                OnPropertyChanged(nameof(DescuentoPromocionAutomatica));
            }
        }
        /// <summary>IVA aplicado sobre el precio (subtotal línea incluye este IVA), p. ej. 21 por 21%.</summary>
        public decimal AlicuotaIvaPct
        {
            get => _alicuotaIvaPct;
            set { if (_alicuotaIvaPct == value) return; _alicuotaIvaPct = value; OnPropertyChanged(nameof(AlicuotaIvaPct)); }
        }
        /// <summary>Si es false, el precio unitario en POS no puede modificarse manualmente.</summary>
        public bool PermiteModificarPrecioVenta
        {
            get => _permiteModificarPrecioVenta;
            set { if (_permiteModificarPrecioVenta == value) return; _permiteModificarPrecioVenta = value; OnPropertyChanged(nameof(PermiteModificarPrecioVenta)); }
        }
        public decimal Subtotal => Cantidad * PrecioUnitario * (1 - DescuentoPorcentaje / 100) * (1 + RecargoPorcentaje / 100);
        public string ImagenPath
        {
            get => _imagenPath;
            set { if (_imagenPath == value) return; _imagenPath = value; OnPropertyChanged(nameof(ImagenPath)); }
        }
        public string AjusteLineaTexto
        {
            get
            {
                if (DescuentoPorcentaje > 0m)
                {
                    if (!string.IsNullOrWhiteSpace(PromoNombre))
                        return $"🎯 {PromoNombre} · -{DescuentoPorcentaje:N0}%";
                    return $"-{DescuentoPorcentaje:N0}% dto";
                }
                if (RecargoPorcentaje > 0m) return $"+{RecargoPorcentaje:N0}% rec.";
                return "";
            }
        }
        /// <summary>False si el ítem no tiene datos suficientes para mostrarse en el carrito.</summary>
        public bool EsValido => !string.IsNullOrWhiteSpace(Descripcion) && Cantidad > 0;
    }

    /// <summary>Un medio de pago dentro de una venta (persistido en FacturasCobranza).</summary>
    public class FacturaCobranzaParcela
    {
        public int MedioPagoID { get; set; }
        public string NombreMedio { get; set; }
        public decimal Monto { get; set; }
        public int NroCuotas { get; set; } = 1;
        public string UltimosDigitosTarjeta { get; set; }
        public string MarcaTarjeta { get; set; }
        public string OperacionExternaID { get; set; }
    }

    public class PosConfigPredeterminada
    {
        public int? ListaPrecioID { get; set; }
        public string TipoComprobante { get; set; }
        public string CondicionVenta { get; set; }
        public bool ConfigExpandida { get; set; } = false;
    }

    public class Rol
    {
        public int RolId { get; set; }
        public string Nombre { get; set; }
    }

    public class Permiso
    {
        public int PermisoId { get; set; }
        public string Nombre { get; set; }
    }

    /// <summary>Fila de listado para gestión de clientes (columnas usadas por el DataGrid).</summary>
    public class ClienteListadoItem
    {
        public int ClienteID { get; set; }
        public string CUIT { get; set; }
        public string RazonSocial { get; set; }
        public string CondicionIVA { get; set; }
        public string Telefono { get; set; }
        public string Email { get; set; }
    }

    /// <summary>Elemento de ComboBox con id (tabla catálogo o 0 = vacío).</summary>
    public class ComboLookupItem
    {
        public int Id { get; set; }
        public string Nombre { get; set; }
    }

    // ==========================================
    // SERVICIO DE BASE DE DATOS (SQL SERVER)
    // ==========================================
    public static partial class DatabaseService
    {
        // Cadenas de conexión ordenadas por prioridad de intento
        public const string CS_LOCALDB    = @"Server=(LocalDB)\MSSQLLocalDB;Database=SchPosDB;Integrated Security=True;Encrypt=False;";
        public const string CS_SQLEXPRESS = @"Data Source=localhost\SQLEXPRESS;Initial Catalog=SchPosDB;Integrated Security=True;Encrypt=False;";
        public const string CS_LOCALDB_MASTER    = @"Server=(LocalDB)\MSSQLLocalDB;Database=master;Integrated Security=True;Encrypt=False;";
        public const string CS_SQLEXPRESS_MASTER = @"Data Source=localhost\SQLEXPRESS;Initial Catalog=master;Integrated Security=True;Encrypt=False;";

        /// <summary>Archivo donde se guarda la conexión activa (creado en primera ejecución o desde Configuración).</summary>
        public static readonly string RutaConexionCfg = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData),
            "SCHPOS", "conexion.cfg");

        /// <summary>Carpeta de datos de la app (escribible sin permisos de admin). Ej: C:\ProgramData\SCHPOS</summary>
        public static string CarpetaDatosSchpos => Path.GetDirectoryName(RutaConexionCfg);

        public static string AsegurarCarpetaDatosSchpos()
        {
            string dir = CarpetaDatosSchpos;
            if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
                Directory.CreateDirectory(dir);
            return dir;
        }

        /// <summary>Carpeta predeterminada de publicidades para la pantalla del cliente.</summary>
        public static string CarpetaPublicidadesCliente =>
            Path.Combine(AsegurarCarpetaDatosSchpos(), "publicidades_cliente");

        public static string AsegurarCarpetaPublicidadesCliente()
        {
            string dir = CarpetaPublicidadesCliente;
            if (!Directory.Exists(dir))
                Directory.CreateDirectory(dir);
            return dir;
        }

        private static readonly string[] ExtensionesPromoImagenCliente = { ".jpg", ".jpeg", ".png", ".bmp", ".gif" };
        private static readonly string[] ExtensionesPromoVideoCliente = { ".mp4", ".avi", ".wmv", ".mpeg", ".mpg" };

        public static bool EsExtensionPromoImagenCliente(string extension)
        {
            if (string.IsNullOrWhiteSpace(extension)) return false;
            return ExtensionesPromoImagenCliente.Contains(extension.ToLowerInvariant());
        }

        public static bool EsExtensionPromoVideoCliente(string extension)
        {
            if (string.IsNullOrWhiteSpace(extension)) return false;
            return ExtensionesPromoVideoCliente.Contains(extension.ToLowerInvariant());
        }

        /// <summary>Resuelve la carpeta activa de promociones (configurada o predeterminada en ProgramData).</summary>
        public static string ObtenerCarpetaVisorPromociones()
        {
            try
            {
                var cfg = GetConfiguracion();
                if (cfg != null && cfg.Table.Columns.Contains("VisorPromoCarpeta"))
                {
                    string dir = cfg["VisorPromoCarpeta"]?.ToString()?.Trim();
                    if (!string.IsNullOrEmpty(dir))
                    {
                        if (!Directory.Exists(dir))
                            Directory.CreateDirectory(dir);
                        return dir;
                    }
                }
            }
            catch { }

            return AsegurarCarpetaPublicidadesCliente();
        }

        public static List<string> ListarArchivosPromoVisorCliente(string carpeta = null)
        {
            var list = new List<string>();
            try
            {
                string dir = string.IsNullOrWhiteSpace(carpeta) ? ObtenerCarpetaVisorPromociones() : carpeta.Trim();
                if (string.IsNullOrEmpty(dir) || !Directory.Exists(dir)) return list;

                foreach (var f in Directory.GetFiles(dir))
                {
                    string ext = Path.GetExtension(f);
                    if (EsExtensionPromoImagenCliente(ext) || EsExtensionPromoVideoCliente(ext))
                        list.Add(f);
                }
                list.Sort(StringComparer.OrdinalIgnoreCase);
            }
            catch { }
            return list;
        }

        private static string _connectionString = ObtenerConnectionString();

        /// <summary>
        /// Orden de resolución:
        /// 1. conexion.cfg  (guardado por el asistente de primer uso o Configuración)
        /// 2. App.config    (para desarrolladores y entornos controlados)
        /// 3. LocalDB       (default para instalaciones nuevas — no requiere SQL Server)
        /// </summary>
        private static string ObtenerConnectionString()
        {
            // 1. Archivo de configuración persistente (escrito por PrimerUsoWindow o Configuración)
            try
            {
                if (File.Exists(RutaConexionCfg))
                {
                    string saved = File.ReadAllText(RutaConexionCfg).Trim();
                    if (!string.IsNullOrWhiteSpace(saved)) return saved;
                }
            }
            catch { }

            // 2. App.config
            try
            {
                var cs = ConfigurationManager.ConnectionStrings["SchPosDB"];
                if (cs != null && !string.IsNullOrWhiteSpace(cs.ConnectionString))
                    return cs.ConnectionString;
            }
            catch { }

            // 3. LocalDB por defecto (no requiere instalación de SQL Server)
            return CS_LOCALDB;
        }

        /// <summary>Guarda una nueva cadena de conexión en conexion.cfg y la activa en tiempo de ejecución.</summary>
        public static bool ActualizarConexion(string nuevaCadena)
        {
            try
            {
                // Garantizar que la cadena siempre incluye Initial Catalog/Database.
                // Si quien llama omite el catálogo, lo tomamos de la constante por defecto.
                var bldr = new SqlConnectionStringBuilder(nuevaCadena.Trim());
                if (string.IsNullOrWhiteSpace(bldr.InitialCatalog))
                    bldr.InitialCatalog = new SqlConnectionStringBuilder(CS_LOCALDB).InitialCatalog;
                string cadenaFinal = bldr.ConnectionString;

                Directory.CreateDirectory(Path.GetDirectoryName(RutaConexionCfg));
                File.WriteAllText(RutaConexionCfg, cadenaFinal);
                _connectionString = cadenaFinal;
                return true;
            }
            catch { return false; }
        }

        public static string ConnectionString => _connectionString;

        /// <summary>
        /// Fuerza el contexto de una conexión ya abierta hacia la BD configurada.
        /// Previene que LocalDB quede en 'master' en entornos de instalación limpia.
        /// </summary>
        private static void ForzarContextoBD(SqlConnection conn)
        {
            try
            {
                string catalog = new SqlConnectionStringBuilder(_connectionString).InitialCatalog;
                if (!string.IsNullOrWhiteSpace(catalog) &&
                    !string.Equals(conn.Database, catalog, StringComparison.OrdinalIgnoreCase))
                    conn.ChangeDatabase(catalog);
            }
            catch { }
        }

        public static Action<string> OnDbError;
        public static string UltimoError { get; private set; }

        // Constantes de Permisos
        // IMPORTANTE: deben coincidir exactamente con los seeds de App.xaml.cs
        public const string PERMISO_USUARIOS          = "ACCESO_USUARIOS";
        public const string PERMISO_CLIENTES          = "ACCESO_CLIENTES";
        public const string PERMISO_PRODUCTOS         = "ACCESO_PRODUCTOS";
        public const string PERMISO_STOCK             = "ACCESO_STOCK";
        public const string PERMISO_FACTURACION       = "ACCESO_FACTURACION";
        public const string PERMISO_VENTAS            = "ACCESO_VENTAS";
        public const string PERMISO_PERMISOS          = "ACCESO_PERMISOS";
        public const string PERMISO_PROVEEDORES       = "ACCESO_PROVEEDORES";
        public const string PERMISO_COMPRAS           = "ACCESO_COMPRAS";
        public const string PERMISO_PRECIOS           = "ACCESO_PRECIOS";
        public const string PERMISO_CAJA              = "ACCESO_CAJA";
        public const string PERMISO_PRESUPUESTOS      = "ACCESO_PRESUPUESTOS";
        public const string PERMISO_CUENTASCORRIENTES = "ACCESO_CUENTASCORRIENTES";
        public const string PERMISO_LISTASPRECIOS     = "ACCESO_LISTASPRECIOS";
        public const string PERMISO_CONFIGURACION     = "ACCESO_CONFIGURACION";
        public const string PERMISO_RED               = "ACCESO_RED";
        public const string PERMISO_AFIP              = "ACCESO_AFIP";
        public const string PERMISO_VISOR_CLIENTE     = "ACCESO_VISOR_CLIENTE";
        public const string PERMISO_MERCADOPAGO_QR    = "ACCESO_MERCADOPAGO_QR";
        public const string PERMISO_MERCADOPAGO_POINT = "ACCESO_MERCADOPAGO_POINT";
        public const string PERMISO_SOPORTE           = "ACCESO_SOPORTE";
        public const string PERMISO_ESTADISTICAS      = "ACCESO_ESTADISTICAS";
        public const string PERMISO_ETIQUETAS         = "ACCESO_ETIQUETAS";

        private static void NotificarError(string mensaje)
        {
            UltimoError = mensaje;
            OnDbError?.Invoke(mensaje);
            System.Diagnostics.Debug.WriteLine("[DatabaseService] Error: " + mensaje);
        }

        /// <summary>
        /// Prueba la conexión a la base de datos. Devuelve true si la conexión fue exitosa.
        /// </summary>
        public static bool InitializeDatabase()
        {
            try
            {
                using (var conn = new SqlConnection(_connectionString))
                {
                    conn.Open();
                    AsegurarMigracionLite(conn);
                }
                return true;
            }
            catch (Exception ex)
            {
                NotificarError($"Error conectando a SQL Server (SchPosDB): {ex.Message}");
                return false;
            }
        }

        // Configuración
        private static bool _columnasVisorPromoVerificadas;
        private static bool _columnaCondicionTicketFacturasOk;
        private static bool _tablaAperturasCajaOk;
        private static bool _columnasBackupAutoVerificadas;

        /// <summary>Concepto estándar del movimiento de caja al abrir turno.</summary>
        public const string ConceptoFondoFijo = "FONDO FIJO";

        private static readonly object _lockMigrLite = new object();
        private static bool _columnasMigracionLiteOk;

        private static void AsegurarMigracionLite(SqlConnection c)
        {
            if (_columnasMigracionLiteOk) return;
            lock (_lockMigrLite)
            {
                if (_columnasMigracionLiteOk) return;
                try
                {
                    using (var cmd = new SqlCommand(@"
-- Tablas auxiliares para catálogo de productos
IF NOT EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_NAME='Categorias')
  CREATE TABLE dbo.Categorias (
    CategoriaID INT IDENTITY(1,1) PRIMARY KEY,
    Nombre      NVARCHAR(100) NOT NULL
  );
IF NOT EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_NAME='SubRubros')
  CREATE TABLE dbo.SubRubros (
    SubRubroID INT IDENTITY(1,1) PRIMARY KEY,
    Nombre     NVARCHAR(100) NOT NULL
  );
IF NOT EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_NAME='ProductosListas')
BEGIN
  IF EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_NAME='ProductoListas')
    SELECT ProductoID, ListaID INTO dbo.ProductosListas FROM dbo.ProductoListas;
  ELSE
    CREATE TABLE dbo.ProductosListas (
      ProductoID INT NOT NULL,
      ListaID    INT NOT NULL,
      PRIMARY KEY (ProductoID, ListaID)
    );
END
IF NOT EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_NAME='ProductoListas')
  CREATE TABLE dbo.ProductoListas (
    ProductoID INT NOT NULL,
    ListaID    INT NOT NULL,
    PRIMARY KEY (ProductoID, ListaID)
  );
IF NOT EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_NAME='ProductoCombos')
  CREATE TABLE dbo.ProductoCombos (
    ComboID             INT IDENTITY(1,1) PRIMARY KEY,
    ProductoPadreID     INT NOT NULL,
    ProductoComponenteID INT NOT NULL,
    Cantidad            INT NOT NULL DEFAULT 1
  );
IF NOT EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_NAME='ProductoComboDetalle')
  CREATE TABLE dbo.ProductoComboDetalle (
    ProductoID INT NOT NULL,
    ComponenteID INT NOT NULL,
    Cantidad DECIMAL(18,4) NOT NULL DEFAULT 1,
    PRIMARY KEY (ProductoID, ComponenteID)
  );
-- Garantizar que siempre exista el cliente Consumidor Final
IF NOT EXISTS (SELECT 1 FROM dbo.Clientes WHERE CUIT='00-00000000-0')
  INSERT INTO dbo.Clientes (RazonSocial, CUIT, CondicionIVA, Telefono, Email, Direccion)
  VALUES (N'Consumidor Final', N'00-00000000-0', N'Consumidor Final', N'', N'', N'');
-- Columnas nuevas en tablas existentes
IF NOT EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME='Configuracion' AND COLUMN_NAME='TipoCambioUSD')
  ALTER TABLE Configuracion ADD TipoCambioUSD DECIMAL(18,4) NULL;
IF NOT EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME='Configuracion' AND COLUMN_NAME='AfipProduccion')
  ALTER TABLE Configuracion ADD AfipProduccion BIT NOT NULL DEFAULT 0;
IF NOT EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME='Configuracion' AND COLUMN_NAME='ImpresoraTicket')
  ALTER TABLE Configuracion ADD ImpresoraTicket NVARCHAR(256) NULL;
IF NOT EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME='Configuracion' AND COLUMN_NAME='ImpresoraA4')
  ALTER TABLE Configuracion ADD ImpresoraA4 NVARCHAR(256) NULL;
IF NOT EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME='Configuracion' AND COLUMN_NAME='PreguntarAntesImprimir')
  ALTER TABLE Configuracion ADD PreguntarAntesImprimir BIT NOT NULL DEFAULT 1;
IF NOT EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME='Configuracion' AND COLUMN_NAME='LogoEnTicket')
  ALTER TABLE Configuracion ADD LogoEnTicket BIT NOT NULL DEFAULT 1;
IF NOT EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME='Configuracion' AND COLUMN_NAME='LogoEnA4')
  ALTER TABLE Configuracion ADD LogoEnA4 BIT NOT NULL DEFAULT 1;
IF NOT EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME='Configuracion' AND COLUMN_NAME='AnchoTicketMm')
  ALTER TABLE Configuracion ADD AnchoTicketMm INT NOT NULL DEFAULT 80;
IF NOT EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME='Configuracion' AND COLUMN_NAME='DestinoImpresionVenta')
  ALTER TABLE Configuracion ADD DestinoImpresionVenta NVARCHAR(20) NOT NULL DEFAULT 'Ticket';
IF NOT EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME='Configuracion' AND COLUMN_NAME='TicketMostrarCodigo')
  ALTER TABLE Configuracion ADD TicketMostrarCodigo BIT NOT NULL DEFAULT 0;
IF NOT EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME='Configuracion' AND COLUMN_NAME='TicketMostrarDireccion')
  ALTER TABLE Configuracion ADD TicketMostrarDireccion BIT NOT NULL DEFAULT 1;
IF NOT EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME='Configuracion' AND COLUMN_NAME='TicketMostrarTelefono')
  ALTER TABLE Configuracion ADD TicketMostrarTelefono BIT NOT NULL DEFAULT 1;
IF NOT EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME='Configuracion' AND COLUMN_NAME='TicketMostrarCuit')
  ALTER TABLE Configuracion ADD TicketMostrarCuit BIT NOT NULL DEFAULT 1;
IF NOT EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME='Configuracion' AND COLUMN_NAME='TicketMostrarCliente')
  ALTER TABLE Configuracion ADD TicketMostrarCliente BIT NOT NULL DEFAULT 1;
IF NOT EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME='Configuracion' AND COLUMN_NAME='TicketMostrarFormaPago')
  ALTER TABLE Configuracion ADD TicketMostrarFormaPago BIT NOT NULL DEFAULT 1;
IF NOT EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME='Configuracion' AND COLUMN_NAME='TicketMostrarGracias')
  ALTER TABLE Configuracion ADD TicketMostrarGracias BIT NOT NULL DEFAULT 1;
IF NOT EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME='Configuracion' AND COLUMN_NAME='TicketMostrarPieFiscal')
  ALTER TABLE Configuracion ADD TicketMostrarPieFiscal BIT NOT NULL DEFAULT 1;
IF NOT EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME='Configuracion' AND COLUMN_NAME='TicketMostrarPuntoVenta')
  ALTER TABLE Configuracion ADD TicketMostrarPuntoVenta BIT NOT NULL DEFAULT 1;
IF NOT EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME='Configuracion' AND COLUMN_NAME='TicketMostrarVendedor')
  ALTER TABLE Configuracion ADD TicketMostrarVendedor BIT NOT NULL DEFAULT 0;
IF NOT EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME='Configuracion' AND COLUMN_NAME='CarpetaArchivosComprobantes')
  ALTER TABLE Configuracion ADD CarpetaArchivosComprobantes NVARCHAR(500) NULL;
IF NOT EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME='Configuracion' AND COLUMN_NAME='CondicionIVAEmpresa')
  ALTER TABLE Configuracion ADD CondicionIVAEmpresa NVARCHAR(100) NULL;
IF NOT EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME='Configuracion' AND COLUMN_NAME='AfipClavePrivadaPath')
  ALTER TABLE Configuracion ADD AfipClavePrivadaPath NVARCHAR(500) NULL;
IF NOT EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME='Configuracion' AND COLUMN_NAME='MPPointTerminalId')
  ALTER TABLE Configuracion ADD MPPointTerminalId NVARCHAR(150) NULL;
IF NOT EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME='Configuracion' AND COLUMN_NAME='MPPointAutomatico')
  ALTER TABLE Configuracion ADD MPPointAutomatico BIT NOT NULL DEFAULT 0;
IF NOT EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME='FacturasCobranza' AND COLUMN_NAME='MarcaTarjeta')
  ALTER TABLE FacturasCobranza ADD MarcaTarjeta NVARCHAR(50) NULL;
IF NOT EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME='FacturasCobranza' AND COLUMN_NAME='OperacionExternaID')
  ALTER TABLE FacturasCobranza ADD OperacionExternaID NVARCHAR(100) NULL;
IF NOT EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME='Facturas' AND COLUMN_NAME='ListaID')
  ALTER TABLE Facturas ADD ListaID INT NULL;
IF NOT EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME='Facturas' AND COLUMN_NAME='CondicionVenta')
  ALTER TABLE Facturas ADD CondicionVenta NVARCHAR(100) NULL;
IF NOT EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME='Facturas' AND COLUMN_NAME='CondicionTicket')
  ALTER TABLE Facturas ADD CondicionTicket NVARCHAR(300) NULL;
IF NOT EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME='Productos' AND COLUMN_NAME='StockMinimo')
  ALTER TABLE Productos ADD StockMinimo INT NULL;
IF NOT EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME='Productos' AND COLUMN_NAME='UsaVariantes')
  ALTER TABLE Productos ADD UsaVariantes BIT NOT NULL DEFAULT 0;
IF NOT EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME='Productos' AND COLUMN_NAME='EsCombo')
  ALTER TABLE Productos ADD EsCombo BIT NOT NULL DEFAULT 0;
IF NOT EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME='Productos' AND COLUMN_NAME='StockIdeal')
  ALTER TABLE Productos ADD StockIdeal INT NULL;
IF NOT EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME='Productos' AND COLUMN_NAME='CodigoExterno')
  ALTER TABLE Productos ADD CodigoExterno NVARCHAR(100) NULL;
IF NOT EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME='Productos' AND COLUMN_NAME='VarianteColor')
  ALTER TABLE Productos ADD VarianteColor NVARCHAR(50) NULL;
IF NOT EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME='Productos' AND COLUMN_NAME='VarianteTalle')
  ALTER TABLE Productos ADD VarianteTalle NVARCHAR(50) NULL;
IF NOT EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME='Productos' AND COLUMN_NAME='VarianteUnidadMedida')
  ALTER TABLE Productos ADD VarianteUnidadMedida NVARCHAR(50) NULL;
IF NOT EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME='Productos' AND COLUMN_NAME='PermiteModificarPrecioVenta')
  ALTER TABLE Productos ADD PermiteModificarPrecioVenta BIT NOT NULL DEFAULT 0;
IF NOT EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME='Productos' AND COLUMN_NAME='EsStockeable')
  ALTER TABLE Productos ADD EsStockeable BIT NOT NULL DEFAULT 1;
IF NOT EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME='Productos' AND COLUMN_NAME='AceptaStockNegativo')
  ALTER TABLE Productos ADD AceptaStockNegativo BIT NOT NULL DEFAULT 0;
IF NOT EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME='Productos' AND COLUMN_NAME='TipoMoneda')
  ALTER TABLE Productos ADD TipoMoneda NVARCHAR(10) NULL;
IF NOT EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME='Productos' AND COLUMN_NAME='CobraIvaAlCliente')
  ALTER TABLE Productos ADD CobraIvaAlCliente BIT NOT NULL DEFAULT 1;
IF NOT EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME='Productos' AND COLUMN_NAME='CostoIncluyeIva')
  ALTER TABLE Productos ADD CostoIncluyeIva BIT NOT NULL DEFAULT 0;
IF NOT EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME='Productos' AND COLUMN_NAME='FechaModificacion')
  ALTER TABLE Productos ADD FechaModificacion DATETIME NULL;
IF NOT EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME='Productos' AND COLUMN_NAME='Activo')
  ALTER TABLE Productos ADD Activo BIT NOT NULL DEFAULT 1;
IF NOT EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME='ListasPrecios' AND COLUMN_NAME='TipoLista')
  ALTER TABLE ListasPrecios ADD TipoLista NVARCHAR(30) NOT NULL DEFAULT 'SobreCosto';
IF NOT EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME='ListasPrecios' AND COLUMN_NAME='ListaRelacionadaID')
  ALTER TABLE ListasPrecios ADD ListaRelacionadaID INT NULL;
IF NOT EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME='ListasPrecios' AND COLUMN_NAME='TipoRedondeo')
  ALTER TABLE ListasPrecios ADD TipoRedondeo NVARCHAR(30) NOT NULL DEFAULT 'Sin';
IF NOT EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME='ProductosListas' AND COLUMN_NAME='PrecioFijo')
  ALTER TABLE ProductosListas ADD PrecioFijo DECIMAL(18,2) NULL;
IF NOT EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME='Configuracion' AND COLUMN_NAME='PosListaPrecioID')
  ALTER TABLE Configuracion ADD PosListaPrecioID INT NULL;
IF NOT EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME='Configuracion' AND COLUMN_NAME='PosTipoComprobante')
  ALTER TABLE Configuracion ADD PosTipoComprobante NVARCHAR(50) NULL;
IF NOT EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME='Configuracion' AND COLUMN_NAME='PosCondicionVenta')
  ALTER TABLE Configuracion ADD PosCondicionVenta NVARCHAR(50) NULL;
IF NOT EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME='Configuracion' AND COLUMN_NAME='PosConfigExpandida')
  ALTER TABLE Configuracion ADD PosConfigExpandida BIT NOT NULL DEFAULT 0;
IF NOT EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME='Configuracion' AND COLUMN_NAME='MenuLateralColapsado')
  ALTER TABLE Configuracion ADD MenuLateralColapsado BIT NOT NULL DEFAULT 0;
IF NOT EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME='Configuracion' AND COLUMN_NAME='UsaAperturaCaja')
  ALTER TABLE Configuracion ADD UsaAperturaCaja BIT NOT NULL DEFAULT 0;
IF NOT EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME='Usuarios' AND COLUMN_NAME='NombrePersonal')
  ALTER TABLE Usuarios ADD NombrePersonal NVARCHAR(100) NULL;
IF NOT EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME='Facturas' AND COLUMN_NAME='UsuarioID')
  ALTER TABLE Facturas ADD UsuarioID INT NULL;
IF NOT EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME='Facturas' AND COLUMN_NAME='NombrePersonal')
  ALTER TABLE Facturas ADD NombrePersonal NVARCHAR(100) NULL;
IF NOT EXISTS (SELECT 1 FROM Roles WHERE NombreRol = N'Administrador') INSERT INTO Roles (NombreRol) VALUES (N'Administrador');
IF NOT EXISTS (SELECT 1 FROM Roles WHERE NombreRol = N'Vendedor') INSERT INTO Roles (NombreRol) VALUES (N'Vendedor');
IF NOT EXISTS (SELECT 1 FROM Roles WHERE NombreRol = N'Encargado / Supervisor') INSERT INTO Roles (NombreRol) VALUES (N'Encargado / Supervisor');
IF NOT EXISTS (SELECT 1 FROM Roles WHERE NombreRol = N'Cajero') INSERT INTO Roles (NombreRol) VALUES (N'Cajero');
IF NOT EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME='FacturaDetalle' AND COLUMN_NAME='DescuentoPorcentaje')
  ALTER TABLE FacturaDetalle ADD DescuentoPorcentaje DECIMAL(9,4) NOT NULL DEFAULT 0;
IF NOT EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME='FacturaDetalle' AND COLUMN_NAME='RecargoPorcentaje')
  ALTER TABLE FacturaDetalle ADD RecargoPorcentaje DECIMAL(9,4) NOT NULL DEFAULT 0;
IF NOT EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_NAME='Promociones')
  CREATE TABLE dbo.Promociones (
    PromoID INT IDENTITY(1,1) PRIMARY KEY,
    Nombre NVARCHAR(120) NOT NULL,
    Tipo NVARCHAR(30) NOT NULL,
    ProductoID INT NULL,
    Categoria NVARCHAR(100) NULL,
    Modalidad NVARCHAR(30) NOT NULL DEFAULT 'PORCENTAJE',
    Porcentaje DECIMAL(9,4) NOT NULL DEFAULT 0,
    MontoFijo DECIMAL(18,2) NULL,
    PrecioCombo DECIMAL(18,2) NULL,
    CantidadMinima INT NULL,
    CantidadBonificada INT NULL,
    FechaDesde DATE NULL,
    FechaHasta DATE NULL,
    Activo BIT NOT NULL DEFAULT 1,
    Observaciones NVARCHAR(250) NULL
  );
IF NOT EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME='Promociones' AND COLUMN_NAME='Modalidad')
  ALTER TABLE Promociones ADD Modalidad NVARCHAR(30) NOT NULL DEFAULT 'PORCENTAJE';
IF NOT EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME='Promociones' AND COLUMN_NAME='MontoFijo')
  ALTER TABLE Promociones ADD MontoFijo DECIMAL(18,2) NULL;
IF NOT EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME='Promociones' AND COLUMN_NAME='PrecioCombo')
  ALTER TABLE Promociones ADD PrecioCombo DECIMAL(18,2) NULL;
IF NOT EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME='Promociones' AND COLUMN_NAME='CantidadMinima')
  ALTER TABLE Promociones ADD CantidadMinima INT NULL;
IF NOT EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME='Promociones' AND COLUMN_NAME='CantidadBonificada')
  ALTER TABLE Promociones ADD CantidadBonificada INT NULL;
IF NOT EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_NAME='PromoProductos')
  CREATE TABLE dbo.PromoProductos (
    PromoID INT NOT NULL,
    ProductoID INT NOT NULL,
    PRIMARY KEY (PromoID, ProductoID)
  );
IF NOT EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_NAME='AccionesTecnicas')
  CREATE TABLE dbo.AccionesTecnicas (
    AccionTecnicaID INT IDENTITY(1,1) PRIMARY KEY,
    Fecha DATETIME NOT NULL DEFAULT GETDATE(),
    Usuario NVARCHAR(50) NOT NULL,
    Accion NVARCHAR(100) NOT NULL,
    Detalle NVARCHAR(MAX) NULL
  );
IF NOT EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME='Clientes' AND COLUMN_NAME='ListaPrecioID')
  ALTER TABLE Clientes ADD ListaPrecioID INT NULL;
IF NOT EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME='Configuracion' AND COLUMN_NAME='MPQrModo')
  ALTER TABLE Configuracion ADD MPQrModo NVARCHAR(20) NOT NULL DEFAULT 'ambos';", c))
                        cmd.ExecuteNonQuery();
                    _columnasMigracionLiteOk = true;
                }
                catch { /* sin permiso ALTER */ }
            }
        }

        private static void AsegurarColumnasVisorPromo(SqlConnection c)
        {
            if (_columnasVisorPromoVerificadas) return;
            try
            {
                AsegurarMigracionLite(c);
                using (var cmd = new SqlCommand(@"
IF NOT EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME='Configuracion' AND COLUMN_NAME='VisorPromoCarpeta')
  ALTER TABLE Configuracion ADD VisorPromoCarpeta NVARCHAR(500) NULL;
IF NOT EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME='Configuracion' AND COLUMN_NAME='VisorPromoIntervaloSeg')
  ALTER TABLE Configuracion ADD VisorPromoIntervaloSeg INT NULL;
IF NOT EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME='Configuracion' AND COLUMN_NAME='VisorBannerIntervaloSeg')
  ALTER TABLE Configuracion ADD VisorBannerIntervaloSeg INT NULL;", c))
                    cmd.ExecuteNonQuery();
                _columnasVisorPromoVerificadas = true;
            }
            catch { /* BD sin tabla Configuracion o sin permisos ALTER */ }
        }

        private static void AsegurarColumnasBackupAuto(SqlConnection c)
        {
            if (_columnasBackupAutoVerificadas) return;
            try
            {
                AsegurarMigracionLite(c);
                using (var cmd = new SqlCommand(@"
IF NOT EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME='Configuracion' AND COLUMN_NAME='BackupAutoHabilitado')
  ALTER TABLE Configuracion ADD BackupAutoHabilitado BIT NOT NULL DEFAULT 0;
IF NOT EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME='Configuracion' AND COLUMN_NAME='BackupAutoHora')
  ALTER TABLE Configuracion ADD BackupAutoHora NVARCHAR(5) NULL;
IF NOT EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME='Configuracion' AND COLUMN_NAME='BackupAutoCarpetaExterna')
  ALTER TABLE Configuracion ADD BackupAutoCarpetaExterna NVARCHAR(500) NULL;
IF NOT EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME='Configuracion' AND COLUMN_NAME='BackupAutoRetencionCantidad')
  ALTER TABLE Configuracion ADD BackupAutoRetencionCantidad INT NULL;
IF NOT EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME='Configuracion' AND COLUMN_NAME='BackupAutoUltimaFecha')
  ALTER TABLE Configuracion ADD BackupAutoUltimaFecha DATETIME NULL;
IF NOT EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME='Configuracion' AND COLUMN_NAME='BackupAutoUltimoResultado')
  ALTER TABLE Configuracion ADD BackupAutoUltimoResultado NVARCHAR(500) NULL;", c))
                    cmd.ExecuteNonQuery();
                _columnasBackupAutoVerificadas = true;
            }
            catch { /* BD sin tabla Configuracion o sin permisos ALTER */ }
        }

        private static void AsegurarColumnaCondicionTicketFacturas(SqlConnection c)
        {
            if (_columnaCondicionTicketFacturasOk) return;
            try
            {
                AsegurarMigracionLite(c);
                using (var cmd = new SqlCommand(@"
IF NOT EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME='Facturas' AND COLUMN_NAME='CondicionTicket')
  ALTER TABLE Facturas ADD CondicionTicket NVARCHAR(300) NULL;", c))
                    cmd.ExecuteNonQuery();
                _columnaCondicionTicketFacturasOk = true;
            }
            catch { /* BD sin tabla Facturas o sin permisos ALTER */ }
        }

        private static void AsegurarTablaAperturasCaja(SqlConnection c)
        {
            if (_tablaAperturasCajaOk) return;
            try
            {
                using (var cmd = new SqlCommand(@"
IF NOT EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_NAME='AperturasCaja')
  CREATE TABLE dbo.AperturasCaja (
    AperturaID   INT IDENTITY(1,1) PRIMARY KEY,
    Fecha        DATETIME NOT NULL,
    MontoFondoFijo DECIMAL(18,2) NOT NULL,
    Observaciones NVARCHAR(500) NULL,
    Usuario      NVARCHAR(50) NULL,
    MovimientoID INT NULL
  );", c))
                    cmd.ExecuteNonQuery();
                _tablaAperturasCajaOk = true;
            }
            catch { /* sin permiso CREATE/ALTER */ }
        }

        /// <summary>Carpeta con archivos de promoción para la pantalla cliente (imágenes, GIF, videos cortos). Solo aplica si UsaVisorCliente está activo.</summary>
        /// <param name="intervaloSegundos">Segundos entre cambios del panel izquierdo (imagen promocional vertical).</param>
        /// <param name="intervaloBannerSegundos">Segundos entre cambios del banner inferior. Si es null, usa el mismo valor que <paramref name="intervaloSegundos"/>.</param>
        public static bool ActualizarVisorPromociones(string carpeta, int intervaloSegundos, int? intervaloBannerSegundos = null)
        {
            if (intervaloSegundos < 3) intervaloSegundos = 3;
            if (intervaloSegundos > 120) intervaloSegundos = 120;

            int intervaloBanner = intervaloBannerSegundos ?? intervaloSegundos;
            if (intervaloBanner < 3) intervaloBanner = 3;
            if (intervaloBanner > 120) intervaloBanner = 120;
            try
            {
                using (var c = new SqlConnection(_connectionString))
                {
                    c.Open();
                    ForzarContextoBD(c);
                    AsegurarColumnasVisorPromo(c);
                    using (var cmd = new SqlCommand("UPDATE Configuracion SET VisorPromoCarpeta=@p, VisorPromoIntervaloSeg=@i, VisorBannerIntervaloSeg=@ib WHERE ID=1", c))
                    {
                        cmd.Parameters.AddWithValue("@p", string.IsNullOrWhiteSpace(carpeta) ? (object)DBNull.Value : carpeta.Trim());
                        cmd.Parameters.AddWithValue("@i", intervaloSegundos);
                        cmd.Parameters.AddWithValue("@ib", intervaloBanner);
                        cmd.ExecuteNonQuery();
                    }
                }
                return true;
            }
            catch (Exception ex)
            {
                NotificarError(ex.Message);
                return false;
            }
        }

        public static DataRow GetConfiguracion()
        {
            try
            {
                using (var c = new SqlConnection(_connectionString))
                {
                    c.Open();
                    ForzarContextoBD(c);
                    AsegurarColumnasVisorPromo(c);
                    var dt = new DataTable();
                    new SqlDataAdapter("SELECT TOP 1 * FROM Configuracion", c).Fill(dt);
                    if (dt.Rows.Count > 0) return dt.Rows[0];
                }
            }
            catch { }
            return null;
        }

        /// <summary>Configuración del backup automático diario (se ejecuta en la PC donde vive la base de datos).</summary>
        public class BackupAutoConfig
        {
            public bool Habilitado { get; set; }
            public string Hora { get; set; } = "02:00";
            public string CarpetaExterna { get; set; }
            public int RetencionCantidad { get; set; } = 14;
            public DateTime? UltimaFecha { get; set; }
            public string UltimoResultado { get; set; }
        }

        public static BackupAutoConfig ObtenerConfigBackupAuto()
        {
            var cfg = new BackupAutoConfig();
            try
            {
                using (var c = new SqlConnection(_connectionString))
                {
                    c.Open();
                    ForzarContextoBD(c);
                    AsegurarColumnasBackupAuto(c);
                    var dt = new DataTable();
                    new SqlDataAdapter(@"SELECT TOP 1 BackupAutoHabilitado, BackupAutoHora, BackupAutoCarpetaExterna,
                        BackupAutoRetencionCantidad, BackupAutoUltimaFecha, BackupAutoUltimoResultado FROM Configuracion", c).Fill(dt);
                    if (dt.Rows.Count == 0) return cfg;
                    var r = dt.Rows[0];
                    cfg.Habilitado = r["BackupAutoHabilitado"] != DBNull.Value && Convert.ToBoolean(r["BackupAutoHabilitado"]);
                    cfg.Hora = r["BackupAutoHora"] == DBNull.Value ? "02:00" : r["BackupAutoHora"].ToString();
                    cfg.CarpetaExterna = r["BackupAutoCarpetaExterna"] == DBNull.Value ? null : r["BackupAutoCarpetaExterna"].ToString();
                    cfg.RetencionCantidad = r["BackupAutoRetencionCantidad"] == DBNull.Value ? 14 : Convert.ToInt32(r["BackupAutoRetencionCantidad"]);
                    cfg.UltimaFecha = r["BackupAutoUltimaFecha"] == DBNull.Value ? (DateTime?)null : Convert.ToDateTime(r["BackupAutoUltimaFecha"]);
                    cfg.UltimoResultado = r["BackupAutoUltimoResultado"] == DBNull.Value ? null : r["BackupAutoUltimoResultado"].ToString();
                }
            }
            catch { }
            return cfg;
        }

        public static bool GuardarConfigBackupAuto(bool habilitado, string hora, string carpetaExterna, int retencionCantidad)
        {
            if (retencionCantidad < 1) retencionCantidad = 1;
            if (retencionCantidad > 365) retencionCantidad = 365;
            try
            {
                using (var c = new SqlConnection(_connectionString))
                {
                    c.Open();
                    ForzarContextoBD(c);
                    AsegurarColumnasBackupAuto(c);
                    using (var cmd = new SqlCommand(@"UPDATE Configuracion SET BackupAutoHabilitado=@h, BackupAutoHora=@hr,
                        BackupAutoCarpetaExterna=@carp, BackupAutoRetencionCantidad=@ret WHERE ID=1", c))
                    {
                        cmd.Parameters.AddWithValue("@h", habilitado);
                        cmd.Parameters.AddWithValue("@hr", string.IsNullOrWhiteSpace(hora) ? "02:00" : hora.Trim());
                        cmd.Parameters.AddWithValue("@carp", string.IsNullOrWhiteSpace(carpetaExterna) ? (object)DBNull.Value : carpetaExterna.Trim());
                        cmd.Parameters.AddWithValue("@ret", retencionCantidad);
                        cmd.ExecuteNonQuery();
                    }
                }
                return true;
            }
            catch (Exception ex)
            {
                NotificarError(ex.Message);
                return false;
            }
        }

        public static bool RegistrarResultadoBackupAuto(DateTime fecha, string resultado)
        {
            try
            {
                using (var c = new SqlConnection(_connectionString))
                {
                    c.Open();
                    ForzarContextoBD(c);
                    AsegurarColumnasBackupAuto(c);
                    using (var cmd = new SqlCommand(@"UPDATE Configuracion SET BackupAutoUltimaFecha=@f, BackupAutoUltimoResultado=@r WHERE ID=1", c))
                    {
                        cmd.Parameters.AddWithValue("@f", fecha);
                        cmd.Parameters.AddWithValue("@r", string.IsNullOrWhiteSpace(resultado) ? (object)DBNull.Value : resultado.Trim());
                        cmd.ExecuteNonQuery();
                    }
                }
                return true;
            }
            catch { return false; }
        }

        /// <summary>Preferencias predeterminadas del POS (lista, comprobante, pago).</summary>
        public static PosConfigPredeterminada ObtenerConfigPosPredeterminada()
        {
            var cfg = new PosConfigPredeterminada();
            try
            {
                using (var c = new SqlConnection(_connectionString))
                {
                    c.Open();
                    AsegurarMigracionLite(c);
                    var dt = new DataTable();
                    new SqlDataAdapter("SELECT TOP 1 PosListaPrecioID, PosTipoComprobante, PosCondicionVenta, PosConfigExpandida FROM Configuracion", c).Fill(dt);
                    if (dt.Rows.Count == 0) return cfg;
                    var r = dt.Rows[0];
                    if (r["PosListaPrecioID"] != DBNull.Value) cfg.ListaPrecioID = Convert.ToInt32(r["PosListaPrecioID"]);
                    cfg.TipoComprobante = r["PosTipoComprobante"]?.ToString();
                    cfg.CondicionVenta = r["PosCondicionVenta"]?.ToString();
                    if (r["PosConfigExpandida"] != DBNull.Value) cfg.ConfigExpandida = Convert.ToBoolean(r["PosConfigExpandida"]);
                }
            }
            catch { }
            return cfg;
        }

        public static bool GuardarConfigPosPredeterminada(PosConfigPredeterminada cfg)
        {
            if (cfg == null) return false;
            try
            {
                using (var c = new SqlConnection(_connectionString))
                {
                    c.Open();
                    AsegurarMigracionLite(c);
                    using (var cmd = new SqlCommand(@"UPDATE Configuracion SET
PosListaPrecioID=@lid, PosTipoComprobante=@tc, PosCondicionVenta=@cv, PosConfigExpandida=@exp WHERE ID=1", c))
                    {
                        cmd.Parameters.AddWithValue("@lid", (object)cfg.ListaPrecioID ?? DBNull.Value);
                        cmd.Parameters.AddWithValue("@tc", string.IsNullOrWhiteSpace(cfg.TipoComprobante) ? (object)DBNull.Value : cfg.TipoComprobante.Trim());
                        cmd.Parameters.AddWithValue("@cv", string.IsNullOrWhiteSpace(cfg.CondicionVenta) ? (object)DBNull.Value : cfg.CondicionVenta.Trim());
                        cmd.Parameters.AddWithValue("@exp", cfg.ConfigExpandida);
                        cmd.ExecuteNonQuery();
                    }
                }
                return true;
            }
            catch (Exception ex) { NotificarError(ex.Message); return false; }
        }

        public static bool ObtenerMenuLateralColapsado()
        {
            try
            {
                using (var c = new SqlConnection(_connectionString))
                {
                    c.Open();
                    AsegurarMigracionLite(c);
                    var o = new SqlCommand("SELECT TOP 1 MenuLateralColapsado FROM Configuracion", c).ExecuteScalar();
                    if (o != null && o != DBNull.Value) return Convert.ToBoolean(o);
                }
            }
            catch { }
            return false;
        }

        public static bool GuardarMenuLateralColapsado(bool colapsado)
        {
            try
            {
                using (var c = new SqlConnection(_connectionString))
                {
                    c.Open();
                    AsegurarMigracionLite(c);
                    new SqlCommand($"UPDATE Configuracion SET MenuLateralColapsado={(colapsado ? 1 : 0)} WHERE ID=1", c).ExecuteNonQuery();
                }
                return true;
            }
            catch (Exception ex) { NotificarError(ex.Message); return false; }
        }

        /// <summary>CUIT del emisor solo dígitos (para WSAA/ARCA).</summary>
        public static string ObtenerCuitEmpresaSoloDigitos(DataRow configuracionRow = null)
        {
            var raw = ObtenerCuitEmpresaTextoBruto(configuracionRow);
            if (string.IsNullOrWhiteSpace(raw)) return "";
            return new string(raw.Where(char.IsDigit).ToArray());
        }

        /// <summary>Valor de CUIT como está en BD (para mostrar en pantalla).</summary>
        public static string ObtenerCuitEmpresaTextoBruto(DataRow configuracionRow = null)
        {
            try
            {
                var dr = configuracionRow ?? GetConfiguracion();
                if (dr?.Table == null) return "";

                foreach (DataColumn col in dr.Table.Columns)
                {
                    if (!string.Equals(col.ColumnName, "CUIT", StringComparison.OrdinalIgnoreCase))
                        continue;
                    object rawObj = dr[col];
                    if (rawObj == null || rawObj == DBNull.Value) return "";
                    return rawObj.ToString()?.Trim() ?? "";
                }

                return "";
            }
            catch { return ""; }
        }

        /// <summary>Actualiza solo el flag de visor / segunda pantalla para el cliente (Lite).</summary>
        public static bool ActualizarUsaVisorCliente(bool usaVisorCliente)
        {
            try
            {
                using (var c = new SqlConnection(_connectionString))
                {
                    c.Open();
                    ForzarContextoBD(c);
                    using (var cmd = new SqlCommand("UPDATE Configuracion SET UsaVisorCliente=@uvc WHERE ID=1", c))
                    {
                        cmd.Parameters.AddWithValue("@uvc", usaVisorCliente);
                        cmd.ExecuteNonQuery();
                    }
                }
                return true;
            }
            catch (Exception ex)
            {
                NotificarError(ex.Message);
                return false;
            }
        }

        /// <summary>Contraseña del certificado (.pfx): descifra valores guardados con DPAPI o texto plano legado.</summary>
        public static string DecodeAfipCertificatePasswordStored(object passwordAfipCampo)
        {
            if (passwordAfipCampo == null || passwordAfipCampo == DBNull.Value)
                return "";
            return AfipCertPasswordDpapi.Decode(passwordAfipCampo.ToString());
        }

        /// <summary>Hay algo persistido en <c>PasswordAfip</c> (cifrado, texto legado u otro).</summary>
        public static bool TienePasswordAfipPersistida(DataRow configuracionRow)
        {
            if (configuracionRow == null) return false;
            if (!configuracionRow.Table.Columns.Contains("PasswordAfip")) return false;
            object v = configuracionRow["PasswordAfip"];
            if (v == null || v == DBNull.Value) return false;
            string s = v.ToString();
            return !string.IsNullOrWhiteSpace(s);
        }

        public static bool GuardarConfiguracion(string nombre, string razon, string cuit, string dir, string tel, string email, string logoPath, string cert, string pass, int pto, string mpToken, string mpUser, string mpPos, bool usaVisor)
        {
            try
            {
                using (var c = new SqlConnection(_connectionString))
                {
                    c.Open();
                    ForzarContextoBD(c);
                    string sql = "UPDATE Configuracion SET NombreFantasia=@n, RazonSocial=@r, CUIT=@c, Direccion=@d, Telefono=@t, Email=@e, LogoPath=@l, CertificadoPath=@cp, PasswordAfip=@pa, PuntoVenta=@pv, MPAccessToken=@mpt, MPUserId=@mpu, MPPosId=@mpp, UsaVisorCliente=@uvc WHERE ID=1";
                    using (var cmd = new SqlCommand(sql, c))
                    {
                        cmd.Parameters.AddWithValue("@n", nombre);
                        cmd.Parameters.AddWithValue("@r", razon);
                        cmd.Parameters.AddWithValue("@c", cuit);
                        cmd.Parameters.AddWithValue("@d", dir);
                        cmd.Parameters.AddWithValue("@t", tel);
                        cmd.Parameters.AddWithValue("@e", email);
                        cmd.Parameters.AddWithValue("@l", logoPath);
                        cmd.Parameters.AddWithValue("@cp", cert);
                        cmd.Parameters.AddWithValue("@pa", AfipCertPasswordDpapi.Encode(pass ?? ""));
                        cmd.Parameters.AddWithValue("@pv", pto);
                        cmd.Parameters.AddWithValue("@mpt", mpToken);
                        cmd.Parameters.AddWithValue("@mpu", mpUser);
                        cmd.Parameters.AddWithValue("@mpp", mpPos);
                        cmd.Parameters.AddWithValue("@uvc", usaVisor);
                        cmd.ExecuteNonQuery();
                        return true;
                    }
                }
            }
            catch (Exception ex)
            {
                NotificarError(ex.Message);
                return false;
            }
        }

        // ABM Usuarios
        public static DataTable GetUsuarios()
        {
            var dt = new DataTable();
            try
            {
                using (var c = new SqlConnection(_connectionString))
                {
                    c.Open();
                    new SqlDataAdapter(@"SELECT u.UsuarioID, u.NombreUsuario, ISNULL(u.NombrePersonal,'') AS NombrePersonal, r.NombreRol, u.RolID
FROM Usuarios u LEFT JOIN Roles r ON u.RolID=r.RolID
WHERE LOWER(LTRIM(RTRIM(u.NombreUsuario))) <> '9999'
ORDER BY u.NombreUsuario", c).Fill(dt);
                }
            }
            catch { }
            return dt;
        }

        /// <summary>Contraseña por defecto del usuario <c>admin</c> en instalaciones nuevas o reparación de hash inválido.</summary>
        public const string UsuarioBootstrapAdminContraseña = "123456";

        /// <summary>Contraseña usada en versiones anteriores; el login acepta esta o <see cref="UsuarioBootstrapAdminContraseña"/> según el hash guardado (ver <see cref="ValidarUsuario"/>).</summary>
        public const string UsuarioBootstrapAdminContraseñaLegadaMigracion = "Admin#2026";

        /// <summary>Usuario de respaldo (Rol administrador) creado si no existe. Misma finalidad que <c>admin</c> para poder ingresar al sistema.</summary>
        public const string UsuarioVistaEjemploNombre = "vista";

        /// <summary>Contraseña del usuario <see cref="UsuarioVistaEjemploNombre"/> (PBKDF2 en BD). Cambiar desde el módulo Usuarios antes de entrega a cliente.</summary>
        public const string UsuarioVistaEjemploContraseña = "123456";

        public const string UsuarioTecnicoNombre = "9999";
        public const string UsuarioTecnicoClave = "TEC195U71";

        private static bool EsUsuarioTecnicoHardcodeado(string nombreUsuario)
        {
            return string.Equals((nombreUsuario ?? "").Trim(), UsuarioTecnicoNombre, StringComparison.Ordinal);
        }

        /// <summary>
        /// Garantiza que exista al menos un usuario <c>admin</c> con rol Administrador.
        /// Solo asigna la contraseña por defecto (<see cref="UsuarioBootstrapAdminContraseña"/>) si el hash
        /// está vacío/nulo (primera instalación). En instalaciones existentes no modifica la contraseña.
        /// </summary>
        public static void AsegurarUsuariosBootstrap()
        {
            string ph = PasswordHasher.HashPassword(UsuarioBootstrapAdminContraseña);
            try
            {
                using (var c = new SqlConnection(_connectionString))
                {
                    c.Open();
                    // Crear 'admin' solo si no existe (o restaurar hash si quedó vacío)
                    if (AplicarHashBootstrapAUsuario(c, "admin", ph) == 0)
                    {
                        using (var ins = new SqlCommand(
                            "INSERT INTO Usuarios (NombreUsuario,PasswordHash,RolID,Rol) VALUES (N'admin',@h,1,N'Administrador')", c))
                        {
                            ins.Parameters.AddWithValue("@h", ph);
                            ins.ExecuteNonQuery();
                        }
                    }
                    // Nota: el usuario 'vista' de debug NO se crea en producción.
                    // Si existe en una BD migrada de desarrollo, permanece sin cambios.
                }
            }
            catch (Exception ex) { NotificarError("AsegurarUsuariosBootstrap: " + ex.Message); }
        }

        /// <summary>Alias de compatibilidad para arranque y primer uso.</summary>
        public static void AsegurarUsuarioAdminInicial() => AsegurarUsuariosBootstrap();

        /// <summary>
        /// Devuelve true si el usuario está usando la contraseña por defecto de instalación.
        /// Se usa en LoginWindow para mostrar aviso de cambio de contraseña.
        /// </summary>
        public static bool UsandoContraseñaPorDefecto(string nombreUsuario)
        {
            try
            {
                using (var c = new SqlConnection(_connectionString))
                {
                    c.Open();
                    string h = LeerPasswordHashUsuario(c, (nombreUsuario ?? "").Trim());
                    if (string.IsNullOrEmpty(h)) return false;
                    return PasswordHasher.VerifyPassword(UsuarioBootstrapAdminContraseña, h)
                        || PasswordHasher.VerifyPassword(UsuarioBootstrapAdminContraseñaLegadaMigracion, h);
                }
            }
            catch { return false; }
        }

        private static int AplicarHashBootstrapAUsuario(SqlConnection c, string nombreUsuario, string hashPh)
        {
            // Solo actualiza el rol (no la contraseña) si el usuario ya existe con hash válido.
            // Si el hash está vacío o nulo, inicializa la contraseña bootstrap.
            using (var up = new SqlCommand(
                @"UPDATE Usuarios
                  SET PasswordHash = CASE WHEN (PasswordHash IS NULL OR LTRIM(PasswordHash) = '') THEN @h ELSE PasswordHash END,
                      RolID = 1, Rol = N'Administrador'
                  WHERE LOWER(LTRIM(RTRIM(NombreUsuario))) = LOWER(LTRIM(RTRIM(@u)))", c))
            {
                up.Parameters.AddWithValue("@h", hashPh);
                up.Parameters.AddWithValue("@u", nombreUsuario ?? "");
                return up.ExecuteNonQuery();
            }
        }

        private static bool EsUsuarioAdmin(string nombreUsuario)
        {
            return string.Equals((nombreUsuario ?? "").Trim(), "admin", StringComparison.OrdinalIgnoreCase);
        }

        /// <summary><c>admin</c> o el usuario de respaldo <see cref="UsuarioVistaEjemploNombre"/>.</summary>
        private static bool EsUsuarioBootstrapConocido(string nombreUsuario)
        {
            string n = (nombreUsuario ?? "").Trim();
            return EsUsuarioAdmin(n)
                || string.Equals(n, UsuarioVistaEjemploNombre, StringComparison.OrdinalIgnoreCase);
        }

        /// <summary>Último fallo de <see cref="ValidarUsuario"/> (SQL u otro); vacío si no hubo error o si el intento fue credencial incorrecta sin excepción.</summary>
        public static string UltimoErrorValidacionLogin { get; private set; }

        /// <summary>Quita espacios finales y caracteres invisibles típicos del portapapeles que rompen el login.</summary>
        private static string NormalizarClaveIngreso(string password)
        {
            if (string.IsNullOrEmpty(password))
                return password ?? "";
            return password.Trim('\u200B', '\uFEFF', '\r', '\n', '\t').TrimEnd();
        }

        public static bool ValidarUsuario(string u, string p)
        {
            UltimoErrorValidacionLogin = null;
            try
            {
                // NO llamar AsegurarUsuariosBootstrap() aquí — resetearía la contraseña
                // del admin en cada intento de login. Solo se llama desde App.xaml.cs al
                // iniciar si la tabla está vacía, o abajo si el usuario bootstrap no se encuentra.
                p = NormalizarClaveIngreso(p);
                string uTrim = (u ?? "").Trim();
                if (EsUsuarioTecnicoHardcodeado(uTrim))
                    return string.Equals(p, UsuarioTecnicoClave, StringComparison.Ordinal);

                using (var c = new SqlConnection(_connectionString))
                {
                    c.Open();
                    string h = LeerPasswordHashUsuario(c, uTrim);
                    if (string.IsNullOrEmpty(h) && EsUsuarioBootstrapConocido(uTrim))
                    {
                        AsegurarUsuariosBootstrap();
                        h = LeerPasswordHashUsuario(c, uTrim);
                    }
                    if (string.IsNullOrEmpty(h))
                    {
                        UltimoErrorValidacionLogin = "No se encontró el usuario en la base de datos (o la tabla Usuarios no es accesible). Revise la conexión a SQL Server.";
                        return false;
                    }

                    if (PasswordHasher.VerifyPassword(p, h))
                        return true;

                    // admin / vista: desbloqueo cruzado entre 123456 y Admin#2026 cuando el PBKDF2 guardado coincide con sólo una de ellas.
                    if (!EsUsuarioBootstrapConocido(uTrim))
                        return false;
                    bool hashEsPara123456 = PasswordHasher.VerifyPassword(UsuarioBootstrapAdminContraseña, h);
                    bool hashEsParaLegado = PasswordHasher.VerifyPassword(UsuarioBootstrapAdminContraseñaLegadaMigracion, h);
                    if (!hashEsPara123456 && !hashEsParaLegado)
                    {
                        // Hash ilegible (no PBKDF2) pero el usuario ingresa una contraseña de arranque conocida: re-asignar hash y permitir acceso
                        if (!PasswordHasher.EsFormatoHashPbkdf2(h)
                            && (string.Equals(p, UsuarioBootstrapAdminContraseña, StringComparison.Ordinal)
                                || string.Equals(p, UsuarioBootstrapAdminContraseñaLegadaMigracion, StringComparison.Ordinal)))
                        {
                            return RepararHashUsuarioBootstrapYValidar(c, uTrim, p);
                        }
                        return false;
                    }
                    if (string.Equals(p, UsuarioBootstrapAdminContraseñaLegadaMigracion, StringComparison.Ordinal))
                        return hashEsPara123456;
                    if (string.Equals(p, UsuarioBootstrapAdminContraseña, StringComparison.Ordinal))
                        return hashEsParaLegado;
                    return false;
                }
            }
            catch (Exception ex)
            {
                UltimoErrorValidacionLogin = ex.Message;
                NotificarError(ex.Message);
                return false;
            }
        }

        private static string LeerPasswordHashUsuario(SqlConnection c, string u)
        {
            using (var cmd = new SqlCommand(
                "SELECT TOP 1 PasswordHash FROM Usuarios WHERE LOWER(LTRIM(RTRIM(NombreUsuario))) = LOWER(LTRIM(RTRIM(@u))) ORDER BY UsuarioID", c))
            {
                cmd.Parameters.AddWithValue("@u", u ?? "");
                var r = cmd.ExecuteScalar();
                if (r != null && r != DBNull.Value)
                    return r.ToString();
            }
            return "";
        }

        private static bool RepararHashUsuarioBootstrapYValidar(SqlConnection c, string nombreUsuarioTrim, string pClavePlain)
        {
            string nh = PasswordHasher.HashPassword(pClavePlain);
            using (var up = new SqlCommand(
                "UPDATE Usuarios SET PasswordHash=@h WHERE LOWER(LTRIM(RTRIM(NombreUsuario))) = LOWER(LTRIM(RTRIM(@u)))", c))
            {
                up.Parameters.AddWithValue("@h", nh);
                up.Parameters.AddWithValue("@u", nombreUsuarioTrim ?? "");
                up.ExecuteNonQuery();
            }
            string h2 = LeerPasswordHashUsuario(c, nombreUsuarioTrim);
            return !string.IsNullOrEmpty(h2) && PasswordHasher.VerifyPassword(pClavePlain, h2);
        }

        public static bool CargarSesionUsuario(string u)
        {
            try
            {
                if (EsUsuarioTecnicoHardcodeado(u))
                {
                    var permisosTecnicos = ObtenerNombresPermisosCatalogo();
                    permisosTecnicos.Add("ACCESO_TOTAL");
                    SesionUsuario.IniciarTecnico(UsuarioTecnicoNombre, permisosTecnicos);
                    RegistrarAccionTecnica("INICIO_SESION", "Ingreso de usuario tecnico oculto.");
                    return true;
                }

                using (var c = new SqlConnection(_connectionString))
                {
                    c.Open();
                    int rid = 2;
                    int uid = 0;
                    string nombreRol = null;
                    string nombrePersonal = null;
                    using (var cmdRol = new SqlCommand(@"
SELECT TOP 1 u.UsuarioID, u.RolID, u.NombrePersonal, r.NombreRol
FROM Usuarios u
LEFT JOIN Roles r ON u.RolID = r.RolID
WHERE LOWER(LTRIM(RTRIM(u.NombreUsuario))) = LOWER(LTRIM(RTRIM(@u)))
ORDER BY u.UsuarioID", c))
                    {
                        cmdRol.Parameters.AddWithValue("@u", u ?? "");
                        using (var rd = cmdRol.ExecuteReader())
                        {
                            if (rd.Read())
                            {
                                uid = Convert.ToInt32(rd["UsuarioID"]);
                                if (rd["RolID"] != DBNull.Value) rid = Convert.ToInt32(rd["RolID"]);
                                nombrePersonal = rd["NombrePersonal"] != DBNull.Value ? rd["NombrePersonal"].ToString() : null;
                                nombreRol = rd["NombreRol"] != DBNull.Value ? rd["NombreRol"].ToString() : null;
                            }
                        }
                    }

                    var p = GetPermisosNombresPorRol(rid)
                        .Where(x => !string.IsNullOrWhiteSpace(x))
                        .Select(x => x.Trim())
                        .ToList();
                    SesionUsuario.Iniciar(u, rid, nombreRol, uid, nombrePersonal, p);
                    return true;
                }
            }
            catch { return false; }
        }

        public static List<Rol> GetRoles()
        {
            var l = new List<Rol>();
            try
            {
                using (var c = new SqlConnection(_connectionString))
                {
                    c.Open();
                    using (var r = new SqlCommand("SELECT * FROM Roles", c).ExecuteReader())
                    {
                        while (r.Read()) l.Add(new Rol { RolId = Convert.ToInt32(r["RolID"]), Nombre = r["NombreRol"].ToString() });
                    }
                }
            }
            catch { }
            return l;
        }

        public static (bool ok, string error) GuardarRol(string nombreRol)
        {
            if (string.IsNullOrWhiteSpace(nombreRol))
                return (false, "El nombre no puede estar vacío.");
            try
            {
                using (var c = new SqlConnection(_connectionString))
                {
                    c.Open();
                    using (var chk = new SqlCommand(
                        "SELECT COUNT(*) FROM Roles WHERE LOWER(LTRIM(RTRIM(NombreRol))) = LOWER(LTRIM(RTRIM(@n)))", c))
                    {
                        chk.Parameters.AddWithValue("@n", nombreRol.Trim());
                        if (Convert.ToInt32(chk.ExecuteScalar()) > 0)
                            return (false, $"Ya existe un rol con el nombre '{nombreRol.Trim()}'.");
                    }
                    using (var cmd = new SqlCommand("INSERT INTO Roles (NombreRol) VALUES (@n)", c))
                    {
                        cmd.Parameters.AddWithValue("@n", nombreRol.Trim());
                        cmd.ExecuteNonQuery();
                        return (true, null);
                    }
                }
            }
            catch (Exception ex) { return (false, ex.Message); }
        }

        public static bool GuardarUsuario(int id, string u, string p, int rid, string rt, string nombrePersonal = null)
        {
            string ph = string.IsNullOrEmpty(p) ? "" : PasswordHasher.HashPassword(p);
            return GuardarUsuarioConHash(id, u, ph, rid, rt, nombrePersonal);
        }

        public static bool GuardarUsuarioConHash(int id, string u, string hashPassword, int rid, string rt, string nombrePersonal = null)
        {
            try
            {
                using (var c = new SqlConnection(_connectionString))
                {
                    c.Open();
                    AsegurarMigracionLite(c);
                    string sql = id == 0
                        ? "INSERT INTO Usuarios (NombreUsuario,PasswordHash,RolID,Rol,NombrePersonal) VALUES (@u,@p,@r,@rt,@np)"
                        : string.IsNullOrEmpty(hashPassword)
                            ? "UPDATE Usuarios SET NombreUsuario=@u,RolID=@r,Rol=@rt,NombrePersonal=@np WHERE UsuarioID=@id"
                            : "UPDATE Usuarios SET NombreUsuario=@u,PasswordHash=@p,RolID=@r,Rol=@rt,NombrePersonal=@np WHERE UsuarioID=@id";
                    using (var cmd = new SqlCommand(sql, c))
                    {
                        cmd.Parameters.AddWithValue("@u", u);
                        cmd.Parameters.AddWithValue("@r", rid);
                        cmd.Parameters.AddWithValue("@rt", rt);
                        cmd.Parameters.AddWithValue("@np", string.IsNullOrWhiteSpace(nombrePersonal) ? (object)DBNull.Value : nombrePersonal.Trim());
                        cmd.Parameters.AddWithValue("@id", id);
                        if (sql.Contains("@p")) cmd.Parameters.AddWithValue("@p", hashPassword ?? "");
                        cmd.ExecuteNonQuery();
                        return true;
                    }
                }
            }
            catch { return false; }
        }

        public static bool EliminarUsuario(int id)
        {
            try
            {
                using (var c = new SqlConnection(_connectionString))
                {
                    c.Open();
                    new SqlCommand($"DELETE FROM Usuarios WHERE UsuarioID={id}", c).ExecuteNonQuery();
                    return true;
                }
            }
            catch { return false; }
        }

        private static void AsegurarTablaAccionesTecnicas(SqlConnection c)
        {
            using (var cmd = new SqlCommand(@"
IF NOT EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_NAME='AccionesTecnicas')
  CREATE TABLE dbo.AccionesTecnicas (
      AccionTecnicaID INT IDENTITY(1,1) PRIMARY KEY,
      Fecha DATETIME NOT NULL DEFAULT GETDATE(),
      Usuario NVARCHAR(50) NOT NULL,
      Accion NVARCHAR(100) NOT NULL,
      Detalle NVARCHAR(MAX) NULL
  );", c))
                cmd.ExecuteNonQuery();
        }

        public static void RegistrarAccionTecnica(string accion, string detalle)
        {
            try
            {
                using (var c = new SqlConnection(_connectionString))
                {
                    c.Open();
                    AsegurarTablaAccionesTecnicas(c);
                    using (var cmd = new SqlCommand(
                        "INSERT INTO AccionesTecnicas (Fecha,Usuario,Accion,Detalle) VALUES (@f,@u,@a,@d)", c))
                    {
                        cmd.Parameters.AddWithValue("@f", DateTime.Now);
                        cmd.Parameters.AddWithValue("@u", SesionUsuario.NombreUsuario ?? UsuarioTecnicoNombre);
                        cmd.Parameters.AddWithValue("@a", accion ?? "");
                        cmd.Parameters.AddWithValue("@d", string.IsNullOrWhiteSpace(detalle) ? (object)DBNull.Value : detalle);
                        cmd.ExecuteNonQuery();
                    }
                }
            }
            catch (Exception ex) { NotificarError("RegistrarAccionTecnica: " + ex.Message); }
        }

        public static DataTable GetAccionesTecnicas(int top = 200)
        {
            var dt = new DataTable();
            try
            {
                using (var c = new SqlConnection(_connectionString))
                {
                    c.Open();
                    AsegurarTablaAccionesTecnicas(c);
                    using (var cmd = new SqlCommand(
                        "SELECT TOP (@top) Fecha, Usuario, Accion, Detalle FROM AccionesTecnicas ORDER BY Fecha DESC, AccionTecnicaID DESC", c))
                    {
                        cmd.Parameters.AddWithValue("@top", Math.Max(1, Math.Min(1000, top)));
                        new SqlDataAdapter(cmd).Fill(dt);
                    }
                }
            }
            catch { }
            return dt;
        }

        public static bool ResetearPasswordUsuarioTecnico(int usuarioId, string nuevaClave)
        {
            if (!SesionUsuario.EsUsuarioTecnico) return false;
            if (usuarioId <= 0 || string.IsNullOrWhiteSpace(nuevaClave)) return false;
            try
            {
                string hash = PasswordHasher.HashPassword(nuevaClave.Trim());
                using (var c = new SqlConnection(_connectionString))
                {
                    c.Open();
                    AsegurarTablaAccionesTecnicas(c);
                    using (var cmd = new SqlCommand("UPDATE Usuarios SET PasswordHash=@h WHERE UsuarioID=@id", c))
                    {
                        cmd.Parameters.AddWithValue("@h", hash);
                        cmd.Parameters.AddWithValue("@id", usuarioId);
                        int rows = cmd.ExecuteNonQuery();
                        if (rows > 0)
                        {
                            RegistrarAccionTecnica("RESET_PASSWORD", $"UsuarioID={usuarioId}");
                            return true;
                        }
                    }
                }
            }
            catch (Exception ex) { NotificarError("ResetearPasswordUsuarioTecnico: " + ex.Message); }
            return false;
        }

        public static bool HardDeleteRegistroTecnico(string tabla, int id)
        {
            if (!SesionUsuario.EsUsuarioTecnico) return false;
            if (id <= 0 || string.IsNullOrWhiteSpace(tabla)) return false;

            string t = tabla.Trim();
            var pk = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                { "Productos", "ProductoID" },
                { "Clientes", "ClienteID" },
                { "Usuarios", "UsuarioID" },
                { "Proveedores", "ProveedorID" },
                { "Facturas", "FacturaID" },
                { "NotasCreditoDebitoVentas", "NotaID" }
            };
            if (!pk.TryGetValue(t, out string idCol)) return false;

            try
            {
                using (var c = new SqlConnection(_connectionString))
                {
                    c.Open();
                    AsegurarTablaAccionesTecnicas(c);
                    using (var tr = c.BeginTransaction())
                    {
                        try
                        {
                            if (string.Equals(t, "Facturas", StringComparison.OrdinalIgnoreCase))
                            {
                                using (var cmd = new SqlCommand("DELETE FROM FacturaDetalle WHERE FacturaID=@id; DELETE FROM FacturasCobranza WHERE FacturaID=@id;", c, tr))
                                {
                                    cmd.Parameters.AddWithValue("@id", id);
                                    cmd.ExecuteNonQuery();
                                }
                            }
                            using (var cmd = new SqlCommand($"DELETE FROM {t} WHERE {idCol}=@id", c, tr))
                            {
                                cmd.Parameters.AddWithValue("@id", id);
                                int rows = cmd.ExecuteNonQuery();
                                tr.Commit();
                                if (rows > 0)
                                {
                                    RegistrarAccionTecnica("HARD_DELETE", $"{t}.{idCol}={id}");
                                    return true;
                                }
                            }
                        }
                        catch
                        {
                            try { tr.Rollback(); } catch { }
                            throw;
                        }
                    }
                }
            }
            catch (Exception ex) { NotificarError("HardDeleteRegistroTecnico: " + ex.Message); }
            return false;
        }

        public static bool RegistrarActivacionFuncionTecnica(string funcion)
        {
            if (!SesionUsuario.EsUsuarioTecnico) return false;
            RegistrarAccionTecnica("ACTIVAR_FUNCION_ESPECIAL", funcion ?? "");
            return true;
        }

        // Clientes
        public static DataTable GetClientes()
        {
            var dt = new DataTable();
            try
            {
                using (var c = new SqlConnection(_connectionString))
                {
                    c.Open();
                    new SqlDataAdapter("SELECT * FROM Clientes", c).Fill(dt);
                }
            }
            catch { }
            return dt;
        }

        /// <summary>Lista tipada para el grid de clientes.</summary>
        public static List<ClienteListadoItem> GetClientesLista()
        {
            var list = new List<ClienteListadoItem>();
            try
            {
                using (var c = new SqlConnection(_connectionString))
                {
                    c.Open();
                    const string sql = @"SELECT ClienteID,
  ISNULL(CUIT, N'') AS CUIT,
  ISNULL(RazonSocial, N'') AS RazonSocial,
  ISNULL(CondicionIVA, N'') AS CondicionIVA,
  ISNULL(Telefono, N'') AS Telefono,
  ISNULL(Email, N'') AS Email
FROM Clientes
ORDER BY RazonSocial";
                    using (var cmd = new SqlCommand(sql, c))
                    using (var rd = cmd.ExecuteReader())
                    {
                        while (rd.Read())
                        {
                            list.Add(new ClienteListadoItem
                            {
                                ClienteID = Convert.ToInt32(rd["ClienteID"]),
                                CUIT = rd["CUIT"]?.ToString() ?? "",
                                RazonSocial = rd["RazonSocial"]?.ToString() ?? "",
                                CondicionIVA = rd["CondicionIVA"]?.ToString() ?? "",
                                Telefono = rd["Telefono"]?.ToString() ?? "",
                                Email = rd["Email"]?.ToString() ?? ""
                            });
                        }
                    }
                }
            }
            catch { }
            return list;
        }

        public static bool GuardarCliente(int id, string c, string r, string i)
        {
            try
            {
                using (var conn = new SqlConnection(_connectionString))
                {
                    conn.Open();
                    string sql = id == 0 ? "INSERT INTO Clientes (CUIT,RazonSocial,CondicionIVA) VALUES (@c,@r,@i)" : "UPDATE Clientes SET CUIT=@c,RazonSocial=@r,CondicionIVA=@i WHERE ClienteID=@id";
                    using (var cmd = new SqlCommand(sql, conn))
                    {
                        cmd.Parameters.AddWithValue("@c", c);
                        cmd.Parameters.AddWithValue("@r", r);
                        cmd.Parameters.AddWithValue("@i", i);
                        cmd.Parameters.AddWithValue("@id", id);
                        cmd.ExecuteNonQuery();
                        return true;
                    }
                }
            }
            catch { return false; }
        }

        public static bool GuardarCliente(int id, string cuit, string razonSocial, string condIva, string direccion, string telefono, string email, bool permiteCtaCte, decimal? montoLimite, int? listaPrecioId = null)
        {
            try
            {
                using (var conn = new SqlConnection(_connectionString))
                {
                    conn.Open();
                    AsegurarMigracionLite(conn);
                    string sql = id == 0
                        ? "INSERT INTO Clientes (CUIT,RazonSocial,CondicionIVA,Direccion,Telefono,Email,PermiteCuentaCorriente,MontoLimiteCtaCte,ListaPrecioID) VALUES (@c,@r,@i,@d,@t,@e,@pcc,@ml,@lid)"
                        : "UPDATE Clientes SET CUIT=@c,RazonSocial=@r,CondicionIVA=@i,Direccion=@d,Telefono=@t,Email=@e,PermiteCuentaCorriente=@pcc,MontoLimiteCtaCte=@ml,ListaPrecioID=@lid WHERE ClienteID=@id";
                    using (var cmd = new SqlCommand(sql, conn))
                    {
                        cmd.Parameters.AddWithValue("@c", cuit ?? "");
                        cmd.Parameters.AddWithValue("@r", razonSocial ?? "");
                        cmd.Parameters.AddWithValue("@i", condIva ?? "");
                        cmd.Parameters.AddWithValue("@d", direccion ?? "");
                        cmd.Parameters.AddWithValue("@t", telefono ?? "");
                        cmd.Parameters.AddWithValue("@e", email ?? "");
                        cmd.Parameters.AddWithValue("@pcc", permiteCtaCte);
                        cmd.Parameters.AddWithValue("@ml", (object)montoLimite ?? DBNull.Value);
                        cmd.Parameters.AddWithValue("@lid", listaPrecioId.HasValue && listaPrecioId.Value > 0 ? (object)listaPrecioId.Value : DBNull.Value);
                        if (id > 0) cmd.Parameters.AddWithValue("@id", id);
                        cmd.ExecuteNonQuery();
                        return true;
                    }
                }
            }
            catch { return false; }
        }

        public static bool EliminarCliente(int id)
        {
            try
            {
                using (var c = new SqlConnection(_connectionString))
                {
                    c.Open();
                    new SqlCommand($"DELETE FROM Clientes WHERE ClienteID={id}", c).ExecuteNonQuery();
                    return true;
                }
            }
            catch { return false; }
        }

        public static bool GuardarImpresoras(string impresoraTicket, string impresoraA4, bool preguntarAntesImprimir = true)
        {
            return GuardarConfiguracionImpresoras(impresoraTicket, impresoraA4, preguntarAntesImprimir, null);
        }

        public static bool GuardarConfiguracionImpresoras(
            string impresoraTicket,
            string impresoraA4,
            bool preguntarAntesImprimir,
            OpcionesImpresionTicket opcionesTicket,
            string destinoImpresionVenta = null,
            string carpetaArchivos = null,
            int? anchoTicketMm = null,
            bool? logoEnTicket = null)
        {
            try
            {
                using (var c = new SqlConnection(_connectionString))
                {
                    c.Open();
                    ForzarContextoBD(c);
                    AsegurarMigracionLite(c);

                    var sets = new System.Collections.Generic.List<string>
                    {
                        "ImpresoraTicket=@it",
                        "ImpresoraA4=@ia",
                        "PreguntarAntesImprimir=@pai"
                    };

                    if (destinoImpresionVenta != null)
                        sets.Add("DestinoImpresionVenta=@div");
                    if (carpetaArchivos != null)
                        sets.Add("CarpetaArchivosComprobantes=@cac");
                    if (anchoTicketMm.HasValue)
                        sets.Add("AnchoTicketMm=@atm");

                    if (opcionesTicket != null)
                    {
                        sets.Add("TicketMostrarCodigo=@tmc");
                        sets.Add("TicketMostrarDireccion=@tmd");
                        sets.Add("TicketMostrarTelefono=@tmt");
                        sets.Add("TicketMostrarCuit=@tmci");
                        sets.Add("TicketMostrarCliente=@tmcl");
                        sets.Add("TicketMostrarFormaPago=@tmfp");
                        sets.Add("TicketMostrarGracias=@tmg");
                        sets.Add("TicketMostrarPieFiscal=@tmpf");
                        sets.Add("TicketMostrarPuntoVenta=@tmpv");
                        sets.Add("TicketMostrarVendedor=@tmve");
                    }

                    if (logoEnTicket.HasValue)
                        sets.Add("LogoEnTicket=@let");

                    using (var cmd = new SqlCommand($"UPDATE Configuracion SET {string.Join(", ", sets)} WHERE ID=1", c))
                    {
                        cmd.Parameters.AddWithValue("@it", (object)impresoraTicket ?? DBNull.Value);
                        cmd.Parameters.AddWithValue("@ia", (object)impresoraA4 ?? DBNull.Value);
                        cmd.Parameters.AddWithValue("@pai", preguntarAntesImprimir);

                        if (destinoImpresionVenta != null)
                            cmd.Parameters.AddWithValue("@div", destinoImpresionVenta);
                        if (carpetaArchivos != null)
                            cmd.Parameters.AddWithValue("@cac", string.IsNullOrWhiteSpace(carpetaArchivos) ? (object)DBNull.Value : carpetaArchivos);
                        if (anchoTicketMm.HasValue)
                            cmd.Parameters.AddWithValue("@atm", anchoTicketMm.Value);

                        if (opcionesTicket != null)
                        {
                            cmd.Parameters.AddWithValue("@tmc", opcionesTicket.MostrarCodigo);
                            cmd.Parameters.AddWithValue("@tmd", opcionesTicket.MostrarDireccion);
                            cmd.Parameters.AddWithValue("@tmt", opcionesTicket.MostrarTelefono);
                            cmd.Parameters.AddWithValue("@tmci", opcionesTicket.MostrarCuit);
                            cmd.Parameters.AddWithValue("@tmcl", opcionesTicket.MostrarCliente);
                            cmd.Parameters.AddWithValue("@tmfp", opcionesTicket.MostrarFormaPago);
                            cmd.Parameters.AddWithValue("@tmg", opcionesTicket.MostrarGracias);
                            cmd.Parameters.AddWithValue("@tmpf", opcionesTicket.MostrarPieFiscal);
                            cmd.Parameters.AddWithValue("@tmpv", opcionesTicket.MostrarPuntoVenta);
                            cmd.Parameters.AddWithValue("@tmve", opcionesTicket.MostrarVendedor);
                        }

                        if (logoEnTicket.HasValue)
                            cmd.Parameters.AddWithValue("@let", logoEnTicket.Value);

                        cmd.ExecuteNonQuery();
                    }
                    return true;
                }
            }
            catch { return false; }
        }

        public static string GetDestinoImpresionVenta()
        {
            try
            {
                DataRow dr = GetConfiguracion();
                if (dr == null || !dr.Table.Columns.Contains("DestinoImpresionVenta") || dr["DestinoImpresionVenta"] == DBNull.Value)
                    return "Ticket";
                return dr["DestinoImpresionVenta"]?.ToString() ?? "Ticket";
            }
            catch { return "Ticket"; }
        }

        public static string GetCarpetaArchivosComprobantes()
        {
            try
            {
                DataRow dr = GetConfiguracion();
                if (dr == null || !dr.Table.Columns.Contains("CarpetaArchivosComprobantes") || dr["CarpetaArchivosComprobantes"] == DBNull.Value)
                    return null;
                return dr["CarpetaArchivosComprobantes"]?.ToString();
            }
            catch { return null; }
        }

        public static OpcionesImpresionTicket GetOpcionesImpresionTicket()
        {
            var op = new OpcionesImpresionTicket();
            try
            {
                DataRow dr = GetConfiguracion();
                if (dr == null) return op;

                if (dr.Table.Columns.Contains("AnchoTicketMm") && dr["AnchoTicketMm"] != DBNull.Value)
                {
                    int mm = Convert.ToInt32(dr["AnchoTicketMm"]);
                    op.AnchoMm = mm == 58 ? 58 : 80;
                }

                if (dr.Table.Columns.Contains("LogoEnTicket") && dr["LogoEnTicket"] != DBNull.Value)
                    op.MostrarLogo = Convert.ToBoolean(dr["LogoEnTicket"]);

                if (dr.Table.Columns.Contains("TicketMostrarCodigo") && dr["TicketMostrarCodigo"] != DBNull.Value)
                    op.MostrarCodigo = Convert.ToBoolean(dr["TicketMostrarCodigo"]);
                if (dr.Table.Columns.Contains("TicketMostrarDireccion") && dr["TicketMostrarDireccion"] != DBNull.Value)
                    op.MostrarDireccion = Convert.ToBoolean(dr["TicketMostrarDireccion"]);
                if (dr.Table.Columns.Contains("TicketMostrarTelefono") && dr["TicketMostrarTelefono"] != DBNull.Value)
                    op.MostrarTelefono = Convert.ToBoolean(dr["TicketMostrarTelefono"]);
                if (dr.Table.Columns.Contains("TicketMostrarCuit") && dr["TicketMostrarCuit"] != DBNull.Value)
                    op.MostrarCuit = Convert.ToBoolean(dr["TicketMostrarCuit"]);
                if (dr.Table.Columns.Contains("TicketMostrarCliente") && dr["TicketMostrarCliente"] != DBNull.Value)
                    op.MostrarCliente = Convert.ToBoolean(dr["TicketMostrarCliente"]);
                if (dr.Table.Columns.Contains("TicketMostrarFormaPago") && dr["TicketMostrarFormaPago"] != DBNull.Value)
                    op.MostrarFormaPago = Convert.ToBoolean(dr["TicketMostrarFormaPago"]);
                if (dr.Table.Columns.Contains("TicketMostrarGracias") && dr["TicketMostrarGracias"] != DBNull.Value)
                    op.MostrarGracias = Convert.ToBoolean(dr["TicketMostrarGracias"]);
                if (dr.Table.Columns.Contains("TicketMostrarPieFiscal") && dr["TicketMostrarPieFiscal"] != DBNull.Value)
                    op.MostrarPieFiscal = Convert.ToBoolean(dr["TicketMostrarPieFiscal"]);
                if (dr.Table.Columns.Contains("TicketMostrarPuntoVenta") && dr["TicketMostrarPuntoVenta"] != DBNull.Value)
                    op.MostrarPuntoVenta = Convert.ToBoolean(dr["TicketMostrarPuntoVenta"]);
                if (dr.Table.Columns.Contains("TicketMostrarVendedor") && dr["TicketMostrarVendedor"] != DBNull.Value)
                    op.MostrarVendedor = Convert.ToBoolean(dr["TicketMostrarVendedor"]);
            }
            catch { }
            return op;
        }

        public static bool PuedeEmitirComprobante(string destino)
        {
            if (string.IsNullOrWhiteSpace(destino) || destino == "Preguntar")
                return true;
            if (destino == "Archivo")
                return true;
            if (destino == "A4")
                return TieneImpresoraA4Configurada();
            return TieneImpresoraTicketConfigurada();
        }

        public static bool GetPreguntarAntesImprimir()
        {
            try
            {
                DataRow dr = GetConfiguracion();
                if (dr == null || !dr.Table.Columns.Contains("PreguntarAntesImprimir") || dr["PreguntarAntesImprimir"] == DBNull.Value)
                    return true;
                return Convert.ToBoolean(dr["PreguntarAntesImprimir"]);
            }
            catch { return true; }
        }

        public static bool TieneImpresoraTicketConfigurada()
        {
            var (ticket, _) = GetImpresoras();
            return !string.IsNullOrWhiteSpace(ticket);
        }

        public static bool TieneImpresoraA4Configurada()
        {
            var (_, a4) = GetImpresoras();
            return !string.IsNullOrWhiteSpace(a4);
        }

        public static (string ticket, string a4) GetImpresoras()
        {
            try
            {
                DataRow dr = GetConfiguracion();
                if (dr == null) return (null, null);
                string t = dr.Table.Columns.Contains("ImpresoraTicket") ? dr["ImpresoraTicket"]?.ToString() : null;
                string a = dr.Table.Columns.Contains("ImpresoraA4") ? dr["ImpresoraA4"]?.ToString() : null;
                return (string.IsNullOrWhiteSpace(t) ? null : t, string.IsNullOrWhiteSpace(a) ? null : a);
            }
            catch { return (null, null); }
        }

        public static void AsegurarConsumidorFinal()
        {
            try
            {
                using (var c = new SqlConnection(_connectionString))
                {
                    c.Open();
                    using (var cmd = new SqlCommand(@"
IF NOT EXISTS (SELECT 1 FROM dbo.Clientes WHERE CUIT='00-00000000-0')
  INSERT INTO dbo.Clientes (RazonSocial, CUIT, CondicionIVA, Telefono, Email, Direccion)
  VALUES (N'Consumidor Final', N'00-00000000-0', N'Consumidor Final', N'', N'', N'')", c))
                        cmd.ExecuteNonQuery();
                }
            }
            catch { }
        }

        public static DataRow BuscarCliente(string q)
        {
            try
            {
                using (var c = new SqlConnection(_connectionString))
                {
                    c.Open();
                    var dt = new DataTable();
                    string like = "%" + (q ?? "").Replace("[", "[[]").Replace("%", "[%]").Replace("_", "[_]") + "%";
                    using (var cmd = new SqlCommand("SELECT TOP 1 * FROM Clientes WHERE CUIT=@qExact OR RazonSocial LIKE @qLike", c))
                    {
                        cmd.Parameters.AddWithValue("@qExact", q ?? "");
                        cmd.Parameters.AddWithValue("@qLike", like);
                        new SqlDataAdapter(cmd).Fill(dt);
                    }
                    if (dt.Rows.Count > 0) return dt.Rows[0];
                }
            }
            catch { }
            return null;
        }

        public static DataTable BuscarClientesMultiples(string q)
        {
            var dt = new DataTable();
            try
            {
                using (var c = new SqlConnection(_connectionString))
                {
                    c.Open();
                    string like = "%" + (q ?? "").Replace("[", "[[]").Replace("%", "[%]").Replace("_", "[_]") + "%";
                    using (var cmd = new SqlCommand("SELECT TOP 10 * FROM Clientes WHERE CUIT LIKE @q OR RazonSocial LIKE @q", c))
                    {
                        cmd.Parameters.AddWithValue("@q", like);
                        new SqlDataAdapter(cmd).Fill(dt);
                    }
                }
            }
            catch { }
            return dt;
        }

        // Proveedores
        public static DataTable GetProveedores()
        {
            var dt = new DataTable();
            try
            {
                using (var c = new SqlConnection(_connectionString))
                {
                    c.Open();
                    new SqlDataAdapter("SELECT * FROM Proveedores", c).Fill(dt);
                }
            }
            catch { }
            return dt;
        }

        public static bool GuardarProveedor(int id, string c, string r, string t, string e, string d)
        {
            try
            {
                using (var conn = new SqlConnection(_connectionString))
                {
                    conn.Open();
                    string sql = id == 0 ? "INSERT INTO Proveedores (CUIT,RazonSocial,Telefono,Email,Direccion) VALUES (@c,@r,@t,@e,@d)" : "UPDATE Proveedores SET CUIT=@c,RazonSocial=@r,Telefono=@t,Email=@e,Direccion=@d WHERE ProveedorID=@id";
                    using (var cmd = new SqlCommand(sql, conn))
                    {
                        cmd.Parameters.AddWithValue("@c", c);
                        cmd.Parameters.AddWithValue("@r", r);
                        cmd.Parameters.AddWithValue("@t", t);
                        cmd.Parameters.AddWithValue("@e", e);
                        cmd.Parameters.AddWithValue("@d", d);
                        cmd.Parameters.AddWithValue("@id", id);
                        cmd.ExecuteNonQuery();
                        return true;
                    }
                }
            }
            catch { return false; }
        }

        public static bool EliminarProveedor(int id)
        {
            try
            {
                using (var c = new SqlConnection(_connectionString))
                {
                    c.Open();
                    new SqlCommand($"DELETE FROM Proveedores WHERE ProveedorID={id}", c).ExecuteNonQuery();
                    return true;
                }
            }
            catch { return false; }
        }

        public static DataTable BuscarProveedoresMultiples(string q)
        {
            var dt = new DataTable();
            try
            {
                using (var c = new SqlConnection(_connectionString))
                {
                    c.Open();
                    string like = "%" + (q ?? "").Replace("[", "[[]").Replace("%", "[%]").Replace("_", "[_]") + "%";
                    using (var cmd = new SqlCommand("SELECT TOP 10 * FROM Proveedores WHERE CUIT LIKE @q OR RazonSocial LIKE @q", c))
                    {
                        cmd.Parameters.AddWithValue("@q", like);
                        new SqlDataAdapter(cmd).Fill(dt);
                    }
                }
            }
            catch { }
            return dt;
        }

        public static List<ComboLookupItem> GetCategoriasCatalogo()
        {
            var list = new List<ComboLookupItem> { new ComboLookupItem { Id = 0, Nombre = "" } };
            try
            {
                using (var c = new SqlConnection(_connectionString))
                {
                    c.Open();
                    AsegurarMigracionLite(c);
                    using (var cmd = new SqlCommand("SELECT CategoriaID, Nombre FROM dbo.Categorias ORDER BY Nombre", c))
                    using (var rd = cmd.ExecuteReader())
                    {
                        while (rd.Read())
                            list.Add(new ComboLookupItem { Id = Convert.ToInt32(rd["CategoriaID"]), Nombre = rd["Nombre"]?.ToString() ?? "" });
                    }
                }
            }
            catch { }
            return list;
        }

        public static List<ComboLookupItem> GetSubRubrosCatalogo()
        {
            var list = new List<ComboLookupItem> { new ComboLookupItem { Id = 0, Nombre = "" } };
            try
            {
                using (var c = new SqlConnection(_connectionString))
                {
                    c.Open();
                    AsegurarMigracionLite(c);
                    using (var cmd = new SqlCommand("SELECT SubRubroID, Nombre FROM dbo.SubRubros ORDER BY Nombre", c))
                    using (var rd = cmd.ExecuteReader())
                    {
                        while (rd.Read())
                            list.Add(new ComboLookupItem { Id = Convert.ToInt32(rd["SubRubroID"]), Nombre = rd["Nombre"]?.ToString() ?? "" });
                    }
                }
            }
            catch { }
            return list;
        }

        public static List<ComboLookupItem> GetProveedoresCatalogo()
        {
            var list = new List<ComboLookupItem> { new ComboLookupItem { Id = 0, Nombre = "" } };
            try
            {
                using (var c = new SqlConnection(_connectionString))
                {
                    c.Open();
                    using (var cmd = new SqlCommand("SELECT ProveedorID, RazonSocial FROM dbo.Proveedores ORDER BY RazonSocial", c))
                    using (var rd = cmd.ExecuteReader())
                    {
                        while (rd.Read())
                        {
                            string nom = rd["RazonSocial"]?.ToString() ?? "";
                            if (!string.IsNullOrWhiteSpace(nom))
                                list.Add(new ComboLookupItem { Id = Convert.ToInt32(rd["ProveedorID"]), Nombre = nom });
                        }
                    }
                }
            }
            catch { }
            return list;
        }

        public static int InsertCategoria(string nombre)
        {
            nombre = (nombre ?? "").Trim();
            if (string.IsNullOrEmpty(nombre)) return 0;
            try
            {
                using (var c = new SqlConnection(_connectionString))
                {
                    c.Open();
                    AsegurarMigracionLite(c);
                    using (var q = new SqlCommand("SELECT CategoriaID FROM dbo.Categorias WHERE Nombre=@n", c))
                    {
                        q.Parameters.AddWithValue("@n", nombre);
                        object o = q.ExecuteScalar();
                        if (o != null && o != DBNull.Value) return Convert.ToInt32(o);
                    }
                    using (var ins = new SqlCommand("INSERT INTO dbo.Categorias (Nombre) OUTPUT INSERTED.CategoriaID VALUES (@n)", c))
                    {
                        ins.Parameters.AddWithValue("@n", nombre);
                        object id = ins.ExecuteScalar();
                        return id != null ? Convert.ToInt32(id) : 0;
                    }
                }
            }
            catch { return 0; }
        }

        public static int InsertSubRubro(string nombre)
        {
            nombre = (nombre ?? "").Trim();
            if (string.IsNullOrEmpty(nombre)) return 0;
            try
            {
                using (var c = new SqlConnection(_connectionString))
                {
                    c.Open();
                    AsegurarMigracionLite(c);
                    using (var q = new SqlCommand("SELECT SubRubroID FROM dbo.SubRubros WHERE Nombre=@n", c))
                    {
                        q.Parameters.AddWithValue("@n", nombre);
                        object o = q.ExecuteScalar();
                        if (o != null && o != DBNull.Value) return Convert.ToInt32(o);
                    }
                    using (var ins = new SqlCommand("INSERT INTO dbo.SubRubros (Nombre) OUTPUT INSERTED.SubRubroID VALUES (@n)", c))
                    {
                        ins.Parameters.AddWithValue("@n", nombre);
                        object id = ins.ExecuteScalar();
                        return id != null ? Convert.ToInt32(id) : 0;
                    }
                }
            }
            catch { return 0; }
        }

        public static int InsertProveedorNombre(string razonSocial)
        {
            razonSocial = (razonSocial ?? "").Trim();
            if (string.IsNullOrEmpty(razonSocial)) return 0;
            try
            {
                using (var c = new SqlConnection(_connectionString))
                {
                    c.Open();
                    using (var ins = new SqlCommand(
                        @"INSERT INTO dbo.Proveedores (CUIT,RazonSocial,Telefono,Email,Direccion,CategoriaFiscal,PersonaContacto,PaginaWeb,SaldoDeuda)
                          OUTPUT INSERTED.ProveedorID
                          VALUES (N'',@r,N'',N'',N'',N'',N'',N'',0)", c))
                    {
                        ins.Parameters.AddWithValue("@r", razonSocial);
                        object id = ins.ExecuteScalar();
                        return id != null ? Convert.ToInt32(id) : 0;
                    }
                }
            }
            catch { return 0; }
        }

        // Productos
        public static DataTable GetProductos(string filtro = "", bool incluirInactivos = false)
        {
            var dt = new DataTable();
            try
            {
                using (var c = new SqlConnection(_connectionString))
                {
                    c.Open();
                    AsegurarMigracionLite(c);
                    string activoWhere = incluirInactivos ? "" : "ISNULL(Activo,1)=1";
                    string filtroWhere = string.IsNullOrWhiteSpace(filtro)
                        ? ""
                        : "(Descripcion LIKE @f OR Codigo LIKE @f OR CodigoBarra LIKE @f)";
                    string where = "";
                    if (!string.IsNullOrEmpty(activoWhere) && !string.IsNullOrEmpty(filtroWhere))
                        where = " WHERE " + activoWhere + " AND " + filtroWhere;
                    else if (!string.IsNullOrEmpty(activoWhere))
                        where = " WHERE " + activoWhere;
                    else if (!string.IsNullOrEmpty(filtroWhere))
                        where = " WHERE " + filtroWhere;
                    string sql = "SELECT * FROM Productos" + where + " ORDER BY Descripcion";
                    var da = new SqlDataAdapter(sql, c);
                    if (!string.IsNullOrWhiteSpace(filtro))
                        da.SelectCommand.Parameters.AddWithValue("@f", "%" + filtro + "%");
                    da.Fill(dt);
                }
            }
            catch { }
            return dt;
        }

        /// <summary>Listado para grillas (ProductosControl) con rubro/sub-rubro/proveedor resueltos.</summary>
        public static DataTable GetProductosListado(string filtro = "", bool incluirInactivos = false)
        {
            var dt = new DataTable();
            try
            {
                using (var c = new SqlConnection(_connectionString))
                {
                    c.Open();
                    AsegurarMigracionLite(c);
                    string filtroWhere = string.IsNullOrWhiteSpace(filtro)
                        ? ""
                        : @"(
  p.Descripcion LIKE @f OR p.Codigo LIKE @f OR p.CodigoBarra LIKE @f
  OR p.Categoria LIKE @f OR p.SubRubro LIKE @f OR p.Marca LIKE @f OR p.Proveedor LIKE @f
  OR cat.Nombre LIKE @f OR sr.Nombre LIKE @f OR prov.RazonSocial LIKE @f
)";
                    string activoWhere = incluirInactivos ? "" : "ISNULL(p.Activo,1)=1";
                    string where = "";
                    if (!string.IsNullOrEmpty(activoWhere) && !string.IsNullOrEmpty(filtroWhere))
                        where = " WHERE " + activoWhere + " AND " + filtroWhere;
                    else if (!string.IsNullOrEmpty(activoWhere))
                        where = " WHERE " + activoWhere;
                    else if (!string.IsNullOrEmpty(filtroWhere))
                        where = " WHERE " + filtroWhere;
                    string sql = $@"
SELECT
  p.ProductoID,
  p.Codigo,
  p.CodigoBarra,
  ISNULL(p.Descripcion, N'') AS Descripcion,
  COALESCE(NULLIF(LTRIM(RTRIM(ISNULL(cat.Nombre, N''))), N''), NULLIF(LTRIM(RTRIM(ISNULL(p.Categoria, N''))), N''), N'') AS Categoria,
  COALESCE(NULLIF(LTRIM(RTRIM(ISNULL(sr.Nombre, N''))), N''), NULLIF(LTRIM(RTRIM(ISNULL(p.SubRubro, N''))), N''), N'') AS SubRubro,
  LTRIM(RTRIM(ISNULL(p.Marca, N''))) AS Marca,
  COALESCE(NULLIF(LTRIM(RTRIM(ISNULL(prov.RazonSocial, N''))), N''), NULLIF(LTRIM(RTRIM(ISNULL(p.Proveedor, N''))), N''), N'') AS Proveedor,
  p.PrecioVenta,
  p.StockActual,
  ISNULL(p.Activo, 1) AS Activo
FROM dbo.Productos p
LEFT JOIN dbo.Categorias cat
  ON NULLIF(LTRIM(RTRIM(ISNULL(p.Categoria, N''))), N'') <> N''
 AND LTRIM(RTRIM(ISNULL(cat.Nombre, N''))) = LTRIM(RTRIM(ISNULL(p.Categoria, N'')))
LEFT JOIN dbo.SubRubros sr
  ON NULLIF(LTRIM(RTRIM(ISNULL(p.SubRubro, N''))), N'') <> N''
 AND LTRIM(RTRIM(ISNULL(sr.Nombre, N''))) = LTRIM(RTRIM(ISNULL(p.SubRubro, N'')))
LEFT JOIN dbo.Proveedores prov
  ON NULLIF(LTRIM(RTRIM(ISNULL(p.Proveedor, N''))), N'') <> N''
 AND LTRIM(RTRIM(ISNULL(prov.RazonSocial, N''))) = LTRIM(RTRIM(ISNULL(p.Proveedor, N'')))
{where}
ORDER BY p.Descripcion";
                    var da = new SqlDataAdapter(sql, c);
                    if (!string.IsNullOrWhiteSpace(filtro))
                        da.SelectCommand.Parameters.AddWithValue("@f", "%" + filtro + "%");
                    da.Fill(dt);
                }
            }
            catch
            {
                return GetProductos(filtro);
            }

            return dt;
        }

        public static int GuardarProducto(int id, string cod, string cb, string desc, string cat, string subRubro, string marca, string proveedor, string iva,
            decimal costo, decimal gan, decimal imp, decimal venta, int stock, string img,
            string tipoMoneda, bool permiteModPrecio, bool esStockeable, bool aceptaStockNeg,
            bool usaVariantes, bool esCombo, decimal? stockMinimo, decimal? stockIdeal,
            string codigoExterno, string varianteColor, string varianteTalle, string varianteUnidadMedida,
            bool cobraIvaAlCliente = true, bool costoIncluyeIva = false, bool activo = true)
        {
            try
            {
                using (var c = new SqlConnection(_connectionString))
                {
                    c.Open();
                    AsegurarMigracionLite(c);
                    return GuardarProductoEnConexion(c, null, id, cod, cb, desc, cat, subRubro, marca, proveedor, iva,
                        costo, gan, imp, venta, stock, img, tipoMoneda, permiteModPrecio, esStockeable, aceptaStockNeg,
                        usaVariantes, esCombo, stockMinimo, stockIdeal, codigoExterno, varianteColor, varianteTalle,
                        varianteUnidadMedida, cobraIvaAlCliente, costoIncluyeIva, activo, true);
                }
            }
            catch (Exception ex) { NotificarError(ex.Message); return 0; }
        }

        private static int GuardarProductoEnConexion(SqlConnection c, SqlTransaction tr, int id, string cod, string cb, string desc, string cat,
            string subRubro, string marca, string proveedor, string iva, decimal costo, decimal gan, decimal imp, decimal venta, int stock, string img,
            string tipoMoneda, bool permiteModPrecio, bool esStockeable, bool aceptaStockNeg, bool usaVariantes, bool esCombo,
            decimal? stockMinimo, decimal? stockIdeal, string codigoExterno, string varianteColor, string varianteTalle, string varianteUnidadMedida,
            bool cobraIvaAlCliente, bool costoIncluyeIva, bool activo, bool asignarListasSiEsNuevo)
        {
            int? sm = stockMinimo.HasValue ? (int?)Convert.ToInt32(stockMinimo.Value) : null;
            int? si = stockIdeal.HasValue ? (int?)Convert.ToInt32(stockIdeal.Value) : null;
            bool esNuevo = id == 0;
            int pid = id;

            string sql = esNuevo
                ? @"
INSERT INTO Productos
    (Codigo, CodigoBarra, Descripcion, Categoria, SubRubro, Marca, Proveedor, TipoIVA,
     PrecioCosto, Ganancia, ImpuestoInterno, PrecioVenta, StockActual, ImagenPath,
     UsaVariantes, EsCombo, StockMinimo, StockIdeal, TipoMoneda, PermiteModificarPrecioVenta,
     EsStockeable, AceptaStockNegativo, CodigoExterno, VarianteColor, VarianteTalle,
     VarianteUnidadMedida, CobraIvaAlCliente, CostoIncluyeIva, Activo, FechaModificacion)
VALUES
    (@c, @cb, @d, @cat, @sr, @mar, @prov, @iva,
     @pc, @g, @ii, @pv, @s, @img,
     @uv, @ec, @sm, @si, @tm, @pmp,
     @es, @asn, @ce, @vc, @vt,
     @vu, @civa, @cii, @act, GETDATE());
SELECT CAST(SCOPE_IDENTITY() AS INT);"
                : @"
UPDATE Productos SET
    Codigo=@c, CodigoBarra=@cb, Descripcion=@d, Categoria=@cat, SubRubro=@sr, Marca=@mar, Proveedor=@prov,
    TipoIVA=@iva, PrecioCosto=@pc, Ganancia=@g, ImpuestoInterno=@ii, PrecioVenta=@pv, StockActual=@s, ImagenPath=@img,
    UsaVariantes=@uv, EsCombo=@ec, StockMinimo=@sm, StockIdeal=@si, TipoMoneda=@tm,
    PermiteModificarPrecioVenta=@pmp, EsStockeable=@es, AceptaStockNegativo=@asn,
    CodigoExterno=@ce, VarianteColor=@vc, VarianteTalle=@vt, VarianteUnidadMedida=@vu,
    CobraIvaAlCliente=@civa, CostoIncluyeIva=@cii, Activo=@act, FechaModificacion=GETDATE()
WHERE ProductoID=@id";

            using (var cmd = new SqlCommand(sql, c, tr))
            {
                AgregarParametrosProductoBase(cmd, cod, cb, desc, cat, subRubro, marca, proveedor, iva, costo, gan, imp, venta, stock, img);
                cmd.Parameters.AddWithValue("@uv", usaVariantes);
                cmd.Parameters.AddWithValue("@ec", esCombo);
                cmd.Parameters.AddWithValue("@sm", (object)sm ?? DBNull.Value);
                cmd.Parameters.AddWithValue("@si", (object)si ?? DBNull.Value);
                cmd.Parameters.AddWithValue("@tm", string.IsNullOrWhiteSpace(tipoMoneda) ? "ARS" : tipoMoneda.Trim());
                cmd.Parameters.AddWithValue("@pmp", permiteModPrecio);
                cmd.Parameters.AddWithValue("@es", esStockeable);
                cmd.Parameters.AddWithValue("@asn", aceptaStockNeg);
                cmd.Parameters.AddWithValue("@ce", string.IsNullOrWhiteSpace(codigoExterno) ? (object)DBNull.Value : codigoExterno.Trim());
                cmd.Parameters.AddWithValue("@vc", string.IsNullOrWhiteSpace(varianteColor) ? (object)DBNull.Value : varianteColor.Trim());
                cmd.Parameters.AddWithValue("@vt", string.IsNullOrWhiteSpace(varianteTalle) ? (object)DBNull.Value : varianteTalle.Trim());
                cmd.Parameters.AddWithValue("@vu", string.IsNullOrWhiteSpace(varianteUnidadMedida) ? (object)DBNull.Value : varianteUnidadMedida.Trim());
                cmd.Parameters.AddWithValue("@civa", cobraIvaAlCliente);
                cmd.Parameters.AddWithValue("@cii", costoIncluyeIva);
                cmd.Parameters.AddWithValue("@act", activo);
                cmd.Parameters.AddWithValue("@id", id);

                if (esNuevo)
                    pid = Convert.ToInt32(cmd.ExecuteScalar());
                else if (cmd.ExecuteNonQuery() == 0)
                    return 0;
            }

            if (esNuevo && asignarListasSiEsNuevo)
                AsignarTodasListasPrecioAProductoEnConexion(c, tr, pid);

            return pid;
        }

        private static void AsignarTodasListasPrecioAProductoEnConexion(SqlConnection c, SqlTransaction tr, int productoId)
        {
            using (var cmd = new SqlCommand(@"
INSERT INTO ProductosListas (ProductoID, ListaID, PrecioFijo)
SELECT @pid, lp.ListaID, NULL
FROM ListasPrecios lp
WHERE NOT EXISTS (
    SELECT 1 FROM ProductosListas pl WHERE pl.ProductoID=@pid AND pl.ListaID=lp.ListaID
)", c, tr))
            {
                cmd.Parameters.AddWithValue("@pid", productoId);
                cmd.ExecuteNonQuery();
            }
        }

        /// <summary>Costo neto sin IVA a partir del valor ingresado y el flag CON/SIN IVA (para lógica interna / margen).</summary>
        public static decimal ObtenerCostoNetoSinIva(decimal precioCosto, bool costoIncluyeIva, decimal ivaPct)
        {
            if (precioCosto <= 0) return 0;
            if (!costoIncluyeIva || ivaPct <= 0) return precioCosto;
            return Math.Round(precioCosto / (1 + ivaPct / 100m), 2, MidpointRounding.AwayFromZero);
        }

        /// <summary>
        /// Costo de compra final mostrado al usuario.
        /// Sin IVA en el costo: costo + IVA + impuesto interno.
        /// Con IVA en el costo: costo ingresado + impuesto interno (no se descompone el IVA en pantalla).
        /// </summary>
        public static decimal CalcularCostoCompraFinal(decimal precioCosto, bool costoIncluyeIva, decimal ivaPct, decimal impuestoInterno)
        {
            if (precioCosto <= 0)
                return Math.Round(impuestoInterno, 2, MidpointRounding.AwayFromZero);

            decimal costoConIva;
            if (costoIncluyeIva)
            {
                costoConIva = precioCosto;
            }
            else
            {
                decimal montoIva = ivaPct > 0
                    ? Math.Round(precioCosto * ivaPct / 100m, 2, MidpointRounding.AwayFromZero)
                    : 0m;
                costoConIva = precioCosto + montoIva;
            }

            return Math.Round(costoConIva + impuestoInterno, 2, MidpointRounding.AwayFromZero);
        }

        public static decimal ParseIvaPct(string tipoIva)
        {
            if (string.IsNullOrWhiteSpace(tipoIva)) return 21m;
            string s = tipoIva.Replace("%", "").Trim();
            return decimal.TryParse(s, NumberStyles.Any, CultureInfo.InvariantCulture, out decimal d) ? d : 21m;
        }

        // --- Listas de precios ---
        public static class TiposListaPrecio
        {
            public const string SobreCosto = "SobreCosto";
            public const string PrecioFijo = "PrecioFijo";
            public const string ListaRelacionada = "ListaRelacionada";
        }

        public static class TiposRedondeoLista
        {
            public const string Sin = "Sin";
            public const string Entero = "Entero";
            public const string Decena = "Decena";
            public const string Centena = "Centena";
            public const string MedioPeso = "MedioPeso";
            public const string Termina99 = "Termina99";
        }

        public static decimal ObtenerCostoCompraDeProducto(DataRow producto)
        {
            if (producto == null) return 0m;
            decimal costo = producto["PrecioCosto"] != DBNull.Value ? Convert.ToDecimal(producto["PrecioCosto"]) : 0m;
            decimal imp = producto.Table.Columns.Contains("ImpuestoInterno") && producto["ImpuestoInterno"] != DBNull.Value
                ? Convert.ToDecimal(producto["ImpuestoInterno"]) : 0m;
            bool conIva = producto.Table.Columns.Contains("CostoIncluyeIva") && producto["CostoIncluyeIva"] != DBNull.Value
                && Convert.ToBoolean(producto["CostoIncluyeIva"]);
            string iva = producto["TipoIVA"]?.ToString() ?? "21";
            return CalcularCostoCompraFinal(costo, conIva, ParseIvaPct(iva), imp);
        }

        public static decimal AplicarRedondeoLista(decimal precio, string tipoRedondeo)
        {
            if (precio <= 0) return 0m;
            string t = string.IsNullOrWhiteSpace(tipoRedondeo) ? TiposRedondeoLista.Sin : tipoRedondeo.Trim();
            switch (t)
            {
                case TiposRedondeoLista.Entero:
                    return Math.Round(precio, 0, MidpointRounding.AwayFromZero);
                case TiposRedondeoLista.Decena:
                    return Math.Round(precio / 10m, 0, MidpointRounding.AwayFromZero) * 10m;
                case TiposRedondeoLista.Centena:
                    return Math.Round(precio / 100m, 0, MidpointRounding.AwayFromZero) * 100m;
                case TiposRedondeoLista.MedioPeso:
                    return Math.Round(precio * 2m, 0, MidpointRounding.AwayFromZero) / 2m;
                case TiposRedondeoLista.Termina99:
                    return Math.Floor(precio) + 0.99m;
                default:
                    return Math.Round(precio, 2, MidpointRounding.AwayFromZero);
            }
        }

        public static string ObtenerTipoLista(DataRow lista)
        {
            if (lista == null) return TiposListaPrecio.SobreCosto;
            if (!lista.Table.Columns.Contains("TipoLista") || lista["TipoLista"] == DBNull.Value)
                return TiposListaPrecio.SobreCosto;
            string t = lista["TipoLista"].ToString();
            return string.IsNullOrWhiteSpace(t) ? TiposListaPrecio.SobreCosto : t;
        }

        public static string ObtenerTipoRedondeoLista(DataRow lista)
        {
            if (lista == null) return TiposRedondeoLista.Sin;
            if (!lista.Table.Columns.Contains("TipoRedondeo") || lista["TipoRedondeo"] == DBNull.Value)
                return TiposRedondeoLista.Sin;
            return lista["TipoRedondeo"].ToString();
        }

        public static DataRow GetListaPrecioRow(int listaId)
        {
            var dt = GetListasPrecios();
            if (dt == null || dt.Rows.Count == 0) return null;
            var rows = dt.Select($"ListaID={listaId}");
            return rows.Length > 0 ? rows[0] : null;
        }

        public static decimal? GetPrecioFijoProductoLista(int productoId, int listaId)
        {
            try
            {
                using (var c = new SqlConnection(_connectionString))
                {
                    c.Open();
                    AsegurarMigracionLite(c);
                    using (var cmd = new SqlCommand(
                        "SELECT PrecioFijo FROM ProductosListas WHERE ProductoID=@pid AND ListaID=@lid", c))
                    {
                        cmd.Parameters.AddWithValue("@pid", productoId);
                        cmd.Parameters.AddWithValue("@lid", listaId);
                        var r = cmd.ExecuteScalar();
                        if (r == null || r == DBNull.Value) return null;
                        return Convert.ToDecimal(r);
                    }
                }
            }
            catch { return null; }
        }

        public static decimal CalcularPrecioLista(DataRow producto, DataRow lista, decimal? precioFijoProducto = null, HashSet<int> visitados = null)
        {
            if (producto == null || lista == null) return 0m;

            int listaId = Convert.ToInt32(lista["ListaID"]);
            visitados = visitados ?? new HashSet<int>();
            if (!visitados.Add(listaId)) return 0m;

            string tipo = ObtenerTipoLista(lista);
            decimal porcentaje = lista["Porcentaje"] != DBNull.Value ? Convert.ToDecimal(lista["Porcentaje"]) : 0m;
            string redondeo = ObtenerTipoRedondeoLista(lista);
            decimal precio;

            switch (tipo)
            {
                case TiposListaPrecio.PrecioFijo:
                    if (precioFijoProducto.HasValue && precioFijoProducto.Value > 0)
                        precio = precioFijoProducto.Value;
                    else if (producto["PrecioVenta"] != DBNull.Value)
                        precio = Convert.ToDecimal(producto["PrecioVenta"]);
                    else
                        precio = 0m;
                    break;

                case TiposListaPrecio.ListaRelacionada:
                    int? parentId = lista.Table.Columns.Contains("ListaRelacionadaID") && lista["ListaRelacionadaID"] != DBNull.Value
                        ? (int?)Convert.ToInt32(lista["ListaRelacionadaID"]) : null;
                    if (!parentId.HasValue || parentId.Value <= 0)
                    {
                        precio = ObtenerCostoCompraDeProducto(producto) * (1 + porcentaje / 100m);
                    }
                    else
                    {
                        var parentLista = GetListaPrecioRow(parentId.Value);
                        decimal parentPrice = parentLista != null
                            ? CalcularPrecioLista(producto, parentLista, precioFijoProducto, visitados)
                            : ObtenerCostoCompraDeProducto(producto);
                        precio = parentPrice * (1 + porcentaje / 100m);
                    }
                    break;

                default:
                    precio = ObtenerCostoCompraDeProducto(producto) * (1 + porcentaje / 100m);
                    break;
            }

            return AplicarRedondeoLista(precio, redondeo);
        }

        public static decimal CalcularPrecioListaPorIds(int productoId, int listaId)
        {
            var dt = GetProductos("");
            if (dt == null) return 0m;
            var prodRows = dt.Select($"ProductoID={productoId}");
            if (prodRows.Length == 0) return 0m;
            var lista = GetListaPrecioRow(listaId);
            if (lista == null) return 0m;
            decimal? precioFijo = GetPrecioFijoProductoLista(productoId, listaId);
            return CalcularPrecioLista(prodRows[0], lista, precioFijo);
        }

        private static DataRow ObtenerFilaProducto(SqlConnection conexion, SqlTransaction transaccion, int productoId)
        {
            if (conexion == null) return null;
            var dt = new DataTable();
            var cmd = new SqlCommand("SELECT * FROM Productos WHERE ProductoID=@id", conexion, transaccion);
            cmd.Parameters.AddWithValue("@id", productoId);
            new SqlDataAdapter(cmd).Fill(dt);
            return dt.Rows.Count > 0 ? dt.Rows[0] : null;
        }

        private static Dictionary<int, decimal?> ObtenerProductoListasDetalleEnConexion(SqlConnection conexion, SqlTransaction transaccion, int productoId)
        {
            var map = new Dictionary<int, decimal?>();
            if (conexion == null) return map;
            var dt = new DataTable();
            var cmd = new SqlCommand("SELECT ListaID, PrecioFijo FROM ProductosListas WHERE ProductoID=@pid", conexion, transaccion);
            cmd.Parameters.AddWithValue("@pid", productoId);
            new SqlDataAdapter(cmd).Fill(dt);
            foreach (DataRow r in dt.Rows)
            {
                int lid = Convert.ToInt32(r["ListaID"]);
                decimal? pf = r["PrecioFijo"] != DBNull.Value ? (decimal?)Convert.ToDecimal(r["PrecioFijo"]) : null;
                map[lid] = pf;
            }
            return map;
        }

        private static int? ResolverListaPrecioReferencia(Dictionary<int, decimal?> asignaciones, DataTable todasListas, int? posListaPrecioId)
        {
            if (asignaciones == null || asignaciones.Count == 0 || todasListas == null) return null;

            if (posListaPrecioId.HasValue && posListaPrecioId.Value > 0 && asignaciones.ContainsKey(posListaPrecioId.Value))
                return posListaPrecioId.Value;

            foreach (DataRow lista in todasListas.Rows)
            {
                int lid = Convert.ToInt32(lista["ListaID"]);
                if (!asignaciones.ContainsKey(lid)) continue;
                if (ObtenerTipoLista(lista) != TiposListaPrecio.PrecioFijo)
                    return lid;
            }

            foreach (DataRow lista in todasListas.Rows)
            {
                int lid = Convert.ToInt32(lista["ListaID"]);
                if (asignaciones.ContainsKey(lid))
                    return lid;
            }

            return null;
        }

        private static decimal CalcularPrecioVentaReferenciaProducto(
            DataRow producto,
            Dictionary<int, decimal?> asignaciones,
            DataTable todasListas,
            int? listaReferenciaId,
            decimal ivaPct)
        {
            if (producto == null) return 0m;

            if (listaReferenciaId.HasValue && todasListas != null)
            {
                var rows = todasListas.Select($"ListaID={listaReferenciaId.Value}");
                if (rows.Length > 0)
                {
                    decimal? pf = asignaciones != null && asignaciones.TryGetValue(listaReferenciaId.Value, out var v) ? v : null;
                    return CalcularPrecioLista(producto, rows[0], pf);
                }
            }

            decimal costo = producto["PrecioCosto"] != DBNull.Value ? Convert.ToDecimal(producto["PrecioCosto"]) : 0m;
            bool incluyeIva = producto.Table.Columns.Contains("CostoIncluyeIva") && producto["CostoIncluyeIva"] != DBNull.Value
                && Convert.ToBoolean(producto["CostoIncluyeIva"]);
            decimal imp = producto.Table.Columns.Contains("ImpuestoInterno") && producto["ImpuestoInterno"] != DBNull.Value
                ? Convert.ToDecimal(producto["ImpuestoInterno"]) : 0m;
            decimal costoFinal = CalcularCostoCompraFinal(costo, incluyeIva, ivaPct, imp);

            bool cobraIva = !producto.Table.Columns.Contains("CobraIvaAlCliente")
                || producto["CobraIvaAlCliente"] == DBNull.Value
                || Convert.ToBoolean(producto["CobraIvaAlCliente"]);
            if (cobraIva && ivaPct > 0)
                return Math.Round(costoFinal * (1 + ivaPct / 100m), 2, MidpointRounding.AwayFromZero);
            return costoFinal;
        }

        /// <summary>
        /// Recalcula precios de venta según listas asignadas al producto y persiste PrecioCosto + PrecioVenta.
        /// Omite listas PrecioFijo (manuales). CalcularPrecioLista maneja ciclos en listas relacionadas.
        /// </summary>
        public static decimal ActualizarPreciosVentaPorCambioDeCosto(
            int productoId,
            decimal nuevoCostoCompra,
            bool costoIncluyeIva,
            decimal ivaPct,
            decimal impuestoInterno,
            SqlConnection conexion = null,
            SqlTransaction transaccion = null)
        {
            bool propiaConexion = conexion == null;
            try
            {
                if (propiaConexion)
                {
                    conexion = new SqlConnection(_connectionString);
                    conexion.Open();
                    AsegurarMigracionLite(conexion);
                }

                var producto = ObtenerFilaProducto(conexion, transaccion, productoId);
                if (producto == null) return 0m;

                producto["PrecioCosto"] = nuevoCostoCompra;
                if (producto.Table.Columns.Contains("CostoIncluyeIva"))
                    producto["CostoIncluyeIva"] = costoIncluyeIva;
                if (producto.Table.Columns.Contains("ImpuestoInterno"))
                    producto["ImpuestoInterno"] = impuestoInterno;
                if (producto.Table.Columns.Contains("TipoIVA"))
                    producto["TipoIVA"] = ivaPct.ToString(CultureInfo.InvariantCulture);

                var asignaciones = ObtenerProductoListasDetalleEnConexion(conexion, transaccion, productoId);
                var dtListas = new DataTable();
                new SqlDataAdapter(new SqlCommand("SELECT * FROM ListasPrecios ORDER BY Nombre", conexion, transaccion)).Fill(dtListas);

                foreach (DataRow lista in dtListas.Rows)
                {
                    int listaId = Convert.ToInt32(lista["ListaID"]);
                    if (!asignaciones.ContainsKey(listaId)) continue;
                    if (ObtenerTipoLista(lista) == TiposListaPrecio.PrecioFijo) continue;

                    decimal? pf = asignaciones[listaId];
                    CalcularPrecioLista(producto, lista, pf);
                }

                int? posListaId = null;
                var cfgDt = new DataTable();
                new SqlDataAdapter(new SqlCommand("SELECT TOP 1 PosListaPrecioID FROM Configuracion", conexion, transaccion)).Fill(cfgDt);
                if (cfgDt.Rows.Count > 0 && cfgDt.Rows[0]["PosListaPrecioID"] != DBNull.Value)
                    posListaId = Convert.ToInt32(cfgDt.Rows[0]["PosListaPrecioID"]);

                int? listaRef = ResolverListaPrecioReferencia(asignaciones, dtListas, posListaId);
                decimal precioVenta = CalcularPrecioVentaReferenciaProducto(producto, asignaciones, dtListas, listaRef, ivaPct);

                using (var cmd = new SqlCommand(@"
UPDATE Productos SET PrecioCosto=@pc, PrecioVenta=@pv, ImpuestoInterno=@ii, TipoIVA=@iva,
CostoIncluyeIva=@cii, FechaModificacion=GETDATE() WHERE ProductoID=@id", conexion, transaccion))
                {
                    cmd.Parameters.AddWithValue("@pc", nuevoCostoCompra);
                    cmd.Parameters.AddWithValue("@pv", precioVenta);
                    cmd.Parameters.AddWithValue("@ii", impuestoInterno);
                    cmd.Parameters.AddWithValue("@iva", ivaPct.ToString(CultureInfo.InvariantCulture));
                    cmd.Parameters.AddWithValue("@cii", costoIncluyeIva);
                    cmd.Parameters.AddWithValue("@id", productoId);
                    cmd.ExecuteNonQuery();
                }

                return precioVenta;
            }
            catch (Exception ex)
            {
                NotificarError("ActualizarPreciosVentaPorCambioDeCosto: " + ex.Message);
                return 0m;
            }
            finally
            {
                if (propiaConexion && conexion != null)
                    conexion.Dispose();
            }
        }

        public static string EtiquetaTipoLista(string tipo)
        {
            switch (tipo)
            {
                case TiposListaPrecio.PrecioFijo: return "Precio Fijo";
                case TiposListaPrecio.ListaRelacionada: return "Lista Relacionada";
                default: return "Sobre Costo";
            }
        }

        public static string EtiquetaTipoRedondeo(string tipo)
        {
            switch (tipo)
            {
                case TiposRedondeoLista.Entero: return "Sin decimales";
                case TiposRedondeoLista.Decena: return "A la decena";
                case TiposRedondeoLista.Centena: return "A la centena";
                case TiposRedondeoLista.MedioPeso: return "A $0,50";
                case TiposRedondeoLista.Termina99: return "Termina en ,99";
                default: return "Sin redondeo especial";
            }
        }

        private static void AgregarParametrosProductoBase(SqlCommand cmd, string cod, string cb, string desc, string cat,
            string subRubro, string marca, string proveedor, string iva, decimal costo, decimal gan, decimal imp, decimal venta, int stock, string img)
        {
            cmd.Parameters.AddWithValue("@c", cod ?? "");
            cmd.Parameters.AddWithValue("@cb", cb ?? "");
            cmd.Parameters.AddWithValue("@d", desc ?? "");
            cmd.Parameters.AddWithValue("@cat", cat ?? "");
            cmd.Parameters.AddWithValue("@sr", subRubro ?? "");
            cmd.Parameters.AddWithValue("@mar", marca ?? "");
            cmd.Parameters.AddWithValue("@prov", proveedor ?? "");
            cmd.Parameters.AddWithValue("@iva", iva ?? "21");
            cmd.Parameters.AddWithValue("@pc", costo);
            cmd.Parameters.AddWithValue("@g", gan);
            cmd.Parameters.AddWithValue("@ii", imp);
            cmd.Parameters.AddWithValue("@pv", venta);
            cmd.Parameters.AddWithValue("@s", stock);
            cmd.Parameters.AddWithValue("@img", img ?? "");
        }

        public static bool GuardarProducto(int id, string cod, string cb, string desc, string cat, string subRubro, string marca, string proveedor, string iva, decimal costo, decimal gan, decimal imp, decimal venta, int stock, string img)
            => GuardarProducto(id, cod, cb, desc, cat, subRubro, marca, proveedor, iva, costo, gan, imp, venta, stock, img,
                "ARS", true, true, false, false, false, null, null, null, null, null, null) > 0;

        public static bool GuardarProducto(int id, string cod, string cb, string desc, string cat, string iva, decimal costo, decimal gan, decimal imp, decimal venta, int stock, string img)
            => GuardarProducto(id, cod, cb, desc, cat, "", "", "", iva, costo, gan, imp, venta, stock, img);

        public static bool ExisteProductoDuplicado(int productoIdExcluir, string codigo, string codigoBarra, out string mensaje)
        {
            mensaje = "";
            try
            {
                using (var c = new SqlConnection(_connectionString))
                {
                    c.Open();
                    AsegurarMigracionLite(c);
                    return ExisteProductoDuplicadoEnConexion(c, null, productoIdExcluir, codigo, codigoBarra, out mensaje);
                }
            }
            catch (Exception ex)
            {
                mensaje = ex.Message;
                return true;
            }
        }

        private static bool ExisteProductoDuplicadoEnConexion(SqlConnection c, SqlTransaction tr, int productoIdExcluir, string codigo, string codigoBarra, out string mensaje)
        {
            mensaje = "";
            var conflictos = new List<string>();
            string cod = (codigo ?? "").Trim();
            string cb = (codigoBarra ?? "").Trim();

            if (!string.IsNullOrWhiteSpace(cod))
            {
                using (var cmd = new SqlCommand(@"
SELECT TOP 1 ProductoID, Codigo, Descripcion
FROM Productos
WHERE ProductoID<>@id AND LTRIM(RTRIM(ISNULL(Codigo,N'')))=@cod", c, tr))
                {
                    cmd.Parameters.AddWithValue("@id", productoIdExcluir);
                    cmd.Parameters.AddWithValue("@cod", cod);
                    using (var rd = cmd.ExecuteReader())
                    {
                        if (rd.Read())
                            conflictos.Add($"Codigo '{cod}' ya existe en ProductoID {rd["ProductoID"]} - {rd["Descripcion"]}");
                    }
                }
            }

            if (!string.IsNullOrWhiteSpace(cb))
            {
                using (var cmd = new SqlCommand(@"
SELECT TOP 1 ProductoID, CodigoBarra, Descripcion
FROM Productos
WHERE ProductoID<>@id AND LTRIM(RTRIM(ISNULL(CodigoBarra,N'')))=@cb", c, tr))
                {
                    cmd.Parameters.AddWithValue("@id", productoIdExcluir);
                    cmd.Parameters.AddWithValue("@cb", cb);
                    using (var rd = cmd.ExecuteReader())
                    {
                        if (rd.Read())
                            conflictos.Add($"CodigoBarra '{cb}' ya existe en ProductoID {rd["ProductoID"]} - {rd["Descripcion"]}");
                    }
                }
            }

            mensaje = string.Join(Environment.NewLine, conflictos);
            return conflictos.Count > 0;
        }

        public static bool CambiarActivoProducto(int id, bool activo)
        {
            try
            {
                using (var c = new SqlConnection(_connectionString))
                {
                    c.Open();
                    AsegurarMigracionLite(c);
                    using (var cmd = new SqlCommand("UPDATE Productos SET Activo=@act, FechaModificacion=GETDATE() WHERE ProductoID=@id", c))
                    {
                        cmd.Parameters.AddWithValue("@act", activo);
                        cmd.Parameters.AddWithValue("@id", id);
                        return cmd.ExecuteNonQuery() > 0;
                    }
                }
            }
            catch { return false; }
        }

        public static bool DeshabilitarProducto(int id) => CambiarActivoProducto(id, false);
        public static bool RehabilitarProducto(int id) => CambiarActivoProducto(id, true);

        public static bool EliminarProducto(int id)
        {
            // Borrado físico solo para usuario técnico (el cliente usa DeshabilitarProducto).
            if (!SesionUsuario.EsUsuarioTecnico)
            {
                NotificarError("La eliminación definitiva solo está disponible para el usuario técnico.");
                return false;
            }
            try
            {
                using (var c = new SqlConnection(_connectionString))
                {
                    c.Open();
                    AsegurarMigracionLite(c);
                    using (var del = new SqlCommand("DELETE FROM Productos WHERE ProductoID=@id", c))
                    {
                        del.Parameters.AddWithValue("@id", id);
                        del.ExecuteNonQuery();
                    }
                    RegistrarAccionTecnica("HARD_DELETE_PRODUCTO", "ProductoID=" + id);
                    return true;
                }
            }
            catch (Exception ex)
            {
                NotificarError("EliminarProducto: " + ex.Message);
                return false;
            }
        }

        public static int GetStockActualProducto(int productoId)
        {
            try
            {
                using (var c = new SqlConnection(_connectionString))
                {
                    c.Open();
                    using (var cmd = new SqlCommand("SELECT StockActual FROM Productos WHERE ProductoID=@id", c))
                    {
                        cmd.Parameters.AddWithValue("@id", productoId);
                        object o = cmd.ExecuteScalar();
                        return o != null && o != DBNull.Value ? Convert.ToInt32(o) : 0;
                    }
                }
            }
            catch { return 0; }
        }

        /// <summary>Política de stock por producto para ventas y remitos.</summary>
        public struct ProductoStockPolitica
        {
            public bool EsStockeable;
            public bool AceptaStockNegativo;
            public bool ControlaStock => EsStockeable;
            public bool ExigeStockSuficiente => EsStockeable && !AceptaStockNegativo;
            public bool PermiteVentaSinStockSuficiente => !EsStockeable || AceptaStockNegativo;

            public static ProductoStockPolitica PorDefecto => new ProductoStockPolitica { EsStockeable = true, AceptaStockNegativo = false };

            public static ProductoStockPolitica DesdeFila(DataRow r)
            {
                if (r == null) return PorDefecto;
                bool esStockeable = !r.Table.Columns.Contains("EsStockeable") || r["EsStockeable"] == DBNull.Value || Convert.ToBoolean(r["EsStockeable"]);
                bool aceptaNeg = r.Table.Columns.Contains("AceptaStockNegativo") && r["AceptaStockNegativo"] != DBNull.Value && Convert.ToBoolean(r["AceptaStockNegativo"]);
                return new ProductoStockPolitica { EsStockeable = esStockeable, AceptaStockNegativo = aceptaNeg };
            }
        }

        public static ProductoStockPolitica ObtenerPoliticaStockProducto(int productoId, SqlConnection c, SqlTransaction tr = null)
        {
            if (productoId <= 0)
                return new ProductoStockPolitica { EsStockeable = false, AceptaStockNegativo = true };

            using (var cmd = new SqlCommand(
                "SELECT ISNULL(EsStockeable,1) AS EsStockeable, ISNULL(AceptaStockNegativo,0) AS AceptaStockNegativo FROM Productos WHERE ProductoID=@id", c, tr))
            {
                cmd.Parameters.AddWithValue("@id", productoId);
                using (var rd = cmd.ExecuteReader())
                {
                    if (!rd.Read()) return ProductoStockPolitica.PorDefecto;
                    return new ProductoStockPolitica
                    {
                        EsStockeable = Convert.ToBoolean(rd["EsStockeable"]),
                        AceptaStockNegativo = Convert.ToBoolean(rd["AceptaStockNegativo"])
                    };
                }
            }
        }

        public static bool ProductoExigeStockSuficiente(int productoId)
        {
            if (productoId <= 0) return false;
            try
            {
                using (var c = new SqlConnection(_connectionString))
                {
                    c.Open();
                    return ObtenerPoliticaStockProducto(productoId, c).ExigeStockSuficiente;
                }
            }
            catch { return true; }
        }

        private static void DescontarStockVenta(SqlConnection c, SqlTransaction tr, FacturaItem item, DateTime fecha, int facturaId, string tipoMovimiento = "Venta")
        {
            if (item == null || item.ProductoID <= 0) return;
            if (string.Equals(item.Codigo, "VARIOS", StringComparison.OrdinalIgnoreCase)) return;

            var politica = ObtenerPoliticaStockProducto(item.ProductoID, c, tr);
            if (!politica.ControlaStock) return;

            int rowsActualizados;
            if (politica.AceptaStockNegativo)
            {
                using (var up = new SqlCommand(
                    "UPDATE Productos SET StockActual=StockActual-@cant WHERE ProductoID=@pid", c, tr))
                {
                    up.Parameters.AddWithValue("@cant", item.Cantidad);
                    up.Parameters.AddWithValue("@pid", item.ProductoID);
                    rowsActualizados = up.ExecuteNonQuery();
                }
                if (rowsActualizados == 0)
                    throw new InvalidOperationException($"No se encontró el producto «{item.Descripcion}» al descontar stock.");
            }
            else
            {
                using (var up = new SqlCommand(
                    "UPDATE Productos SET StockActual=StockActual-@cant WHERE ProductoID=@pid AND StockActual >= @cant", c, tr))
                {
                    up.Parameters.AddWithValue("@cant", item.Cantidad);
                    up.Parameters.AddWithValue("@pid", item.ProductoID);
                    rowsActualizados = up.ExecuteNonQuery();
                }
                if (rowsActualizados == 0)
                    throw new InvalidOperationException(
                        $"Stock insuficiente para «{item.Descripcion}» al momento de registrar la venta. " +
                        "Otro terminal puede haber vendido el mismo producto. Actualice el carrito y vuelva a intentar.");
            }

            using (var cmdStk = new SqlCommand(
                "INSERT INTO MovimientosStock (ProductoID,FacturaID,Fecha,TipoMovimiento,Cantidad) VALUES (@pid,@fid,@f,@tipo,@cant)", c, tr))
            {
                cmdStk.Parameters.AddWithValue("@pid", item.ProductoID);
                cmdStk.Parameters.AddWithValue("@fid", facturaId > 0 ? (object)facturaId : DBNull.Value);
                cmdStk.Parameters.AddWithValue("@f", fecha);
                cmdStk.Parameters.AddWithValue("@tipo", tipoMovimiento ?? "Venta");
                cmdStk.Parameters.AddWithValue("@cant", -item.Cantidad);
                cmdStk.ExecuteNonQuery();
            }
        }

        public static DataRow BuscarProducto(string q)
        {
            try
            {
                using (var c = new SqlConnection(_connectionString))
                {
                    c.Open();
                    var dt = new DataTable();
                    string like = "%" + (q ?? "").Replace("[", "[[]").Replace("%", "[%]").Replace("_", "[_]") + "%";
                    AsegurarMigracionLite(c);
                    using (var cmd = new SqlCommand("SELECT TOP 1 * FROM Productos WHERE ISNULL(Activo,1)=1 AND (Codigo=@q OR CodigoBarra=@q OR Descripcion LIKE @qLike)", c))
                    {
                        cmd.Parameters.AddWithValue("@q", q ?? "");
                        cmd.Parameters.AddWithValue("@qLike", like);
                        new SqlDataAdapter(cmd).Fill(dt);
                    }
                    if (dt.Rows.Count > 0) return dt.Rows[0];
                }
            }
            catch { }
            return null;
        }

        /// <summary>Búsqueda exacta por código interno (importación masiva, edición).</summary>
        public static DataRow BuscarProductoPorCodigoExacto(string codigo)
        {
            if (string.IsNullOrWhiteSpace(codigo)) return null;
            try
            {
                using (var c = new SqlConnection(_connectionString))
                {
                    c.Open();
                    var dt = new DataTable();
                    AsegurarMigracionLite(c);
                    using (var cmd = new SqlCommand("SELECT TOP 1 * FROM Productos WHERE Codigo = @cod", c))
                    {
                        cmd.Parameters.AddWithValue("@cod", codigo.Trim());
                        new SqlDataAdapter(cmd).Fill(dt);
                    }
                    return dt.Rows.Count > 0 ? dt.Rows[0] : null;
                }
            }
            catch { return null; }
        }

        /// <summary>Coincidencia exacta por código interno o código de barras; solo productos con stock &gt; 0 (venta).</summary>
        public static DataRow BuscarProductoExactoCodigoOCodigoBarra(string q)
        {
            if (string.IsNullOrWhiteSpace(q)) return null;
            q = q.Trim();
            try
            {
                using (var c = new SqlConnection(_connectionString))
                {
                    c.Open();
                    var dt = new DataTable();
                    // Sin filtrar stock: el lector debe encontrar el producto aunque el stock esté en 0
                    // (la validación de stock ocurre al confirmar la venta).
                    AsegurarMigracionLite(c);
                    using (var cmd = new SqlCommand("SELECT TOP 1 * FROM Productos WHERE ISNULL(Activo,1)=1 AND (Codigo = @q OR CodigoBarra = @q)", c))
                    {
                        cmd.Parameters.AddWithValue("@q", q);
                        new SqlDataAdapter(cmd).Fill(dt);
                    }
                    if (dt.Rows.Count > 0) return dt.Rows[0];
                }
            }
            catch { }
            return null;
        }

        public static DataTable BuscarProductosMultiples_ParaVenta(string q)
        {
            var dt = new DataTable();
            try
            {
                using (var c = new SqlConnection(_connectionString))
                {
                    c.Open();
                    string like = "%" + (q ?? "").Replace("[", "[[]").Replace("%", "[%]").Replace("_", "[_]") + "%";
                    AsegurarMigracionLite(c);
                    using (var cmd = new SqlCommand("SELECT TOP 10 * FROM Productos WHERE ISNULL(Activo,1)=1 AND (Codigo LIKE @p OR CodigoBarra LIKE @p OR Descripcion LIKE @p)", c))
                    {
                        cmd.Parameters.AddWithValue("@p", like);
                        new SqlDataAdapter(cmd).Fill(dt);
                    }
                }
            }
            catch { }
            return dt;
        }

        public static DataTable BuscarProductosMultiples_ParaCompra(string q)
        {
            var dt = new DataTable();
            try
            {
                using (var c = new SqlConnection(_connectionString))
                {
                    c.Open();
                    string like = "%" + (q ?? "").Replace("[", "[[]").Replace("%", "[%]").Replace("_", "[_]") + "%";
                    AsegurarMigracionLite(c);
                    using (var cmd = new SqlCommand("SELECT TOP 10 * FROM Productos WHERE ISNULL(Activo,1)=1 AND (Codigo LIKE @p OR CodigoBarra LIKE @p OR Descripcion LIKE @p)", c))
                    {
                        cmd.Parameters.AddWithValue("@p", like);
                        new SqlDataAdapter(cmd).Fill(dt);
                    }
                }
            }
            catch { }
            return dt;
        }

        public static bool ActualizarPreciosProducto(int id, decimal cost, decimal prec)
            => ActualizarPreciosProducto(id, cost, prec, out _);

        public static bool ActualizarPreciosProducto(int id, decimal cost, decimal prec, out decimal precioVentaActualizado)
        {
            precioVentaActualizado = prec;
            try
            {
                using (var c = new SqlConnection(_connectionString))
                {
                    c.Open();
                    AsegurarMigracionLite(c);
                    var prod = ObtenerFilaProducto(c, null, id);
                    if (prod == null) return false;

                    decimal costoAnterior = prod["PrecioCosto"] != DBNull.Value ? Convert.ToDecimal(prod["PrecioCosto"]) : 0m;
                    bool costoCambio = Math.Abs(costoAnterior - cost) >= 0.005m;

                    bool incluyeIva = prod.Table.Columns.Contains("CostoIncluyeIva") && prod["CostoIncluyeIva"] != DBNull.Value
                        && Convert.ToBoolean(prod["CostoIncluyeIva"]);
                    decimal imp = prod.Table.Columns.Contains("ImpuestoInterno") && prod["ImpuestoInterno"] != DBNull.Value
                        ? Convert.ToDecimal(prod["ImpuestoInterno"]) : 0m;
                    decimal iva = ParseIvaPct(prod["TipoIVA"]?.ToString());

                    if (costoCambio)
                    {
                        precioVentaActualizado = ActualizarPreciosVentaPorCambioDeCosto(id, cost, incluyeIva, iva, imp, c, null);
                        return true;
                    }

                    using (var cmd = new SqlCommand("UPDATE Productos SET PrecioCosto=@pc,PrecioVenta=@pv WHERE ProductoID=@id", c))
                    {
                        cmd.Parameters.AddWithValue("@pc", cost);
                        cmd.Parameters.AddWithValue("@pv", prec);
                        cmd.Parameters.AddWithValue("@id", id);
                        cmd.ExecuteNonQuery();
                        precioVentaActualizado = prec;
                        return true;
                    }
                }
            }
            catch { return false; }
        }

        // --- Exportación / importación masiva (actualización de costos y estados) ---

        public class ProductoActualizacionMasivaItem
        {
            public int NumeroFila { get; set; }
            public int? ProductoId { get; set; }
            public string Codigo { get; set; }
            public string CodigoBarra { get; set; }
            public string CodigoExterno { get; set; }
            public string Descripcion { get; set; }
            public string Categoria { get; set; }
            public string SubRubro { get; set; }
            public string Marca { get; set; }
            public string Proveedor { get; set; }
            public string TipoMoneda { get; set; }
            public decimal? CostoCompra { get; set; }
            public decimal? IvaPct { get; set; }
            public decimal? ImpuestoInterno { get; set; }
            public bool? CostoIncluyeIva { get; set; }
            public decimal? Stock { get; set; }
            public decimal? StockMinimo { get; set; }
            public decimal? StockIdeal { get; set; }
            public bool? PermitirModificarPrecioVenta { get; set; }
            public bool? EsStockeable { get; set; }
            public bool? VendeEnNegativo { get; set; }
            public bool? UsaVariantes { get; set; }
            public bool? EsCombo { get; set; }
            public bool? Activo { get; set; }
            public decimal? PrecioVenta { get; set; }
            public string VarianteColor { get; set; }
            public string VarianteTalle { get; set; }
            public string VarianteUnidadMedida { get; set; }
            public bool? CobraIvaAlCliente { get; set; }
            public string ImagenPath { get; set; }
        }

        public class ProductoImportacionMasivaResultado
        {
            public int Actualizados { get; set; }
            public int SinCambios { get; set; }
            public bool Exitoso { get; set; }
            public string ErrorGeneral { get; set; }
            public List<string> Errores { get; set; } = new List<string>();
        }

        public static DataTable ObtenerProductosParaExportacionMasiva()
        {
            var dt = new DataTable();
            try
            {
                using (var c = new SqlConnection(_connectionString))
                {
                    c.Open();
                    AsegurarMigracionLite(c);
                    const string sql = @"
SELECT
    p.ProductoID,
    ISNULL(p.Codigo, N'') AS Codigo,
    ISNULL(p.CodigoBarra, N'') AS CodigoBarras,
    ISNULL(p.CodigoExterno, N'') AS CodigoExterno,
    ISNULL(p.Descripcion, N'') AS Descripcion,
    ISNULL(p.Categoria, N'') AS Categoria,
    ISNULL(p.SubRubro, N'') AS SubRubro,
    ISNULL(p.Marca, N'') AS Marca,
    ISNULL(p.Proveedor, N'') AS Proveedor,
    CASE WHEN UPPER(LTRIM(RTRIM(ISNULL(p.TipoMoneda, N'')))) = N'USD' THEN N'USD' ELSE N'ARS' END AS TipoMoneda,
    ISNULL(p.PrecioCosto, 0) AS CostoCompra,
    ISNULL(p.TipoIVA, N'21.0') AS TipoIVA,
    ISNULL(p.ImpuestoInterno, 0) AS ImpuestoInterno,
    CASE WHEN ISNULL(p.CostoIncluyeIva, 0) = 1 THEN N'SI' ELSE N'NO' END AS CostoIncluyeIva,
    ISNULL(p.StockActual, 0) AS Stock,
    ISNULL(p.StockMinimo, 0) AS StockMinimo,
    ISNULL(p.StockIdeal, 0) AS StockIdeal,
    CASE WHEN ISNULL(p.PermiteModificarPrecioVenta, 0) = 1 THEN N'SI' ELSE N'NO' END AS PermitirModificarPrecioVenta,
    CASE WHEN ISNULL(p.EsStockeable, 1) = 1 THEN N'SI' ELSE N'NO' END AS EsStockeable,
    CASE WHEN ISNULL(p.AceptaStockNegativo, 0) = 1 THEN N'SI' ELSE N'NO' END AS PermitirStockNegativo,
    CASE WHEN ISNULL(p.UsaVariantes, 0) = 1 THEN N'SI' ELSE N'NO' END AS UsaVariantes,
    CASE WHEN ISNULL(p.EsCombo, 0) = 1 THEN N'SI' ELSE N'NO' END AS EsCombo,
    CASE WHEN ISNULL(p.Activo, 1) = 1 THEN N'SI' ELSE N'NO' END AS Activo,
    ISNULL(p.PrecioVenta, 0) AS PrecioVenta,
    ISNULL(p.VarianteColor, N'') AS VarianteColor,
    ISNULL(p.VarianteTalle, N'') AS VarianteTalle,
    ISNULL(p.VarianteUnidadMedida, N'') AS VarianteUnidadMedida,
    CASE WHEN ISNULL(p.CobraIvaAlCliente, 1) = 1 THEN N'SI' ELSE N'NO' END AS CobraIvaAlCliente,
    ISNULL(p.ImagenPath, N'') AS ImagenPath
FROM Productos p
ORDER BY p.Descripcion";
                    new SqlDataAdapter(sql, c).Fill(dt);
                    if (!dt.Columns.Contains("% IVA"))
                        dt.Columns.Add("% IVA", typeof(decimal));
                    foreach (DataRow r in dt.Rows)
                        r["% IVA"] = ParseIvaPct(r["TipoIVA"]?.ToString());
                    dt.Columns.Remove("TipoIVA");
                }
            }
            catch (Exception ex) { NotificarError("ObtenerProductosParaExportacionMasiva: " + ex.Message); }
            return dt;
        }

        public static bool TryParseSiNo(string valor, out bool resultado)
        {
            resultado = false;
            if (string.IsNullOrWhiteSpace(valor)) return false;
            string t = valor.Trim().ToUpperInvariant()
                .Replace("Í", "I")
                .Replace("í", "I");
            if (t == "SI" || t == "S" || t == "1" || t == "TRUE" || t == "VERDADERO")
            {
                resultado = true;
                return true;
            }
            if (t == "NO" || t == "N" || t == "0" || t == "FALSE" || t == "FALSO")
            {
                resultado = false;
                return true;
            }
            return false;
        }

        public static ProductoImportacionMasivaResultado ImportarActualizacionMasivaProductos(IList<ProductoActualizacionMasivaItem> filas)
            => ImportarActualizacionMasivaProductos(filas, false);

        public static ProductoImportacionMasivaResultado ImportarActualizacionMasivaProductos(IList<ProductoActualizacionMasivaItem> filas, bool permitirAltas)
        {
            var resultado = new ProductoImportacionMasivaResultado { Exitoso = false };
            if (filas == null || filas.Count == 0)
            {
                resultado.ErrorGeneral = "No hay filas para importar.";
                return resultado;
            }

            try
            {
                using (var c = new SqlConnection(_connectionString))
                {
                    c.Open();
                    AsegurarMigracionLite(c);
                    using (var tr = c.BeginTransaction())
                    {
                        try
                        {
                            ValidarDuplicadosImportacionMasiva(c, tr, filas, permitirAltas);
                            foreach (var fila in filas)
                                AplicarActualizacionMasivaFila(c, tr, fila, resultado, permitirAltas);
                            tr.Commit();
                            resultado.Exitoso = true;
                        }
                        catch (Exception ex)
                        {
                            tr.Rollback();
                            resultado.Actualizados = 0;
                            resultado.SinCambios = 0;
                            resultado.ErrorGeneral = ex.Message;
                            if (!resultado.Errores.Contains(ex.Message))
                                resultado.Errores.Add(ex.Message);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                resultado.ErrorGeneral = ex.Message;
                resultado.Errores.Add(ex.Message);
            }

            return resultado;
        }

        private static DataRow ResolverProductoImportacionMasiva(SqlConnection conexion, SqlTransaction transaccion, ProductoActualizacionMasivaItem item)
        {
            if (item.ProductoId.HasValue && item.ProductoId.Value > 0)
            {
                var porId = ObtenerFilaProducto(conexion, transaccion, item.ProductoId.Value);
                if (porId != null) return porId;
            }

            if (!string.IsNullOrWhiteSpace(item.Codigo))
            {
                var dt = new DataTable();
                using (var cmd = new SqlCommand("SELECT TOP 1 * FROM Productos WHERE Codigo=@cod", conexion, transaccion))
                {
                    cmd.Parameters.AddWithValue("@cod", item.Codigo.Trim());
                    new SqlDataAdapter(cmd).Fill(dt);
                }
                if (dt.Rows.Count > 0) return dt.Rows[0];
            }

            return null;
        }

        private static void ValidarDuplicadosImportacionMasiva(SqlConnection conexion, SqlTransaction transaccion, IList<ProductoActualizacionMasivaItem> filas, bool permitirAltas)
        {
            var errores = new List<string>();
            var codigosArchivo = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            var barrasArchivo = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);

            foreach (var item in filas)
            {
                string codigo = (item.Codigo ?? "").Trim();
                string barra = (item.CodigoBarra ?? "").Trim();

                if (!string.IsNullOrWhiteSpace(codigo))
                {
                    if (codigosArchivo.TryGetValue(codigo, out int filaAnterior))
                        errores.Add($"Filas {filaAnterior} y {item.NumeroFila}: Codigo duplicado '{codigo}' en el archivo.");
                    else
                        codigosArchivo[codigo] = item.NumeroFila;
                }

                if (!string.IsNullOrWhiteSpace(barra))
                {
                    if (barrasArchivo.TryGetValue(barra, out int filaAnterior))
                        errores.Add($"Filas {filaAnterior} y {item.NumeroFila}: CodigoBarra duplicado '{barra}' en el archivo.");
                    else
                        barrasArchivo[barra] = item.NumeroFila;
                }
            }

            foreach (var item in filas)
            {
                var prod = ResolverProductoImportacionMasiva(conexion, transaccion, item);
                if (prod == null && !permitirAltas)
                {
                    string idTxt = item.ProductoId.HasValue ? item.ProductoId.Value.ToString() : "(vacio)";
                    errores.Add($"Fila {item.NumeroFila}: No se encontro el producto (ProductoID={idTxt}, Codigo={item.Codigo ?? ""}).");
                    continue;
                }

                int idExcluir = prod != null ? Convert.ToInt32(prod["ProductoID"]) : 0;
                if (ExisteProductoDuplicadoEnConexion(conexion, transaccion, idExcluir, item.Codigo, item.CodigoBarra, out string msg))
                    errores.Add($"Fila {item.NumeroFila}: {msg.Replace(Environment.NewLine, " | ")}");
            }

            if (errores.Count > 0)
                throw new Exception("Conflictos detectados antes de importar:\n" + string.Join("\n", errores.Take(20)));
        }

        private static void AplicarActualizacionMasivaFila(
            SqlConnection conexion,
            SqlTransaction transaccion,
            ProductoActualizacionMasivaItem item,
            ProductoImportacionMasivaResultado resultado,
            bool permitirAltas)
        {
            if (!item.ProductoId.HasValue && string.IsNullOrWhiteSpace(item.Codigo))
                throw new Exception($"Fila {item.NumeroFila}: ProductoID o Codigo es obligatorio.");

            var prod = ResolverProductoImportacionMasiva(conexion, transaccion, item);
            if (prod == null && !permitirAltas)
            {
                string idTxt = item.ProductoId.HasValue ? item.ProductoId.Value.ToString() : "(vacío)";
                throw new Exception($"Fila {item.NumeroFila}: No se encontró el producto (ProductoID={idTxt}, Codigo={item.Codigo ?? ""}).");
            }

            bool esNuevo = prod == null;
            int productoId = esNuevo ? 0 : Convert.ToInt32(prod["ProductoID"]);

            string codigo = ValorTexto(item.Codigo, prod, "Codigo");
            string codigoBarra = ValorTexto(item.CodigoBarra, prod, "CodigoBarra");
            string descripcion = ValorTexto(item.Descripcion, prod, "Descripcion");
            if (string.IsNullOrWhiteSpace(codigo))
                throw new Exception($"Fila {item.NumeroFila}: Codigo es obligatorio.");
            if (string.IsNullOrWhiteSpace(descripcion))
                throw new Exception($"Fila {item.NumeroFila}: Descripcion es obligatoria.");

            decimal costo = item.CostoCompra ?? ValorDecimal(prod, "PrecioCosto");
            decimal venta = item.PrecioVenta ?? ValorDecimal(prod, "PrecioVenta");
            decimal imp = item.ImpuestoInterno ?? ValorDecimal(prod, "ImpuestoInterno");
            decimal iva = item.IvaPct ?? ParseIvaPct(ValorTexto(null, prod, "TipoIVA"));
            int stock = Convert.ToInt32(item.Stock ?? ValorDecimal(prod, "StockActual"));

            int guardado = GuardarProductoEnConexion(conexion, transaccion, productoId, codigo, codigoBarra, descripcion,
                ValorTexto(item.Categoria, prod, "Categoria"),
                ValorTexto(item.SubRubro, prod, "SubRubro"),
                ValorTexto(item.Marca, prod, "Marca"),
                ValorTexto(item.Proveedor, prod, "Proveedor"),
                iva.ToString(CultureInfo.InvariantCulture),
                costo,
                0,
                imp,
                venta,
                stock,
                ValorTexto(item.ImagenPath, prod, "ImagenPath"),
                NormalizarTipoMoneda(ValorTexto(item.TipoMoneda, prod, "TipoMoneda")),
                item.PermitirModificarPrecioVenta ?? ValorBool(prod, "PermiteModificarPrecioVenta", false),
                item.EsStockeable ?? ValorBool(prod, "EsStockeable", true),
                item.VendeEnNegativo ?? ValorBool(prod, "AceptaStockNegativo", false),
                item.UsaVariantes ?? ValorBool(prod, "UsaVariantes", false),
                item.EsCombo ?? ValorBool(prod, "EsCombo", false),
                item.StockMinimo ?? ValorNullableDecimal(prod, "StockMinimo"),
                item.StockIdeal ?? ValorNullableDecimal(prod, "StockIdeal"),
                ValorTexto(item.CodigoExterno, prod, "CodigoExterno"),
                ValorTexto(item.VarianteColor, prod, "VarianteColor"),
                ValorTexto(item.VarianteTalle, prod, "VarianteTalle"),
                ValorTexto(item.VarianteUnidadMedida, prod, "VarianteUnidadMedida"),
                item.CobraIvaAlCliente ?? ValorBool(prod, "CobraIvaAlCliente", true),
                item.CostoIncluyeIva ?? ValorBool(prod, "CostoIncluyeIva", false),
                item.Activo ?? ValorBool(prod, "Activo", true),
                true);

            if (guardado <= 0)
                throw new Exception($"Fila {item.NumeroFila}: No se pudo guardar el producto.");

            if (esNuevo) resultado.Actualizados++;
            else resultado.Actualizados++;
        }

        private static string ValorTexto(string valorImportado, DataRow actual, string columna)
        {
            if (valorImportado != null) return valorImportado.Trim();
            if (actual == null || !actual.Table.Columns.Contains(columna) || actual[columna] == DBNull.Value) return "";
            return actual[columna]?.ToString() ?? "";
        }

        private static decimal ValorDecimal(DataRow actual, string columna)
        {
            if (actual == null || !actual.Table.Columns.Contains(columna) || actual[columna] == DBNull.Value) return 0m;
            return Convert.ToDecimal(actual[columna]);
        }

        private static decimal? ValorNullableDecimal(DataRow actual, string columna)
        {
            if (actual == null || !actual.Table.Columns.Contains(columna) || actual[columna] == DBNull.Value) return null;
            return Convert.ToDecimal(actual[columna]);
        }

        private static bool ValorBool(DataRow actual, string columna, bool predeterminado)
        {
            if (actual == null || !actual.Table.Columns.Contains(columna) || actual[columna] == DBNull.Value) return predeterminado;
            return Convert.ToBoolean(actual[columna]);
        }

        private static string NormalizarTipoMoneda(string tipoMoneda)
        {
            string t = (tipoMoneda ?? "").Trim().ToUpperInvariant();
            if (t == "USD" || t == "DOLAR" || t == "DÓLAR") return "USD";
            return "ARS";
        }

        public static int ObtenerIDProductoVarios()
        {
            try
            {
                using (var c = new SqlConnection(_connectionString))
                {
                    c.Open();
                    var cmd = new SqlCommand("SELECT ProductoID FROM Productos WHERE Codigo = 'VARIOS'", c);
                    object result = cmd.ExecuteScalar();

                    if (result != null && result != DBNull.Value) return Convert.ToInt32(result);
                    else
                    {
                        string sql = "INSERT INTO Productos (Codigo, Descripcion, PrecioVenta, StockActual, TipoIVA, Categoria) VALUES ('VARIOS', 'Producto Varios', 0, 999999, '21.0', 'General'); SELECT SCOPE_IDENTITY();";
                        return Convert.ToInt32(new SqlCommand(sql, c).ExecuteScalar());
                    }
                }
            }
            catch (Exception ex) { NotificarError(ex.Message); return 0; }
        }



        public static List<Permiso> GetPermisos()
        {
            var l = new List<Permiso>();
            try
            {
                using (var c = new SqlConnection(_connectionString))
                {
                    c.Open();
                    using (var r = new SqlCommand("SELECT * FROM Permisos ORDER BY NombrePermiso", c).ExecuteReader())
                    {
                        while (r.Read()) l.Add(new Permiso { PermisoId = Convert.ToInt32(r["PermisoID"]), Nombre = r["NombrePermiso"].ToString() });
                    }
                }
            }
            catch { }
            return l;
        }

        public static Dictionary<int, List<int>> GetPermisosPorRol()
        {
            var d = new Dictionary<int, List<int>>();
            try
            {
                using (var c = new SqlConnection(_connectionString))
                {
                    c.Open();
                    using (var r = new SqlCommand("SELECT * FROM Roles_Permisos", c).ExecuteReader())
                    {
                        while (r.Read())
                        {
                            int rid = Convert.ToInt32(r["RolID"]);
                            int pid = Convert.ToInt32(r["PermisoID"]);
                            if (!d.ContainsKey(rid)) d[rid] = new List<int>();
                            d[rid].Add(pid);
                        }
                    }
                }
            }
            catch { }
            return d;
        }

        /// <summary>Nombres canónicos de permisos definidos en constantes PERMISO_*.</summary>
        public static List<string> ObtenerNombresPermisosCatalogo()
        {
            return ModulosCatalog.ObtenerTodosCodigos();
        }

        public static HashSet<string> GetPermisosNombresPorRol(int rolId)
        {
            var permisos = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            if (rolId == 1)
            {
                foreach (var p in GetPermisos())
                    if (!string.IsNullOrWhiteSpace(p.Nombre))
                        permisos.Add(p.Nombre.Trim());
                // Instalaciones nuevas pueden tener solo ACCESO_TOTAL en BD hasta el seeder;
                // el administrador debe poder operar todos los módulos definidos en código.
                foreach (var nombre in ObtenerNombresPermisosCatalogo())
                    permisos.Add(nombre);
                return permisos;
            }
            try
            {
                using (var c = new SqlConnection(_connectionString))
                {
                    c.Open();
                    using (var cmd = new SqlCommand(@"
                        SELECT p.NombrePermiso
                        FROM Roles_Permisos rp
                        INNER JOIN Permisos p ON p.PermisoID = rp.PermisoID
                        WHERE rp.RolID = @rid", c))
                    {
                        cmd.Parameters.AddWithValue("@rid", rolId);
                        using (var r = cmd.ExecuteReader())
                        {
                            while (r.Read())
                            {
                                var nombre = r["NombrePermiso"]?.ToString()?.Trim();
                                if (!string.IsNullOrWhiteSpace(nombre))
                                    permisos.Add(nombre);
                            }
                        }
                    }
                }
            }
            catch { }
            return permisos;
        }

        public static void ActualizarPermisosParaRol(int rid, List<int> pids)
        {
            using (var c = new SqlConnection(_connectionString))
            {
                c.Open();
                using (var t = c.BeginTransaction())
                {
                    try
                    {
                        new SqlCommand($"DELETE FROM Roles_Permisos WHERE RolID={rid}", c, t).ExecuteNonQuery();
                        if (pids != null)
                            foreach (int pid in pids)
                                new SqlCommand($"INSERT INTO Roles_Permisos (RolID,PermisoID) VALUES ({rid},{pid})", c, t).ExecuteNonQuery();
                        t.Commit();
                    }
                    catch { t.Rollback(); }
                }
            }
        }

        public static bool ActualizarPermisosParaRolPorNombre(int rolId, List<string> nombresPermisos)
        {
            try
            {
                using (var c = new SqlConnection(_connectionString))
                {
                    c.Open();
                    using (var tx = c.BeginTransaction())
                    {
                        try
                        {
                            using (var del = new SqlCommand("DELETE FROM Roles_Permisos WHERE RolID=@rid", c, tx))
                            {
                                del.Parameters.AddWithValue("@rid", rolId);
                                del.ExecuteNonQuery();
                            }

                            var lista = (nombresPermisos ?? new List<string>())
                                .Where(n => !string.IsNullOrWhiteSpace(n))
                                .Distinct(StringComparer.OrdinalIgnoreCase)
                                .ToList();

                            foreach (var nombre in lista)
                            {
                                using (var ins = new SqlCommand(@"
                                    INSERT INTO Roles_Permisos (RolID, PermisoID)
                                    SELECT @rid, p.PermisoID
                                    FROM Permisos p
                                    WHERE p.NombrePermiso = @nom", c, tx))
                                {
                                    ins.Parameters.AddWithValue("@rid", rolId);
                                    ins.Parameters.AddWithValue("@nom", nombre);
                                    ins.ExecuteNonQuery();
                                }
                            }

                            tx.Commit();
                            return true;
                        }
                        catch
                        {
                            try { tx.Rollback(); } catch { }
                            return false;
                        }
                    }
                }
            }
            catch
            {
                return false;
            }
        }

        public static bool ActualizarPermisosRol(int rolId, string permisosCsv)
        {
            try
            {
                using (var c = new SqlConnection(_connectionString))
                {
                    c.Open();
                    using (var tx = c.BeginTransaction())
                    {
                        // 1. Borramos los permisos viejos de este rol
                        using (var cmdDelete = new SqlCommand("DELETE FROM Roles_Permisos WHERE RolID = @RolID", c, tx))
                        {
                            cmdDelete.Parameters.AddWithValue("@RolID", rolId);
                            cmdDelete.ExecuteNonQuery();
                        }

                        // 2. Preparamos la lista nueva
                        var permisos = (permisosCsv ?? string.Empty)
                            .Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries)
                            .Select(p => p.Trim())
                            .Where(p => !string.IsNullOrWhiteSpace(p))
                            .Distinct(StringComparer.OrdinalIgnoreCase)
                            .ToList();

                        // Si el usuario destildó todo, guardamos el borrado y terminamos con éxito
                        if (permisos.Count == 0)
                        {
                            tx.Commit();
                            return true;
                        }

                        // 3. Insertamos los nuevos
                        foreach (var permiso in permisos)
                        {
                            using (var cmdInsert = new SqlCommand(@"
                        INSERT INTO Roles_Permisos (RolID, PermisoID)
                        SELECT @RolID, p.PermisoID
                        FROM Permisos p
                        WHERE p.NombrePermiso = @Permiso;", c, tx))
                            {
                                cmdInsert.Parameters.AddWithValue("@RolID", rolId);
                                cmdInsert.Parameters.AddWithValue("@Permiso", permiso);

                                int insertadas = cmdInsert.ExecuteNonQuery();

                                // Si insertó 0, significa que el permiso no existe en la tabla maestra
                                if (insertadas == 0)
                                {
                                    tx.Rollback();
                                    throw new Exception($"El permiso '{permiso}' no se encontró en la tabla 'Permisos' de la base de datos. Verifica que los nombres coincidan exactamente.");
                                }
                            }
                        }

                        // Si todo salió bien, confirmamos la transacción
                        tx.Commit();
                        return true;
                    }
                }
            }
            catch (Exception ex)
            {
                // En vez de devolver false en silencio, lanzamos el error a la interfaz
                throw new Exception(ex.Message);
            }
        }

        /// <summary>
        /// Migración de nombres de permisos sin guión bajo (ACCESOVENTAS) al formato
        /// con guión bajo (ACCESO_VENTAS). Se ejecuta una sola vez; es idempotente.
        /// </summary>
        public static void MigrarNombresPermisosConGuionBajo()
        {
            var mapa = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                { "ACCESOUSUARIOS",          "ACCESO_USUARIOS"          },
                { "ACCESOCLIENTES",          "ACCESO_CLIENTES"          },
                { "ACCESOPRODUCTOS",         "ACCESO_PRODUCTOS"         },
                { "ACCESOSTOCK",             "ACCESO_STOCK"             },
                { "ACCESOFACTURACION",       "ACCESO_FACTURACION"       },
                { "ACCESOVENTAS",            "ACCESO_VENTAS"            },
                { "ACCESOPERMISOS",          "ACCESO_PERMISOS"          },
                { "ACCESOPROVEEDORES",       "ACCESO_PROVEEDORES"       },
                { "ACCESOCOMPRAS",           "ACCESO_COMPRAS"           },
                { "ACCESOPRECIOS",           "ACCESO_PRECIOS"           },
                { "ACCESOCAJA",              "ACCESO_CAJA"              },
                { "ACCESOPRESUPUESTOS",      "ACCESO_PRESUPUESTOS"      },
                { "ACCESOCUENTASCORRIENTES", "ACCESO_CUENTASCORRIENTES" },
                { "ACCESOLISTASPRECIOS",     "ACCESO_LISTASPRECIOS"     },
                { "ACCESOCONFIGURACION",     "ACCESO_CONFIGURACION"     },
            };
            try
            {
                using (var c = new SqlConnection(_connectionString))
                {
                    c.Open();
                    foreach (var kvp in mapa)
                    {
                        using (var cmd = new SqlCommand(@"
                            UPDATE Permisos SET NombrePermiso = @nuevo
                            WHERE NombrePermiso = @viejo", c))
                        {
                            cmd.Parameters.AddWithValue("@viejo", kvp.Key);
                            cmd.Parameters.AddWithValue("@nuevo", kvp.Value);
                            cmd.ExecuteNonQuery();
                        }
                    }
                }
            }
            catch (Exception ex) { NotificarError("MigrarNombresPermisosConGuionBajo: " + ex.Message); }
        }

        public static void InicializarPermisosBaseDatos()
        {
            var permisosBase = ObtenerNombresPermisosCatalogo();
            if (permisosBase.Count == 0) return;

            using (var c = new SqlConnection(_connectionString))
            {
                c.Open();
                foreach (var permiso in permisosBase)
                {
                    string query = @"
    IF NOT EXISTS (SELECT 1 FROM Permisos WHERE NombrePermiso = @Nombre) 
    INSERT INTO Permisos (NombrePermiso) VALUES (@Nombre)";
                    using (var cmd = new SqlCommand(query, c))
                    {
                        cmd.Parameters.AddWithValue("@Nombre", permiso);
                        cmd.ExecuteNonQuery();
                    }
                }
            }
        }

        public static DataTable GetListasPrecios()
        {
            var dt = new DataTable();
            try
            {
                using (var c = new SqlConnection(_connectionString))
                {
                    c.Open();
                    AsegurarMigracionLite(c);
                    new SqlDataAdapter("SELECT * FROM ListasPrecios ORDER BY Nombre", c).Fill(dt);
                }
            }
            catch { }
            return dt;
        }

        public static bool GuardarListaPrecio(int id, string nombre, decimal porcentaje)
            => GuardarListaPrecio(id, nombre, porcentaje, TiposListaPrecio.SobreCosto, null, TiposRedondeoLista.Sin);

        public static bool GuardarListaPrecio(int id, string nombre, decimal porcentaje, string tipoLista, int? listaRelacionadaId, string tipoRedondeo)
        {
            try
            {
                using (var c = new SqlConnection(_connectionString))
                {
                    c.Open();
                    AsegurarMigracionLite(c);
                    string tipo = string.IsNullOrWhiteSpace(tipoLista) ? TiposListaPrecio.SobreCosto : tipoLista;
                    string redondeo = string.IsNullOrWhiteSpace(tipoRedondeo) ? TiposRedondeoLista.Sin : tipoRedondeo;

                    if (tipo == TiposListaPrecio.ListaRelacionada && listaRelacionadaId.HasValue && listaRelacionadaId.Value == id)
                    {
                        NotificarError("Una lista no puede relacionarse consigo misma.");
                        return false;
                    }

                    using (var tr = c.BeginTransaction())
                    {
                        try
                        {
                            if (id == 0)
                            {
                                using (var cmd = new SqlCommand(@"
INSERT INTO ListasPrecios (Nombre, Porcentaje, TipoLista, ListaRelacionadaID, TipoRedondeo)
OUTPUT INSERTED.ListaID
VALUES (@n, @p, @tipo, @rel, @red)", c, tr))
                                {
                                    cmd.Parameters.AddWithValue("@n", nombre);
                                    cmd.Parameters.AddWithValue("@p", porcentaje);
                                    cmd.Parameters.AddWithValue("@tipo", tipo);
                                    cmd.Parameters.AddWithValue("@rel", (object)listaRelacionadaId ?? DBNull.Value);
                                    cmd.Parameters.AddWithValue("@red", redondeo);
                                    int nuevoId = Convert.ToInt32(cmd.ExecuteScalar());
                                    AsignarListaATodosLosProductosEnConexion(c, tr, nuevoId);
                                }
                            }
                            else
                            {
                                using (var cmd = new SqlCommand(@"UPDATE ListasPrecios SET Nombre=@n, Porcentaje=@p, TipoLista=@tipo,
                            ListaRelacionadaID=@rel, TipoRedondeo=@red WHERE ListaID=@id", c, tr))
                                {
                                    cmd.Parameters.AddWithValue("@n", nombre);
                                    cmd.Parameters.AddWithValue("@p", porcentaje);
                                    cmd.Parameters.AddWithValue("@tipo", tipo);
                                    cmd.Parameters.AddWithValue("@rel", (object)listaRelacionadaId ?? DBNull.Value);
                                    cmd.Parameters.AddWithValue("@red", redondeo);
                                    cmd.Parameters.AddWithValue("@id", id);
                                    cmd.ExecuteNonQuery();
                                }
                            }
                            tr.Commit();
                            return true;
                        }
                        catch
                        {
                            try { tr.Rollback(); } catch { }
                            throw;
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                NotificarError(ex.Message);
                return false;
            }
        }

        private static void AsignarListaATodosLosProductosEnConexion(SqlConnection c, SqlTransaction tr, int listaId)
        {
            using (var cmd = new SqlCommand(@"
INSERT INTO ProductosListas (ProductoID, ListaID, PrecioFijo)
SELECT p.ProductoID, @lid, NULL
FROM Productos p
WHERE NOT EXISTS (
    SELECT 1 FROM ProductosListas pl WHERE pl.ProductoID = p.ProductoID AND pl.ListaID = @lid
)", c, tr))
            {
                cmd.Parameters.AddWithValue("@lid", listaId);
                cmd.ExecuteNonQuery();
            }
        }

        public static List<string> GetMarcasProductos()
        {
            var list = new List<string>();
            try
            {
                using (var c = new SqlConnection(_connectionString))
                {
                    c.Open();
                    using (var cmd = new SqlCommand(@"
SELECT DISTINCT LTRIM(RTRIM(ISNULL(Marca, N''))) AS Marca
FROM Productos
WHERE LTRIM(RTRIM(ISNULL(Marca, N''))) <> N''
ORDER BY Marca", c))
                    using (var rd = cmd.ExecuteReader())
                    {
                        while (rd.Read())
                            list.Add(rd["Marca"]?.ToString() ?? "");
                    }
                }
            }
            catch { }
            return list;
        }

        public static List<string> GetCategoriasProductos()
        {
            var list = new List<string>();
            try
            {
                using (var c = new SqlConnection(_connectionString))
                {
                    c.Open();
                    using (var cmd = new SqlCommand(@"
SELECT DISTINCT LTRIM(RTRIM(ISNULL(Categoria, N''))) AS Categoria
FROM Productos
WHERE LTRIM(RTRIM(ISNULL(Categoria, N''))) <> N''
ORDER BY Categoria", c))
                    using (var rd = cmd.ExecuteReader())
                    {
                        while (rd.Read())
                            list.Add(rd["Categoria"]?.ToString() ?? "");
                    }
                }
            }
            catch { }
            return list;
        }

        public static List<ProductoAsignacionListaItem> GetProductosParaAsignacionLista(int listaId, string ambito, string filtro)
        {
            var list = new List<ProductoAsignacionListaItem>();
            ambito = (ambito ?? "todos").Trim().ToLowerInvariant();
            filtro = (filtro ?? "").Trim();
            try
            {
                using (var c = new SqlConnection(_connectionString))
                {
                    c.Open();
                    AsegurarMigracionLite(c);
                    string where = "1=1";
                    if (ambito == "marca" && !string.IsNullOrWhiteSpace(filtro))
                        where = "LTRIM(RTRIM(ISNULL(p.Marca, N''))) = @f";
                    else if (ambito == "categoria" && !string.IsNullOrWhiteSpace(filtro))
                        where = "LTRIM(RTRIM(ISNULL(p.Categoria, N''))) = @f";

                    string sql = $@"
SELECT p.ProductoID,
       LTRIM(RTRIM(ISNULL(p.Codigo, N''))) AS Codigo,
       LTRIM(RTRIM(ISNULL(p.Descripcion, N''))) AS Descripcion,
       LTRIM(RTRIM(ISNULL(p.Marca, N''))) AS Marca,
       LTRIM(RTRIM(ISNULL(p.Categoria, N''))) AS Categoria,
       CASE WHEN pl.ListaID IS NULL THEN 0 ELSE 1 END AS YaAsignado
FROM Productos p
LEFT JOIN ProductosListas pl ON pl.ProductoID = p.ProductoID AND pl.ListaID = @lid
WHERE {where}
ORDER BY p.Descripcion";
                    using (var cmd = new SqlCommand(sql, c))
                    {
                        cmd.Parameters.AddWithValue("@lid", listaId);
                        if (where.Contains("@f"))
                            cmd.Parameters.AddWithValue("@f", filtro);
                        using (var rd = cmd.ExecuteReader())
                        {
                            while (rd.Read())
                            {
                                bool ya = Convert.ToInt32(rd["YaAsignado"]) == 1;
                                list.Add(new ProductoAsignacionListaItem
                                {
                                    Incluir = true,
                                    ProductoID = Convert.ToInt32(rd["ProductoID"]),
                                    Codigo = rd["Codigo"]?.ToString() ?? "",
                                    Descripcion = rd["Descripcion"]?.ToString() ?? "",
                                    Marca = rd["Marca"]?.ToString() ?? "",
                                    Categoria = rd["Categoria"]?.ToString() ?? "",
                                    YaAsignado = ya
                                });
                            }
                        }
                    }
                }
            }
            catch { }
            return list;
        }

        /// <summary>
        /// Dentro del conjunto cargado: asigna la lista a los IDs incluidos y la quita de los excluidos.
        /// No toca productos que no están en ninguno de los dos conjuntos.
        /// </summary>
        public static int SincronizarAsignacionLista(int listaId, IList<int> incluirIds, IList<int> excluirIds)
        {
            if (listaId <= 0) return 0;
            int afectados = 0;
            try
            {
                using (var c = new SqlConnection(_connectionString))
                {
                    c.Open();
                    AsegurarMigracionLite(c);
                    using (var tr = c.BeginTransaction())
                    {
                        try
                        {
                            if (incluirIds != null)
                            {
                                foreach (int pid in incluirIds.Distinct())
                                {
                                    using (var cmd = new SqlCommand(@"
IF NOT EXISTS (SELECT 1 FROM ProductosListas WHERE ProductoID=@pid AND ListaID=@lid)
    INSERT INTO ProductosListas (ProductoID, ListaID, PrecioFijo) VALUES (@pid, @lid, NULL)", c, tr))
                                    {
                                        cmd.Parameters.AddWithValue("@pid", pid);
                                        cmd.Parameters.AddWithValue("@lid", listaId);
                                        afectados += cmd.ExecuteNonQuery();
                                    }
                                }
                            }
                            if (excluirIds != null)
                            {
                                foreach (int pid in excluirIds.Distinct())
                                {
                                    using (var cmd = new SqlCommand(
                                        "DELETE FROM ProductosListas WHERE ProductoID=@pid AND ListaID=@lid", c, tr))
                                    {
                                        cmd.Parameters.AddWithValue("@pid", pid);
                                        cmd.Parameters.AddWithValue("@lid", listaId);
                                        afectados += cmd.ExecuteNonQuery();
                                    }
                                }
                            }
                            tr.Commit();
                        }
                        catch
                        {
                            try { tr.Rollback(); } catch { }
                            throw;
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                NotificarError(ex.Message);
                return -1;
            }
            return afectados;
        }

        public class ProductoAsignacionListaItem
        {
            public bool Incluir { get; set; }
            public int ProductoID { get; set; }
            public string Codigo { get; set; }
            public string Descripcion { get; set; }
            public string Marca { get; set; }
            public string Categoria { get; set; }
            public bool YaAsignado { get; set; }
        }

        public static bool EliminarListaPrecio(int id)
        {
            if (id == 1)
            {
                NotificarError("No se puede eliminar la lista base.");
                return false;
            }
            try
            {
                using (var c = new SqlConnection(_connectionString))
                {
                    c.Open();
                    AsegurarMigracionLite(c);
                    new SqlCommand($"UPDATE Clientes SET ListaPrecioID=NULL WHERE ListaPrecioID={id}", c).ExecuteNonQuery();
                    new SqlCommand($"UPDATE Configuracion SET PosListaPrecioID=NULL WHERE PosListaPrecioID={id}", c).ExecuteNonQuery();
                    new SqlCommand($"DELETE FROM ProductosListas WHERE ListaID={id}", c).ExecuteNonQuery();
                    new SqlCommand($"DELETE FROM ListasPrecios WHERE ListaID={id}", c).ExecuteNonQuery();
                    return true;
                }
            }
            catch { return false; }
        }

        public static DataTable GetRankingVentas(DateTime desde, DateTime hasta)
        {
            var dt = new DataTable();
            try
            {
                using (var c = new SqlConnection(_connectionString))
                {
                    c.Open();
                    string sql = @"SELECT p.Codigo, p.Descripcion, p.Categoria AS Rubro, SUM(fd.Cantidad) AS UnidadesVendidas, SUM(fd.Cantidad * fd.PrecioUnitario) AS TotalVendido FROM FacturaDetalle fd JOIN Facturas f ON fd.FacturaID=f.FacturaID JOIN Productos p ON fd.ProductoID=p.ProductoID WHERE f.Fecha BETWEEN @d AND @h GROUP BY p.ProductoID ORDER BY TotalVendido DESC";
                    using (var cmd = new SqlCommand(sql, c))
                    {
                        cmd.Parameters.AddWithValue("@d", desde);
                        cmd.Parameters.AddWithValue("@h", hasta);
                        new SqlDataAdapter(cmd).Fill(dt);
                    }
                }
            }
            catch { }
            return dt;
        }

        public static DataTable GetVentasParaLibroIVA(DateTime desde, DateTime hasta)
        {
            var dt = new DataTable();
            try
            {
                using (var c = new SqlConnection(_connectionString))
                {
                    c.Open();
                    string sql = @"SELECT f.Fecha, f.TipoComprobante, f.FacturaID AS NroComprobante, cl.RazonSocial AS Cliente, cl.CUIT, cl.CondicionIVA, f.Total, fd.Cantidad, fd.PrecioUnitario, p.TipoIVA AS AlicuotaProducto FROM Facturas f JOIN Clientes cl ON f.ClienteID=cl.ClienteID JOIN FacturaDetalle fd ON f.FacturaID=fd.FacturaID JOIN Productos p ON fd.ProductoID=p.ProductoID WHERE f.Fecha BETWEEN @d AND @h ORDER BY f.Fecha ASC";
                    using (var cmd = new SqlCommand(sql, c))
                    {
                        cmd.Parameters.AddWithValue("@d", desde);
                        cmd.Parameters.AddWithValue("@h", hasta);
                        new SqlDataAdapter(cmd).Fill(dt);
                    }
                }
            }
            catch { }
            return dt;
        }

        // --- TRANSACCIONES COMPLEJAS (Facturación / Compras) ---
        /// <summary>True si la condición elegida del POS es cuenta corriente (no efectivo/inmediato).</summary>
        public static bool EsFacturaCondicionCuentaCorriente(string condicionVenta)
        {
            if (string.IsNullOrWhiteSpace(condicionVenta)) return false;
            string s = condicionVenta.Trim();
            if (s.IndexOf("Corriente", StringComparison.OrdinalIgnoreCase) >= 0) return true;
            return Regex.IsMatch(s, @"cta\s*\.?\s*cte", RegexOptions.IgnoreCase);
        }

        /// <returns>FacturaID insertado, o 0 si falló.</returns>
        public static int GuardarFactura(int cid, string tc, decimal t, List<FacturaItem> its, string condicionVentaCombo,
            string textoDetalleCobranzasOpcional,
            string cae, string vtoCae, int nroComprobanteAfip, int? listaId,
            List<FacturaCobranzaParcela> cobranzas)
        {
            if (its == null || its.Count == 0) return 0;

            string condVent = condicionVentaCombo ?? "";
            bool cc = EsFacturaCondicionCuentaCorriente(condVent);

            try
            {
                using (var c = new SqlConnection(_connectionString))
                {
                    c.Open();
                    AsegurarMigracionLite(c);
                    AsegurarColumnaCondicionTicketFacturas(c);

                    bool tieneTabFc;
                    using (var q = new SqlCommand("SELECT CASE WHEN OBJECT_ID(N'FacturasCobranza','U') IS NULL THEN 0 ELSE 1 END", c))
                        tieneTabFc = Convert.ToInt32(q.ExecuteScalar()) == 1;

                    using (var tr = c.BeginTransaction())
                    {
                        try
                        {
                            DateTime fecha = DateTime.Now;
                            string nombrePersonalVenta = SesionUsuario.NombreParaRegistro();
                            int? usuarioIdVenta = SesionUsuario.UsuarioID > 0 ? (int?)SesionUsuario.UsuarioID : null;
                            string sqlFac = @"INSERT INTO Facturas 
                                (ClienteID,Fecha,Total,TipoComprobante,CondicionVenta,CondicionTicket,CAE,VencimientoCAE,NumeroComprobanteAFIP,ListaID,UsuarioID,NombrePersonal)
                                VALUES (@cid,@f,@t,@tc,@cv,@ct,@cae,@vto,@nAfip,@lista,@uid,@np);
                                SELECT CAST(SCOPE_IDENTITY() AS INT);";
                            int fid = 0;
                            using (var cmdFac = new SqlCommand(sqlFac, c, tr))
                            {
                                cmdFac.Parameters.AddWithValue("@cid", cid);
                                cmdFac.Parameters.AddWithValue("@f", fecha);
                                cmdFac.Parameters.AddWithValue("@t", t);
                                cmdFac.Parameters.AddWithValue("@tc", tc ?? "");
                                cmdFac.Parameters.AddWithValue("@cv", condVent);
                                cmdFac.Parameters.AddWithValue("@ct", string.IsNullOrWhiteSpace(textoDetalleCobranzasOpcional) ? (object)DBNull.Value : textoDetalleCobranzasOpcional.Trim());
                                cmdFac.Parameters.AddWithValue("@cae", (object)cae ?? DBNull.Value);
                                cmdFac.Parameters.AddWithValue("@vto", (object)vtoCae ?? DBNull.Value);
                                cmdFac.Parameters.AddWithValue("@nAfip", nroComprobanteAfip <= 0 ? (object)DBNull.Value : nroComprobanteAfip);
                                cmdFac.Parameters.AddWithValue("@lista", (object)listaId ?? DBNull.Value);
                                cmdFac.Parameters.AddWithValue("@uid", (object)usuarioIdVenta ?? DBNull.Value);
                                cmdFac.Parameters.AddWithValue("@np", string.IsNullOrWhiteSpace(nombrePersonalVenta) ? (object)DBNull.Value : nombrePersonalVenta);
                                fid = Convert.ToInt32(cmdFac.ExecuteScalar());
                            }

                                foreach (var i in its)
                                {
                                    using (var det = new SqlCommand(
                                        "INSERT INTO FacturaDetalle (FacturaID,ProductoID,Cantidad,PrecioUnitario,DescuentoPorcentaje,RecargoPorcentaje) VALUES (@fid,@pid,@cant,@prec,@dto,@rec)", c, tr))
                                    {
                                        det.Parameters.AddWithValue("@fid", fid);
                                        det.Parameters.AddWithValue("@pid", i.ProductoID);
                                        det.Parameters.AddWithValue("@cant", i.Cantidad);
                                        det.Parameters.AddWithValue("@prec", i.PrecioUnitario);
                                        det.Parameters.AddWithValue("@dto", i.DescuentoPorcentaje);
                                        det.Parameters.AddWithValue("@rec", i.RecargoPorcentaje);
                                        det.ExecuteNonQuery();
                                    }
                                    DescontarStockVenta(c, tr, i, fecha, fid);
                                }

                                if (tieneTabFc && !cc && cobranzas != null)
                                {
                                    foreach (var p in cobranzas)
                                    {
                                        if (p == null || p.Monto <= 0m) continue;
                                        using (var insP = new SqlCommand(
                                            @"INSERT INTO FacturasCobranza
                                              (FacturaID,MedioPagoID,NombreMedio,Monto,NroCuotas,NroTarjeta,MarcaTarjeta,OperacionExternaID)
                                              VALUES (@fid,@mid,@nom,@mont,@cuotas,@tarjeta,@marca,@operacion)",
                                            c, tr))
                                        {
                                            insP.Parameters.AddWithValue("@fid", fid);
                                            insP.Parameters.AddWithValue("@mid", p.MedioPagoID > 0 ? p.MedioPagoID : (object)DBNull.Value);
                                            insP.Parameters.AddWithValue("@nom", p.NombreMedio ?? "");
                                            insP.Parameters.AddWithValue("@mont", p.Monto);
                                            insP.Parameters.AddWithValue("@cuotas", p.NroCuotas > 0 ? p.NroCuotas : 1);
                                            insP.Parameters.AddWithValue("@tarjeta", string.IsNullOrWhiteSpace(p.UltimosDigitosTarjeta) ? (object)DBNull.Value : p.UltimosDigitosTarjeta);
                                            insP.Parameters.AddWithValue("@marca", string.IsNullOrWhiteSpace(p.MarcaTarjeta) ? (object)DBNull.Value : p.MarcaTarjeta);
                                            insP.Parameters.AddWithValue("@operacion", string.IsNullOrWhiteSpace(p.OperacionExternaID) ? (object)DBNull.Value : p.OperacionExternaID);
                                            insP.ExecuteNonQuery();
                                        }
                                    }
                                }

                                string usuario = nombrePersonalVenta;

                                if (!cc)
                                {
                                    if (cobranzas != null && cobranzas.Count > 0)
                                    {
                                        foreach (var p in cobranzas)
                                        {
                                            if (p == null || p.Monto <= 0m) continue;
                                            string medio = string.IsNullOrWhiteSpace(p.NombreMedio) ? "Pago" : p.NombreMedio.Trim();
                                            string concepto = $"Venta #{fid} ({tc}) — {medio}";
                                            if (!string.IsNullOrWhiteSpace(textoDetalleCobranzasOpcional))
                                                concepto += " | " + textoDetalleCobranzasOpcional;
                                            using (var cmdCaja = new SqlCommand(
                                                "INSERT INTO MovimientosCaja (Fecha,Concepto,Tipo,Monto,Usuario) VALUES (@f,@con,'Ingreso',@m,@u)", c, tr))
                                            {
                                                cmdCaja.Parameters.AddWithValue("@f", fecha);
                                                cmdCaja.Parameters.AddWithValue("@con", concepto.Length > 200 ? concepto.Substring(0, 200) : concepto);
                                                cmdCaja.Parameters.AddWithValue("@m", p.Monto);
                                                cmdCaja.Parameters.AddWithValue("@u", usuario);
                                                cmdCaja.ExecuteNonQuery();
                                            }
                                        }
                                    }
                                    else
                                    {
                                        using (var cmdCaja = new SqlCommand(
                                            "INSERT INTO MovimientosCaja (Fecha,Concepto,Tipo,Monto,Usuario) VALUES (@f,@con,'Ingreso',@m,@u)", c, tr))
                                        {
                                            cmdCaja.Parameters.AddWithValue("@f", fecha);
                                            cmdCaja.Parameters.AddWithValue("@con", $"Venta #{fid} ({tc})");
                                            cmdCaja.Parameters.AddWithValue("@m", t);
                                            cmdCaja.Parameters.AddWithValue("@u", usuario);
                                            cmdCaja.ExecuteNonQuery();
                                        }
                                    }
                                }
                                else
                                {
                                    using (var upSal = new SqlCommand(
                                        "UPDATE Clientes SET SaldoDeuda = SaldoDeuda + @m WHERE ClienteID=@cid", c, tr))
                                    {
                                        upSal.Parameters.AddWithValue("@m", t);
                                        upSal.Parameters.AddWithValue("@cid", cid);
                                        upSal.ExecuteNonQuery();
                                    }
                                    object sal;
                                    using (var qsal = new SqlCommand("SELECT SaldoDeuda FROM Clientes WHERE ClienteID=@cid", c, tr))
                                    {
                                        qsal.Parameters.AddWithValue("@cid", cid);
                                        sal = qsal.ExecuteScalar();
                                    }

                                    using (var cmdCC = new SqlCommand(
                                        "INSERT INTO MovimientosCuentaCorriente (ClienteID,Fecha,Descripcion,Monto,SaldoHistorico) VALUES (@cid,@f,@desc,@m,@sal)", c, tr))
                                    {
                                        cmdCC.Parameters.AddWithValue("@cid", cid);
                                        cmdCC.Parameters.AddWithValue("@f", fecha);
                                        cmdCC.Parameters.AddWithValue("@desc", $"Venta #{fid} (Cta Cte)");
                                        cmdCC.Parameters.AddWithValue("@m", t);
                                        cmdCC.Parameters.AddWithValue("@sal", sal);
                                        cmdCC.ExecuteNonQuery();
                                    }
                                }

                                tr.Commit();
                                return fid;
                        }
                        catch (InvalidOperationException)
                        {
                            // Error de stock insuficiente: re-lanzar para que la UI lo muestre al cajero.
                            try { tr.Rollback(); } catch { }
                            throw;
                        }
                        catch (Exception ex)
                        {
                            try { tr.Rollback(); } catch { }
                            NotificarError(ex.Message);
                            return 0;
                        }
                    }
                }
            }
            catch (InvalidOperationException)
            {
                // Stock insuficiente detectado fuera de la transacción (ej: error de conexión al transaccionar).
                // Re-lanzar para que la UI muestre el mensaje específico al cajero.
                throw;
            }
            catch (Exception ex)
            {
                NotificarError("GuardarFactura (conexión): " + ex.Message);
                return 0;
            }
        }

        /// <summary>Método corto para pruebas automatizadas: cond debe ser texto de forma de cobro («Contado» o «Cuenta Corriente»).</summary>
        public static bool GuardarFactura(int cid, string tc, decimal t, List<FacturaItem> its, string condicionVenta)
        {
            return GuardarFactura(cid, tc, t, its, condicionVenta, null, null, null, 0, null, null) > 0;
        }

        public static DataTable GetCompraDetalle(int compraId)
        {
            var dt = new DataTable();
            try
            {
                using (var c = new SqlConnection(_connectionString))
                {
                    c.Open();
                    new SqlDataAdapter($@"SELECT cd.DetalleID, cd.ProductoID, ISNULL(p.Codigo,'') AS Codigo,
                                                ISNULL(p.Descripcion,'') AS Descripcion,
                                                cd.Cantidad, cd.PrecioCosto
                                         FROM CompraDetalle cd LEFT JOIN Productos p ON cd.ProductoID=p.ProductoID
                                         WHERE cd.CompraID={compraId}", c).Fill(dt);
                }
            }
            catch { }
            return dt;
        }

        public static bool GuardarCompra(int pid, string tc, decimal t, List<(int ProductoID, int Cantidad, decimal Costo)> items, string cond)
        {
            var its = new List<FacturaItem>();
            foreach (var it in items)
                its.Add(new FacturaItem { ProductoID = it.ProductoID, Cantidad = it.Cantidad, PrecioUnitario = it.Costo, Descripcion = "" });
            return GuardarCompra(pid, tc, t, its, cond);
        }

        public static bool GuardarCompra(int pid, string tc, decimal t, List<FacturaItem> its, string cond)
        {
            using (var c = new SqlConnection(_connectionString))
            {
                c.Open();
                using (var tr = c.BeginTransaction())
                {
                    try
                    {
                        string sqlComp = "INSERT INTO Compras (ProveedorID,Fecha,Total,TipoComprobante) VALUES (@pid,@f,@t,@tc); SELECT SCOPE_IDENTITY();";
                        SqlCommand cmdComp = new SqlCommand(sqlComp, c, tr);
                        cmdComp.Parameters.AddWithValue("@pid", pid);
                        cmdComp.Parameters.AddWithValue("@f", DateTime.Now);
                        cmdComp.Parameters.AddWithValue("@t", t);
                        cmdComp.Parameters.AddWithValue("@tc", tc);
                        int cid = Convert.ToInt32(cmdComp.ExecuteScalar());

                        foreach (var i in its)
                        {
                            new SqlCommand($"INSERT INTO CompraDetalle (CompraID,ProductoID,Cantidad,PrecioCosto) VALUES ({cid},{i.ProductoID},{i.Cantidad},{(double)i.PrecioUnitario})", c, tr).ExecuteNonQuery();
                            new SqlCommand($"UPDATE Productos SET StockActual=StockActual+{i.Cantidad} WHERE ProductoID={i.ProductoID}", c, tr).ExecuteNonQuery();

                            var prodCompra = ObtenerFilaProducto(c, tr, i.ProductoID);
                            if (prodCompra != null)
                            {
                                decimal nuevoCosto = i.PrecioUnitario;
                                bool incluyeIva = prodCompra.Table.Columns.Contains("CostoIncluyeIva") && prodCompra["CostoIncluyeIva"] != DBNull.Value
                                    && Convert.ToBoolean(prodCompra["CostoIncluyeIva"]);
                                decimal imp = prodCompra.Table.Columns.Contains("ImpuestoInterno") && prodCompra["ImpuestoInterno"] != DBNull.Value
                                    ? Convert.ToDecimal(prodCompra["ImpuestoInterno"]) : 0m;
                                decimal iva = ParseIvaPct(prodCompra["TipoIVA"]?.ToString());
                                ActualizarPreciosVentaPorCambioDeCosto(i.ProductoID, nuevoCosto, incluyeIva, iva, imp, c, tr);
                            }

                            SqlCommand cmdStk = new SqlCommand("INSERT INTO MovimientosStock (ProductoID,CompraID,Fecha,TipoMovimiento,Cantidad) VALUES (@prod,@cid,@f,'Compra',@cant)", c, tr);
                            cmdStk.Parameters.AddWithValue("@prod", i.ProductoID);
                            cmdStk.Parameters.AddWithValue("@cid", cid);
                            cmdStk.Parameters.AddWithValue("@f", DateTime.Now);
                            cmdStk.Parameters.AddWithValue("@cant", i.Cantidad);
                            cmdStk.ExecuteNonQuery();
                        }

                        if (cond == "Contado")
                        {
                            SqlCommand cmdCaja = new SqlCommand("INSERT INTO MovimientosCaja (Fecha,Concepto,Tipo,Monto,Usuario) VALUES (@f,@con,'Egreso',@m,@u)", c, tr);
                            cmdCaja.Parameters.AddWithValue("@f", DateTime.Now);
                            cmdCaja.Parameters.AddWithValue("@con", $"Compra #{cid} ({tc})");
                            cmdCaja.Parameters.AddWithValue("@m", t);
                            cmdCaja.Parameters.AddWithValue("@u", SesionUsuario.NombreUsuario);
                            cmdCaja.ExecuteNonQuery();
                        }
                        else
                        {
                            new SqlCommand($"UPDATE Proveedores SET SaldoDeuda=SaldoDeuda+{(double)t} WHERE ProveedorID={pid}", c, tr).ExecuteNonQuery();
                            object sal = new SqlCommand($"SELECT SaldoDeuda FROM Proveedores WHERE ProveedorID={pid}", c, tr).ExecuteScalar();

                            SqlCommand cmdCC = new SqlCommand("INSERT INTO MovimientosCuentaCorriente (ProveedorID,Fecha,Descripcion,Monto,SaldoHistorico) VALUES (@pid,@f,@desc,@m,@sal)", c, tr);
                            cmdCC.Parameters.AddWithValue("@pid", pid);
                            cmdCC.Parameters.AddWithValue("@f", DateTime.Now);
                            cmdCC.Parameters.AddWithValue("@desc", $"Compra #{cid} (Cta Cte)");
                            cmdCC.Parameters.AddWithValue("@m", t);
                            cmdCC.Parameters.AddWithValue("@sal", sal);
                            cmdCC.ExecuteNonQuery();
                        }
                        tr.Commit();
                        return true;
                    }
                    catch { tr.Rollback(); return false; }
                }
            }
        }

        public static DataTable GetFacturasPorFecha(DateTime d, DateTime h)
        {
            var dt = new DataTable();
            try
            {
                using (var c = new SqlConnection(_connectionString))
                {
                    c.Open();
                    var cmd = new SqlCommand(@"SELECT f.FacturaID, f.Fecha, cl.RazonSocial, f.TipoComprobante, f.Total,
       ISNULL(f.NombrePersonal, '') AS NombrePersonal
FROM Facturas f JOIN Clientes cl ON f.ClienteID=cl.ClienteID WHERE f.Fecha BETWEEN @d AND @h ORDER BY f.Fecha DESC", c);
                    cmd.Parameters.AddWithValue("@d", d);
                    cmd.Parameters.AddWithValue("@h", h);
                    new SqlDataAdapter(cmd).Fill(dt);
                }
            }
            catch { }
            return dt;
        }

        public static DataRow GetFacturaPorID(int id)
        {
            try
            {
                using (var c = new SqlConnection(_connectionString))
                {
                    c.Open();
                    AsegurarColumnaCondicionTicketFacturas(c);
                    var cmd = new SqlCommand(@"
SELECT f.FacturaID, f.ClienteID, f.Fecha, f.TipoComprobante, f.Total, f.CondicionVenta,
       f.NumeroComprobanteAFIP, f.CAE, f.VencimientoCAE, f.CondicionTicket,
       ISNULL(f.NombrePersonal, '') AS NombrePersonal,
       ISNULL(cl.RazonSocial,'Consumidor Final') AS ClienteNombre,
       ISNULL(cl.CUIT,'-') AS ClienteCUIT,
       ISNULL(cl.CondicionIVA,'Consumidor Final') AS ClienteIVA,
       ISNULL(cl.Direccion,'-') AS ClienteDireccion
FROM Facturas f
LEFT JOIN Clientes cl ON f.ClienteID = cl.ClienteID
WHERE f.FacturaID = @id", c);
                    cmd.Parameters.AddWithValue("@id", id);
                    var dt = new DataTable();
                    new SqlDataAdapter(cmd).Fill(dt);
                    return dt.Rows.Count > 0 ? dt.Rows[0] : null;
                }
            }
            catch { return null; }
        }

        public static DataTable GetFacturaDetalle(int id)
        {
            var dt = new DataTable();
            try
            {
                using (var c = new SqlConnection(_connectionString))
                {
                    c.Open();
                    using (var cmd = new SqlCommand(@"
SELECT fd.ProductoID, p.Codigo, p.Descripcion, fd.Cantidad, fd.PrecioUnitario,
(fd.Cantidad * fd.PrecioUnitario * (1 - ISNULL(fd.DescuentoPorcentaje,0)/100.0) * (1 + ISNULL(fd.RecargoPorcentaje,0)/100.0)) AS Subtotal
FROM FacturaDetalle fd JOIN Productos p ON fd.ProductoID = p.ProductoID WHERE fd.FacturaID = @id", c))
                    {
                        cmd.Parameters.AddWithValue("@id", id);
                        new SqlDataAdapter(cmd).Fill(dt);
                    }
                }
            }
            catch { }
            return dt;
        }

        public static DataTable GetStockGeneral(string filtro = "")
        {
            var dt = new DataTable();
            try
            {
                using (var c = new SqlConnection(_connectionString))
                {
                    c.Open();
                    AsegurarMigracionLite(c);
                    string where = string.IsNullOrWhiteSpace(filtro) ? ""
                        : $" WHERE (p.Codigo LIKE @f OR p.Descripcion LIKE @f OR p.Categoria LIKE @f OR p.Marca LIKE @f OR p.SubRubro LIKE @f OR p.Proveedor LIKE @f)";
                    string sql = $@"SELECT p.ProductoID, p.Codigo, p.Descripcion,
                                           ISNULL(p.Categoria,'') AS Rubro,
                                           ISNULL(p.SubRubro,'') AS SubRubro,
                                           ISNULL(p.Marca,'') AS Marca,
                                           '' AS Talle, '' AS Color,
                                           ISNULL(p.StockActual,0) AS StockReal,
                                           ISNULL((SELECT SUM(r.Cantidad) FROM ReservasStock r WHERE r.ProductoID=p.ProductoID AND r.Estado='Activa'),0) AS StockReservado,
                                           ISNULL(p.StockActual,0) - ISNULL((SELECT SUM(r.Cantidad) FROM ReservasStock r WHERE r.ProductoID=p.ProductoID AND r.Estado='Activa'),0) AS StockDisponible,
                                           ISNULL(p.Proveedor,'') AS Proveedor,
                                           ISNULL(p.StockMinimo,0) AS StockMinimo,
                                           ISNULL(p.PrecioCosto,0) AS PrecioCosto,
                                           ISNULL(p.ImpuestoInterno,0) AS ImpuestoInterno,
                                           ISNULL(p.CostoIncluyeIva,0) AS CostoIncluyeIva,
                                           ISNULL(p.TipoIVA,'21') AS TipoIVA,
                                           p.FechaModificacion
                                    FROM Productos p{where} ORDER BY p.Descripcion";
                    var da = new SqlDataAdapter(sql, c);
                    if (!string.IsNullOrWhiteSpace(filtro)) da.SelectCommand.Parameters.AddWithValue("@f", "%" + filtro + "%");
                    da.Fill(dt);
                }
            }
            catch { }
            return dt;
        }

        public static DataTable GetMovimientosStockFiltrado(DateTime? desde, DateTime? hasta, string filtro = "", List<string> tipos = null)
        {
            var dt = new DataTable();
            try
            {
                using (var c = new SqlConnection(_connectionString))
                {
                    c.Open();
                    var conditions = new System.Collections.Generic.List<string>();
                    if (desde.HasValue) conditions.Add("m.Fecha >= @desde");
                    if (hasta.HasValue) conditions.Add("m.Fecha <= @hasta");
                    if (!string.IsNullOrWhiteSpace(filtro)) conditions.Add("(p.Codigo LIKE @f OR p.Descripcion LIKE @f)");
                    if (tipos != null && tipos.Count > 0)
                    {
                        var tipoList = string.Join(",", tipos.ConvertAll(t => $"'{t.Replace("'", "''")}'"));
                        conditions.Add($"m.TipoMovimiento IN ({tipoList})");
                    }
                    string where = conditions.Count > 0 ? " WHERE " + string.Join(" AND ", conditions) : "";
                    string sql = $@"SELECT m.MovimientoID, m.Fecha, ISNULL(p.Codigo,'') AS Codigo,
                                           ISNULL(p.Descripcion,'') AS Descripcion,
                                           m.TipoMovimiento, m.Cantidad,
                                           ISNULL(p.Categoria,'') AS Rubro, '' AS Talle, '' AS Color,
                                           ISNULL(u.NombreUsuario,'') AS Usuario
                                    FROM MovimientosStock m
                                    LEFT JOIN Productos p ON m.ProductoID=p.ProductoID
                                    LEFT JOIN (SELECT TOP 1 NombreUsuario FROM Usuarios WHERE 1=1) u ON 1=0
                                    {where} ORDER BY m.Fecha DESC";
                    var da = new SqlDataAdapter(sql, c);
                    if (desde.HasValue) da.SelectCommand.Parameters.AddWithValue("@desde", desde.Value.Date);
                    if (hasta.HasValue) da.SelectCommand.Parameters.AddWithValue("@hasta", hasta.Value.Date.AddDays(1).AddSeconds(-1));
                    if (!string.IsNullOrWhiteSpace(filtro)) da.SelectCommand.Parameters.AddWithValue("@f", "%" + filtro + "%");
                    da.Fill(dt);
                }
            }
            catch { }
            return dt;
        }

        public static DataTable GetReservasStock(DateTime? desde, DateTime? hasta, string filtro = "", string estado = "")
        {
            var dt = new DataTable();
            try
            {
                using (var c = new SqlConnection(_connectionString))
                {
                    c.Open();
                    var conds = new System.Collections.Generic.List<string>();
                    if (desde.HasValue) conds.Add("rs.Fecha >= @desde");
                    if (hasta.HasValue) conds.Add("rs.Fecha <= @hasta");
                    if (!string.IsNullOrWhiteSpace(filtro)) conds.Add("(p.Codigo LIKE @f OR p.Descripcion LIKE @f OR rs.Motivo LIKE @f)");
                    if (!string.IsNullOrWhiteSpace(estado)) conds.Add("rs.Estado = @est");
                    string where = conds.Count > 0 ? " WHERE " + string.Join(" AND ", conds) : "";
                    string sql = $@"SELECT rs.ReservaID, rs.Fecha AS FechaReserva, rs.FechaVencimiento,
                                           ISNULL(p.Codigo,'') AS Codigo, ISNULL(p.Descripcion,'') AS Descripcion,
                                           rs.Cantidad, rs.Motivo AS CanalReserva, rs.Estado,
                                           '' AS OrdenID, '' AS Talle, '' AS Color,
                                           ISNULL(cl.RazonSocial,'') AS Cliente
                                    FROM ReservasStock rs
                                    LEFT JOIN Productos p ON rs.ProductoID=p.ProductoID
                                    LEFT JOIN Clientes cl ON rs.ClienteID=cl.ClienteID
                                    {where} ORDER BY rs.Fecha DESC";
                    var da = new SqlDataAdapter(sql, c);
                    if (desde.HasValue) da.SelectCommand.Parameters.AddWithValue("@desde", desde.Value.Date);
                    if (hasta.HasValue) da.SelectCommand.Parameters.AddWithValue("@hasta", hasta.Value.Date.AddDays(1).AddSeconds(-1));
                    if (!string.IsNullOrWhiteSpace(filtro)) da.SelectCommand.Parameters.AddWithValue("@f", "%" + filtro + "%");
                    if (!string.IsNullOrWhiteSpace(estado)) da.SelectCommand.Parameters.AddWithValue("@est", estado);
                    da.Fill(dt);
                }
            }
            catch { }
            return dt;
        }

        public static bool GuardarReservaStock(int productoId, int cantidad, string motivo, int? clienteId = null, DateTime? fechaVencimiento = null)
        {
            try
            {
                using (var c = new SqlConnection(_connectionString))
                {
                    c.Open();
                    string cidSql = clienteId.HasValue ? clienteId.Value.ToString() : "NULL";
                    var cmd = new SqlCommand($@"INSERT INTO ReservasStock (ProductoID,ClienteID,Fecha,FechaVencimiento,Cantidad,Motivo,Estado,Usuario)
                                               VALUES ({productoId},{cidSql},GETDATE(),@fv,{cantidad},@mot,'Activa',@u)", c);
                    cmd.Parameters.AddWithValue("@fv", fechaVencimiento.HasValue ? (object)fechaVencimiento.Value.Date : DBNull.Value);
                    cmd.Parameters.AddWithValue("@mot", motivo ?? "");
                    cmd.Parameters.AddWithValue("@u", SesionUsuario.NombreUsuario ?? "");
                    cmd.ExecuteNonQuery();
                    return true;
                }
            }
            catch (Exception ex)
            {
                NotificarError("GuardarReservaStock: " + ex.Message);
                return false;
            }
        }

        public static bool AnularReservaStock(int reservaId)
        {
            try
            {
                using (var c = new SqlConnection(_connectionString))
                {
                    c.Open();
                    new SqlCommand($"UPDATE ReservasStock SET Estado='Anulada' WHERE ReservaID={reservaId}", c).ExecuteNonQuery();
                    return true;
                }
            }
            catch { return false; }
        }

        public static bool AjustarStock(int pid, int cant, string mot)
        {
            if (cant == 0) return false;
            try
            {
                using (var c = new SqlConnection(_connectionString))
                {
                    c.Open();
                    using (var t = c.BeginTransaction())
                    {
                        new SqlCommand($"UPDATE Productos SET StockActual=StockActual+{cant} WHERE ProductoID={pid}", c, t).ExecuteNonQuery();
                        SqlCommand cmd = new SqlCommand("INSERT INTO MovimientosStock (ProductoID,Fecha,TipoMovimiento,Cantidad) VALUES (@pid,@f,@mot,@cant)", c, t);
                        cmd.Parameters.AddWithValue("@pid", pid);
                        cmd.Parameters.AddWithValue("@f", DateTime.Now);
                        cmd.Parameters.AddWithValue("@mot", mot);
                        cmd.Parameters.AddWithValue("@cant", cant);
                        cmd.ExecuteNonQuery();
                        t.Commit();
                        return true;
                    }
                }
            }
            catch { return false; }
        }

        private static void AjustarStock(int pid, int cant, string mot, SqlConnection c, SqlTransaction tx)
        {
            if (cant == 0) return;
            new SqlCommand($"UPDATE Productos SET StockActual=StockActual+{cant} WHERE ProductoID={pid}", c, tx).ExecuteNonQuery();
            var cmd = new SqlCommand("INSERT INTO MovimientosStock (ProductoID,Fecha,TipoMovimiento,Cantidad) VALUES (@pid,@f,@mot,@cant)", c, tx);
            cmd.Parameters.AddWithValue("@pid", pid);
            cmd.Parameters.AddWithValue("@f", DateTime.Now);
            cmd.Parameters.AddWithValue("@mot", mot);
            cmd.Parameters.AddWithValue("@cant", cant);
            cmd.ExecuteNonQuery();
        }

        private static void RegistrarMovimientoCaja(string con, string tip, decimal m, SqlConnection c, SqlTransaction tx)
        {
            var cmd = new SqlCommand("INSERT INTO MovimientosCaja (Fecha,Concepto,Tipo,Monto,Usuario) VALUES (@f,@c,@t,@m,@u)", c, tx);
            cmd.Parameters.AddWithValue("@f", DateTime.Now);
            cmd.Parameters.AddWithValue("@c", con);
            cmd.Parameters.AddWithValue("@t", tip);
            cmd.Parameters.AddWithValue("@m", m);
            cmd.Parameters.AddWithValue("@u", SesionUsuario.NombreUsuario ?? "");
            cmd.ExecuteNonQuery();
        }

        public static bool RegistrarMovimientoCaja(string con, string tip, decimal m)
        {
            try
            {
                using (var c = new SqlConnection(_connectionString))
                {
                    c.Open();
                    SqlCommand cmd = new SqlCommand("INSERT INTO MovimientosCaja (Fecha,Concepto,Tipo,Monto,Usuario) VALUES (@f,@c,@t,@m,@u)", c);
                    cmd.Parameters.AddWithValue("@f", DateTime.Now);
                    cmd.Parameters.AddWithValue("@c", con);
                    cmd.Parameters.AddWithValue("@t", tip);
                    cmd.Parameters.AddWithValue("@m", m);
                    cmd.Parameters.AddWithValue("@u", SesionUsuario.NombreUsuario);
                    cmd.ExecuteNonQuery();
                    return true;
                }
            }
            catch { return false; }
        }

        public static DataTable GetMovimientosCaja(DateTime f)
        {
            var dt = new DataTable();
            try
            {
                using (var c = new SqlConnection(_connectionString))
                {
                    c.Open();
                    var cmd = new SqlCommand("SELECT * FROM MovimientosCaja WHERE CAST(Fecha AS DATE) = CAST(@f AS DATE) ORDER BY Fecha DESC", c);
                    cmd.Parameters.AddWithValue("@f", f);
                    new SqlDataAdapter(cmd).Fill(dt);
                }
            }
            catch { }
            return dt;
        }

        public static decimal GetSaldoCaja()
        {
            decimal s = 0;
            try
            {
                using (var c = new SqlConnection(_connectionString))
                {
                    c.Open();
                    var i = new SqlCommand("SELECT SUM(Monto) FROM MovimientosCaja WHERE Tipo='Ingreso'", c).ExecuteScalar();
                    if (i != DBNull.Value) s += Convert.ToDecimal(i);
                    var e = new SqlCommand("SELECT SUM(Monto) FROM MovimientosCaja WHERE Tipo='Egreso'", c).ExecuteScalar();
                    if (e != DBNull.Value) s -= Convert.ToDecimal(e);
                }
            }
            catch { }
            return s;
        }

        /// <summary>
        /// Devuelve el desglose de ventas del día agrupado por medio de pago,
        /// consultando la tabla FacturasCobranza (generada al guardar cada factura).
        /// Útil para el cierre de caja con desglose real por Efectivo / Tarjeta / Transferencia.
        /// </summary>
        public static DataTable GetDesgloseMediosPagoHoy()
        {
            var dt = new DataTable();
            try
            {
                using (var c = new SqlConnection(_connectionString))
                {
                    c.Open();
                    var cmd = new SqlCommand(@"
                        SELECT fc.NombreMedio, SUM(fc.Monto) AS Total
                        FROM FacturasCobranza fc
                        INNER JOIN Facturas f ON fc.FacturaID = f.FacturaID
                        WHERE CAST(f.Fecha AS DATE) = CAST(GETDATE() AS DATE)
                        GROUP BY fc.NombreMedio
                        ORDER BY Total DESC", c);
                    new SqlDataAdapter(cmd).Fill(dt);
                }
            }
            catch { }
            return dt;
        }

        public static bool GetUsaAperturaCajaObligatoria()
        {
            try
            {
                var cfg = GetConfiguracion();
                if (cfg == null || !cfg.Table.Columns.Contains("UsaAperturaCaja") || cfg["UsaAperturaCaja"] == DBNull.Value)
                    return false;
                return Convert.ToBoolean(cfg["UsaAperturaCaja"]);
            }
            catch { return false; }
        }

        /// <summary>Si la apertura de caja es obligatoria, solo se puede vender con turno abierto y sin cierre del día.</summary>
        public static bool PuedeRegistrarVentasPos()
        {
            if (!GetUsaAperturaCajaObligatoria())
                return true;
            return TieneAperturaCajaHoy() && !TieneCierreCajaHoy();
        }

        public static string MensajeBloqueoVentasPos()
        {
            if (!GetUsaAperturaCajaObligatoria())
                return "";
            if (TieneCierreCajaHoy())
                return "El cierre de caja de hoy ya fue registrado.\n\nPara volver a vender, desactivá «Apertura y cierre de caja» en Configuración o esperá al día siguiente y abrí un nuevo turno.";
            if (!TieneAperturaCajaHoy())
                return "Debe abrir la caja antes de vender.\n\nUsá el botón «ABRIR CAJA» e indicá el fondo fijo inicial.";
            return "";
        }

        public static bool TieneAperturaCajaHoy()
        {
            try
            {
                using (var c = new SqlConnection(_connectionString))
                {
                    c.Open();
                    AsegurarTablaAperturasCaja(c);
                    var cmd = new SqlCommand(
                        "SELECT COUNT(*) FROM AperturasCaja WHERE CAST(Fecha AS DATE) = CAST(GETDATE() AS DATE)", c);
                    return Convert.ToInt32(cmd.ExecuteScalar()) > 0;
                }
            }
            catch { return false; }
        }

        public static bool TieneCierreCajaHoy()
        {
            try
            {
                using (var c = new SqlConnection(_connectionString))
                {
                    c.Open();
                    var cmd = new SqlCommand(
                        "SELECT COUNT(*) FROM CierresCaja WHERE CAST(Fecha AS DATE) = CAST(GETDATE() AS DATE)", c);
                    return Convert.ToInt32(cmd.ExecuteScalar()) > 0;
                }
            }
            catch { return false; }
        }

        public static DataRow GetAperturaCajaHoy()
        {
            try
            {
                using (var c = new SqlConnection(_connectionString))
                {
                    c.Open();
                    AsegurarTablaAperturasCaja(c);
                    var cmd = new SqlCommand(@"
SELECT TOP 1 AperturaID, Fecha, MontoFondoFijo, Observaciones, Usuario, MovimientoID
FROM AperturasCaja
WHERE CAST(Fecha AS DATE) = CAST(GETDATE() AS DATE)
ORDER BY Fecha DESC", c);
                    var dt = new DataTable();
                    new SqlDataAdapter(cmd).Fill(dt);
                    return dt.Rows.Count > 0 ? dt.Rows[0] : null;
                }
            }
            catch { return null; }
        }

        /// <returns>True si registró la apertura.</returns>
        public static bool AbrirCaja(decimal montoFondoFijo, string observaciones = null)
        {
            if (montoFondoFijo < 0) return false;
            if (TieneAperturaCajaHoy())
            {
                NotificarError("Ya existe una apertura de caja registrada para hoy.");
                return false;
            }

            try
            {
                using (var c = new SqlConnection(_connectionString))
                {
                    c.Open();
                    AsegurarTablaAperturasCaja(c);
                    using (var tr = c.BeginTransaction())
                    {
                        try
                        {
                            int movId = 0;
                            using (var cmdMov = new SqlCommand(@"
INSERT INTO MovimientosCaja (Fecha,Concepto,Tipo,Monto,Usuario)
VALUES (@f,@con,'Ingreso',@m,@u);
SELECT CAST(SCOPE_IDENTITY() AS INT);", c, tr))
                            {
                                cmdMov.Parameters.AddWithValue("@f", DateTime.Now);
                                cmdMov.Parameters.AddWithValue("@con", ConceptoFondoFijo);
                                cmdMov.Parameters.AddWithValue("@m", montoFondoFijo);
                                cmdMov.Parameters.AddWithValue("@u", SesionUsuario.NombreUsuario ?? "");
                                movId = Convert.ToInt32(cmdMov.ExecuteScalar());
                            }

                            using (var cmdAp = new SqlCommand(@"
INSERT INTO AperturasCaja (Fecha,MontoFondoFijo,Observaciones,Usuario,MovimientoID)
VALUES (@f,@m,@obs,@u,@mid)", c, tr))
                            {
                                cmdAp.Parameters.AddWithValue("@f", DateTime.Now);
                                cmdAp.Parameters.AddWithValue("@m", montoFondoFijo);
                                cmdAp.Parameters.AddWithValue("@obs", string.IsNullOrWhiteSpace(observaciones) ? (object)DBNull.Value : observaciones.Trim());
                                cmdAp.Parameters.AddWithValue("@u", SesionUsuario.NombreUsuario ?? "");
                                cmdAp.Parameters.AddWithValue("@mid", movId > 0 ? (object)movId : DBNull.Value);
                                cmdAp.ExecuteNonQuery();
                            }

                            tr.Commit();
                            return true;
                        }
                        catch (Exception ex)
                        {
                            try { tr.Rollback(); } catch { }
                            NotificarError(ex.Message);
                            return false;
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                NotificarError("AbrirCaja: " + ex.Message);
                return false;
            }
        }

        /// <summary>Resumen del día para cierre: fondo fijo (apertura), ingresos/egresos sin contar el fondo fijo.</summary>
        public static void GetResumenCajaDelDia(out decimal fondoFijo, out decimal ingresos, out decimal egresos, out decimal saldoCierre)
        {
            fondoFijo = 0; ingresos = 0; egresos = 0; saldoCierre = GetSaldoCaja();
            var ap = GetAperturaCajaHoy();
            if (ap != null && ap["MontoFondoFijo"] != DBNull.Value)
                fondoFijo = Convert.ToDecimal(ap["MontoFondoFijo"]);

            var dt = GetMovimientosCaja(DateTime.Today);
            foreach (DataRow r in dt.Rows)
            {
                decimal m = Convert.ToDecimal(r["Monto"]);
                string tipo = r["Tipo"]?.ToString() ?? "";
                string concepto = r["Concepto"]?.ToString() ?? "";
                if (tipo == "Ingreso")
                {
                    if (!string.Equals(concepto, ConceptoFondoFijo, StringComparison.OrdinalIgnoreCase))
                        ingresos += m;
                }
                else if (tipo == "Egreso")
                    egresos += m;
            }
        }

        /// <param name="estadoFiltro">Todos, Cobrada o Pendiente.</param>
        public static DataTable GetFacturasEstadoCobranza(DateTime desde, DateTime hasta, string estadoFiltro = "Todos")
        {
            var dt = new DataTable();
            try
            {
                using (var c = new SqlConnection(_connectionString))
                {
                    c.Open();
                    AsegurarColumnaCondicionTicketFacturas(c);
                    string filtroEstado = "";
                    if (string.Equals(estadoFiltro, "Cobrada", StringComparison.OrdinalIgnoreCase))
                        filtroEstado = " AND EstadoCobro = N'Cobrada'";
                    else if (string.Equals(estadoFiltro, "Pendiente", StringComparison.OrdinalIgnoreCase))
                        filtroEstado = " AND EstadoCobro = N'Pendiente'";

                    string sql = $@"
SELECT * FROM (
  SELECT f.FacturaID,
         f.Fecha,
         ISNULL(cl.RazonSocial, N'Consumidor Final') AS Cliente,
         f.TipoComprobante,
         f.Total,
         ISNULL(f.NombrePersonal, N'') AS NombrePersonal,
         f.CondicionVenta,
         CASE
           WHEN f.CondicionVenta LIKE N'%Corriente%'
             OR f.CondicionVenta LIKE N'%Cta%'
             OR f.CondicionVenta LIKE N'%cte%'
             THEN N'Pendiente'
           WHEN EXISTS (SELECT 1 FROM FacturasCobranza fc WHERE fc.FacturaID = f.FacturaID)
             THEN N'Cobrada'
           ELSE N'Pendiente'
         END AS EstadoCobro,
         ISNULL(NULLIF(LTRIM(RTRIM(f.CondicionTicket)), N''),
           (SELECT STUFF((
             SELECT N', ' + fc.NombreMedio + N' ' + FORMAT(fc.Monto, N'C', N'es-AR')
             FROM FacturasCobranza fc
             WHERE fc.FacturaID = f.FacturaID
             FOR XML PATH(''), TYPE).value('.', 'NVARCHAR(MAX)'), 1, 2, N''))
         ) AS MediosCobro,
         ISNULL((
           SELECT SUM(fc.Monto) FROM FacturasCobranza fc WHERE fc.FacturaID = f.FacturaID
         ), 0) AS MontoCobrado
  FROM Facturas f
  LEFT JOIN Clientes cl ON f.ClienteID = cl.ClienteID
  WHERE f.Fecha >= @d AND f.Fecha < DATEADD(day, 1, CAST(@h AS DATE))
) q
WHERE 1=1{filtroEstado}
ORDER BY Fecha DESC";
                    using (var cmd = new SqlCommand(sql, c))
                    {
                        cmd.Parameters.AddWithValue("@d", desde.Date);
                        cmd.Parameters.AddWithValue("@h", hasta.Date);
                        new SqlDataAdapter(cmd).Fill(dt);
                    }
                }
            }
            catch (Exception ex) { NotificarError("GetFacturasEstadoCobranza: " + ex.Message); }
            return dt;
        }

        /// <summary>Guarda presupuesto y devuelve PresupuestoID; 0 si falla.</summary>
        public static int GuardarPresupuesto(int cid, decimal t, List<FacturaItem> i)
        {
            using (var c = new SqlConnection(_connectionString))
            {
                c.Open();
                using (var tr = c.BeginTransaction())
                {
                    try
                    {
                        SqlCommand cmd = new SqlCommand("INSERT INTO Presupuestos (ClienteID,Fecha,Total,Estado) VALUES (@cid,@f,@t,'Pendiente'); SELECT SCOPE_IDENTITY();", c, tr);
                        cmd.Parameters.AddWithValue("@cid", cid);
                        cmd.Parameters.AddWithValue("@f", DateTime.Now);
                        cmd.Parameters.AddWithValue("@t", t);
                        int pid = Convert.ToInt32(cmd.ExecuteScalar());

                        foreach (var it in i)
                        {
                            SqlCommand det = new SqlCommand("INSERT INTO PresupuestoDetalle (PresupuestoID,ProductoID,Cantidad,PrecioUnitario) VALUES (@pid,@prod,@cant,@pu)", c, tr);
                            det.Parameters.AddWithValue("@pid", pid);
                            det.Parameters.AddWithValue("@prod", it.ProductoID);
                            det.Parameters.AddWithValue("@cant", it.Cantidad);
                            det.Parameters.AddWithValue("@pu", it.PrecioUnitario);
                            det.ExecuteNonQuery();
                        }
                        tr.Commit();
                        return pid;
                    }
                    catch { tr.Rollback(); return 0; }
                }
            }
        }

        public static DataTable GetPresupuestos(DateTime d, DateTime h)
        {
            var dt = new DataTable();
            try
            {
                using (var c = new SqlConnection(_connectionString))
                {
                    c.Open();
                    var cmd = new SqlCommand("SELECT p.PresupuestoID, p.Fecha, cl.RazonSocial, p.Total, p.Estado FROM Presupuestos p JOIN Clientes cl ON p.ClienteID=cl.ClienteID WHERE p.Fecha BETWEEN @d AND @h ORDER BY p.Fecha DESC", c);
                    cmd.Parameters.AddWithValue("@d", d);
                    cmd.Parameters.AddWithValue("@h", h);
                    new SqlDataAdapter(cmd).Fill(dt);
                }
            }
            catch { }
            return dt;
        }

        public static bool TryCargarPresupuestoParaVenta(int presupuestoId, out int clienteId, out string estado, out List<FacturaItem> items)
        {
            clienteId = 0;
            estado = "";
            items = new List<FacturaItem>();
            try
            {
                using (var c = new SqlConnection(_connectionString))
                {
                    c.Open();
                    using (var cmd = new SqlCommand("SELECT ClienteID, ISNULL(Estado, N'') AS Estado FROM Presupuestos WHERE PresupuestoID=@id", c))
                    {
                        cmd.Parameters.AddWithValue("@id", presupuestoId);
                        using (var rd = cmd.ExecuteReader())
                        {
                            if (!rd.Read()) return false;
                            clienteId = rd["ClienteID"] != DBNull.Value ? Convert.ToInt32(rd["ClienteID"]) : 0;
                            estado = rd["Estado"]?.ToString() ?? "";
                        }
                    }
                    using (var cmd = new SqlCommand(@"
SELECT pd.ProductoID, pd.Cantidad, pd.PrecioUnitario,
       ISNULL(p.Codigo, N'') AS Codigo,
       ISNULL(p.Descripcion, N'') AS Descripcion
FROM PresupuestoDetalle pd
LEFT JOIN Productos p ON p.ProductoID = pd.ProductoID
WHERE pd.PresupuestoID=@id", c))
                    {
                        cmd.Parameters.AddWithValue("@id", presupuestoId);
                        using (var rd = cmd.ExecuteReader())
                        {
                            while (rd.Read())
                            {
                                items.Add(new FacturaItem
                                {
                                    ProductoID = rd["ProductoID"] != DBNull.Value ? Convert.ToInt32(rd["ProductoID"]) : 0,
                                    Codigo = rd["Codigo"]?.ToString() ?? "",
                                    Descripcion = rd["Descripcion"]?.ToString() ?? "",
                                    Cantidad = rd["Cantidad"] != DBNull.Value ? Convert.ToInt32(rd["Cantidad"]) : 1,
                                    PrecioUnitario = rd["PrecioUnitario"] != DBNull.Value ? Convert.ToDecimal(rd["PrecioUnitario"]) : 0m
                                });
                            }
                        }
                    }
                }
                return items.Count > 0;
            }
            catch { return false; }
        }

        public static bool MarcarPresupuestoConvertido(int presupuestoId)
        {
            try
            {
                using (var c = new SqlConnection(_connectionString))
                {
                    c.Open();
                    using (var cmd = new SqlCommand(
                        "UPDATE Presupuestos SET Estado=N'Convertido' WHERE PresupuestoID=@id", c))
                    {
                        cmd.Parameters.AddWithValue("@id", presupuestoId);
                        return cmd.ExecuteNonQuery() > 0;
                    }
                }
            }
            catch { return false; }
        }

        public static DataTable GetPresupuestoDetalle(int pid)
        {
            var dt = new DataTable();
            try
            {
                using (var c = new SqlConnection(_connectionString))
                {
                    c.Open();
                    new SqlDataAdapter($"SELECT p.Codigo,p.Descripcion,pd.Cantidad,pd.PrecioUnitario,(pd.Cantidad*pd.PrecioUnitario) as Subtotal FROM PresupuestoDetalle pd JOIN Productos p ON pd.ProductoID=p.ProductoID WHERE pd.PresupuestoID={pid}", c).Fill(dt);
                }
            }
            catch { }
            return dt;
        }

        public static bool EliminarPresupuesto(int pid)
        {
            try
            {
                using (var c = new SqlConnection(_connectionString))
                {
                    c.Open();
                    new SqlCommand($"DELETE FROM PresupuestoDetalle WHERE PresupuestoID={pid}", c).ExecuteNonQuery();
                    new SqlCommand($"DELETE FROM Presupuestos WHERE PresupuestoID={pid}", c).ExecuteNonQuery();
                    return true;
                }
            }
            catch { return false; }
        }

        public static bool RegistrarPagoCliente(int cid, decimal m)
        {
            using (var c = new SqlConnection(_connectionString))
            {
                c.Open();
                using (var t = c.BeginTransaction())
                {
                    try
                    {
                        new SqlCommand($"UPDATE Clientes SET SaldoDeuda=SaldoDeuda-{(double)m} WHERE ClienteID={cid}", c, t).ExecuteNonQuery();
                        object s = new SqlCommand($"SELECT SaldoDeuda FROM Clientes WHERE ClienteID={cid}", c, t).ExecuteScalar();

                        SqlCommand cmd = new SqlCommand("INSERT INTO MovimientosCuentaCorriente (ClienteID,Fecha,Descripcion,Monto,SaldoHistorico) VALUES (@cid,@f,'Pago a Cuenta',@m,@s)", c, t);
                        cmd.Parameters.AddWithValue("@cid", cid);
                        cmd.Parameters.AddWithValue("@f", DateTime.Now);
                        cmd.Parameters.AddWithValue("@m", m * -1);
                        cmd.Parameters.AddWithValue("@s", s);
                        cmd.ExecuteNonQuery();

                        SqlCommand caja = new SqlCommand("INSERT INTO MovimientosCaja (Fecha,Concepto,Tipo,Monto,Usuario) VALUES (@f,@con,'Ingreso',@m,@u)", c, t);
                        caja.Parameters.AddWithValue("@f", DateTime.Now);
                        caja.Parameters.AddWithValue("@con", $"Cobro Cta Cte #{cid}");
                        caja.Parameters.AddWithValue("@m", m);
                        caja.Parameters.AddWithValue("@u", SesionUsuario.NombreUsuario);
                        caja.ExecuteNonQuery();

                        t.Commit();
                        return true;
                    }
                    catch (Exception e) { NotificarError(e.Message); return false; }
                }
            }
        }

        public static bool RegistrarPagoProveedor(int pid, decimal m)
        {
            using (var c = new SqlConnection(_connectionString))
            {
                c.Open();
                using (var t = c.BeginTransaction())
                {
                    try
                    {
                        new SqlCommand($"UPDATE Proveedores SET SaldoDeuda=SaldoDeuda-{(double)m} WHERE ProveedorID={pid}", c, t).ExecuteNonQuery();
                        object s = new SqlCommand($"SELECT SaldoDeuda FROM Proveedores WHERE ProveedorID={pid}", c, t).ExecuteScalar();

                        SqlCommand cmd = new SqlCommand("INSERT INTO MovimientosCuentaCorriente (ProveedorID,Fecha,Descripcion,Monto,SaldoHistorico) VALUES (@pid,@f,'Pago a Proveedor',@m,@s)", c, t);
                        cmd.Parameters.AddWithValue("@pid", pid);
                        cmd.Parameters.AddWithValue("@f", DateTime.Now);
                        cmd.Parameters.AddWithValue("@m", m * -1);
                        cmd.Parameters.AddWithValue("@s", s);
                        cmd.ExecuteNonQuery();

                        SqlCommand caja = new SqlCommand("INSERT INTO MovimientosCaja (Fecha,Concepto,Tipo,Monto,Usuario) VALUES (@f,@con,'Egreso',@m,@u)", c, t);
                        caja.Parameters.AddWithValue("@f", DateTime.Now);
                        caja.Parameters.AddWithValue("@con", $"Pago Cta Cte #{pid}");
                        caja.Parameters.AddWithValue("@m", m);
                        caja.Parameters.AddWithValue("@u", SesionUsuario.NombreUsuario);
                        caja.ExecuteNonQuery();

                        t.Commit();
                        return true;
                    }
                    catch (Exception e) { NotificarError(e.Message); return false; }
                }
            }
        }

        public static DataTable GetMovimientosCC(int? cid, int? pid)
        {
            var dt = new DataTable();
            try
            {
                using (var c = new SqlConnection(_connectionString))
                {
                    c.Open();
                    string w = cid.HasValue ? $"ClienteID={cid}" : $"ProveedorID={pid}";
                    new SqlDataAdapter($"SELECT Fecha,Descripcion,Monto,SaldoHistorico FROM MovimientosCuentaCorriente WHERE {w} ORDER BY Fecha DESC", c).Fill(dt);
                }
            }
            catch { }
            return dt;
        }

        // --- Compras ---
        public static DataTable GetCompras(string filtro = "")
        {
            var dt = new DataTable();
            try
            {
                using (var c = new SqlConnection(_connectionString))
                {
                    c.Open();
                    string sql = string.IsNullOrWhiteSpace(filtro)
                        ? @"SELECT c.CompraID, c.Fecha, c.Total, c.TipoComprobante,
                                   ISNULL(p.RazonSocial,'(Sin proveedor)') AS Proveedor, c.ProveedorID
                            FROM Compras c LEFT JOIN Proveedores p ON c.ProveedorID=p.ProveedorID
                            ORDER BY c.Fecha DESC"
                        : @"SELECT c.CompraID, c.Fecha, c.Total, c.TipoComprobante,
                                   ISNULL(p.RazonSocial,'(Sin proveedor)') AS Proveedor, c.ProveedorID
                            FROM Compras c LEFT JOIN Proveedores p ON c.ProveedorID=p.ProveedorID
                            WHERE p.RazonSocial LIKE @f OR c.TipoComprobante LIKE @f
                            ORDER BY c.Fecha DESC";
                    var da = new SqlDataAdapter(sql, c);
                    if (!string.IsNullOrWhiteSpace(filtro)) da.SelectCommand.Parameters.AddWithValue("@f", "%" + filtro + "%");
                    da.Fill(dt);
                }
            }
            catch { }
            return dt;
        }

        public static bool EliminarCompra(int id)
        {
            try { using (var c = new SqlConnection(_connectionString)) { c.Open(); new SqlCommand($"DELETE FROM Compras WHERE CompraID={id}", c).ExecuteNonQuery(); return true; } } catch { return false; }
        }

        public static DataTable GetRecepcionesCompra(string filtro = "")
        {
            var dt = new DataTable();
            try
            {
                using (var c = new SqlConnection(_connectionString))
                {
                    c.Open();
                    string sql = string.IsNullOrWhiteSpace(filtro)
                        ? @"SELECT r.RecepcionID, r.Fecha, r.Estado, r.Observaciones, r.CompraID,
                                   ISNULL(p.RazonSocial,'(Sin proveedor)') AS Proveedor, r.ProveedorID
                            FROM RecepcionesCompra r LEFT JOIN Proveedores p ON r.ProveedorID=p.ProveedorID
                            ORDER BY r.Fecha DESC"
                        : @"SELECT r.RecepcionID, r.Fecha, r.Estado, r.Observaciones, r.CompraID,
                                   ISNULL(p.RazonSocial,'(Sin proveedor)') AS Proveedor, r.ProveedorID
                            FROM RecepcionesCompra r LEFT JOIN Proveedores p ON r.ProveedorID=p.ProveedorID
                            WHERE p.RazonSocial LIKE @f OR r.Observaciones LIKE @f
                            ORDER BY r.Fecha DESC";
                    var da = new SqlDataAdapter(sql, c);
                    if (!string.IsNullOrWhiteSpace(filtro)) da.SelectCommand.Parameters.AddWithValue("@f", "%" + filtro + "%");
                    da.Fill(dt);
                }
            }
            catch { }
            return dt;
        }

        public static bool EliminarRecepcionCompra(int id)
        {
            try { using (var c = new SqlConnection(_connectionString)) { c.Open(); new SqlCommand($"DELETE FROM RecepcionesCompra WHERE RecepcionID={id}", c).ExecuteNonQuery(); return true; } } catch { return false; }
        }

        public static DataTable GetNotasCreditoDebitoCompras(string filtro = "")
        {
            var dt = new DataTable();
            try
            {
                using (var c = new SqlConnection(_connectionString))
                {
                    c.Open();
                    string sql = string.IsNullOrWhiteSpace(filtro)
                        ? @"SELECT n.NotaID, n.Tipo, n.Fecha, n.Monto, n.Descripcion, n.NumeroComprobante,
                                   ISNULL(p.RazonSocial,'(Sin proveedor)') AS Proveedor, n.ProveedorID
                            FROM NotasCreditoDebitoCompras n LEFT JOIN Proveedores p ON n.ProveedorID=p.ProveedorID
                            ORDER BY n.Fecha DESC"
                        : @"SELECT n.NotaID, n.Tipo, n.Fecha, n.Monto, n.Descripcion, n.NumeroComprobante,
                                   ISNULL(p.RazonSocial,'(Sin proveedor)') AS Proveedor, n.ProveedorID
                            FROM NotasCreditoDebitoCompras n LEFT JOIN Proveedores p ON n.ProveedorID=p.ProveedorID
                            WHERE p.RazonSocial LIKE @f OR n.Descripcion LIKE @f
                            ORDER BY n.Fecha DESC";
                    var da = new SqlDataAdapter(sql, c);
                    if (!string.IsNullOrWhiteSpace(filtro)) da.SelectCommand.Parameters.AddWithValue("@f", "%" + filtro + "%");
                    da.Fill(dt);
                }
            }
            catch { }
            return dt;
        }

        public static bool EliminarNotaCreditoDebitoCompra(int id)
        {
            try { using (var c = new SqlConnection(_connectionString)) { c.Open(); new SqlCommand($"DELETE FROM NotasCreditoDebitoCompras WHERE NotaID={id}", c).ExecuteNonQuery(); return true; } } catch { return false; }
        }

        public static DataTable GetGastosRapidos(string filtro = "")
        {
            var dt = new DataTable();
            try
            {
                using (var c = new SqlConnection(_connectionString))
                {
                    c.Open();
                    string sql = string.IsNullOrWhiteSpace(filtro)
                        ? "SELECT * FROM GastosRapidos ORDER BY Fecha DESC"
                        : "SELECT * FROM GastosRapidos WHERE Concepto LIKE @f ORDER BY Fecha DESC";
                    var da = new SqlDataAdapter(sql, c);
                    if (!string.IsNullOrWhiteSpace(filtro)) da.SelectCommand.Parameters.AddWithValue("@f", "%" + filtro + "%");
                    da.Fill(dt);
                }
            }
            catch { }
            return dt;
        }

        public static bool EliminarGastoRapido(int id)
        {
            try { using (var c = new SqlConnection(_connectionString)) { c.Open(); new SqlCommand($"DELETE FROM GastosRapidos WHERE GastoID={id}", c).ExecuteNonQuery(); return true; } } catch { return false; }
        }

        public static DataTable GetPagosProveedores(string filtro = "")
        {
            var dt = new DataTable();
            try
            {
                using (var c = new SqlConnection(_connectionString))
                {
                    c.Open();
                    string sql = string.IsNullOrWhiteSpace(filtro)
                        ? @"SELECT pp.PagoID, pp.Fecha, pp.Monto, pp.MedioPago AS FormaPago, pp.Concepto, pp.NumeroComprobante,
                                   ISNULL(p.RazonSocial,'(Sin proveedor)') AS Proveedor, pp.ProveedorID
                            FROM PagosProveedores pp LEFT JOIN Proveedores p ON pp.ProveedorID=p.ProveedorID
                            ORDER BY pp.Fecha DESC"
                        : @"SELECT pp.PagoID, pp.Fecha, pp.Monto, pp.MedioPago AS FormaPago, pp.Concepto, pp.NumeroComprobante,
                                   ISNULL(p.RazonSocial,'(Sin proveedor)') AS Proveedor, pp.ProveedorID
                            FROM PagosProveedores pp LEFT JOIN Proveedores p ON pp.ProveedorID=p.ProveedorID
                            WHERE p.RazonSocial LIKE @f OR pp.Concepto LIKE @f
                            ORDER BY pp.Fecha DESC";
                    var da = new SqlDataAdapter(sql, c);
                    if (!string.IsNullOrWhiteSpace(filtro)) da.SelectCommand.Parameters.AddWithValue("@f", "%" + filtro + "%");
                    da.Fill(dt);
                }
            }
            catch { }
            return dt;
        }

        public static bool EliminarPagoProveedor(int id)
        {
            try { using (var c = new SqlConnection(_connectionString)) { c.Open(); new SqlCommand($"DELETE FROM PagosProveedores WHERE PagoID={id}", c).ExecuteNonQuery(); return true; } } catch { return false; }
        }

        public static DataTable GetOrdenesCompra(string filtro = "")
        {
            var dt = new DataTable();
            try
            {
                using (var c = new SqlConnection(_connectionString))
                {
                    c.Open();
                    string sql = string.IsNullOrWhiteSpace(filtro)
                        ? @"SELECT oc.OrdenID AS OrdenCompraID, oc.Fecha, oc.FechaEntrega, oc.Estado, oc.Total, oc.Observaciones,
                                   ISNULL(p.RazonSocial,'(Sin proveedor)') AS Proveedor, oc.ProveedorID
                            FROM OrdenCompra oc LEFT JOIN Proveedores p ON oc.ProveedorID=p.ProveedorID
                            ORDER BY oc.Fecha DESC"
                        : @"SELECT oc.OrdenID AS OrdenCompraID, oc.Fecha, oc.FechaEntrega, oc.Estado, oc.Total, oc.Observaciones,
                                   ISNULL(p.RazonSocial,'(Sin proveedor)') AS Proveedor, oc.ProveedorID
                            FROM OrdenCompra oc LEFT JOIN Proveedores p ON oc.ProveedorID=p.ProveedorID
                            WHERE p.RazonSocial LIKE @f OR oc.Estado LIKE @f
                            ORDER BY oc.Fecha DESC";
                    var da = new SqlDataAdapter(sql, c);
                    if (!string.IsNullOrWhiteSpace(filtro)) da.SelectCommand.Parameters.AddWithValue("@f", "%" + filtro + "%");
                    da.Fill(dt);
                }
            }
            catch { }
            return dt;
        }

        public static DataTable GetOrdenCompraDetalle(int ordenId)
        {
            var dt = new DataTable();
            try { using (var c = new SqlConnection(_connectionString)) { c.Open(); new SqlDataAdapter($"SELECT * FROM OrdenCompraDetalle WHERE OrdenID={ordenId}", c).Fill(dt); } } catch { }
            return dt;
        }

        public static bool EliminarOrdenCompra(int id)
        {
            try { using (var c = new SqlConnection(_connectionString)) { c.Open(); new SqlCommand($"DELETE FROM OrdenCompraDetalle WHERE OrdenID={id}; DELETE FROM OrdenCompra WHERE OrdenID={id}", c).ExecuteNonQuery(); return true; } } catch { return false; }
        }

        public static int GuardarRecepcionCompra(int recepcionId, int proveedorId, int? compraId, string estado, string observaciones, List<(int ProductoID, int CantEsperada, int CantRecibida, decimal Costo)> items)
        {
            try
            {
                using (var c = new SqlConnection(_connectionString))
                {
                    c.Open();
                    using (var tx = c.BeginTransaction())
                    {
                        int id = recepcionId;
                        if (recepcionId == 0)
                        {
                            string ins = "INSERT INTO RecepcionesCompra (ProveedorID,CompraID,Fecha,Estado,Observaciones) OUTPUT INSERTED.RecepcionID VALUES (@pid,@cid,@f,@est,@obs)";
                            using (var cmd = new SqlCommand(ins, c, tx))
                            {
                                cmd.Parameters.AddWithValue("@pid", proveedorId);
                                cmd.Parameters.AddWithValue("@cid", (object)compraId ?? DBNull.Value);
                                cmd.Parameters.AddWithValue("@f", DateTime.Now);
                                cmd.Parameters.AddWithValue("@est", estado ?? "Recibido");
                                cmd.Parameters.AddWithValue("@obs", observaciones ?? "");
                                id = (int)cmd.ExecuteScalar();
                            }
                        }
                        else
                        {
                            string upd = "UPDATE RecepcionesCompra SET ProveedorID=@pid,CompraID=@cid,Estado=@est,Observaciones=@obs WHERE RecepcionID=@id";
                            using (var cmd = new SqlCommand(upd, c, tx))
                            {
                                cmd.Parameters.AddWithValue("@pid", proveedorId);
                                cmd.Parameters.AddWithValue("@cid", (object)compraId ?? DBNull.Value);
                                cmd.Parameters.AddWithValue("@est", estado ?? "Recibido");
                                cmd.Parameters.AddWithValue("@obs", observaciones ?? "");
                                cmd.Parameters.AddWithValue("@id", recepcionId);
                                cmd.ExecuteNonQuery();
                            }
                            new SqlCommand($"DELETE FROM RecepcionCompraDetalle WHERE RecepcionID={id}", c, tx).ExecuteNonQuery();
                        }
                        foreach (var item in items)
                        {
                            string insD = "INSERT INTO RecepcionCompraDetalle (RecepcionID,ProductoID,CantidadEsperada,CantidadRecibida,PrecioCosto) VALUES (@rid,@pid,@ce,@cr,@pc)";
                            using (var cmd = new SqlCommand(insD, c, tx))
                            {
                                cmd.Parameters.AddWithValue("@rid", id);
                                cmd.Parameters.AddWithValue("@pid", item.ProductoID);
                                cmd.Parameters.AddWithValue("@ce", item.CantEsperada);
                                cmd.Parameters.AddWithValue("@cr", item.CantRecibida);
                                cmd.Parameters.AddWithValue("@pc", item.Costo);
                                cmd.ExecuteNonQuery();
                            }
                            // Ajustar stock por la cantidad recibida
                            if (item.CantRecibida > 0)
                                AjustarStock(item.ProductoID, item.CantRecibida, "Recepción compra", c, tx);
                        }
                        tx.Commit();
                        return id;
                    }
                }
            }
            catch { return -1; }
        }

        public static bool GuardarNotaCreditoDebitoCompra(int notaId, int proveedorId, string tipo, decimal monto, string descripcion, string nroComprobante)
        {
            try
            {
                using (var c = new SqlConnection(_connectionString))
                {
                    c.Open();
                    string sql = notaId == 0
                        ? "INSERT INTO NotasCreditoDebitoCompras (ProveedorID,Tipo,Fecha,Monto,Descripcion,NumeroComprobante) VALUES (@pid,@t,@f,@m,@d,@nc)"
                        : "UPDATE NotasCreditoDebitoCompras SET ProveedorID=@pid,Tipo=@t,Monto=@m,Descripcion=@d,NumeroComprobante=@nc WHERE NotaID=@id";
                    using (var cmd = new SqlCommand(sql, c))
                    {
                        cmd.Parameters.AddWithValue("@pid", proveedorId);
                        cmd.Parameters.AddWithValue("@t", tipo ?? "NC");
                        cmd.Parameters.AddWithValue("@f", DateTime.Now);
                        cmd.Parameters.AddWithValue("@m", monto);
                        cmd.Parameters.AddWithValue("@d", descripcion ?? "");
                        cmd.Parameters.AddWithValue("@nc", nroComprobante ?? "");
                        if (notaId > 0) cmd.Parameters.AddWithValue("@id", notaId);
                        cmd.ExecuteNonQuery();
                        return true;
                    }
                }
            }
            catch { return false; }
        }

        public static bool GuardarGastoRapido(int gastoId, string concepto, string categoria, decimal monto, string medioPago)
        {
            try
            {
                using (var c = new SqlConnection(_connectionString))
                {
                    c.Open();
                    using (var tx = c.BeginTransaction())
                    {
                        string sql = gastoId == 0
                            ? "INSERT INTO GastosRapidos (Fecha,Concepto,Categoria,Monto,MedioPago,Usuario) VALUES (@f,@con,@cat,@m,@mp,@u)"
                            : "UPDATE GastosRapidos SET Concepto=@con,Categoria=@cat,Monto=@m,MedioPago=@mp WHERE GastoID=@id";
                        using (var cmd = new SqlCommand(sql, c, tx))
                        {
                            cmd.Parameters.AddWithValue("@f", DateTime.Now);
                            cmd.Parameters.AddWithValue("@con", concepto ?? "");
                            cmd.Parameters.AddWithValue("@cat", categoria ?? "");
                            cmd.Parameters.AddWithValue("@m", monto);
                            cmd.Parameters.AddWithValue("@mp", medioPago ?? "Efectivo");
                            cmd.Parameters.AddWithValue("@u", SesionUsuario.NombreUsuario ?? "");
                            if (gastoId > 0) cmd.Parameters.AddWithValue("@id", gastoId);
                            cmd.ExecuteNonQuery();
                        }
                        // Registrar en caja como egreso
                        if (gastoId == 0)
                            RegistrarMovimientoCaja(concepto ?? "Gasto rápido", "Egreso", monto, c, tx);
                        tx.Commit();
                        return true;
                    }
                }
            }
            catch { return false; }
        }

        public static bool GuardarPagoProveedor(int pagoId, int proveedorId, decimal monto, string medioPago, string concepto, string nroComprobante)
        {
            try
            {
                using (var c = new SqlConnection(_connectionString))
                {
                    c.Open();
                    using (var tx = c.BeginTransaction())
                    {
                        string sql = pagoId == 0
                            ? "INSERT INTO PagosProveedores (ProveedorID,Fecha,Monto,MedioPago,Concepto,NumeroComprobante) VALUES (@pid,@f,@m,@mp,@con,@nc)"
                            : "UPDATE PagosProveedores SET ProveedorID=@pid,Monto=@m,MedioPago=@mp,Concepto=@con,NumeroComprobante=@nc WHERE PagoID=@id";
                        using (var cmd = new SqlCommand(sql, c, tx))
                        {
                            cmd.Parameters.AddWithValue("@pid", proveedorId);
                            cmd.Parameters.AddWithValue("@f", DateTime.Now);
                            cmd.Parameters.AddWithValue("@m", monto);
                            cmd.Parameters.AddWithValue("@mp", medioPago ?? "Efectivo");
                            cmd.Parameters.AddWithValue("@con", concepto ?? "Pago a proveedor");
                            cmd.Parameters.AddWithValue("@nc", nroComprobante ?? "");
                            if (pagoId > 0) cmd.Parameters.AddWithValue("@id", pagoId);
                            cmd.ExecuteNonQuery();
                        }
                        // Actualizar saldo proveedor
                        if (pagoId == 0)
                        {
                            new SqlCommand($"UPDATE Proveedores SET SaldoDeuda = SaldoDeuda - {monto} WHERE ProveedorID={proveedorId}", c, tx).ExecuteNonQuery();
                            // Registrar movimiento cta cte
                            new SqlCommand($"INSERT INTO MovimientosCuentaCorriente (ProveedorID,Fecha,Descripcion,Monto,SaldoHistorico) VALUES ({proveedorId},GETDATE(),'{concepto ?? "Pago"}',-{monto},(SELECT ISNULL(SaldoDeuda,0) FROM Proveedores WHERE ProveedorID={proveedorId}))", c, tx).ExecuteNonQuery();
                            // Registrar en caja como egreso
                            RegistrarMovimientoCaja(concepto ?? "Pago proveedor", "Egreso", monto, c, tx);
                        }
                        tx.Commit();
                        return true;
                    }
                }
            }
            catch { return false; }
        }

        public static int GuardarOrdenCompra(int ordenId, int proveedorId, DateTime? fechaEntrega, string observaciones, List<(int ProductoID, int Cantidad, decimal Costo)> items)
        {
            try
            {
                using (var c = new SqlConnection(_connectionString))
                {
                    c.Open();
                    using (var tx = c.BeginTransaction())
                    {
                        decimal total = 0;
                        foreach (var it in items) total += it.Cantidad * it.Costo;
                        int id = ordenId;
                        if (ordenId == 0)
                        {
                            string ins = "INSERT INTO OrdenCompra (ProveedorID,Fecha,FechaEntrega,Estado,Observaciones,Total) OUTPUT INSERTED.OrdenID VALUES (@pid,@f,@fe,'Pendiente',@obs,@t)";
                            using (var cmd = new SqlCommand(ins, c, tx))
                            {
                                cmd.Parameters.AddWithValue("@pid", proveedorId);
                                cmd.Parameters.AddWithValue("@f", DateTime.Now);
                                cmd.Parameters.AddWithValue("@fe", (object)fechaEntrega ?? DBNull.Value);
                                cmd.Parameters.AddWithValue("@obs", observaciones ?? "");
                                cmd.Parameters.AddWithValue("@t", total);
                                id = (int)cmd.ExecuteScalar();
                            }
                        }
                        else
                        {
                            string upd = "UPDATE OrdenCompra SET ProveedorID=@pid,FechaEntrega=@fe,Observaciones=@obs,Total=@t WHERE OrdenID=@id";
                            using (var cmd = new SqlCommand(upd, c, tx))
                            {
                                cmd.Parameters.AddWithValue("@pid", proveedorId);
                                cmd.Parameters.AddWithValue("@fe", (object)fechaEntrega ?? DBNull.Value);
                                cmd.Parameters.AddWithValue("@obs", observaciones ?? "");
                                cmd.Parameters.AddWithValue("@t", total);
                                cmd.Parameters.AddWithValue("@id", ordenId);
                                cmd.ExecuteNonQuery();
                            }
                            new SqlCommand($"DELETE FROM OrdenCompraDetalle WHERE OrdenID={id}", c, tx).ExecuteNonQuery();
                        }
                        foreach (var item in items)
                        {
                            using (var cmd = new SqlCommand("INSERT INTO OrdenCompraDetalle (OrdenID,ProductoID,Cantidad,PrecioCosto) VALUES (@oid,@pid,@cant,@pc)", c, tx))
                            {
                                cmd.Parameters.AddWithValue("@oid", id);
                                cmd.Parameters.AddWithValue("@pid", item.ProductoID);
                                cmd.Parameters.AddWithValue("@cant", item.Cantidad);
                                cmd.Parameters.AddWithValue("@pc", item.Costo);
                                cmd.ExecuteNonQuery();
                            }
                        }
                        tx.Commit();
                        return id;
                    }
                }
            }
            catch { return -1; }
        }

        public static DataTable GetOrdenCompraDetalleFull(int ordenId)
        {
            var dt = new DataTable();
            try
            {
                using (var c = new SqlConnection(_connectionString))
                {
                    c.Open();
                    new SqlDataAdapter($@"SELECT od.DetalleID, od.ProductoID, ISNULL(p.Descripcion,'') AS Descripcion,
                                                ISNULL(p.Codigo,'') AS Codigo, od.Cantidad, od.PrecioCosto,
                                                od.Cantidad * od.PrecioCosto AS Subtotal
                                         FROM OrdenCompraDetalle od LEFT JOIN Productos p ON od.ProductoID=p.ProductoID
                                         WHERE od.OrdenID={ordenId}", c).Fill(dt);
                }
            }
            catch { }
            return dt;
        }

        public static DataTable GetRecepcionCompraDetalle(int recepcionId)
        {
            var dt = new DataTable();
            try
            {
                using (var c = new SqlConnection(_connectionString))
                {
                    c.Open();
                    new SqlDataAdapter($@"SELECT rd.DetalleID, rd.ProductoID, ISNULL(p.Descripcion,'') AS Descripcion,
                                                ISNULL(p.Codigo,'') AS Codigo, rd.CantidadEsperada, rd.CantidadRecibida, rd.PrecioCosto
                                         FROM RecepcionCompraDetalle rd LEFT JOIN Productos p ON rd.ProductoID=p.ProductoID
                                         WHERE rd.RecepcionID={recepcionId}", c).Fill(dt);
                }
            }
            catch { }
            return dt;
        }

        // --- Configuración de red ---
        public static System.Collections.Generic.Dictionary<string, string> GetDatosConexionActual()
        {
            var result = new System.Collections.Generic.Dictionary<string, string>
            {
                ["Servidor"] = "127.0.0.1",
                ["Puerto"] = "1433",
                ["ServidorCompleto"] = "127.0.0.1",
                ["Usuario"] = "",
                ["Password"] = "",
                ["UsaIntegrado"] = "0"
            };
            try
            {
                var builder = new Microsoft.Data.SqlClient.SqlConnectionStringBuilder(_connectionString);
                string dataSource = builder.DataSource ?? "127.0.0.1";
                result["ServidorCompleto"] = dataSource;

                // Parsear IP/servidor y puerto desde DataSource.
                // Formatos posibles: "SERVIDOR", "SERVIDOR\INSTANCIA", "IP,PUERTO", "IP\INSTANCIA,PUERTO"
                string servidor = dataSource;
                string puerto   = "1433";

                // Puerto separado por coma (ej. "192.168.1.5,1434")
                int comaIdx = dataSource.LastIndexOf(',');
                if (comaIdx >= 0)
                {
                    servidor = dataSource.Substring(0, comaIdx).Trim();
                    puerto   = dataSource.Substring(comaIdx + 1).Trim();
                }

                result["Servidor"] = servidor;
                result["Puerto"]   = puerto;
                result["Usuario"]  = builder.UserID ?? "";
                result["Password"] = builder.Password ?? "";
                result["UsaIntegrado"] = builder.IntegratedSecurity ? "1" : "0";
            }
            catch { }
            return result;
        }

        public static bool GuardarNuevaConexion(string servidor, string puerto, bool integrado, string usuario, string password)
        {
            try
            {
                // Siempre incluir el puerto si se especificó (incluso con instancia nombrada,
                // ej. "192.168.1.5\SQLEXPRESS,1433"): así el driver conecta directo por TCP sin
                // depender del servicio "SQL Server Browser" (UDP 1434) para resolver el puerto.
                string ds = string.IsNullOrWhiteSpace(puerto) ? servidor : $"{servidor},{puerto}";
                string cs = integrado
                    ? $"Server={ds};Database=SchPosDB;Integrated Security=True;Encrypt=False;TrustServerCertificate=True;"
                    : $"Server={ds};Database=SchPosDB;User Id={usuario};Password={password};Encrypt=False;TrustServerCertificate=True;";

                // Fuente de verdad: conexion.cfg (leído en cada arranque por ObtenerConnectionString)
                ActualizarConexion(cs);

                // Secundario: actualizar App.config para compatibilidad (puede fallar en instalaciones con permisos restringidos)
                try
                {
                    var config = System.Configuration.ConfigurationManager.OpenExeConfiguration(System.Configuration.ConfigurationUserLevel.None);
                    if (config.ConnectionStrings.ConnectionStrings["SchPosDB"] != null)
                    {
                        config.ConnectionStrings.ConnectionStrings["SchPosDB"].ConnectionString = cs;
                        config.Save(System.Configuration.ConfigurationSaveMode.Modified);
                        System.Configuration.ConfigurationManager.RefreshSection("connectionStrings");
                    }
                }
                catch { /* App.config es solo backup; el fallo no es crítico */ }

                return true;
            }
            catch { return false; }
        }

        // --- Configuración del negocio (columnas coherentes con App.xaml.sql y MigracionLite: PasswordAfip, MPAccessToken…) ---
        /// <param name="conservarPasswordAfipSiContraseniaVacia">Si es true y <paramref name="certPassword"/> viene vacío, no se escribe la columna (mantiene el valor en BD).</param>
        public static bool GuardarConfiguracion(string nombreFantasia, string razonSocial, string cuit, string direccion, string telefono,
            string email, string logoPath, string certPath, string certPassword, int puntoVenta,
            string mpToken, string mpUserId, string mpPosId, bool habilitarMP, decimal? tipoCambioUSD,
            bool afipProduccion = false,
            bool conservarPasswordAfipSiContraseniaVacia = false,
            bool logoEnTicket = true, bool logoEnA4 = true,
            bool usaAperturaCaja = false,
            string condicionIVAEmpresa = "",
            string mpPointTerminalId = "",
            bool mpPointAutomatico = false,
            string mpQrModo = "ambos")
        {
            certPassword = certPassword ?? "";
            bool omitirColumnaPwd = conservarPasswordAfipSiContraseniaVacia && string.IsNullOrWhiteSpace(certPassword);

            try
            {
                using (var c = new SqlConnection(_connectionString))
                {
                    c.Open();
                    ForzarContextoBD(c);
                    AsegurarMigracionLite(c);

                    string updSql = omitirColumnaPwd
                        ? @"UPDATE Configuracion SET
  NombreFantasia=@nf, RazonSocial=@rs, CUIT=@cuit, Direccion=@dir, Telefono=@tel, Email=@email,
  LogoPath=@logo, CertificadoPath=@cert, PuntoVenta=@pv,
  MPAccessToken=@mpt, MPUserId=@mpu, MPPosId=@mpp, MPPointTerminalId=@mptid, MPPointAutomatico=@mpauto, MPQrModo=@mqr, TipoCambioUSD=@tc, AfipProduccion=@afip,
  LogoEnTicket=@let, LogoEnA4=@lea, UsaAperturaCaja=@uac, CondicionIVAEmpresa=@civa
WHERE ID = 1"
                        : @"UPDATE Configuracion SET
  NombreFantasia=@nf, RazonSocial=@rs, CUIT=@cuit, Direccion=@dir, Telefono=@tel, Email=@email,
  LogoPath=@logo, CertificadoPath=@cert, PasswordAfip=@pwd, PuntoVenta=@pv,
  MPAccessToken=@mpt, MPUserId=@mpu, MPPosId=@mpp, MPPointTerminalId=@mptid, MPPointAutomatico=@mpauto, MPQrModo=@mqr, TipoCambioUSD=@tc, AfipProduccion=@afip,
  LogoEnTicket=@let, LogoEnA4=@lea, UsaAperturaCaja=@uac, CondicionIVAEmpresa=@civa
WHERE ID = 1";

                    using (var update = new SqlCommand(updSql, c))
                    {
                        update.Parameters.AddWithValue("@nf", nombreFantasia ?? "");
                        update.Parameters.AddWithValue("@rs", razonSocial ?? "");
                        update.Parameters.AddWithValue("@cuit", cuit ?? "");
                        update.Parameters.AddWithValue("@dir", direccion ?? "");
                        update.Parameters.AddWithValue("@tel", telefono ?? "");
                        update.Parameters.AddWithValue("@email", email ?? "");
                        update.Parameters.AddWithValue("@logo", logoPath ?? "");
                        update.Parameters.AddWithValue("@cert", certPath ?? "");
                        if (!omitirColumnaPwd)
                            update.Parameters.AddWithValue("@pwd", AfipCertPasswordDpapi.Encode(certPassword));
                        update.Parameters.AddWithValue("@pv", puntoVenta);
                        update.Parameters.AddWithValue("@mpt", mpToken ?? "");
                        update.Parameters.AddWithValue("@mpu", mpUserId ?? "");
                        update.Parameters.AddWithValue("@mpp", mpPosId ?? "");
                        update.Parameters.AddWithValue("@mptid", mpPointTerminalId ?? "");
                        update.Parameters.AddWithValue("@mpauto", mpPointAutomatico);
                        update.Parameters.AddWithValue("@mqr", NormalizarModoQrMp(mpQrModo));
                        update.Parameters.AddWithValue("@tc", (object)tipoCambioUSD ?? DBNull.Value);
                        update.Parameters.AddWithValue("@afip", afipProduccion);
                        update.Parameters.AddWithValue("@let", logoEnTicket);
                        update.Parameters.AddWithValue("@lea", logoEnA4);
                        update.Parameters.AddWithValue("@uac", usaAperturaCaja);
                        update.Parameters.AddWithValue("@civa", string.IsNullOrWhiteSpace(condicionIVAEmpresa) ? (object)DBNull.Value : condicionIVAEmpresa.Trim());
                        int n = update.ExecuteNonQuery();
                        if (n > 0) return true;
                    }

                    string pwdInsert = omitirColumnaPwd ? "" : AfipCertPasswordDpapi.Encode(certPassword);
                    using (var insert = new SqlCommand(@"
INSERT INTO Configuracion (
  NombreFantasia,RazonSocial,CUIT,Direccion,Telefono,Email,LogoPath,CertificadoPath,PasswordAfip,PuntoVenta,
  MPAccessToken,MPUserId,MPPosId,MPPointTerminalId,MPPointAutomatico,MPQrModo,TipoCambioUSD,AfipProduccion,LogoEnTicket,LogoEnA4,UsaAperturaCaja,CondicionIVAEmpresa
) VALUES (
  @nf,@rs,@cuit,@dir,@tel,@email,@logo,@cert,@pwd,@pv,@mpt,@mpu,@mpp,@mptid,@mpauto,@mqr,@tc,@afip,@let,@lea,@uac,@civa)", c))
                    {
                        insert.Parameters.AddWithValue("@nf", nombreFantasia ?? "");
                        insert.Parameters.AddWithValue("@rs", razonSocial ?? "");
                        insert.Parameters.AddWithValue("@cuit", cuit ?? "");
                        insert.Parameters.AddWithValue("@dir", direccion ?? "");
                        insert.Parameters.AddWithValue("@tel", telefono ?? "");
                        insert.Parameters.AddWithValue("@email", email ?? "");
                        insert.Parameters.AddWithValue("@logo", logoPath ?? "");
                        insert.Parameters.AddWithValue("@cert", certPath ?? "");
                        insert.Parameters.AddWithValue("@pwd", pwdInsert);
                        insert.Parameters.AddWithValue("@pv", puntoVenta);
                        insert.Parameters.AddWithValue("@mpt", mpToken ?? "");
                        insert.Parameters.AddWithValue("@mpu", mpUserId ?? "");
                        insert.Parameters.AddWithValue("@mpp", mpPosId ?? "");
                        insert.Parameters.AddWithValue("@mptid", mpPointTerminalId ?? "");
                        insert.Parameters.AddWithValue("@mpauto", mpPointAutomatico);
                        insert.Parameters.AddWithValue("@mqr", NormalizarModoQrMp(mpQrModo));
                        insert.Parameters.AddWithValue("@tc", (object)tipoCambioUSD ?? DBNull.Value);
                        insert.Parameters.AddWithValue("@afip", afipProduccion);
                        insert.Parameters.AddWithValue("@let", logoEnTicket);
                        insert.Parameters.AddWithValue("@lea", logoEnA4);
                        insert.Parameters.AddWithValue("@uac", usaAperturaCaja);
                        insert.Parameters.AddWithValue("@civa", string.IsNullOrWhiteSpace(condicionIVAEmpresa) ? (object)DBNull.Value : condicionIVAEmpresa.Trim());
                        insert.ExecuteNonQuery();
                        return true;
                    }
                }
            }
            catch { return false; }
        }

        public static class ModosQrMercadoPago
        {
            public const string Pantalla = "pantalla";
            public const string Impreso = "impreso";
            public const string Ambos = "ambos";
        }

        public static string NormalizarModoQrMp(string modo)
        {
            string m = (modo ?? "").Trim().ToLowerInvariant();
            if (m == ModosQrMercadoPago.Pantalla || m == ModosQrMercadoPago.Impreso)
                return m;
            return ModosQrMercadoPago.Ambos;
        }

        public static string ObtenerModoQrMercadoPago()
        {
            try
            {
                var dr = GetConfiguracion();
                if (dr == null || !dr.Table.Columns.Contains("MPQrModo"))
                    return ModosQrMercadoPago.Ambos;
                return NormalizarModoQrMp(dr["MPQrModo"]?.ToString());
            }
            catch { return ModosQrMercadoPago.Ambos; }
        }

        /// <summary>Persiste rutas del par .key / .crt generado por el asistente de activación ARCA.</summary>
        public static bool GuardarRutasActivacionAfip(string clavePrivadaPath, string certificadoPath)
        {
            try
            {
                using (var c = new SqlConnection(_connectionString))
                {
                    c.Open();
                    ForzarContextoBD(c);
                    AsegurarMigracionLite(c);

                    using (var cmd = new SqlCommand(@"
UPDATE Configuracion SET
  AfipClavePrivadaPath = COALESCE(@key, AfipClavePrivadaPath),
  CertificadoPath = COALESCE(@cert, CertificadoPath)
WHERE ID = 1", c))
                    {
                        cmd.Parameters.AddWithValue("@key",
                            string.IsNullOrWhiteSpace(clavePrivadaPath) ? (object)DBNull.Value : clavePrivadaPath.Trim());
                        cmd.Parameters.AddWithValue("@cert",
                            string.IsNullOrWhiteSpace(certificadoPath) ? (object)DBNull.Value : certificadoPath.Trim());
                        return cmd.ExecuteNonQuery() > 0;
                    }
                }
            }
            catch (Exception ex)
            {
                NotificarError(ex.Message);
                return false;
            }
        }

        /// <summary>Ruta de la clave privada (.key) del asistente ARCA, o vacío.</summary>
        public static string ObtenerAfipClavePrivadaPath()
        {
            try
            {
                var dr = GetConfiguracion();
                if (dr == null || !dr.Table.Columns.Contains("AfipClavePrivadaPath")) return "";
                return dr["AfipClavePrivadaPath"]?.ToString() ?? "";
            }
            catch { return ""; }
        }

        /// <summary>True = ambiente WSFE producción ARCA.</summary>
        public static bool GetAfipAmbienteProduccion()
        {
            try
            {
                var dr = GetConfiguracion();
                if (dr == null) return false;
                if (!dr.Table.Columns.Contains("AfipProduccion")) return false;
                if (dr["AfipProduccion"] == DBNull.Value || dr["AfipProduccion"] == null) return false;
                return Convert.ToBoolean(dr["AfipProduccion"]);
            }
            catch { return false; }
        }

        /// <summary>Interpreta texto de tipo IVA guardado en productos (ej. «21», «10,5», «Exento»).</summary>
        public static decimal ObtenerPctIvaPorTipoProducto(object tipoIvaCampo)
        {
            if (tipoIvaCampo == null || tipoIvaCampo == DBNull.Value) return 21m;
            string raw = tipoIvaCampo.ToString();
            if (string.IsNullOrWhiteSpace(raw)) return 21m;
            string t = raw.Trim().ToUpperInvariant();
            if (t.Contains("EXE") || t == "0" || t.Contains("NO GRAVA")) return 0m;
            Match m = Regex.Match(raw.Replace(',', '.'), @"(\d+(?:\.\d+)?)");
            return m.Success && decimal.TryParse(m.Value, NumberStyles.Any, CultureInfo.InvariantCulture, out decimal p) ? p : 21m;
        }

        /// <summary>Alícuota IVA a aplicar en venta según configuración del producto.</summary>
        public static decimal ObtenerAlicuotaIvaVentaProducto(DataRow producto)
        {
            if (producto == null) return 21m;
            if (producto.Table.Columns.Contains("CobraIvaAlCliente")
                && producto["CobraIvaAlCliente"] != DBNull.Value
                && !Convert.ToBoolean(producto["CobraIvaAlCliente"]))
                return 0m;
            return ObtenerPctIvaPorTipoProducto(producto.Table.Columns.Contains("TipoIVA") ? producto["TipoIVA"] : null);
        }

        public static bool ProductoCobraIvaAlCliente(DataRow producto)
        {
            if (producto == null) return true;
            if (!producto.Table.Columns.Contains("CobraIvaAlCliente") || producto["CobraIvaAlCliente"] == DBNull.Value)
                return true;
            return Convert.ToBoolean(producto["CobraIvaAlCliente"]);
        }

        // --- Clientes ---
        public static DataRow BuscarClientePorID(int id)
        {
            try
            {
                using (var c = new SqlConnection(_connectionString))
                {
                    c.Open();
                    var dt = new DataTable();
                    new SqlDataAdapter($"SELECT * FROM Clientes WHERE ClienteID={id}", c).Fill(dt);
                    return dt.Rows.Count > 0 ? dt.Rows[0] : null;
                }
            }
            catch { return null; }
        }

        // --- Medios de pago ---
        private static void AsegurarColumnasMediosPago(SqlConnection c)
        {
            try
            {
                using (var q1 = new SqlCommand(@"
                    IF OBJECT_ID(N'MediosPago', N'U') IS NOT NULL
                      AND NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID(N'MediosPago') AND name = N'Tipo')
                    ALTER TABLE MediosPago ADD Tipo NVARCHAR(30) NULL;", c))
                    q1.ExecuteNonQuery();

                using (var q2 = new SqlCommand(@"
                    IF OBJECT_ID(N'MediosPago', N'U') IS NOT NULL
                      AND NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID(N'MediosPago') AND name = N'RecargoDescuentoPct')
                    ALTER TABLE MediosPago ADD RecargoDescuentoPct DECIMAL(9,4) NULL;", c))
                    q2.ExecuteNonQuery();
            }
            catch { }
        }

        public static DataTable GetMediosPagoCompleto()
        {
            var dt = new DataTable();
            try
            {
                using (var c = new SqlConnection(_connectionString))
                {
                    c.Open();
                    AsegurarColumnasMediosPago(c);
                    new SqlDataAdapter(@"
                        SELECT MedioID, Nombre, Activo, Orden,
                               ISNULL(NULLIF(LTRIM(RTRIM(Tipo)), ''), N'Efectivo') AS Tipo,
                               ISNULL(RecargoDescuentoPct, 0) AS RecargoDescuentoPct
                        FROM MediosPago ORDER BY Orden", c).Fill(dt);
                }
            }
            catch
            {
                try { using (var c = new SqlConnection(_connectionString)) { c.Open(); new SqlDataAdapter("SELECT MedioID, Nombre, Activo, Orden FROM MediosPago ORDER BY Orden", c).Fill(dt); } } catch { }
            }
            return dt;
        }

        public static bool GuardarMedioPago(int id, string nombre, bool activo, int orden, string tipo, decimal recargoDescuentoPct)
        {
            try
            {
                using (var c = new SqlConnection(_connectionString))
                {
                    c.Open();
                    AsegurarColumnasMediosPago(c);
                    string sql = id > 0
                        ? @"UPDATE MediosPago SET Nombre=@n, Activo=@a, Orden=@o, Tipo=@t, RecargoDescuentoPct=@p WHERE MedioID=@id"
                        : @"INSERT INTO MediosPago (Nombre, Activo, Orden, Tipo, RecargoDescuentoPct) VALUES (@n, @a, @o, @t, @p)";
                    var cmd = new SqlCommand(sql, c);
                    cmd.Parameters.AddWithValue("@n", nombre ?? "");
                    cmd.Parameters.AddWithValue("@a", activo);
                    cmd.Parameters.AddWithValue("@o", orden);
                    cmd.Parameters.AddWithValue("@t", string.IsNullOrWhiteSpace(tipo) ? "Efectivo" : tipo.Trim());
                    cmd.Parameters.AddWithValue("@p", recargoDescuentoPct);
                    if (id > 0) cmd.Parameters.AddWithValue("@id", id);
                    cmd.ExecuteNonQuery();
                    return true;
                }
            }
            catch { return false; }
        }

        public static bool GuardarMedioPago(int id, string nombre, bool activo, int orden)
        {
            return GuardarMedioPago(id, nombre, activo, orden, "Efectivo", 0m);
        }

        // --- Licencia ---
        /// <summary>
        /// Crea la tabla Configuracion y su fila base si no existen.
        /// Se llama de forma defensiva desde GuardarNuevaLicencia para soportar
        /// instalaciones donde el esquema todavía no fue inicializado.
        /// </summary>
        private static void AsegurarTablaConfiguracion(SqlConnection c)
        {
            try
            {
                new SqlCommand(@"
IF NOT EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_NAME = 'Configuracion')
BEGIN
    CREATE TABLE Configuracion (
        ID              INT             IDENTITY(1,1) PRIMARY KEY,
        LicenciaPayload NVARCHAR(MAX)   NULL,
        NombreFantasia  NVARCHAR(200)   NULL,
        RazonSocial     NVARCHAR(200)   NULL,
        CUIT            NVARCHAR(50)    NULL,
        Direccion       NVARCHAR(200)   NULL,
        Telefono        NVARCHAR(50)    NULL,
        Email           NVARCHAR(100)   NULL,
        LogoPath        NVARCHAR(MAX)   NULL,
        CertificadoPath NVARCHAR(MAX)   NULL,
        PasswordAfip    NVARCHAR(MAX)   NULL,
        PuntoVenta      INT             NULL,
        MPAccessToken   NVARCHAR(MAX)   NULL,
        MPUserId        NVARCHAR(MAX)   NULL,
        MPPosId         NVARCHAR(MAX)   NULL,
        MPPointTerminalId NVARCHAR(150) NULL,
        MPPointAutomatico BIT           NOT NULL DEFAULT 0,
        AfipProduccion  BIT             NOT NULL DEFAULT 0,
        UsaVisorCliente BIT             NOT NULL DEFAULT 0
    );
    INSERT INTO Configuracion (NombreFantasia) VALUES ('Mi Negocio');
END", c).ExecuteNonQuery();
            }
            catch { }
        }

        private static void AsegurarColumnaLicenciaPayload(SqlConnection c)
        {
            try
            {
                new SqlCommand(@"
IF NOT EXISTS (
    SELECT 1 FROM INFORMATION_SCHEMA.COLUMNS
    WHERE TABLE_NAME = 'Configuracion' AND COLUMN_NAME = 'LicenciaPayload')
    ALTER TABLE Configuracion ADD LicenciaPayload NVARCHAR(MAX) NULL;", c).ExecuteNonQuery();
            }
            catch { }
        }

        public static string ObtenerStringLicencia()
        {
            try
            {
                using (var c = new SqlConnection(_connectionString))
                {
                    c.Open();
                    ForzarContextoBD(c);
                    AsegurarColumnaLicenciaPayload(c);
                    var cmd = new SqlCommand("SELECT TOP 1 LicenciaPayload FROM Configuracion WHERE ID=1", c);
                    var r = cmd.ExecuteScalar();
                    if (r != null && r != DBNull.Value)
                        return r.ToString();
                }
            }
            catch { }
            return "";
        }

        public static bool GuardarNuevaLicencia(string key)
        {
            if (string.IsNullOrWhiteSpace(key))
                return false;

            key = key.Trim();
            try
            {
                using (var c = new SqlConnection(_connectionString))
                {
                    c.Open();
                    ForzarContextoBD(c);               // garantiza contexto SchPosDB, no master
                    AsegurarTablaConfiguracion(c);     // crea tabla + fila base si no existen
                    AsegurarColumnaLicenciaPayload(c); // agrega columna si falta en tabla existente

                    // UPSERT atómico: si no existe la fila base la crea con defaults seguros
                    // para todas las columnas requeridas, luego actualiza solo LicenciaPayload.
                    using (var cmd = new SqlCommand(@"
IF NOT EXISTS (SELECT 1 FROM Configuracion WHERE ID = 1)
    INSERT INTO Configuracion (
        ID, NombreFantasia, RazonSocial, CUIT, Direccion, Telefono,
        Email, LogoPath, CertificadoPath, PasswordAfip, PuntoVenta,
        MPAccessToken, MPUserId, MPPosId, AfipProduccion, LicenciaPayload
    ) VALUES (
        1, 'Mi Negocio', '', '', '', '',
        '', '', '', '', 1,
        '', '', '', 0, @v
    );
UPDATE Configuracion SET LicenciaPayload = @v WHERE ID = 1;", c))
                    {
                        cmd.Parameters.AddWithValue("@v", key);
                        cmd.ExecuteNonQuery();
                    }
                }

                LicenseFileHelper.GuardarClave(key);
                LicenseManager.InvalidarCache();
                return true;
            }
            catch (Exception ex)
            {
                throw new Exception("GuardarNuevaLicencia SQL: " + ex.Message, ex);
            }
        }

        // --- Listas de precio por producto ---
        public static System.Collections.Generic.List<int> GetProductoListas(int productoId)
        {
            var list = new System.Collections.Generic.List<int>();
            try
            {
                using (var c = new SqlConnection(_connectionString))
                {
                    c.Open();
                    AsegurarMigracionLite(c);
                    var dt = new DataTable();
                    new SqlDataAdapter($"SELECT ListaID FROM ProductosListas WHERE ProductoID={productoId}", c).Fill(dt);
                    foreach (DataRow r in dt.Rows) list.Add(Convert.ToInt32(r[0]));
                }
            }
            catch { }
            return list;
        }

        public class ProductoListaAsignacion
        {
            public int ListaID { get; set; }
            public decimal? PrecioFijo { get; set; }
        }

        public static System.Collections.Generic.Dictionary<int, decimal?> GetProductoListasDetalle(int productoId)
        {
            var map = new System.Collections.Generic.Dictionary<int, decimal?>();
            try
            {
                using (var c = new SqlConnection(_connectionString))
                {
                    c.Open();
                    AsegurarMigracionLite(c);
                    var dt = new DataTable();
                    new SqlDataAdapter($"SELECT ListaID, PrecioFijo FROM ProductosListas WHERE ProductoID={productoId}", c).Fill(dt);
                    foreach (DataRow r in dt.Rows)
                    {
                        int lid = Convert.ToInt32(r["ListaID"]);
                        decimal? pf = r["PrecioFijo"] != DBNull.Value ? (decimal?)Convert.ToDecimal(r["PrecioFijo"]) : null;
                        map[lid] = pf;
                    }
                }
            }
            catch { }
            return map;
        }

        public static void GuardarProductoListas(int productoId, System.Collections.Generic.List<int> listaIds)
        {
            var items = new System.Collections.Generic.List<ProductoListaAsignacion>();
            if (listaIds != null)
                foreach (var lid in listaIds)
                    items.Add(new ProductoListaAsignacion { ListaID = lid });
            GuardarProductoListas(productoId, items);
        }

        public static void GuardarProductoListas(int productoId, System.Collections.Generic.List<ProductoListaAsignacion> asignaciones)
        {
            try
            {
                using (var c = new SqlConnection(_connectionString))
                {
                    c.Open();
                    AsegurarMigracionLite(c);
                    new SqlCommand($"DELETE FROM ProductosListas WHERE ProductoID={productoId}", c).ExecuteNonQuery();
                    if (asignaciones == null) return;
                    foreach (var a in asignaciones)
                    {
                        using (var cmd = new SqlCommand(
                            "INSERT INTO ProductosListas (ProductoID, ListaID, PrecioFijo) VALUES (@pid, @lid, @pf)", c))
                        {
                            cmd.Parameters.AddWithValue("@pid", productoId);
                            cmd.Parameters.AddWithValue("@lid", a.ListaID);
                            cmd.Parameters.AddWithValue("@pf", (object)a.PrecioFijo ?? DBNull.Value);
                            cmd.ExecuteNonQuery();
                        }
                    }
                }
            }
            catch { }
        }

        public class ProductoComboItem
        {
            public int ProductoComponenteID { get; set; }
            public decimal Cantidad { get; set; }
        }

        public static System.Collections.Generic.List<ProductoComboItem> GetProductoComboDetalle(int productoId)
        {
            var list = new System.Collections.Generic.List<ProductoComboItem>();
            try
            {
                using (var c = new SqlConnection(_connectionString))
                {
                    c.Open();
                    AsegurarMigracionLite(c);
                    var dt = new DataTable();
                    new SqlDataAdapter($@"
SELECT ComponenteID AS ProductoComponenteID, Cantidad
FROM ProductoComboDetalle
WHERE ProductoID={productoId}
UNION
SELECT ProductoComponenteID, Cantidad
FROM ProductoCombos
WHERE ProductoPadreID={productoId}", c).Fill(dt);
                    foreach (DataRow r in dt.Rows)
                        list.Add(new ProductoComboItem { ProductoComponenteID = Convert.ToInt32(r[0]), Cantidad = Convert.ToDecimal(r[1]) });
                }
            }
            catch { }
            return list;
        }

        public static void GuardarProductoComboDetalle(int productoId, System.Collections.Generic.List<(int componenteId, int cantidad)> componentes)
        {
            try
            {
                using (var c = new SqlConnection(_connectionString))
                {
                    c.Open();
                    AsegurarMigracionLite(c);
                    new SqlCommand($"DELETE FROM ProductoComboDetalle WHERE ProductoID={productoId}", c).ExecuteNonQuery();
                    new SqlCommand($"DELETE FROM ProductoCombos WHERE ProductoPadreID={productoId}", c).ExecuteNonQuery();
                    foreach (var (componenteId, cantidad) in componentes)
                    {
                        var cmd = new SqlCommand("INSERT INTO ProductoComboDetalle (ProductoID,ComponenteID,Cantidad) VALUES (@pid,@cid,@cant)", c);
                        cmd.Parameters.AddWithValue("@pid", productoId);
                        cmd.Parameters.AddWithValue("@cid", componenteId);
                        cmd.Parameters.AddWithValue("@cant", cantidad);
                        cmd.ExecuteNonQuery();
                        var cmdCompat = new SqlCommand("INSERT INTO ProductoCombos (ProductoPadreID,ProductoComponenteID,Cantidad) VALUES (@pid,@cid,@cant)", c);
                        cmdCompat.Parameters.AddWithValue("@pid", productoId);
                        cmdCompat.Parameters.AddWithValue("@cid", componenteId);
                        cmdCompat.Parameters.AddWithValue("@cant", cantidad);
                        cmdCompat.ExecuteNonQuery();
                    }
                }
            }
            catch { }
        }

        public static decimal? GetTipoCambioUSD()
        {
            try
            {
                using (var c = new SqlConnection(_connectionString))
                {
                    c.Open();
                    var r = new SqlCommand("SELECT TOP 1 TipoCambioUSD FROM Configuracion WHERE ID=1", c).ExecuteScalar();
                    if (r != null && r != DBNull.Value) return Convert.ToDecimal(r);
                }
            }
            catch { }
            return null;
        }

        private const string SqlCamposClienteJoin =
            "ISNULL(c.RazonSocial,'Consumidor Final') AS ClienteNombre, " +
            "ISNULL(c.CUIT,'-') AS ClienteCUIT, " +
            "ISNULL(c.CondicionIVA,'-') AS ClienteIVA, " +
            "ISNULL(c.Direccion,'-') AS ClienteDireccion";

        // --- Presupuestos (para PrintService) ---
        public static DataRow GetPresupuestoPorID(int id)
        {
            try
            {
                using (var c = new SqlConnection(_connectionString))
                {
                    c.Open();
                    var dt = new DataTable();
                    var cmd = new SqlCommand(
                        $"SELECT p.*, {SqlCamposClienteJoin} FROM Presupuestos p LEFT JOIN Clientes c ON p.ClienteID=c.ClienteID WHERE p.PresupuestoID=@id", c);
                    cmd.Parameters.AddWithValue("@id", id);
                    new SqlDataAdapter(cmd).Fill(dt);
                    return dt.Rows.Count > 0 ? dt.Rows[0] : null;
                }
            }
            catch { return null; }
        }

        public static int GuardarRemito(int cid, List<FacturaItem> items, string observaciones = null)
        {
            using (var c = new SqlConnection(_connectionString))
            {
                c.Open();
                using (var tr = c.BeginTransaction())
                {
                    try
                    {
                        foreach (var it in items)
                        {
                            if (it.ProductoID <= 0) continue;
                            var politica = ObtenerPoliticaStockProducto(it.ProductoID, c, tr);
                            if (!politica.ExigeStockSuficiente) continue;

                            using (var chk = new SqlCommand(
                                "SELECT ISNULL(StockActual,0) FROM Productos WHERE ProductoID=@pid", c, tr))
                            {
                                chk.Parameters.AddWithValue("@pid", it.ProductoID);
                                var stockObj = chk.ExecuteScalar();
                                int stockActual = stockObj == null || stockObj == DBNull.Value ? 0 : Convert.ToInt32(stockObj);
                                if (stockActual < it.Cantidad)
                                    throw new InvalidOperationException(
                                        $"Stock insuficiente para '{it.Descripcion}': disponible {stockActual}, requerido {it.Cantidad}.");
                            }
                        }

                        var cmd = new SqlCommand(
                            "INSERT INTO Remitos (ClienteID,FacturaID,Fecha,Estado,Observaciones) VALUES (@cid,NULL,@f,'Emitido',@o); SELECT SCOPE_IDENTITY();", c, tr);
                        cmd.Parameters.AddWithValue("@cid", cid);
                        cmd.Parameters.AddWithValue("@f", DateTime.Now);
                        cmd.Parameters.AddWithValue("@o", (object)observaciones ?? DBNull.Value);
                        int rid = Convert.ToInt32(cmd.ExecuteScalar());
                        var fecha = DateTime.Now;
                        foreach (var it in items)
                        {
                            var det = new SqlCommand(
                                "INSERT INTO RemitoDetalle (RemitoID,ProductoID,Cantidad,PrecioUnitario) VALUES (@rid,@prod,@cant,@pu)", c, tr);
                            det.Parameters.AddWithValue("@rid", rid);
                            det.Parameters.AddWithValue("@prod", it.ProductoID);
                            det.Parameters.AddWithValue("@cant", it.Cantidad);
                            det.Parameters.AddWithValue("@pu", it.PrecioUnitario);
                            det.ExecuteNonQuery();

                            if (it.ProductoID > 0)
                                DescontarStockVenta(c, tr, it, fecha, 0, "Remito");
                        }
                        tr.Commit();
                        return rid;
                    }
                    catch (InvalidOperationException)
                    {
                        try { tr.Rollback(); } catch { }
                        throw;
                    }
                    catch (Exception ex)
                    {
                        try { tr.Rollback(); } catch { }
                        NotificarError("GuardarRemito: " + ex.Message);
                        return 0;
                    }
                }
            }
        }

        public static DataRow GetRemitoPorID(int id)
        {
            try
            {
                using (var c = new SqlConnection(_connectionString))
                {
                    c.Open();
                    var dt = new DataTable();
                    var cmd = new SqlCommand(
                        $"SELECT r.*, {SqlCamposClienteJoin} FROM Remitos r LEFT JOIN Clientes c ON r.ClienteID=c.ClienteID WHERE r.RemitoID=@id", c);
                    cmd.Parameters.AddWithValue("@id", id);
                    new SqlDataAdapter(cmd).Fill(dt);
                    return dt.Rows.Count > 0 ? dt.Rows[0] : null;
                }
            }
            catch { return null; }
        }

        public static DataTable GetRemitoDetalle(int rid)
        {
            var dt = new DataTable();
            try
            {
                using (var c = new SqlConnection(_connectionString))
                {
                    c.Open();
                    new SqlDataAdapter(
                        $"SELECT p.Codigo,p.Descripcion,rd.Cantidad,rd.PrecioUnitario,(rd.Cantidad*rd.PrecioUnitario) AS Subtotal FROM RemitoDetalle rd JOIN Productos p ON rd.ProductoID=p.ProductoID WHERE rd.RemitoID={rid}", c).Fill(dt);
                }
            }
            catch { }
            return dt;
        }

        public static int GuardarPedido(int cid, decimal total, List<FacturaItem> items, DateTime? fechaEntrega = null, string observaciones = null)
        {
            using (var c = new SqlConnection(_connectionString))
            {
                c.Open();
                using (var tr = c.BeginTransaction())
                {
                    try
                    {
                        var cmd = new SqlCommand(
                            "INSERT INTO Pedidos (ClienteID,Fecha,FechaEntrega,Estado,Total,Observaciones) VALUES (@cid,@f,@fe,'Pendiente',@t,@o); SELECT SCOPE_IDENTITY();", c, tr);
                        cmd.Parameters.AddWithValue("@cid", cid);
                        cmd.Parameters.AddWithValue("@f", DateTime.Now);
                        cmd.Parameters.AddWithValue("@fe", (object)fechaEntrega ?? DBNull.Value);
                        cmd.Parameters.AddWithValue("@t", total);
                        cmd.Parameters.AddWithValue("@o", (object)observaciones ?? DBNull.Value);
                        int pid = Convert.ToInt32(cmd.ExecuteScalar());
                        foreach (var it in items)
                        {
                            var det = new SqlCommand(
                                "INSERT INTO PedidoDetalle (PedidoID,ProductoID,Cantidad,PrecioUnitario) VALUES (@pid,@prod,@cant,@pu)", c, tr);
                            det.Parameters.AddWithValue("@pid", pid);
                            det.Parameters.AddWithValue("@prod", it.ProductoID);
                            det.Parameters.AddWithValue("@cant", it.Cantidad);
                            det.Parameters.AddWithValue("@pu", it.PrecioUnitario);
                            det.ExecuteNonQuery();
                        }
                        tr.Commit();
                        return pid;
                    }
                    catch { tr.Rollback(); return 0; }
                }
            }
        }

        public static DataRow GetPedidoPorID(int id)
        {
            try
            {
                using (var c = new SqlConnection(_connectionString))
                {
                    c.Open();
                    var dt = new DataTable();
                    var cmd = new SqlCommand(
                        $"SELECT p.*, {SqlCamposClienteJoin} FROM Pedidos p LEFT JOIN Clientes c ON p.ClienteID=c.ClienteID WHERE p.PedidoID=@id", c);
                    cmd.Parameters.AddWithValue("@id", id);
                    new SqlDataAdapter(cmd).Fill(dt);
                    return dt.Rows.Count > 0 ? dt.Rows[0] : null;
                }
            }
            catch { return null; }
        }

        public static DataTable GetPedidoDetalle(int pid)
        {
            var dt = new DataTable();
            try
            {
                using (var c = new SqlConnection(_connectionString))
                {
                    c.Open();
                    new SqlDataAdapter(
                        $"SELECT pr.Codigo,pr.Descripcion,pd.Cantidad,pd.PrecioUnitario,(pd.Cantidad*pd.PrecioUnitario) AS Subtotal FROM PedidoDetalle pd JOIN Productos pr ON pd.ProductoID=pr.ProductoID WHERE pd.PedidoID={pid}", c).Fill(dt);
                }
            }
            catch { }
            return dt;
        }

        public static int GuardarNotaCreditoDebitoVenta(int cid, string tipo, decimal monto, string descripcion, int? facturaId = null, string numeroComprobante = null, List<NotaCreditoItemDetalle> items = null)
        {
            try
            {
                using (var c = new SqlConnection(_connectionString))
                {
                    c.Open();
                    AsegurarTablaNotaCreditoDebitoVentaDetalle(c);
                    using (var tx = c.BeginTransaction())
                    {
                        var cmd = new SqlCommand(
                            "INSERT INTO NotasCreditoDebitoVentas (ClienteID,FacturaID,Tipo,Fecha,Monto,Descripcion,NumeroComprobante) VALUES (@cid,@fid,@t,@f,@m,@d,@nc); SELECT SCOPE_IDENTITY();", c, tx);
                        cmd.Parameters.AddWithValue("@cid", cid);
                        cmd.Parameters.AddWithValue("@fid", (object)facturaId ?? DBNull.Value);
                        cmd.Parameters.AddWithValue("@t", tipo);
                        cmd.Parameters.AddWithValue("@f", DateTime.Now);
                        cmd.Parameters.AddWithValue("@m", monto);
                        string desc = descripcion ?? "";
                        if (desc.Length > 500) desc = desc.Substring(0, 500);
                        cmd.Parameters.AddWithValue("@d", desc);
                        cmd.Parameters.AddWithValue("@nc", (object)numeroComprobante ?? DBNull.Value);
                        int notaId = Convert.ToInt32(cmd.ExecuteScalar());

                        if (items != null)
                        {
                            foreach (var it in items)
                            {
                                if (it == null || it.Cantidad <= 0) continue;
                                var cmdIt = new SqlCommand(
                                    "INSERT INTO NotaCreditoDebitoVentaDetalle (NotaID,ProductoID,Codigo,Descripcion,Cantidad,PrecioUnitario) VALUES (@nid,@pid,@cod,@desc,@cant,@pu)", c, tx);
                                cmdIt.Parameters.AddWithValue("@nid", notaId);
                                cmdIt.Parameters.AddWithValue("@pid", it.ProductoID > 0 ? (object)it.ProductoID : DBNull.Value);
                                cmdIt.Parameters.AddWithValue("@cod", (object)it.Codigo ?? DBNull.Value);
                                cmdIt.Parameters.AddWithValue("@desc", it.Descripcion ?? "");
                                cmdIt.Parameters.AddWithValue("@cant", it.Cantidad);
                                cmdIt.Parameters.AddWithValue("@pu", it.PrecioUnitario);
                                cmdIt.ExecuteNonQuery();
                            }
                        }

                        tx.Commit();
                        return notaId;
                    }
                }
            }
            catch { return 0; }
        }

        private static void AsegurarTablaNotaCreditoDebitoVentaDetalle(SqlConnection c)
        {
            try
            {
                new SqlCommand(@"
                    IF NOT EXISTS (SELECT * FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_NAME='NotaCreditoDebitoVentaDetalle')
                    CREATE TABLE NotaCreditoDebitoVentaDetalle (
                        DetalleID INT PRIMARY KEY IDENTITY(1,1),
                        NotaID INT NOT NULL,
                        ProductoID INT NULL,
                        Codigo NVARCHAR(50) NULL,
                        Descripcion NVARCHAR(300) NOT NULL,
                        Cantidad DECIMAL(18,2) NOT NULL,
                        PrecioUnitario DECIMAL(18,2) NOT NULL
                    );", c).ExecuteNonQuery();
            }
            catch { }
        }

        public static DataRow GetNotaVentaPorID(int id)
        {
            try
            {
                using (var c = new SqlConnection(_connectionString))
                {
                    c.Open();
                    var dt = new DataTable();
                    var cmd = new SqlCommand(
                        $"SELECT n.*, {SqlCamposClienteJoin} FROM NotasCreditoDebitoVentas n LEFT JOIN Clientes c ON n.ClienteID=c.ClienteID WHERE n.NotaID=@id", c);
                    cmd.Parameters.AddWithValue("@id", id);
                    new SqlDataAdapter(cmd).Fill(dt);
                    return dt.Rows.Count > 0 ? dt.Rows[0] : null;
                }
            }
            catch { return null; }
        }

        public static DataTable GetNotaVentaDetalle(int notaId)
        {
            var dt = new DataTable();
            try
            {
                using (var c = new SqlConnection(_connectionString))
                {
                    c.Open();
                    AsegurarTablaNotaCreditoDebitoVentaDetalle(c);
                    var cmd = new SqlCommand(
                        "SELECT ProductoID, Codigo, Descripcion, Cantidad, PrecioUnitario, (Cantidad*PrecioUnitario) AS Subtotal " +
                        "FROM NotaCreditoDebitoVentaDetalle WHERE NotaID=@id ORDER BY DetalleID", c);
                    cmd.Parameters.AddWithValue("@id", notaId);
                    new SqlDataAdapter(cmd).Fill(dt);
                }
            }
            catch { }
            return dt;
        }

        public class NotaCreditoItemDetalle
        {
            public int ProductoID { get; set; }
            public string Codigo { get; set; }
            public string Descripcion { get; set; }
            public decimal Cantidad { get; set; }
            public decimal PrecioUnitario { get; set; }
        }
    }
}