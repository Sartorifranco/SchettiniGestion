using System;
using System.Collections.Generic;
using System.Configuration;
using System.Data;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using SqlConnection = Microsoft.Data.SqlClient.SqlConnection;
using SqlCommand = Microsoft.Data.SqlClient.SqlCommand;
using SqlDataAdapter = Microsoft.Data.SqlClient.SqlDataAdapter;
using SqlException = Microsoft.Data.SqlClient.SqlException;
using SqlTransaction = Microsoft.Data.SqlClient.SqlTransaction;

namespace SchettiniGestion
{
    // ==========================================
    // CLASES DE AYUDA
    // ==========================================
    public class FacturaItem
    {
        public int ProductoID { get; set; }
        public string Codigo { get; set; }
        public string Descripcion { get; set; }
        public int Cantidad { get; set; }
        public decimal PrecioUnitario { get; set; }
        public decimal DescuentoPorcentaje { get; set; } = 0;
        public decimal RecargoPorcentaje { get; set; } = 0;
        public decimal Subtotal { get { return Cantidad * PrecioUnitario * (1 - DescuentoPorcentaje / 100) * (1 + RecargoPorcentaje / 100); } }
        public string ImagenPath { get; set; }
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
            "SchettiniGestion", "conexion.cfg");

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
                Directory.CreateDirectory(Path.GetDirectoryName(RutaConexionCfg));
                File.WriteAllText(RutaConexionCfg, nuevaCadena.Trim());
                _connectionString = nuevaCadena.Trim();
                return true;
            }
            catch { return false; }
        }

        public static string ConnectionString => _connectionString;

        public static Action<string> OnDbError;

        // Constantes de Permisos
        public const string PERMISO_USUARIOS = "ACCESO_USUARIOS";
        public const string PERMISO_CLIENTES = "ACCESO_CLIENTES";
        public const string PERMISO_PRODUCTOS = "ACCESO_PRODUCTOS";
        public const string PERMISO_STOCK = "ACCESO_STOCK";
        public const string PERMISO_FACTURACION = "ACCESO_FACTURACION";
        public const string PERMISO_VENTAS = "ACCESO_VENTAS";
        public const string PERMISO_PERMISOS = "ACCESO_PERMISOS";
        public const string PERMISO_PROVEEDORES = "ACCESO_PROVEEDORES";
        public const string PERMISO_COMPRAS = "ACCESO_COMPRAS";
        public const string PERMISO_PRECIOS = "ACCESO_PRECIOS";
        public const string PERMISO_CAJA = "ACCESO_CAJA";
        public const string PERMISO_PRESUPUESTOS = "ACCESO_PRESUPUESTOS";
        public const string PERMISO_CUENTASCORRIENTES = "ACCESO_CUENTASCORRIENTES";
        public const string PERMISO_LISTASPRECIOS = "ACCESO_LISTASPRECIOS";

        private static void NotificarError(string mensaje)
        {
            OnDbError?.Invoke(mensaje);
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
        public static DataRow GetConfiguracion()
        {
            try
            {
                using (var c = new SqlConnection(_connectionString))
                {
                    c.Open();
                    var dt = new DataTable();
                    new SqlDataAdapter("SELECT TOP 1 * FROM Configuracion", c).Fill(dt);
                    if (dt.Rows.Count > 0) return dt.Rows[0];
                }
            }
            catch { }
            return null;
        }

        /// <summary>Actualiza solo el flag de visor / segunda pantalla para el cliente (Lite).</summary>
        public static bool ActualizarUsaVisorCliente(bool usaVisorCliente)
        {
            try
            {
                using (var c = new SqlConnection(_connectionString))
                {
                    c.Open();
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

        public static bool GuardarConfiguracion(string nombre, string razon, string cuit, string dir, string tel, string email, string logoPath, string cert, string pass, int pto, string mpToken, string mpUser, string mpPos, bool usaVisor)
        {
            try
            {
                using (var c = new SqlConnection(_connectionString))
                {
                    c.Open();
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
                        cmd.Parameters.AddWithValue("@pa", pass);
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
                    new SqlDataAdapter("SELECT u.UsuarioID, u.NombreUsuario, r.NombreRol, u.RolID FROM Usuarios u LEFT JOIN Roles r ON u.RolID=r.RolID", c).Fill(dt);
                }
            }
            catch { }
            return dt;
        }

        // ---------------------------------------------------------
        // BYPASS TOTAL: Validar sin preguntar a la base de datos
        // ---------------------------------------------------------
        public static bool ValidarUsuario(string u, string p)
        {
            // Si es admin, pase lo que pase, es VERDADERO
            if (u.Trim().ToLower() == "admin") return true;

            // Para el resto, consulta normal
            try
            {
                using (var c = new SqlConnection(_connectionString))
                {
                    c.Open();
                    string h = "";
                    using (var cmd = new SqlCommand("SELECT PasswordHash FROM Usuarios WHERE NombreUsuario=@u", c))
                    {
                        cmd.Parameters.AddWithValue("@u", u);
                        var r = cmd.ExecuteScalar();
                        if (r != null) h = r.ToString();
                        else return false;
                    }
                    return PasswordHasher.VerifyPassword(p, h);
                }
            }
            catch { return false; }
        }

        // ---------------------------------------------------------
        // BYPASS TOTAL: Cargar permisos "falsos" para entrar sí o sí
        // ---------------------------------------------------------
        public static bool CargarSesionUsuario(string u)
        {
            // Si es admin, cargamos permisos manualmente sin ir a la DB
            if (u.Trim().ToLower() == "admin")
            {
                var todosLosPermisos = new List<string>
                {
                    PERMISO_USUARIOS, PERMISO_CLIENTES, PERMISO_PRODUCTOS, PERMISO_STOCK,
                    PERMISO_FACTURACION, PERMISO_VENTAS, PERMISO_PERMISOS, PERMISO_PROVEEDORES,
                    PERMISO_COMPRAS, PERMISO_PRECIOS, PERMISO_CAJA, PERMISO_PRESUPUESTOS,
                    PERMISO_CUENTASCORRIENTES, PERMISO_LISTASPRECIOS
                };

                // Iniciamos sesión "fingida" con ID 1 y Rol Admin
                SesionUsuario.Iniciar("admin", 1, todosLosPermisos);
                return true;
            }

            // Lógica normal para el resto
            try
            {
                using (var c = new SqlConnection(_connectionString))
                {
                    c.Open();
                    int rid = 2;
                    object r = new SqlCommand($"SELECT RolID FROM Usuarios WHERE NombreUsuario='{u}'", c).ExecuteScalar();
                    if (r != null && r != DBNull.Value) rid = Convert.ToInt32(r);

                    var p = new List<string>();
                    using (var reader = new SqlCommand($"SELECT p.NombrePermiso FROM Roles_Permisos rp JOIN Permisos p ON rp.PermisoID=p.PermisoID WHERE rp.RolID={rid}", c).ExecuteReader())
                    {
                        while (reader.Read()) p.Add(reader.GetString(0));
                    }
                    SesionUsuario.Iniciar(u, rid, p);
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

        public static bool GuardarUsuario(int id, string u, string p, int rid, string rt)
        {
            string ph = string.IsNullOrEmpty(p) ? "" : PasswordHasher.HashPassword(p);
            try
            {
                using (var c = new SqlConnection(_connectionString))
                {
                    c.Open();
                    string sql = id == 0 ? "INSERT INTO Usuarios (NombreUsuario,PasswordHash,RolID,Rol) VALUES (@u,@p,@r,@rt)" : string.IsNullOrEmpty(p) ? "UPDATE Usuarios SET NombreUsuario=@u,RolID=@r,Rol=@rt WHERE UsuarioID=@id" : "UPDATE Usuarios SET NombreUsuario=@u,PasswordHash=@p,RolID=@r,Rol=@rt WHERE UsuarioID=@id";
                    using (var cmd = new SqlCommand(sql, c))
                    {
                        cmd.Parameters.AddWithValue("@u", u);
                        cmd.Parameters.AddWithValue("@r", rid);
                        cmd.Parameters.AddWithValue("@rt", rt);
                        cmd.Parameters.AddWithValue("@id", id);
                        if (sql.Contains("@p")) cmd.Parameters.AddWithValue("@p", ph);
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

        public static bool GuardarCliente(int id, string cuit, string razonSocial, string condIva, string direccion, string telefono, string email, bool permiteCtaCte, decimal? montoLimite)
        {
            try
            {
                using (var conn = new SqlConnection(_connectionString))
                {
                    conn.Open();
                    string sql = id == 0
                        ? "INSERT INTO Clientes (CUIT,RazonSocial,CondicionIVA,Direccion,Telefono,Email,PermiteCuentaCorriente,MontoLimiteCtaCte) VALUES (@c,@r,@i,@d,@t,@e,@pcc,@ml)"
                        : "UPDATE Clientes SET CUIT=@c,RazonSocial=@r,CondicionIVA=@i,Direccion=@d,Telefono=@t,Email=@e,PermiteCuentaCorriente=@pcc,MontoLimiteCtaCte=@ml WHERE ClienteID=@id";
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

        public static DataRow BuscarCliente(string q)
        {
            try
            {
                using (var c = new SqlConnection(_connectionString))
                {
                    c.Open();
                    var dt = new DataTable();
                    new SqlDataAdapter($"SELECT TOP 1 * FROM Clientes WHERE CUIT='{q}' OR RazonSocial LIKE '%{q}%'", c).Fill(dt);
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
                    new SqlDataAdapter($"SELECT TOP 10 * FROM Clientes WHERE CUIT LIKE '%{q}%' OR RazonSocial LIKE '%{q}%'", c).Fill(dt);
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
                    new SqlDataAdapter($"SELECT TOP 10 * FROM Proveedores WHERE CUIT LIKE '%{q}%' OR RazonSocial LIKE '%{q}%'", c).Fill(dt);
                }
            }
            catch { }
            return dt;
        }

        // Productos
        public static DataTable GetProductos(string filtro = "")
        {
            var dt = new DataTable();
            try
            {
                using (var c = new SqlConnection(_connectionString))
                {
                    c.Open();
                    string sql = string.IsNullOrWhiteSpace(filtro)
                        ? "SELECT * FROM Productos ORDER BY Descripcion"
                        : "SELECT * FROM Productos WHERE Descripcion LIKE @f OR Codigo LIKE @f OR CodigoBarra LIKE @f ORDER BY Descripcion";
                    var da = new SqlDataAdapter(sql, c);
                    if (!string.IsNullOrWhiteSpace(filtro))
                        da.SelectCommand.Parameters.AddWithValue("@f", "%" + filtro + "%");
                    da.Fill(dt);
                }
            }
            catch { }
            return dt;
        }

        public static int GuardarProducto(int id, string cod, string cb, string desc, string cat, string subRubro, string marca, string proveedor, string iva,
            decimal costo, decimal gan, decimal imp, decimal venta, int stock, string img,
            string tipoMoneda, bool permiteModPrecio, bool esStockeable, bool aceptaStockNeg,
            bool usaVariantes, bool esCombo, decimal? stockMinimo, decimal? stockIdeal,
            string codigoExterno, string varianteColor, string varianteTalle, string varianteUnidadMedida)
        {
            bool ok = GuardarProducto(id, cod, cb, desc, cat, iva, costo, gan, imp, venta, stock, img);
            if (!ok) return 0;
            try
            {
                using (var c = new SqlConnection(_connectionString))
                {
                    c.Open();
                    // Get the ID just saved/updated
                    if (id == 0)
                    {
                        var r = new SqlCommand("SELECT TOP 1 ProductoID FROM Productos ORDER BY ProductoID DESC", c).ExecuteScalar();
                        return r != null ? Convert.ToInt32(r) : 1;
                    }
                    return id;
                }
            }
            catch { return id > 0 ? id : 1; }
        }

        public static bool GuardarProducto(int id, string cod, string cb, string desc, string cat, string subRubro, string marca, string proveedor, string iva, decimal costo, decimal gan, decimal imp, decimal venta, int stock, string img)
            => GuardarProducto(id, cod, cb, desc, cat, iva, costo, gan, imp, venta, stock, img);

        public static bool GuardarProducto(int id, string cod, string cb, string desc, string cat, string iva, decimal costo, decimal gan, decimal imp, decimal venta, int stock, string img)
        {
            try
            {
                using (var c = new SqlConnection(_connectionString))
                {
                    c.Open();
                    string sql = id == 0 ? "INSERT INTO Productos (Codigo, CodigoBarra, Descripcion, Categoria, TipoIVA, PrecioCosto, Ganancia, ImpuestoInterno, PrecioVenta, StockActual, ImagenPath) VALUES (@c, @cb, @d, @cat, @iva, @pc, @g, @ii, @pv, @s, @img)" : "UPDATE Productos SET Codigo=@c, CodigoBarra=@cb, Descripcion=@d, Categoria=@cat, TipoIVA=@iva, PrecioCosto=@pc, Ganancia=@g, ImpuestoInterno=@ii, PrecioVenta=@pv, StockActual=@s, ImagenPath=@img WHERE ProductoID=@id";
                    using (var cmd = new SqlCommand(sql, c))
                    {
                        cmd.Parameters.AddWithValue("@c", cod);
                        cmd.Parameters.AddWithValue("@cb", cb);
                        cmd.Parameters.AddWithValue("@d", desc);
                        cmd.Parameters.AddWithValue("@cat", cat);
                        cmd.Parameters.AddWithValue("@iva", iva);
                        cmd.Parameters.AddWithValue("@pc", costo);
                        cmd.Parameters.AddWithValue("@g", gan);
                        cmd.Parameters.AddWithValue("@ii", imp);
                        cmd.Parameters.AddWithValue("@pv", venta);
                        cmd.Parameters.AddWithValue("@s", stock);
                        cmd.Parameters.AddWithValue("@img", img);
                        cmd.Parameters.AddWithValue("@id", id);
                        cmd.ExecuteNonQuery();
                        return true;
                    }
                }
            }
            catch (Exception ex) { NotificarError(ex.Message); return false; }
        }

        public static bool EliminarProducto(int id)
        {
            try
            {
                using (var c = new SqlConnection(_connectionString))
                {
                    c.Open();
                    new SqlCommand($"DELETE FROM Productos WHERE ProductoID={id}", c).ExecuteNonQuery();
                    return true;
                }
            }
            catch { return false; }
        }

        public static DataRow BuscarProducto(string q)
        {
            try
            {
                using (var c = new SqlConnection(_connectionString))
                {
                    c.Open();
                    var dt = new DataTable();
                    new SqlDataAdapter($"SELECT TOP 1 * FROM Productos WHERE Codigo='{q}' OR CodigoBarra='{q}' OR Descripcion LIKE '%{q}%'", c).Fill(dt);
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
                    new SqlDataAdapter($"SELECT TOP 10 * FROM Productos WHERE (Codigo LIKE '%{q}%' OR CodigoBarra LIKE '%{q}%' OR Descripcion LIKE '%{q}%') AND StockActual > 0", c).Fill(dt);
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
                    new SqlDataAdapter($"SELECT TOP 10 * FROM Productos WHERE (Codigo LIKE '%{q}%' OR CodigoBarra LIKE '%{q}%' OR Descripcion LIKE '%{q}%')", c).Fill(dt);
                }
            }
            catch { }
            return dt;
        }

        public static bool ActualizarPreciosProducto(int id, decimal cost, decimal prec)
        {
            try
            {
                using (var c = new SqlConnection(_connectionString))
                {
                    c.Open();
                    using (var cmd = new SqlCommand("UPDATE Productos SET PrecioCosto=@pc,PrecioVenta=@pv WHERE ProductoID=@id", c))
                    {
                        cmd.Parameters.AddWithValue("@pc", cost);
                        cmd.Parameters.AddWithValue("@pv", prec);
                        cmd.Parameters.AddWithValue("@id", id);
                        cmd.ExecuteNonQuery();
                        return true;
                    }
                }
            }
            catch { return false; }
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

        public static DataTable GetListasPrecios()
        {
            var dt = new DataTable();
            try
            {
                using (var c = new SqlConnection(_connectionString))
                {
                    c.Open();
                    new SqlDataAdapter("SELECT * FROM ListasPrecios", c).Fill(dt);
                }
            }
            catch { }
            return dt;
        }

        public static bool GuardarListaPrecio(int id, string nombre, decimal porcentaje)
        {
            try
            {
                using (var c = new SqlConnection(_connectionString))
                {
                    c.Open();
                    string sql = id == 0 ? "INSERT INTO ListasPrecios (Nombre, Porcentaje) VALUES (@n, @p)" : "UPDATE ListasPrecios SET Nombre=@n, Porcentaje=@p WHERE ListaID=@id";
                    using (var cmd = new SqlCommand(sql, c))
                    {
                        cmd.Parameters.AddWithValue("@n", nombre);
                        cmd.Parameters.AddWithValue("@p", porcentaje);
                        cmd.Parameters.AddWithValue("@id", id);
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
        public static int GuardarFactura(int cid, string tc, decimal t, List<FacturaItem> its, string cond,
            string cae, string vtoCae, int nroComprobante, int? listaId, object cobranzas)
        {
            bool ok = GuardarFactura(cid, tc, t, its, cond);
            return ok ? 1 : 0;
        }

        public static bool GuardarFactura(int cid, string tc, decimal t, List<FacturaItem> its, string cond)
        {
            using (var c = new SqlConnection(_connectionString))
            {
                c.Open();
                using (var tr = c.BeginTransaction())
                {
                    try
                    {
                        // Insertar Factura y obtener ID
                        string sqlFac = "INSERT INTO Facturas (ClienteID,Fecha,Total,TipoComprobante) VALUES (@cid,@f,@t,@tc); SELECT SCOPE_IDENTITY();";
                        SqlCommand cmdFac = new SqlCommand(sqlFac, c, tr);
                        cmdFac.Parameters.AddWithValue("@cid", cid);
                        cmdFac.Parameters.AddWithValue("@f", DateTime.Now);
                        cmdFac.Parameters.AddWithValue("@t", t);
                        cmdFac.Parameters.AddWithValue("@tc", tc);
                        int fid = Convert.ToInt32(cmdFac.ExecuteScalar());

                        foreach (var i in its)
                        {
                            new SqlCommand($"INSERT INTO FacturaDetalle (FacturaID,ProductoID,Cantidad,PrecioUnitario) VALUES ({fid},{i.ProductoID},{i.Cantidad},{(double)i.PrecioUnitario})", c, tr).ExecuteNonQuery(); // Cast double para SQL decimal
                            new SqlCommand($"UPDATE Productos SET StockActual=StockActual-{i.Cantidad} WHERE ProductoID={i.ProductoID}", c, tr).ExecuteNonQuery();

                            SqlCommand cmdStk = new SqlCommand("INSERT INTO MovimientosStock (ProductoID,FacturaID,Fecha,TipoMovimiento,Cantidad) VALUES (@pid,@fid,@f,'Venta',@cant)", c, tr);
                            cmdStk.Parameters.AddWithValue("@pid", i.ProductoID);
                            cmdStk.Parameters.AddWithValue("@fid", fid);
                            cmdStk.Parameters.AddWithValue("@f", DateTime.Now);
                            cmdStk.Parameters.AddWithValue("@cant", -i.Cantidad);
                            cmdStk.ExecuteNonQuery();
                        }

                        if (cond == "Contado")
                        {
                            SqlCommand cmdCaja = new SqlCommand("INSERT INTO MovimientosCaja (Fecha,Concepto,Tipo,Monto,Usuario) VALUES (@f,@con,'Ingreso',@m,@u)", c, tr);
                            cmdCaja.Parameters.AddWithValue("@f", DateTime.Now);
                            cmdCaja.Parameters.AddWithValue("@con", $"Venta #{fid} ({tc})");
                            cmdCaja.Parameters.AddWithValue("@m", t);
                            cmdCaja.Parameters.AddWithValue("@u", SesionUsuario.NombreUsuario);
                            cmdCaja.ExecuteNonQuery();
                        }
                        else
                        {
                            new SqlCommand($"UPDATE Clientes SET SaldoDeuda=SaldoDeuda+{(double)t} WHERE ClienteID={cid}", c, tr).ExecuteNonQuery();
                            object sal = new SqlCommand($"SELECT SaldoDeuda FROM Clientes WHERE ClienteID={cid}", c, tr).ExecuteScalar();

                            SqlCommand cmdCC = new SqlCommand("INSERT INTO MovimientosCuentaCorriente (ClienteID,Fecha,Descripcion,Monto,SaldoHistorico) VALUES (@cid,@f,@desc,@m,@sal)", c, tr);
                            cmdCC.Parameters.AddWithValue("@cid", cid);
                            cmdCC.Parameters.AddWithValue("@f", DateTime.Now);
                            cmdCC.Parameters.AddWithValue("@desc", $"Venta #{fid} (Cta Cte)");
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
                            new SqlCommand($"UPDATE Productos SET StockActual=StockActual+{i.Cantidad}, PrecioCosto={(double)i.PrecioUnitario} WHERE ProductoID={i.ProductoID}", c, tr).ExecuteNonQuery();

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
                    var cmd = new SqlCommand("SELECT f.FacturaID, f.Fecha, cl.RazonSocial, f.TipoComprobante, f.Total FROM Facturas f JOIN Clientes cl ON f.ClienteID=cl.ClienteID WHERE f.Fecha BETWEEN @d AND @h ORDER BY f.Fecha DESC", c);
                    cmd.Parameters.AddWithValue("@d", d);
                    cmd.Parameters.AddWithValue("@h", h);
                    new SqlDataAdapter(cmd).Fill(dt);
                }
            }
            catch { }
            return dt;
        }

        public static DataTable GetFacturaDetalle(int id)
        {
            var dt = new DataTable();
            try
            {
                using (var c = new SqlConnection(_connectionString))
                {
                    c.Open();
                    new SqlDataAdapter($"SELECT p.Codigo, p.Descripcion, fd.Cantidad, fd.PrecioUnitario, (fd.Cantidad * fd.PrecioUnitario) AS Subtotal FROM FacturaDetalle fd JOIN Productos p ON fd.ProductoID = p.ProductoID WHERE fd.FacturaID = {id}", c).Fill(dt);
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
                                           CAST(0 AS INT) AS StockMinimo
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
                    string fechaSql = fechaVencimiento.HasValue ? $"'{fechaVencimiento.Value:yyyy-MM-dd}'" : "NULL";
                    var cmd = new SqlCommand($@"INSERT INTO ReservasStock (ProductoID,ClienteID,Fecha,FechaVencimiento,Cantidad,Motivo,Estado,Usuario)
                                               VALUES ({productoId},{cidSql},GETDATE(),{fechaSql},{cantidad},@mot,'Activa',@u)", c);
                    cmd.Parameters.AddWithValue("@mot", motivo ?? "");
                    cmd.Parameters.AddWithValue("@u", SesionUsuario.NombreUsuario ?? "");
                    cmd.ExecuteNonQuery();
                    return true;
                }
            }
            catch { return false; }
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

        public static bool GuardarPresupuesto(int cid, decimal t, List<FacturaItem> i)
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
                        return true;
                    }
                    catch { tr.Rollback(); return false; }
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
                ["Usuario"] = "",
                ["Password"] = "",
                ["UsaIntegrado"] = "0"
            };
            try
            {
                var builder = new Microsoft.Data.SqlClient.SqlConnectionStringBuilder(_connectionString);
                result["Servidor"] = builder.DataSource ?? "127.0.0.1";
                result["Usuario"] = builder.UserID ?? "";
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
                string ds = string.IsNullOrWhiteSpace(puerto) || puerto == "1433" ? servidor : $"{servidor},{puerto}";
                string cs = integrado
                    ? $"Server={ds};Database=SchPosDB;Integrated Security=True;TrustServerCertificate=True;"
                    : $"Server={ds};Database=SchPosDB;User Id={usuario};Password={password};TrustServerCertificate=True;";

                var config = System.Configuration.ConfigurationManager.OpenExeConfiguration(System.Configuration.ConfigurationUserLevel.None);
                config.ConnectionStrings.ConnectionStrings["SchPosDB"].ConnectionString = cs;
                config.Save(System.Configuration.ConfigurationSaveMode.Modified);
                System.Configuration.ConfigurationManager.RefreshSection("connectionStrings");
                return true;
            }
            catch { return false; }
        }

        // --- Configuración del negocio ---
        public static bool GuardarConfiguracion(string nombreFantasia, string razonSocial, string cuit, string direccion, string telefono,
            string email, string logoPath, string certPath, string certPassword, int puntoVenta,
            string mpToken, string mpUserId, string mpPosId, bool habilitarMP, decimal? tipoCambioUSD)
        {
            try
            {
                using (var c = new SqlConnection(_connectionString))
                {
                    c.Open();
                    string sql = @"IF EXISTS (SELECT 1 FROM Configuracion WHERE ID=1)
                        UPDATE Configuracion SET NombreFantasia=@nf,RazonSocial=@rs,CUIT=@cuit,Direccion=@dir,Telefono=@tel,
                            Email=@email,LogoPath=@logo,CertificadoPath=@cert,CertificadoPassword=@certpwd,
                            PuntoVenta=@pv,MPToken=@mpt,MPUserID=@mpu,MPPosID=@mpp,TipoCambioUSD=@tc
                        ELSE INSERT INTO Configuracion (ID,NombreFantasia,RazonSocial,CUIT,Direccion,Telefono,Email,
                            LogoPath,CertificadoPath,CertificadoPassword,PuntoVenta,MPToken,MPUserID,MPPosID,TipoCambioUSD)
                        VALUES (1,@nf,@rs,@cuit,@dir,@tel,@email,@logo,@cert,@certpwd,@pv,@mpt,@mpu,@mpp,@tc)";
                    var cmd = new SqlCommand(sql, c);
                    cmd.Parameters.AddWithValue("@nf", nombreFantasia ?? "");
                    cmd.Parameters.AddWithValue("@rs", razonSocial ?? "");
                    cmd.Parameters.AddWithValue("@cuit", cuit ?? "");
                    cmd.Parameters.AddWithValue("@dir", direccion ?? "");
                    cmd.Parameters.AddWithValue("@tel", telefono ?? "");
                    cmd.Parameters.AddWithValue("@email", email ?? "");
                    cmd.Parameters.AddWithValue("@logo", logoPath ?? "");
                    cmd.Parameters.AddWithValue("@cert", certPath ?? "");
                    cmd.Parameters.AddWithValue("@certpwd", certPassword ?? "");
                    cmd.Parameters.AddWithValue("@pv", puntoVenta);
                    cmd.Parameters.AddWithValue("@mpt", mpToken ?? "");
                    cmd.Parameters.AddWithValue("@mpu", mpUserId ?? "");
                    cmd.Parameters.AddWithValue("@mpp", mpPosId ?? "");
                    cmd.Parameters.AddWithValue("@tc", (object)tipoCambioUSD ?? DBNull.Value);
                    cmd.ExecuteNonQuery();
                    return true;
                }
            }
            catch { return false; }
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
        public static DataTable GetMediosPagoCompleto()
        {
            var dt = new DataTable();
            try { using (var c = new SqlConnection(_connectionString)) { c.Open(); new SqlDataAdapter("SELECT MedioPagoID, Nombre, Activo, Orden FROM MediosPago ORDER BY Orden", c).Fill(dt); } } catch { }
            return dt;
        }

        public static bool GuardarMedioPago(int id, string nombre, bool activo, int orden)
        {
            try
            {
                using (var c = new SqlConnection(_connectionString))
                {
                    c.Open();
                    string sql = id > 0
                        ? "UPDATE MediosPago SET Nombre=@n, Activo=@a, Orden=@o WHERE MedioPagoID=@id"
                        : "INSERT INTO MediosPago (Nombre, Activo, Orden) VALUES (@n, @a, @o)";
                    var cmd = new SqlCommand(sql, c);
                    cmd.Parameters.AddWithValue("@n", nombre);
                    cmd.Parameters.AddWithValue("@a", activo);
                    cmd.Parameters.AddWithValue("@o", orden);
                    if (id > 0) cmd.Parameters.AddWithValue("@id", id);
                    cmd.ExecuteNonQuery();
                    return true;
                }
            }
            catch { return false; }
        }

        // --- Licencia ---
        public static string ObtenerStringLicencia()
        {
            try
            {
                using (var c = new SqlConnection(_connectionString))
                {
                    c.Open();
                    var cmd = new SqlCommand("SELECT TOP 1 Valor FROM Configuracion WHERE Clave='LicenciaKey'", c);
                    var r = cmd.ExecuteScalar();
                    return r?.ToString() ?? "";
                }
            }
            catch { return ""; }
        }

        public static bool GuardarNuevaLicencia(string key)
        {
            try
            {
                using (var c = new SqlConnection(_connectionString))
                {
                    c.Open();
                    string sql = @"IF EXISTS (SELECT 1 FROM Configuracion WHERE Clave='LicenciaKey')
                        UPDATE Configuracion SET Valor=@v WHERE Clave='LicenciaKey'
                        ELSE INSERT INTO Configuracion (Clave,Valor) VALUES ('LicenciaKey',@v)";
                    var cmd = new SqlCommand(sql, c);
                    cmd.Parameters.AddWithValue("@v", key);
                    cmd.ExecuteNonQuery();
                    return true;
                }
            }
            catch { return false; }
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
                    var dt = new DataTable();
                    new SqlDataAdapter($"SELECT ListaID FROM ProductosListas WHERE ProductoID={productoId}", c).Fill(dt);
                    foreach (DataRow r in dt.Rows) list.Add(Convert.ToInt32(r[0]));
                }
            }
            catch { }
            return list;
        }

        public static void GuardarProductoListas(int productoId, System.Collections.Generic.List<int> listaIds)
        {
            try
            {
                using (var c = new SqlConnection(_connectionString))
                {
                    c.Open();
                    new SqlCommand($"DELETE FROM ProductosListas WHERE ProductoID={productoId}", c).ExecuteNonQuery();
                    foreach (var lid in listaIds)
                        new SqlCommand($"INSERT INTO ProductosListas (ProductoID,ListaID) VALUES ({productoId},{lid})", c).ExecuteNonQuery();
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
                    var dt = new DataTable();
                    new SqlDataAdapter($"SELECT ComponenteID AS ProductoComponenteID, Cantidad FROM ProductoComboDetalle WHERE ProductoID={productoId}", c).Fill(dt);
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
                    new SqlCommand($"DELETE FROM ProductoComboDetalle WHERE ProductoID={productoId}", c).ExecuteNonQuery();
                    foreach (var (componenteId, cantidad) in componentes)
                    {
                        var cmd = new SqlCommand("INSERT INTO ProductoComboDetalle (ProductoID,ComponenteID,Cantidad) VALUES (@pid,@cid,@cant)", c);
                        cmd.Parameters.AddWithValue("@pid", productoId);
                        cmd.Parameters.AddWithValue("@cid", componenteId);
                        cmd.Parameters.AddWithValue("@cant", cantidad);
                        cmd.ExecuteNonQuery();
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

        // --- Presupuestos (para PrintService) ---
        public static DataRow GetPresupuestoPorID(int id)
        {
            try
            {
                using (var c = new SqlConnection(_connectionString))
                {
                    c.Open();
                    var dt = new DataTable();
                    new SqlDataAdapter($"SELECT * FROM Presupuestos WHERE PresupuestoID={id}", c).Fill(dt);
                    return dt.Rows.Count > 0 ? dt.Rows[0] : null;
                }
            }
            catch { return null; }
        }
    }
}