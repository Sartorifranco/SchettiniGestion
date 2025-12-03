using System;
using System.Collections.Generic;
using System.Data;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using SqlConnection = Microsoft.Data.SqlClient.SqlConnection;
using SqlCommand = Microsoft.Data.SqlClient.SqlCommand;
using SqlDataAdapter = Microsoft.Data.SqlClient.SqlDataAdapter;
using SqlException = Microsoft.Data.SqlClient.SqlException;

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
        public decimal Subtotal { get { return Cantidad * PrecioUnitario; } }
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
    public static class DatabaseService
    {
        // CONEXIÓN A SchPosDB
        // Cambiamos 'Server' por 'Data Source' y usamos 'localhost' que suele ser más compatible
        private static string _connectionString = "Data Source=SIS5\\SQLEXPRESS;Initial Catalog=SchPosDB;Integrated Security=True;TrustServerCertificate=True;";
        
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

        public static void InitializeDatabase()
        {
            try
            {
                // Solo probamos conexión, las tablas ya se crearon con el script SQL
                using (var conn = new SqlConnection(_connectionString))
                {
                    conn.Open();
                }
            }
            catch (Exception ex)
            {
                NotificarError($"Error conectando a SQL Server (SchPosDB): {ex.Message}");
            }
        }

        #region Dashboard
        public static int GetCantidadVentasHoy()
        {
            try
            {
                using (var c = new SqlConnection(_connectionString))
                {
                    c.Open();
                    var res = new SqlCommand("SELECT COUNT(*) FROM Facturas WHERE CAST(Fecha AS DATE) = CAST(GETDATE() AS DATE)", c).ExecuteScalar();
                    return Convert.ToInt32(res);
                }
            }
            catch { return 0; }
        }

        public static decimal GetTotalVentasHoy()
        {
            try
            {
                using (var c = new SqlConnection(_connectionString))
                {
                    c.Open();
                    var res = new SqlCommand("SELECT SUM(Total) FROM Facturas WHERE CAST(Fecha AS DATE) = CAST(GETDATE() AS DATE)", c).ExecuteScalar();
                    return res != DBNull.Value ? Convert.ToDecimal(res) : 0;
                }
            }
            catch { return 0; }
        }

        public static int GetCantidadProductos()
        {
            try
            {
                using (var c = new SqlConnection(_connectionString))
                {
                    c.Open();
                    return Convert.ToInt32(new SqlCommand("SELECT COUNT(*) FROM Productos", c).ExecuteScalar());
                }
            }
            catch { return 0; }
        }

        public static int GetCantidadClientes()
        {
            try
            {
                using (var c = new SqlConnection(_connectionString))
                {
                    c.Open();
                    return Convert.ToInt32(new SqlCommand("SELECT COUNT(*) FROM Clientes", c).ExecuteScalar());
                }
            }
            catch { return 0; }
        }

        public static decimal GetRentabilidadHoy()
        {
            try
            {
                using (var c = new SqlConnection(_connectionString))
                {
                    c.Open();
                    string sql = @"SELECT SUM((fd.PrecioUnitario - p.PrecioCosto) * fd.Cantidad) FROM FacturaDetalle fd JOIN Facturas f ON fd.FacturaID = f.FacturaID JOIN Productos p ON fd.ProductoID = p.ProductoID WHERE CAST(f.Fecha AS DATE) = CAST(GETDATE() AS DATE)";
                    var res = new SqlCommand(sql, c).ExecuteScalar();
                    return res != DBNull.Value ? Convert.ToDecimal(res) : 0;
                }
            }
            catch { return 0; }
        }
        #endregion

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
                        cmd.Parameters.AddWithValue("@rid", rid);
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
                    new SqlDataAdapter($"SELECT TOP 10 * FROM Proveedores WHERE CUIT LIKE '%{q}%' OR RazonSocial LIKE '%{q}%' LIMIT 10", c).Fill(dt);
                }
            }
            catch { }
            return dt;
        }

        // Productos
        public static DataTable GetProductos()
        {
            var dt = new DataTable();
            try
            {
                using (var c = new SqlConnection(_connectionString))
                {
                    c.Open();
                    new SqlDataAdapter("SELECT * FROM Productos", c).Fill(dt);
                }
            }
            catch { }
            return dt;
        }

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
    }
}