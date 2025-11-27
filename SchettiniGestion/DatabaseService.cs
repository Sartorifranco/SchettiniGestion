using System;
using System.Data.SQLite;
using System.IO;
using System.Windows.Forms;
using System.Data;
using System.Collections.Generic;
using System.Linq;

namespace SchettiniGestion
{
    // Clases de Ayuda
    public class FacturaItem
    {
        public int ProductoID { get; set; }
        public string Codigo { get; set; }
        public string Descripcion { get; set; }
        public int Cantidad { get; set; }
        public decimal PrecioUnitario { get; set; }
        public decimal Subtotal
        {
            get { return Cantidad * PrecioUnitario; }
        }
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

    public static class DatabaseService
    {
        private static string _dbPath;

        // --- CONSTANTES DE PERMISOS ---
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

        public static void InitializeDatabase()
        {
            try
            {
                string appFolder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "SchettiniGestion_NUEVO");
                if (!Directory.Exists(appFolder)) Directory.CreateDirectory(appFolder);
                _dbPath = Path.Combine(appFolder, "SchettiniGestion.sqlite");

                using (var conn = new SQLiteConnection($"Data Source={_dbPath};Version=3;"))
                {
                    conn.Open();
                    using (var trans = conn.BeginTransaction())
                    {
                        try
                        {
                            // 1. Tabla Usuarios y Roles
                            new SQLiteCommand("CREATE TABLE IF NOT EXISTS Roles (RolID INTEGER PRIMARY KEY AUTOINCREMENT, NombreRol TEXT NOT NULL UNIQUE);", conn, trans).ExecuteNonQuery();
                            new SQLiteCommand("INSERT OR IGNORE INTO Roles (RolID, NombreRol) VALUES (1, 'Admin'), (2, 'Vendedor'), (3, 'Cajero');", conn, trans).ExecuteNonQuery();

                            new SQLiteCommand("CREATE TABLE IF NOT EXISTS Usuarios (UsuarioID INTEGER PRIMARY KEY AUTOINCREMENT, NombreUsuario TEXT NOT NULL UNIQUE, PasswordHash TEXT NOT NULL, Rol TEXT, RolID INTEGER REFERENCES Roles(RolID));", conn, trans).ExecuteNonQuery();
                            try { new SQLiteCommand("ALTER TABLE Usuarios ADD COLUMN RolID INTEGER REFERENCES Roles(RolID);", conn, trans).ExecuteNonQuery(); } catch { }

                            new SQLiteCommand($"INSERT OR IGNORE INTO Usuarios (NombreUsuario, PasswordHash, Rol, RolID) VALUES ('admin', '{PasswordHasher.HashPassword("12345")}', 'Admin', 1);", conn, trans).ExecuteNonQuery();

                            // 2. Tabla Productos
                            string sqlProductos = @"CREATE TABLE IF NOT EXISTS Productos (
                                ProductoID INTEGER PRIMARY KEY AUTOINCREMENT, 
                                Codigo TEXT UNIQUE, 
                                CodigoBarra TEXT,
                                Descripcion TEXT NOT NULL, 
                                Categoria TEXT,
                                TipoIVA TEXT,
                                PrecioCosto REAL DEFAULT 0, 
                                Ganancia REAL DEFAULT 0,
                                ImpuestoInterno REAL DEFAULT 0,
                                PrecioVenta REAL NOT NULL, 
                                StockActual INTEGER DEFAULT 0,
                                ImagenPath TEXT
                            );";
                            new SQLiteCommand(sqlProductos, conn, trans).ExecuteNonQuery();
                            try { new SQLiteCommand("ALTER TABLE Productos ADD COLUMN PrecioCosto REAL DEFAULT 0;", conn, trans).ExecuteNonQuery(); } catch { }
                            try { new SQLiteCommand("ALTER TABLE Productos ADD COLUMN CodigoBarra TEXT;", conn, trans).ExecuteNonQuery(); } catch { }
                            try { new SQLiteCommand("ALTER TABLE Productos ADD COLUMN Categoria TEXT;", conn, trans).ExecuteNonQuery(); } catch { }
                            try { new SQLiteCommand("ALTER TABLE Productos ADD COLUMN TipoIVA TEXT;", conn, trans).ExecuteNonQuery(); } catch { }
                            try { new SQLiteCommand("ALTER TABLE Productos ADD COLUMN Ganancia REAL DEFAULT 0;", conn, trans).ExecuteNonQuery(); } catch { }
                            try { new SQLiteCommand("ALTER TABLE Productos ADD COLUMN ImpuestoInterno REAL DEFAULT 0;", conn, trans).ExecuteNonQuery(); } catch { }
                            try { new SQLiteCommand("ALTER TABLE Productos ADD COLUMN ImagenPath TEXT;", conn, trans).ExecuteNonQuery(); } catch { }

                            // 3. Tabla Clientes
                            new SQLiteCommand("CREATE TABLE IF NOT EXISTS Clientes (ClienteID INTEGER PRIMARY KEY AUTOINCREMENT, CUIT TEXT UNIQUE, RazonSocial TEXT NOT NULL, CondicionIVA TEXT, SaldoDeuda REAL DEFAULT 0);", conn, trans).ExecuteNonQuery();
                            try { new SQLiteCommand("ALTER TABLE Clientes ADD COLUMN SaldoDeuda REAL DEFAULT 0;", conn, trans).ExecuteNonQuery(); } catch { }
                            new SQLiteCommand("INSERT OR IGNORE INTO Clientes (ClienteID, CUIT, RazonSocial, CondicionIVA) VALUES (1, '00-00000000-0', 'Consumidor Final', 'Consumidor Final');", conn, trans).ExecuteNonQuery();

                            // 4. Tabla Proveedores
                            new SQLiteCommand("CREATE TABLE IF NOT EXISTS Proveedores (ProveedorID INTEGER PRIMARY KEY AUTOINCREMENT, CUIT TEXT UNIQUE, RazonSocial TEXT NOT NULL, Telefono TEXT, Email TEXT, Direccion TEXT, SaldoDeuda REAL DEFAULT 0);", conn, trans).ExecuteNonQuery();
                            try { new SQLiteCommand("ALTER TABLE Proveedores ADD COLUMN SaldoDeuda REAL DEFAULT 0;", conn, trans).ExecuteNonQuery(); } catch { }

                            // 5. Tablas Operativas
                            new SQLiteCommand("CREATE TABLE IF NOT EXISTS Facturas (FacturaID INTEGER PRIMARY KEY AUTOINCREMENT, ClienteID INTEGER NOT NULL, Fecha TEXT NOT NULL, Total REAL NOT NULL, TipoComprobante TEXT, FOREIGN KEY(ClienteID) REFERENCES Clientes(ClienteID));", conn, trans).ExecuteNonQuery();
                            new SQLiteCommand("CREATE TABLE IF NOT EXISTS FacturaDetalle (DetalleID INTEGER PRIMARY KEY AUTOINCREMENT, FacturaID INTEGER NOT NULL, ProductoID INTEGER NOT NULL, Cantidad INTEGER NOT NULL, PrecioUnitario REAL NOT NULL, FOREIGN KEY(FacturaID) REFERENCES Facturas(FacturaID), FOREIGN KEY(ProductoID) REFERENCES Productos(ProductoID));", conn, trans).ExecuteNonQuery();

                            new SQLiteCommand("CREATE TABLE IF NOT EXISTS MovimientosStock (MovimientoID INTEGER PRIMARY KEY AUTOINCREMENT, ProductoID INTEGER NOT NULL, FacturaID INTEGER, CompraID INTEGER, Fecha TEXT NOT NULL, TipoMovimiento TEXT NOT NULL, Cantidad INTEGER NOT NULL);", conn, trans).ExecuteNonQuery();
                            try { new SQLiteCommand("ALTER TABLE MovimientosStock ADD COLUMN CompraID INTEGER;", conn, trans).ExecuteNonQuery(); } catch { }

                            new SQLiteCommand("CREATE TABLE IF NOT EXISTS Compras (CompraID INTEGER PRIMARY KEY AUTOINCREMENT, ProveedorID INTEGER NOT NULL, Fecha TEXT NOT NULL, Total REAL NOT NULL, TipoComprobante TEXT, FOREIGN KEY(ProveedorID) REFERENCES Proveedores(ProveedorID));", conn, trans).ExecuteNonQuery();
                            new SQLiteCommand("CREATE TABLE IF NOT EXISTS CompraDetalle (DetalleID INTEGER PRIMARY KEY AUTOINCREMENT, CompraID INTEGER NOT NULL, ProductoID INTEGER NOT NULL, Cantidad INTEGER NOT NULL, PrecioCosto REAL NOT NULL, FOREIGN KEY(CompraID) REFERENCES Compras(CompraID), FOREIGN KEY(ProductoID) REFERENCES Productos(ProductoID));", conn, trans).ExecuteNonQuery();

                            new SQLiteCommand("CREATE TABLE IF NOT EXISTS MovimientosCaja (CajaID INTEGER PRIMARY KEY AUTOINCREMENT, Fecha TEXT NOT NULL, Concepto TEXT NOT NULL, Tipo TEXT NOT NULL, Monto REAL NOT NULL, Usuario TEXT);", conn, trans).ExecuteNonQuery();

                            new SQLiteCommand("CREATE TABLE IF NOT EXISTS Presupuestos (PresupuestoID INTEGER PRIMARY KEY AUTOINCREMENT, ClienteID INTEGER NOT NULL, Fecha TEXT NOT NULL, Total REAL NOT NULL, Estado TEXT DEFAULT 'Pendiente');", conn, trans).ExecuteNonQuery();
                            new SQLiteCommand("CREATE TABLE IF NOT EXISTS PresupuestoDetalle (DetalleID INTEGER PRIMARY KEY AUTOINCREMENT, PresupuestoID INTEGER NOT NULL, ProductoID INTEGER NOT NULL, Cantidad INTEGER NOT NULL, PrecioUnitario REAL NOT NULL);", conn, trans).ExecuteNonQuery();

                            new SQLiteCommand("CREATE TABLE IF NOT EXISTS MovimientosCuentaCorriente (MovimientoID INTEGER PRIMARY KEY AUTOINCREMENT, ClienteID INTEGER, ProveedorID INTEGER, Fecha TEXT NOT NULL, Descripcion TEXT NOT NULL, Monto REAL NOT NULL, SaldoHistorico REAL NOT NULL);", conn, trans).ExecuteNonQuery();

                            new SQLiteCommand("CREATE TABLE IF NOT EXISTS ListasPrecios (ListaID INTEGER PRIMARY KEY AUTOINCREMENT, Nombre TEXT NOT NULL UNIQUE, Porcentaje REAL DEFAULT 0);", conn, trans).ExecuteNonQuery();
                            new SQLiteCommand("INSERT OR IGNORE INTO ListasPrecios (ListaID, Nombre, Porcentaje) VALUES (1, 'Lista Base (Minorista)', 0);", conn, trans).ExecuteNonQuery();

                            // Tabla Configuración
                            string sqlConfig = @"CREATE TABLE IF NOT EXISTS Configuracion (
                                ID INTEGER PRIMARY KEY AUTOINCREMENT,
                                NombreFantasia TEXT,
                                RazonSocial TEXT,
                                CUIT TEXT,
                                Direccion TEXT,
                                Telefono TEXT,
                                Email TEXT,
                                LogoPath TEXT
                            );";
                            new SQLiteCommand(sqlConfig, conn, trans).ExecuteNonQuery();
                            new SQLiteCommand("INSERT OR IGNORE INTO Configuracion (ID, NombreFantasia) VALUES (1, 'Mi Negocio');", conn, trans).ExecuteNonQuery();

                            // 6. Permisos
                            new SQLiteCommand("CREATE TABLE IF NOT EXISTS Permisos (PermisoID INTEGER PRIMARY KEY AUTOINCREMENT, NombrePermiso TEXT NOT NULL UNIQUE);", conn, trans).ExecuteNonQuery();
                            new SQLiteCommand("CREATE TABLE IF NOT EXISTS Roles_Permisos (RolID INTEGER NOT NULL, PermisoID INTEGER NOT NULL, PRIMARY KEY (RolID, PermisoID));", conn, trans).ExecuteNonQuery();

                            string[] permisos = { PERMISO_USUARIOS, PERMISO_CLIENTES, PERMISO_PRODUCTOS, PERMISO_STOCK, PERMISO_FACTURACION, PERMISO_VENTAS, PERMISO_PERMISOS, PERMISO_PROVEEDORES, PERMISO_COMPRAS, PERMISO_PRECIOS, PERMISO_CAJA, PERMISO_PRESUPUESTOS, PERMISO_CUENTASCORRIENTES, PERMISO_LISTASPRECIOS };
                            foreach (var p in permisos) new SQLiteCommand($"INSERT OR IGNORE INTO Permisos (NombrePermiso) VALUES ('{p}');", conn, trans).ExecuteNonQuery();
                            new SQLiteCommand("INSERT OR IGNORE INTO Roles_Permisos (RolID, PermisoID) SELECT 1, PermisoID FROM Permisos;", conn, trans).ExecuteNonQuery();

                            trans.Commit();
                        }
                        catch { trans.Rollback(); throw; }
                    }
                }
            }
            catch (Exception ex) { MessageBox.Show($"Error Fatal DB: {ex.Message}"); Environment.Exit(1); }
        }

        // ==================== MÉTODOS DE CONFIGURACIÓN ====================

        public static DataRow GetConfiguracion()
        {
            try
            {
                using (var c = new SQLiteConnection($"Data Source={_dbPath};Version=3;"))
                {
                    c.Open();
                    var dt = new DataTable();
                    new SQLiteDataAdapter("SELECT * FROM Configuracion WHERE ID = 1", c).Fill(dt);
                    if (dt.Rows.Count > 0) return dt.Rows[0];
                }
            }
            catch { }
            return null;
        }

        public static bool GuardarConfiguracion(string nombre, string razon, string cuit, string dir, string tel, string email, string logoPath)
        {
            try
            {
                using (var c = new SQLiteConnection($"Data Source={_dbPath};Version=3;"))
                {
                    c.Open();
                    string sql = "UPDATE Configuracion SET NombreFantasia=@n, RazonSocial=@r, CUIT=@c, Direccion=@d, Telefono=@t, Email=@e, LogoPath=@l WHERE ID=1";
                    using (var cmd = new SQLiteCommand(sql, c))
                    {
                        cmd.Parameters.AddWithValue("@n", nombre);
                        cmd.Parameters.AddWithValue("@r", razon);
                        cmd.Parameters.AddWithValue("@c", cuit);
                        cmd.Parameters.AddWithValue("@d", dir);
                        cmd.Parameters.AddWithValue("@t", tel);
                        cmd.Parameters.AddWithValue("@e", email);
                        cmd.Parameters.AddWithValue("@l", logoPath);
                        cmd.ExecuteNonQuery();
                        return true;
                    }
                }
            }
            catch (Exception ex) { MessageBox.Show(ex.Message); return false; }
        }

        // ==================== RESTO DE LOS MÉTODOS ====================

        // Usuarios
        public static bool ValidarUsuario(string u, string p) { try { using (var c = new SQLiteConnection($"Data Source={_dbPath};Version=3;")) { c.Open(); string h = ""; using (var cmd = new SQLiteCommand("SELECT PasswordHash FROM Usuarios WHERE NombreUsuario=@u", c)) { cmd.Parameters.AddWithValue("@u", u); var r = cmd.ExecuteScalar(); if (r != null) h = r.ToString(); else return false; } return PasswordHasher.VerifyPassword(p, h); } } catch { return false; } }
        public static DataTable GetUsuarios() { var dt = new DataTable(); try { using (var c = new SQLiteConnection($"Data Source={_dbPath};Version=3;")) { c.Open(); new SQLiteDataAdapter("SELECT u.UsuarioID, u.NombreUsuario, r.NombreRol, u.RolID FROM Usuarios u LEFT JOIN Roles r ON u.RolID=r.RolID", c).Fill(dt); } } catch { } return dt; }
        public static List<Rol> GetRoles() { var l = new List<Rol>(); try { using (var c = new SQLiteConnection($"Data Source={_dbPath};Version=3;")) { c.Open(); using (var r = new SQLiteCommand("SELECT * FROM Roles", c).ExecuteReader()) { while (r.Read()) l.Add(new Rol { RolId = Convert.ToInt32(r["RolID"]), Nombre = r["NombreRol"].ToString() }); } } } catch { } return l; }
        public static bool GuardarUsuario(int id, string u, string p, int rid, string rt) { string ph = string.IsNullOrEmpty(p) ? "" : PasswordHasher.HashPassword(p); try { using (var c = new SQLiteConnection($"Data Source={_dbPath};Version=3;")) { c.Open(); string sql = id == 0 ? "INSERT INTO Usuarios (NombreUsuario,PasswordHash,RolID,Rol) VALUES (@u,@p,@r,@rt)" : string.IsNullOrEmpty(p) ? "UPDATE Usuarios SET NombreUsuario=@u,RolID=@r,Rol=@rt WHERE UsuarioID=@id" : "UPDATE Usuarios SET NombreUsuario=@u,PasswordHash=@p,RolID=@r,Rol=@rt WHERE UsuarioID=@id"; using (var cmd = new SQLiteCommand(sql, c)) { cmd.Parameters.AddWithValue("@u", u); cmd.Parameters.AddWithValue("@rid", rid); cmd.Parameters.AddWithValue("@rt", rt); cmd.Parameters.AddWithValue("@id", id); if (sql.Contains("@p")) cmd.Parameters.AddWithValue("@p", ph); cmd.ExecuteNonQuery(); return true; } } } catch { return false; } }
        public static bool EliminarUsuario(int id) { try { using (var c = new SQLiteConnection($"Data Source={_dbPath};Version=3;")) { c.Open(); new SQLiteCommand($"DELETE FROM Usuarios WHERE UsuarioID={id}", c).ExecuteNonQuery(); return true; } } catch { return false; } }

        // Clientes
        public static DataTable GetClientes() { var dt = new DataTable(); try { using (var c = new SQLiteConnection($"Data Source={_dbPath};Version=3;")) { c.Open(); new SQLiteDataAdapter("SELECT * FROM Clientes", c).Fill(dt); } } catch { } return dt; }
        public static bool GuardarCliente(int id, string c, string r, string i) { try { using (var conn = new SQLiteConnection($"Data Source={_dbPath};Version=3;")) { conn.Open(); string sql = id == 0 ? "INSERT INTO Clientes (CUIT,RazonSocial,CondicionIVA) VALUES (@c,@r,@i)" : "UPDATE Clientes SET CUIT=@c,RazonSocial=@r,CondicionIVA=@i WHERE ClienteID=@id"; using (var cmd = new SQLiteCommand(sql, conn)) { cmd.Parameters.AddWithValue("@c", c); cmd.Parameters.AddWithValue("@r", r); cmd.Parameters.AddWithValue("@i", i); cmd.Parameters.AddWithValue("@id", id); cmd.ExecuteNonQuery(); return true; } } } catch { return false; } }
        public static bool EliminarCliente(int id) { try { using (var c = new SQLiteConnection($"Data Source={_dbPath};Version=3;")) { c.Open(); new SQLiteCommand($"DELETE FROM Clientes WHERE ClienteID={id}", c).ExecuteNonQuery(); return true; } } catch { return false; } }
        public static DataRow BuscarCliente(string q) { try { using (var c = new SQLiteConnection($"Data Source={_dbPath};Version=3;")) { c.Open(); var dt = new DataTable(); new SQLiteDataAdapter($"SELECT * FROM Clientes WHERE CUIT='{q}' OR RazonSocial LIKE '%{q}%' LIMIT 1", c).Fill(dt); if (dt.Rows.Count > 0) return dt.Rows[0]; } } catch { } return null; }
        public static DataTable BuscarClientesMultiples(string q) { var dt = new DataTable(); try { using (var c = new SQLiteConnection($"Data Source={_dbPath};Version=3;")) { c.Open(); new SQLiteDataAdapter($"SELECT * FROM Clientes WHERE CUIT LIKE '%{q}%' OR RazonSocial LIKE '%{q}%' LIMIT 10", c).Fill(dt); } } catch { } return dt; }

        // Proveedores
        public static DataTable GetProveedores() { var dt = new DataTable(); try { using (var c = new SQLiteConnection($"Data Source={_dbPath};Version=3;")) { c.Open(); new SQLiteDataAdapter("SELECT * FROM Proveedores", c).Fill(dt); } } catch { } return dt; }
        public static bool GuardarProveedor(int id, string c, string r, string t, string e, string d) { try { using (var conn = new SQLiteConnection($"Data Source={_dbPath};Version=3;")) { conn.Open(); string sql = id == 0 ? "INSERT INTO Proveedores (CUIT,RazonSocial,Telefono,Email,Direccion) VALUES (@c,@r,@t,@e,@d)" : "UPDATE Proveedores SET CUIT=@c,RazonSocial=@r,Telefono=@t,Email=@e,Direccion=@d WHERE ProveedorID=@id"; using (var cmd = new SQLiteCommand(sql, conn)) { cmd.Parameters.AddWithValue("@c", c); cmd.Parameters.AddWithValue("@r", r); cmd.Parameters.AddWithValue("@t", t); cmd.Parameters.AddWithValue("@e", e); cmd.Parameters.AddWithValue("@d", d); cmd.Parameters.AddWithValue("@id", id); cmd.ExecuteNonQuery(); return true; } } } catch { return false; } }
        public static bool EliminarProveedor(int id) { try { using (var c = new SQLiteConnection($"Data Source={_dbPath};Version=3;")) { c.Open(); new SQLiteCommand($"DELETE FROM Proveedores WHERE ProveedorID={id}", c).ExecuteNonQuery(); return true; } } catch { return false; } }
        public static DataTable BuscarProveedoresMultiples(string q) { var dt = new DataTable(); try { using (var c = new SQLiteConnection($"Data Source={_dbPath};Version=3;")) { c.Open(); new SQLiteDataAdapter($"SELECT * FROM Proveedores WHERE CUIT LIKE '%{q}%' OR RazonSocial LIKE '%{q}%' LIMIT 10", c).Fill(dt); } } catch { } return dt; }

        // Productos
        public static DataTable GetProductos() { var dt = new DataTable(); try { using (var c = new SQLiteConnection($"Data Source={_dbPath};Version=3;")) { c.Open(); new SQLiteDataAdapter("SELECT * FROM Productos", c).Fill(dt); } } catch { } return dt; }
        public static bool GuardarProducto(int id, string cod, string cb, string desc, string cat, string iva, decimal costo, decimal gan, decimal imp, decimal venta, int stock, string img) { try { using (var c = new SQLiteConnection($"Data Source={_dbPath};Version=3;")) { c.Open(); string sql = id == 0 ? "INSERT INTO Productos (Codigo, CodigoBarra, Descripcion, Categoria, TipoIVA, PrecioCosto, Ganancia, ImpuestoInterno, PrecioVenta, StockActual, ImagenPath) VALUES (@c, @cb, @d, @cat, @iva, @pc, @g, @ii, @pv, @s, @img)" : "UPDATE Productos SET Codigo=@c, CodigoBarra=@cb, Descripcion=@d, Categoria=@cat, TipoIVA=@iva, PrecioCosto=@pc, Ganancia=@g, ImpuestoInterno=@ii, PrecioVenta=@pv, StockActual=@s, ImagenPath=@img WHERE ProductoID=@id"; using (var cmd = new SQLiteCommand(sql, c)) { cmd.Parameters.AddWithValue("@c", cod); cmd.Parameters.AddWithValue("@cb", cb); cmd.Parameters.AddWithValue("@d", desc); cmd.Parameters.AddWithValue("@cat", cat); cmd.Parameters.AddWithValue("@iva", iva); cmd.Parameters.AddWithValue("@pc", (double)costo); cmd.Parameters.AddWithValue("@g", (double)gan); cmd.Parameters.AddWithValue("@ii", (double)imp); cmd.Parameters.AddWithValue("@pv", (double)venta); cmd.Parameters.AddWithValue("@s", stock); cmd.Parameters.AddWithValue("@img", img); cmd.Parameters.AddWithValue("@id", id); cmd.ExecuteNonQuery(); return true; } } } catch (Exception ex) { MessageBox.Show(ex.Message); return false; } }
        public static bool EliminarProducto(int id) { try { using (var c = new SQLiteConnection($"Data Source={_dbPath};Version=3;")) { c.Open(); new SQLiteCommand($"DELETE FROM Productos WHERE ProductoID={id}", c).ExecuteNonQuery(); return true; } } catch { return false; } }
        public static DataRow BuscarProducto(string q) { try { using (var c = new SQLiteConnection($"Data Source={_dbPath};Version=3;")) { c.Open(); var dt = new DataTable(); new SQLiteDataAdapter($"SELECT * FROM Productos WHERE Codigo='{q}' OR CodigoBarra='{q}' OR Descripcion LIKE '%{q}%' LIMIT 1", c).Fill(dt); if (dt.Rows.Count > 0) return dt.Rows[0]; } } catch { } return null; }
        public static DataTable BuscarProductosMultiples_ParaVenta(string q) { var dt = new DataTable(); try { using (var c = new SQLiteConnection($"Data Source={_dbPath};Version=3;")) { c.Open(); new SQLiteDataAdapter($"SELECT * FROM Productos WHERE (Codigo LIKE '%{q}%' OR CodigoBarra LIKE '%{q}%' OR Descripcion LIKE '%{q}%') AND StockActual > 0 LIMIT 10", c).Fill(dt); } } catch { } return dt; }
        public static DataTable BuscarProductosMultiples_ParaCompra(string q) { var dt = new DataTable(); try { using (var c = new SQLiteConnection($"Data Source={_dbPath};Version=3;")) { c.Open(); new SQLiteDataAdapter($"SELECT * FROM Productos WHERE (Codigo LIKE '%{q}%' OR CodigoBarra LIKE '%{q}%' OR Descripcion LIKE '%{q}%') LIMIT 10", c).Fill(dt); } } catch { } return dt; }
        public static bool ActualizarPreciosProducto(int id, decimal cost, decimal prec) { try { using (var c = new SQLiteConnection($"Data Source={_dbPath};Version=3;")) { c.Open(); using (var cmd = new SQLiteCommand("UPDATE Productos SET PrecioCosto=@pc,PrecioVenta=@pv WHERE ProductoID=@id", c)) { cmd.Parameters.AddWithValue("@pc", (double)cost); cmd.Parameters.AddWithValue("@pv", (double)prec); cmd.Parameters.AddWithValue("@id", id); cmd.ExecuteNonQuery(); return true; } } } catch { return false; } }

        // Permisos y Sesion
        public static bool CargarSesionUsuario(string u) { try { using (var c = new SQLiteConnection($"Data Source={_dbPath};Version=3;")) { c.Open(); int rid = 2; object r = new SQLiteCommand($"SELECT RolID FROM Usuarios WHERE NombreUsuario='{u}'", c).ExecuteScalar(); if (r != null && r != DBNull.Value) rid = Convert.ToInt32(r); var p = new List<string>(); using (var reader = new SQLiteCommand($"SELECT p.NombrePermiso FROM Roles_Permisos rp JOIN Permisos p ON rp.PermisoID=p.PermisoID WHERE rp.RolID={rid}", c).ExecuteReader()) { while (reader.Read()) p.Add(reader.GetString(0)); } SesionUsuario.Iniciar(u, rid, p); return true; } } catch { return false; } }
        public static List<Permiso> GetPermisos() { var l = new List<Permiso>(); try { using (var c = new SQLiteConnection($"Data Source={_dbPath};Version=3;")) { c.Open(); using (var r = new SQLiteCommand("SELECT * FROM Permisos ORDER BY NombrePermiso", c).ExecuteReader()) { while (r.Read()) l.Add(new Permiso { PermisoId = Convert.ToInt32(r["PermisoID"]), Nombre = r["NombrePermiso"].ToString() }); } } } catch { } return l; }
        public static Dictionary<int, List<int>> GetPermisosPorRol() { var d = new Dictionary<int, List<int>>(); try { using (var c = new SQLiteConnection($"Data Source={_dbPath};Version=3;")) { c.Open(); using (var r = new SQLiteCommand("SELECT * FROM Roles_Permisos", c).ExecuteReader()) { while (r.Read()) { int rid = Convert.ToInt32(r["RolID"]); int pid = Convert.ToInt32(r["PermisoID"]); if (!d.ContainsKey(rid)) d[rid] = new List<int>(); d[rid].Add(pid); } } } } catch { } return d; }
        public static void ActualizarPermisosParaRol(int rid, List<int> pids) { using (var c = new SQLiteConnection($"Data Source={_dbPath};Version=3;")) { c.Open(); using (var t = c.BeginTransaction()) { try { new SQLiteCommand($"DELETE FROM Roles_Permisos WHERE RolID={rid}", c, t).ExecuteNonQuery(); if (pids != null) foreach (int pid in pids) new SQLiteCommand($"INSERT INTO Roles_Permisos (RolID,PermisoID) VALUES ({rid},{pid})", c, t).ExecuteNonQuery(); t.Commit(); } catch { t.Rollback(); } } } }

        // Listas de Precios
        public static DataTable GetListasPrecios() { var dt = new DataTable(); try { using (var c = new SQLiteConnection($"Data Source={_dbPath};Version=3;")) { c.Open(); new SQLiteDataAdapter("SELECT * FROM ListasPrecios", c).Fill(dt); } } catch { } return dt; }
        public static bool GuardarListaPrecio(int id, string nombre, decimal porcentaje) { try { using (var c = new SQLiteConnection($"Data Source={_dbPath};Version=3;")) { c.Open(); string sql = id == 0 ? "INSERT INTO ListasPrecios (Nombre, Porcentaje) VALUES (@n, @p)" : "UPDATE ListasPrecios SET Nombre=@n, Porcentaje=@p WHERE ListaID=@id"; using (var cmd = new SQLiteCommand(sql, c)) { cmd.Parameters.AddWithValue("@n", nombre); cmd.Parameters.AddWithValue("@p", (double)porcentaje); cmd.Parameters.AddWithValue("@id", id); cmd.ExecuteNonQuery(); return true; } } } catch (Exception ex) { MessageBox.Show(ex.Message); return false; } }
        public static bool EliminarListaPrecio(int id) { if (id == 1) { MessageBox.Show("No se puede eliminar la lista base."); return false; } try { using (var c = new SQLiteConnection($"Data Source={_dbPath};Version=3;")) { c.Open(); new SQLiteCommand($"DELETE FROM ListasPrecios WHERE ListaID={id}", c).ExecuteNonQuery(); return true; } } catch { return false; } }

        // Reportes
        public static DataTable GetRankingVentas(DateTime desde, DateTime hasta) { var dt = new DataTable(); try { using (var c = new SQLiteConnection($"Data Source={_dbPath};Version=3;")) { c.Open(); string sql = @"SELECT p.Codigo, p.Descripcion, p.Categoria AS Rubro, SUM(fd.Cantidad) AS UnidadesVendidas, SUM(fd.Cantidad * fd.PrecioUnitario) AS TotalVendido FROM FacturaDetalle fd JOIN Facturas f ON fd.FacturaID=f.FacturaID JOIN Productos p ON fd.ProductoID=p.ProductoID WHERE f.Fecha BETWEEN @d AND @h GROUP BY p.ProductoID ORDER BY TotalVendido DESC"; using (var cmd = new SQLiteCommand(sql, c)) { cmd.Parameters.AddWithValue("@d", desde.ToString("yyyy-MM-dd 00:00:00")); cmd.Parameters.AddWithValue("@h", hasta.ToString("yyyy-MM-dd 23:59:59")); new SQLiteDataAdapter(cmd).Fill(dt); } } } catch { } return dt; }
        public static DataTable GetVentasParaLibroIVA(DateTime desde, DateTime hasta) { var dt = new DataTable(); try { using (var c = new SQLiteConnection($"Data Source={_dbPath};Version=3;")) { c.Open(); string sql = @"SELECT f.Fecha, f.TipoComprobante, f.FacturaID AS NroComprobante, cl.RazonSocial AS Cliente, cl.CUIT, cl.CondicionIVA, f.Total, fd.Cantidad, fd.PrecioUnitario, p.TipoIVA AS AlicuotaProducto FROM Facturas f JOIN Clientes cl ON f.ClienteID=cl.ClienteID JOIN FacturaDetalle fd ON f.FacturaID=fd.FacturaID JOIN Productos p ON fd.ProductoID=p.ProductoID WHERE f.Fecha BETWEEN @d AND @h ORDER BY f.Fecha ASC"; using (var cmd = new SQLiteCommand(sql, c)) { cmd.Parameters.AddWithValue("@d", desde.ToString("yyyy-MM-dd 00:00:00")); cmd.Parameters.AddWithValue("@h", hasta.ToString("yyyy-MM-dd 23:59:59")); new SQLiteDataAdapter(cmd).Fill(dt); } } } catch { } return dt; }

        // Facturación, Compras, Caja, Presupuestos, Cta Cte
        public static bool GuardarFactura(int cid, string tc, decimal t, List<FacturaItem> its, string cond) { using (var c = new SQLiteConnection($"Data Source={_dbPath};Version=3;")) { c.Open(); using (var tr = c.BeginTransaction()) { try { string f = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"); new SQLiteCommand($"INSERT INTO Facturas (ClienteID,Fecha,Total,TipoComprobante) VALUES ({cid},'{f}',{(double)t},'{tc}')", c, tr).ExecuteNonQuery(); long fid = c.LastInsertRowId; foreach (var i in its) { new SQLiteCommand($"INSERT INTO FacturaDetalle (FacturaID,ProductoID,Cantidad,PrecioUnitario) VALUES ({fid},{i.ProductoID},{i.Cantidad},{(double)i.PrecioUnitario})", c, tr).ExecuteNonQuery(); new SQLiteCommand($"UPDATE Productos SET StockActual=StockActual-{i.Cantidad} WHERE ProductoID={i.ProductoID}", c, tr).ExecuteNonQuery(); new SQLiteCommand($"INSERT INTO MovimientosStock (ProductoID,FacturaID,Fecha,TipoMovimiento,Cantidad) VALUES ({i.ProductoID},{fid},'{f}','Venta',{-i.Cantidad})", c, tr).ExecuteNonQuery(); } if (cond == "Contado") new SQLiteCommand($"INSERT INTO MovimientosCaja (Fecha,Concepto,Tipo,Monto,Usuario) VALUES ('{f}','Venta #{fid} ({tc})','Ingreso',{(double)t},'{SesionUsuario.NombreUsuario}')", c, tr).ExecuteNonQuery(); else { new SQLiteCommand($"UPDATE Clientes SET SaldoDeuda=SaldoDeuda+{(double)t} WHERE ClienteID={cid}", c, tr).ExecuteNonQuery(); object sal = new SQLiteCommand($"SELECT SaldoDeuda FROM Clientes WHERE ClienteID={cid}", c, tr).ExecuteScalar(); new SQLiteCommand($"INSERT INTO MovimientosCuentaCorriente (ClienteID,Fecha,Descripcion,Monto,SaldoHistorico) VALUES ({cid},'{f}','Venta #{fid} (Cta Cte)',{(double)t},{(double)Convert.ToDecimal(sal)})", c, tr).ExecuteNonQuery(); } tr.Commit(); return true; } catch { tr.Rollback(); return false; } } } }
        public static bool GuardarCompra(int pid, string tc, decimal t, List<FacturaItem> its, string cond) { using (var c = new SQLiteConnection($"Data Source={_dbPath};Version=3;")) { c.Open(); using (var tr = c.BeginTransaction()) { try { string f = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"); new SQLiteCommand($"INSERT INTO Compras (ProveedorID,Fecha,Total,TipoComprobante) VALUES ({pid},'{f}',{(double)t},'{tc}')", c, tr).ExecuteNonQuery(); long cid = c.LastInsertRowId; foreach (var i in its) { new SQLiteCommand($"INSERT INTO CompraDetalle (CompraID,ProductoID,Cantidad,PrecioCosto) VALUES ({cid},{i.ProductoID},{i.Cantidad},{(double)i.PrecioUnitario})", c, tr).ExecuteNonQuery(); new SQLiteCommand($"UPDATE Productos SET StockActual=StockActual+{i.Cantidad}, PrecioCosto={(double)i.PrecioUnitario} WHERE ProductoID={i.ProductoID}", c, tr).ExecuteNonQuery(); new SQLiteCommand($"INSERT INTO MovimientosStock (ProductoID,CompraID,Fecha,TipoMovimiento,Cantidad) VALUES ({i.ProductoID},{cid},'{f}','Compra',{i.Cantidad})", c, tr).ExecuteNonQuery(); } if (cond == "Contado") new SQLiteCommand($"INSERT INTO MovimientosCaja (Fecha,Concepto,Tipo,Monto,Usuario) VALUES ('{f}','Compra #{cid} ({tc})','Egreso',{(double)t},'{SesionUsuario.NombreUsuario}')", c, tr).ExecuteNonQuery(); else { new SQLiteCommand($"UPDATE Proveedores SET SaldoDeuda=SaldoDeuda+{(double)t} WHERE ProveedorID={pid}", c, tr).ExecuteNonQuery(); object sal = new SQLiteCommand($"SELECT SaldoDeuda FROM Proveedores WHERE ProveedorID={pid}", c, tr).ExecuteScalar(); new SQLiteCommand($"INSERT INTO MovimientosCuentaCorriente (ProveedorID,Fecha,Descripcion,Monto,SaldoHistorico) VALUES ({pid},'{f}','Compra #{cid} (Cta Cte)',{(double)t},{(double)Convert.ToDecimal(sal)})", c, tr).ExecuteNonQuery(); } tr.Commit(); return true; } catch { tr.Rollback(); return false; } } } }
        public static DataTable GetFacturasPorFecha(DateTime d, DateTime h) { var dt = new DataTable(); try { using (var c = new SQLiteConnection($"Data Source={_dbPath};Version=3;")) { c.Open(); new SQLiteDataAdapter($"SELECT f.FacturaID, f.Fecha, cl.RazonSocial, f.TipoComprobante, f.Total FROM Facturas f JOIN Clientes cl ON f.ClienteID=cl.ClienteID WHERE f.Fecha BETWEEN '{d:yyyy-MM-dd} 00:00:00' AND '{h:yyyy-MM-dd} 23:59:59' ORDER BY f.Fecha DESC", c).Fill(dt); } } catch { } return dt; }
        public static DataTable GetFacturaDetalle(int id) { var dt = new DataTable(); try { using (var c = new SQLiteConnection($"Data Source={_dbPath};Version=3;")) { c.Open(); new SQLiteDataAdapter($"SELECT p.Codigo, p.Descripcion, fd.Cantidad, fd.PrecioUnitario, (fd.Cantidad * fd.PrecioUnitario) AS Subtotal FROM FacturaDetalle fd JOIN Productos p ON fd.ProductoID = p.ProductoID WHERE fd.FacturaID = {id}", c).Fill(dt); } } catch { } return dt; }
        public static bool AjustarStock(int pid, int cant, string mot) { if (cant == 0) return false; try { using (var c = new SQLiteConnection($"Data Source={_dbPath};Version=3;")) { c.Open(); using (var t = c.BeginTransaction()) { new SQLiteCommand($"UPDATE Productos SET StockActual=StockActual+{cant} WHERE ProductoID={pid}", c, t).ExecuteNonQuery(); new SQLiteCommand($"INSERT INTO MovimientosStock (ProductoID,Fecha,TipoMovimiento,Cantidad) VALUES ({pid},'{DateTime.Now:yyyy-MM-dd HH:mm:ss}','{mot}',{cant})", c, t).ExecuteNonQuery(); t.Commit(); return true; } } } catch { return false; } }
        public static bool RegistrarMovimientoCaja(string con, string tip, decimal m) { try { using (var c = new SQLiteConnection($"Data Source={_dbPath};Version=3;")) { c.Open(); new SQLiteCommand($"INSERT INTO MovimientosCaja (Fecha,Concepto,Tipo,Monto,Usuario) VALUES ('{DateTime.Now:yyyy-MM-dd HH:mm:ss}','{con}','{tip}',{(double)m},'{SesionUsuario.NombreUsuario}')", c).ExecuteNonQuery(); return true; } } catch { return false; } }
        public static DataTable GetMovimientosCaja(DateTime f) { var dt = new DataTable(); try { using (var c = new SQLiteConnection($"Data Source={_dbPath};Version=3;")) { c.Open(); new SQLiteDataAdapter($"SELECT * FROM MovimientosCaja WHERE date(Fecha)=date('{f:yyyy-MM-dd}') ORDER BY Fecha DESC", c).Fill(dt); } } catch { } return dt; }
        public static decimal GetSaldoCaja() { decimal s = 0; try { using (var c = new SQLiteConnection($"Data Source={_dbPath};Version=3;")) { c.Open(); var i = new SQLiteCommand("SELECT SUM(Monto) FROM MovimientosCaja WHERE Tipo='Ingreso'", c).ExecuteScalar(); if (i != DBNull.Value) s += Convert.ToDecimal(i); var e = new SQLiteCommand("SELECT SUM(Monto) FROM MovimientosCaja WHERE Tipo='Egreso'", c).ExecuteScalar(); if (e != DBNull.Value) s -= Convert.ToDecimal(e); } } catch { } return s; }
        public static bool GuardarPresupuesto(int cid, decimal t, List<FacturaItem> i) { using (var c = new SQLiteConnection($"Data Source={_dbPath};Version=3;")) { c.Open(); using (var tr = c.BeginTransaction()) { try { new SQLiteCommand($"INSERT INTO Presupuestos (ClienteID,Fecha,Total,Estado) VALUES ({cid},'{DateTime.Now:yyyy-MM-dd HH:mm:ss}',{(double)t},'Pendiente')", c, tr).ExecuteNonQuery(); long pid = c.LastInsertRowId; foreach (var it in i) new SQLiteCommand($"INSERT INTO PresupuestoDetalle (PresupuestoID,ProductoID,Cantidad,PrecioUnitario) VALUES ({pid},{it.ProductoID},{it.Cantidad},{(double)it.PrecioUnitario})", c, tr).ExecuteNonQuery(); tr.Commit(); return true; } catch { tr.Rollback(); return false; } } } }
        public static DataTable GetPresupuestos(DateTime d, DateTime h) { var dt = new DataTable(); try { using (var c = new SQLiteConnection($"Data Source={_dbPath};Version=3;")) { c.Open(); new SQLiteDataAdapter($"SELECT p.PresupuestoID, p.Fecha, cl.RazonSocial, p.Total, p.Estado FROM Presupuestos p JOIN Clientes cl ON p.ClienteID=cl.ClienteID WHERE p.Fecha BETWEEN '{d:yyyy-MM-dd} 00:00:00' AND '{h:yyyy-MM-dd} 23:59:59' ORDER BY p.Fecha DESC", c).Fill(dt); } } catch { } return dt; }
        public static DataTable GetPresupuestoDetalle(int pid) { var dt = new DataTable(); try { using (var c = new SQLiteConnection($"Data Source={_dbPath};Version=3;")) { c.Open(); new SQLiteDataAdapter($"SELECT p.Codigo,p.Descripcion,pd.Cantidad,pd.PrecioUnitario,(pd.Cantidad*pd.PrecioUnitario) as Subtotal FROM PresupuestoDetalle pd JOIN Productos p ON pd.ProductoID=p.ProductoID WHERE pd.PresupuestoID={pid}", c).Fill(dt); } } catch { } return dt; }
        public static bool EliminarPresupuesto(int pid) { try { using (var c = new SQLiteConnection($"Data Source={_dbPath};Version=3;")) { c.Open(); new SQLiteCommand($"DELETE FROM PresupuestoDetalle WHERE PresupuestoID={pid}", c).ExecuteNonQuery(); new SQLiteCommand($"DELETE FROM Presupuestos WHERE PresupuestoID={pid}", c).ExecuteNonQuery(); return true; } } catch { return false; } }
        public static bool RegistrarPagoCliente(int cid, decimal m) { using (var c = new SQLiteConnection($"Data Source={_dbPath};Version=3;")) { c.Open(); using (var t = c.BeginTransaction()) { try { new SQLiteCommand($"UPDATE Clientes SET SaldoDeuda=SaldoDeuda-{(double)m} WHERE ClienteID={cid}", c, t).ExecuteNonQuery(); object s = new SQLiteCommand($"SELECT SaldoDeuda FROM Clientes WHERE ClienteID={cid}", c, t).ExecuteScalar(); new SQLiteCommand($"INSERT INTO MovimientosCuentaCorriente (ClienteID,Fecha,Descripcion,Monto,SaldoHistorico) VALUES ({cid},'{DateTime.Now:yyyy-MM-dd HH:mm:ss}','Pago a Cuenta',{(double)(m * -1)},{(double)Convert.ToDecimal(s)})", c, t).ExecuteNonQuery(); new SQLiteCommand($"INSERT INTO MovimientosCaja (Fecha,Concepto,Tipo,Monto,Usuario) VALUES ('{DateTime.Now:yyyy-MM-dd HH:mm:ss}','Cobro Cta Cte #{cid}','Ingreso',{(double)m},'{SesionUsuario.NombreUsuario}')", c, t).ExecuteNonQuery(); t.Commit(); return true; } catch (Exception e) { t.Rollback(); MessageBox.Show(e.Message); return false; } } } }
        public static bool RegistrarPagoProveedor(int pid, decimal m) { using (var c = new SQLiteConnection($"Data Source={_dbPath};Version=3;")) { c.Open(); using (var t = c.BeginTransaction()) { try { new SQLiteCommand($"UPDATE Proveedores SET SaldoDeuda=SaldoDeuda-{(double)m} WHERE ProveedorID={pid}", c, t).ExecuteNonQuery(); object s = new SQLiteCommand($"SELECT SaldoDeuda FROM Proveedores WHERE ProveedorID={pid}", c, t).ExecuteScalar(); new SQLiteCommand($"INSERT INTO MovimientosCuentaCorriente (ProveedorID,Fecha,Descripcion,Monto,SaldoHistorico) VALUES ({pid},'{DateTime.Now:yyyy-MM-dd HH:mm:ss}','Pago a Proveedor',{(double)(m * -1)},{(double)Convert.ToDecimal(s)})", c, t).ExecuteNonQuery(); new SQLiteCommand($"INSERT INTO MovimientosCaja (Fecha,Concepto,Tipo,Monto,Usuario) VALUES ('{DateTime.Now:yyyy-MM-dd HH:mm:ss}','Pago Cta Cte #{pid}','Egreso',{(double)m},'{SesionUsuario.NombreUsuario}')", c, t).ExecuteNonQuery(); t.Commit(); return true; } catch (Exception e) { t.Rollback(); MessageBox.Show(e.Message); return false; } } } }
        public static DataTable GetMovimientosCC(int? cid, int? pid) { var dt = new DataTable(); try { using (var c = new SQLiteConnection($"Data Source={_dbPath};Version=3;")) { c.Open(); string w = cid.HasValue ? $"ClienteID={cid}" : $"ProveedorID={pid}"; new SQLiteDataAdapter($"SELECT Fecha,Descripcion,Monto,SaldoHistorico FROM MovimientosCuentaCorriente WHERE {w} ORDER BY Fecha DESC", c).Fill(dt); } } catch { } return dt; }
    }
}