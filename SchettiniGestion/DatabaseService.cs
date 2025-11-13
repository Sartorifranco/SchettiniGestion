using System;
using System.Data.SQLite;
using System.IO;
using System.Windows.Forms; // Usamos MessageBox de Forms (es universal)
using System.Data;        // ¡Importante para DataTable!
using System.Collections.Generic; // ¡Importante para List<T>!
using System.Linq; // ¡Importante para el nuevo método!

namespace SchettiniGestion
{
    // ... (Clases FacturaItem, Rol, Permiso) ...
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
        public const string PERMISO_USUARIOS = "ACCESO_USUARIOS";
        public const string PERMISO_CLIENTES = "ACCESO_CLIENTES";
        public const string PERMISO_PRODUCTOS = "ACCESO_PRODUCTOS";
        public const string PERMISO_STOCK = "ACCESO_STOCK";
        public const string PERMISO_FACTURACION = "ACCESO_FACTURACION";
        public const string PERMISO_VENTAS = "ACCESO_VENTAS";
        public const string PERMISO_PERMISOS = "ACCESO_PERMISOS";

        public static void InitializeDatabase()
        {
            // ... (Todo tu código de InitializeDatabase. Sin cambios aquí) ...
            try
            {
                string appDataFolder = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
                string appFolder = Path.Combine(appDataFolder, "SchettiniGestion_NUEVO");

                if (!Directory.Exists(appFolder))
                {
                    Directory.CreateDirectory(appFolder);
                }

                _dbPath = Path.Combine(appFolder, "SchettiniGestion.sqlite");

                using (var connection = new SQLiteConnection($"Data Source={_dbPath};Version=3;"))
                {
                    connection.Open();
                    using (var transaction = connection.BeginTransaction())
                    {
                        try
                        {
                            // --- Tablas de ABMs (ya existentes) ---
                            string sqlUsuarios = @"
                            CREATE TABLE IF NOT EXISTS Usuarios (
                                UsuarioID INTEGER PRIMARY KEY AUTOINCREMENT,
                                NombreUsuario TEXT NOT NULL UNIQUE,
                                PasswordHash TEXT NOT NULL,
                                Rol TEXT, 
                                RolID INTEGER REFERENCES Roles(RolID)
                            );";
                            try
                            {
                                new SQLiteCommand("ALTER TABLE Usuarios ADD COLUMN RolID INTEGER REFERENCES Roles(RolID);", connection, transaction).ExecuteNonQuery();
                            }
                            catch (SQLiteException ex)
                            {
                                if (!ex.Message.Contains("duplicate column name")) throw;
                            }
                            new SQLiteCommand(sqlUsuarios, connection, transaction).ExecuteNonQuery();

                            string adminPassHash = PasswordHasher.HashPassword("12345");
                            string sqlAdmin = @"
                            INSERT OR IGNORE INTO Usuarios (NombreUsuario, PasswordHash, Rol) 
                            VALUES ('admin', @pass, 'Admin');";
                            using (var command = new SQLiteCommand(sqlAdmin, connection, transaction))
                            {
                                command.Parameters.AddWithValue("@pass", adminPassHash);
                                command.ExecuteNonQuery();
                            }

                            // ... (Resto de tu CREATE TABLE de Clientes, Productos, Facturas, etc.) ...

                            string sqlProductos = @"
                            CREATE TABLE IF NOT EXISTS Productos (
                                ProductoID INTEGER PRIMARY KEY AUTOINCREMENT,
                                Codigo TEXT UNIQUE,
                                Descripcion TEXT NOT NULL,
                                PrecioVenta REAL NOT NULL,
                                StockActual INTEGER DEFAULT 0
                            );";
                            new SQLiteCommand(sqlProductos, connection, transaction).ExecuteNonQuery();

                            string sqlClientes = @"
                            CREATE TABLE IF NOT EXISTS Clientes (
                                ClienteID INTEGER PRIMARY KEY AUTOINCREMENT,
                                CUIT TEXT UNIQUE,
                                RazonSocial TEXT NOT NULL,
                                CondicionIVA TEXT
                            );";
                            new SQLiteCommand(sqlClientes, connection, transaction).ExecuteNonQuery();

                            string sqlClienteDefault = @"
                            INSERT OR IGNORE INTO Clientes (ClienteID, CUIT, RazonSocial, CondicionIVA) 
                            VALUES (1, '00-00000000-0', 'Consumidor Final', 'Consumidor Final');";
                            new SQLiteCommand(sqlClienteDefault, connection, transaction).ExecuteNonQuery();

                            string sqlFacturas = @"
                            CREATE TABLE IF NOT EXISTS Facturas (
                                FacturaID INTEGER PRIMARY KEY AUTOINCREMENT,
                                ClienteID INTEGER NOT NULL,
                                Fecha TEXT NOT NULL,
                                Total REAL NOT NULL,
                                TipoComprobante TEXT,
                                FOREIGN KEY(ClienteID) REFERENCES Clientes(ClienteID)
                            );";
                            new SQLiteCommand(sqlFacturas, connection, transaction).ExecuteNonQuery();

                            string sqlFacturaDetalle = @"
                            CREATE TABLE IF NOT EXISTS FacturaDetalle (
                                DetalleID INTEGER PRIMARY KEY AUTOINCREMENT,
                                FacturaID INTEGER NOT NULL,
                                ProductoID INTEGER NOT NULL,
                                Cantidad INTEGER NOT NULL,
                                PrecioUnitario REAL NOT NULL,
                                FOREIGN KEY(FacturaID) REFERENCES Facturas(FacturaID),
                                FOREIGN KEY(ProductoID) REFERENCES Productos(ProductoID)
                            );";
                            new SQLiteCommand(sqlFacturaDetalle, connection, transaction).ExecuteNonQuery();

                            string sqlMovimientosStock = @"
                            CREATE TABLE IF NOT EXISTS MovimientosStock (
                                MovimientoID INTEGER PRIMARY KEY AUTOINCREMENT,
                                ProductoID INTEGER NOT NULL,
                                FacturaID INTEGER,
                                Fecha TEXT NOT NULL,
                                TipoMovimiento TEXT NOT NULL,
                                Cantidad INTEGER NOT NULL,
                                FOREIGN KEY(ProductoID) REFERENCES Productos(ProductoID),
                                FOREIGN KEY(FacturaID) REFERENCES Facturas(FacturaID)
                            );";
                            new SQLiteCommand(sqlMovimientosStock, connection, transaction).ExecuteNonQuery();


                            // --- Tablas de Permisos ---
                            new SQLiteCommand("CREATE TABLE IF NOT EXISTS Roles (RolID INTEGER PRIMARY KEY AUTOINCREMENT, NombreRol TEXT NOT NULL UNIQUE);", connection, transaction).ExecuteNonQuery();
                            new SQLiteCommand("CREATE TABLE IF NOT EXISTS Permisos (PermisoID INTEGER PRIMARY KEY AUTOINCREMENT, NombrePermiso TEXT NOT NULL UNIQUE);", connection, transaction).ExecuteNonQuery();
                            new SQLiteCommand(@"
                                CREATE TABLE IF NOT EXISTS Roles_Permisos (
                                    RolID INTEGER NOT NULL,
                                    PermisoID INTEGER NOT NULL,
                                    PRIMARY KEY (RolID, PermisoID),
                                    FOREIGN KEY(RolID) REFERENCES Roles(RolID),
                                    FOREIGN KEY(PermisoID) REFERENCES Permisos(PermisoID)
                                );", connection, transaction).ExecuteNonQuery();

                            // Poblar Roles y Permisos
                            new SQLiteCommand("INSERT OR IGNORE INTO Roles (RolID, NombreRol) VALUES (1, 'Admin');", connection, transaction).ExecuteNonQuery();
                            new SQLiteCommand("INSERT OR IGNORE INTO Roles (RolID, NombreRol) VALUES (2, 'Vendedor');", connection, transaction).ExecuteNonQuery();
                            new SQLiteCommand("INSERT OR IGNORE INTO Roles (RolID, NombreRol) VALUES (3, 'Cajero');", connection, transaction).ExecuteNonQuery();

                            new SQLiteCommand($"INSERT OR IGNORE INTO Permisos (NombrePermiso) VALUES ('{PERMISO_USUARIOS}');", connection, transaction).ExecuteNonQuery();
                            new SQLiteCommand($"INSERT OR IGNORE INTO Permisos (NombrePermiso) VALUES ('{PERMISO_CLIENTES}');", connection, transaction).ExecuteNonQuery();
                            new SQLiteCommand($"INSERT OR IGNORE INTO Permisos (NombrePermiso) VALUES ('{PERMISO_PRODUCTOS}');", connection, transaction).ExecuteNonQuery();
                            new SQLiteCommand($"INSERT OR IGNORE INTO Permisos (NombrePermiso) VALUES ('{PERMISO_STOCK}');", connection, transaction).ExecuteNonQuery();
                            new SQLiteCommand($"INSERT OR IGNORE INTO Permisos (NombrePermiso) VALUES ('{PERMISO_FACTURACION}');", connection, transaction).ExecuteNonQuery();
                            new SQLiteCommand($"INSERT OR IGNORE INTO Permisos (NombrePermiso) VALUES ('{PERMISO_VENTAS}');", connection, transaction).ExecuteNonQuery();
                            new SQLiteCommand($"INSERT OR IGNORE INTO Permisos (NombrePermiso) VALUES ('{PERMISO_PERMISOS}');", connection, transaction).ExecuteNonQuery();

                            // Asignar Permisos (Admin tiene todo)
                            new SQLiteCommand($"INSERT OR IGNORE INTO Roles_Permisos (RolID, PermisoID) SELECT 1, PermisoID FROM Permisos;", connection, transaction).ExecuteNonQuery();

                            // Asignar Permisos a Vendedor
                            new SQLiteCommand($"INSERT OR IGNORE INTO Roles_Permisos (RolID, PermisoID) SELECT 2, PermisoID FROM Permisos WHERE NombrePermiso = '{PERMISO_FACTURACION}';", connection, transaction).ExecuteNonQuery();
                            new SQLiteCommand($"INSERT OR IGNORE INTO Roles_Permisos (RolID, PermisoID) SELECT 2, PermisoID FROM Permisos WHERE NombrePermiso = '{PERMISO_CLIENTES}';", connection, transaction).ExecuteNonQuery();
                            new SQLiteCommand($"INSERT OR IGNORE INTO Roles_Permisos (RolID, PermisoID) SELECT 2, PermisoID FROM Permisos WHERE NombrePermiso = '{PERMISO_VENTAS}';", connection, transaction).ExecuteNonQuery();

                            // Migrar Usuarios Viejos
                            new SQLiteCommand(@"
                                UPDATE Usuarios 
                                SET RolID = (SELECT RolID FROM Roles WHERE NombreRol = Usuarios.Rol) 
                                WHERE RolID IS NULL AND Rol IS NOT NULL;
                            ", connection, transaction).ExecuteNonQuery();

                            new SQLiteCommand("UPDATE Usuarios SET RolID = 2 WHERE RolID IS NULL;", connection, transaction).ExecuteNonQuery();

                            transaction.Commit();
                        }
                        catch (Exception)
                        {
                            transaction.Rollback();
                            throw;
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error fatal al inicializar la base de datos: {ex.Message}");
                Environment.Exit(1);
            }
        }


        // --- MÉTODOS DE USUARIOS ---
        #region Metodos Usuarios
        public static bool ValidarUsuario(string nombreUsuario, string password)
        {
            try
            {
                using (var connection = new SQLiteConnection($"Data Source={_dbPath};Version=3;"))
                {
                    connection.Open();
                    string sql = "SELECT PasswordHash FROM Usuarios WHERE NombreUsuario = @user";
                    string storedHash = "";
                    using (var command = new SQLiteCommand(sql, connection))
                    {
                        command.Parameters.AddWithValue("@user", nombreUsuario);
                        object result = command.ExecuteScalar();
                        if (result != null) storedHash = result.ToString();
                        else return false;
                    }
                    return PasswordHasher.VerifyPassword(password, storedHash);
                }
            }
            catch (Exception ex) { MessageBox.Show($"Error al validar usuario: {ex.Message}"); return false; }
        }

        public static DataTable GetUsuarios()
        {
            var dt = new DataTable();
            try
            {
                using (var connection = new SQLiteConnection($"Data Source={_dbPath};Version=3;"))
                {
                    connection.Open();
                    string sql = @"
                        SELECT u.UsuarioID, u.NombreUsuario, r.NombreRol, u.RolID 
                        FROM Usuarios u
                        LEFT JOIN Roles r ON u.RolID = r.RolID";
                    using (var command = new SQLiteCommand(sql, connection))
                    {
                        using (var adapter = new SQLiteDataAdapter(command)) { adapter.Fill(dt); }
                    }
                }
            }
            catch (Exception ex) { MessageBox.Show($"Error al cargar usuarios: {ex.Message}"); }
            return dt;
        }

        public static List<Rol> GetRoles()
        {
            var listaRoles = new List<Rol>();
            try
            {
                using (var connection = new SQLiteConnection($"Data Source={_dbPath};Version=3;"))
                {
                    connection.Open();
                    string sql = "SELECT RolID, NombreRol FROM Roles ORDER BY NombreRol";
                    using (var command = new SQLiteCommand(sql, connection))
                    {
                        using (var reader = command.ExecuteReader())
                        {
                            while (reader.Read())
                            {
                                listaRoles.Add(new Rol
                                {
                                    RolId = Convert.ToInt32(reader["RolID"]),
                                    Nombre = reader["NombreRol"].ToString()
                                });
                            }
                        }
                    }
                }
            }
            catch (Exception ex) { MessageBox.Show($"Error al cargar roles: {ex.Message}"); }
            return listaRoles;
        }


        public static bool GuardarUsuario(int usuarioID, string nombreUsuario, string password, int rolID, string rolTexto)
        {
            string passHash = "";
            if (!string.IsNullOrEmpty(password)) passHash = PasswordHasher.HashPassword(password);
            try
            {
                using (var connection = new SQLiteConnection($"Data Source={_dbPath};Version=3;"))
                {
                    connection.Open();
                    string sql = "";
                    if (usuarioID == 0)
                    {
                        if (string.IsNullOrEmpty(passHash)) { MessageBox.Show("Contraseña obligatoria.", "Error"); return false; }
                        sql = "INSERT INTO Usuarios (NombreUsuario, PasswordHash, RolID, Rol) VALUES (@user, @pass, @rolID, @rolTexto)";
                    }
                    else
                    {
                        sql = string.IsNullOrEmpty(passHash)
                            ? "UPDATE Usuarios SET NombreUsuario = @user, RolID = @rolID, Rol = @rolTexto WHERE UsuarioID = @id"
                            : "UPDATE Usuarios SET NombreUsuario = @user, PasswordHash = @pass, RolID = @rolID, Rol = @rolTexto WHERE UsuarioID = @id";
                    }
                    using (var command = new SQLiteCommand(sql, connection))
                    {
                        command.Parameters.AddWithValue("@user", nombreUsuario);
                        command.Parameters.AddWithValue("@rolID", rolID);
                        command.Parameters.AddWithValue("@rolTexto", rolTexto);
                        command.Parameters.AddWithValue("@id", usuarioID);
                        if (sql.Contains("@pass")) command.Parameters.AddWithValue("@pass", passHash);
                        command.ExecuteNonQuery();
                        return true;
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error al guardar usuario: {ex.Message}", "Error de Base de Datos", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return false;
            }
        }

        public static bool EliminarUsuario(int usuarioID)
        {
            try
            {
                using (var connection = new SQLiteConnection($"Data Source={_dbPath};Version=3;"))
                {
                    connection.Open();
                    string sql = "DELETE FROM Usuarios WHERE UsuarioID = @id";
                    using (var command = new SQLiteCommand(sql, connection))
                    {
                        command.Parameters.AddWithValue("@id", usuarioID);
                        command.ExecuteNonQuery();
                        return true;
                    }
                }
            }
            catch (Exception ex) { MessageBox.Show($"Error al eliminar usuario: {ex.Message}"); return false; }
        }
        #endregion

        // ... (Todos tus métodos de Clientes, Productos, Facturación) ...
        // ... (No hay cambios allí) ...
        #region Metodos Clientes
        public static DataTable GetClientes()
        {
            var dt = new DataTable();
            try
            {
                using (var connection = new SQLiteConnection($"Data Source={_dbPath};Version=3;"))
                {
                    connection.Open();
                    string sql = "SELECT ClienteID, CUIT, RazonSocial, CondicionIVA FROM Clientes";
                    using (var command = new SQLiteCommand(sql, connection))
                    {
                        using (var adapter = new SQLiteDataAdapter(command)) { adapter.Fill(dt); }
                    }
                }
            }
            catch (Exception ex) { MessageBox.Show($"Error al cargar clientes: {ex.Message}"); }
            return dt;
        }
        public static bool GuardarCliente(int clienteID, string cuit, string razonSocial, string condicionIVA)
        {
            try
            {
                using (var connection = new SQLiteConnection($"Data Source={_dbPath};Version=3;"))
                {
                    connection.Open();
                    string sql = (clienteID == 0)
                        ? "INSERT INTO Clientes (CUIT, RazonSocial, CondicionIVA) VALUES (@cuit, @razon, @iva)"
                        : "UPDATE Clientes SET CUIT = @cuit, RazonSocial = @razon, CondicionIVA = @iva WHERE ClienteID = @id";
                    using (var command = new SQLiteCommand(sql, connection))
                    {
                        command.Parameters.AddWithValue("@cuit", cuit);
                        command.Parameters.AddWithValue("@razon", razonSocial);
                        command.Parameters.AddWithValue("@iva", condicionIVA);
                        command.Parameters.AddWithValue("@id", clienteID);
                        command.ExecuteNonQuery();
                        return true;
                    }
                }
            }
            catch (Exception ex) { MessageBox.Show($"Error al guardar cliente: {ex.Message}"); return false; }
        }
        public static bool EliminarCliente(int clienteID)
        {
            try
            {
                using (var connection = new SQLiteConnection($"Data Source={_dbPath};Version=3;"))
                {
                    connection.Open();
                    string sql = "DELETE FROM Clientes WHERE ClienteID = @id";
                    using (var command = new SQLiteCommand(sql, connection))
                    {
                        command.Parameters.AddWithValue("@id", clienteID);
                        command.ExecuteNonQuery();
                        return true;
                    }
                }
            }
            catch (Exception ex) { MessageBox.Show($"Error al eliminar cliente: {ex.Message}"); return false; }
        }
        public static DataRow BuscarCliente(string query)
        {
            DataRow row = null;
            try
            {
                using (var connection = new SQLiteConnection($"Data Source={_dbPath};Version=3;"))
                {
                    connection.Open();
                    string sql = "SELECT * FROM Clientes WHERE CUIT = @query OR RazonSocial LIKE @likeQuery LIMIT 1";
                    var dt = new DataTable();
                    using (var command = new SQLiteCommand(sql, connection))
                    {
                        command.Parameters.AddWithValue("@query", query);
                        command.Parameters.AddWithValue("@likeQuery", $"%{query}%");
                        using (var adapter = new SQLiteDataAdapter(command)) { adapter.Fill(dt); }
                    }
                    if (dt.Rows.Count > 0) row = dt.Rows[0];
                }
            }
            catch (Exception ex) { MessageBox.Show($"Error al buscar cliente: {ex.Message}"); }
            return row;
        }
        public static DataTable BuscarClientesMultiples(string query)
        {
            var dt = new DataTable();
            try
            {
                using (var connection = new SQLiteConnection($"Data Source={_dbPath};Version=3;"))
                {
                    connection.Open();
                    string sql = "SELECT ClienteID, CUIT, RazonSocial FROM Clientes WHERE CUIT LIKE @likeQuery OR RazonSocial LIKE @likeQuery LIMIT 10";
                    using (var command = new SQLiteCommand(sql, connection))
                    {
                        command.Parameters.AddWithValue("@likeQuery", $"%{query}%");
                        using (var adapter = new SQLiteDataAdapter(command)) { adapter.Fill(dt); }
                    }
                }
            }
            catch (Exception ex) { MessageBox.Show($"Error al buscar clientes: {ex.Message}"); }
            return dt;
        }
        #endregion
        #region Metodos Productos
        public static DataTable GetProductos()
        {
            var dt = new DataTable();
            try
            {
                using (var connection = new SQLiteConnection($"Data Source={_dbPath};Version=3;"))
                {
                    connection.Open();
                    string sql = "SELECT ProductoID, Codigo, Descripcion, PrecioVenta, StockActual FROM Productos";
                    using (var command = new SQLiteCommand(sql, connection))
                    {
                        using (var adapter = new SQLiteDataAdapter(command)) { adapter.Fill(dt); }
                    }
                }
            }
            catch (Exception ex) { MessageBox.Show($"Error al cargar productos: {ex.Message}"); }
            return dt;
        }
        public static bool GuardarProducto(int productoID, string codigo, string descripcion, decimal precioVenta, int stockActual)
        {
            try
            {
                using (var connection = new SQLiteConnection($"Data Source={_dbPath};Version=3;"))
                {
                    connection.Open();
                    string sql = (productoID == 0)
                        ? "INSERT INTO Productos (Codigo, Descripcion, PrecioVenta, StockActual) VALUES (@codigo, @desc, @precio, @stock)"
                        : "UPDATE Productos SET Codigo = @codigo, Descripcion = @desc, PrecioVenta = @precio, StockActual = @stock WHERE ProductoID = @id";
                    using (var command = new SQLiteCommand(sql, connection))
                    {
                        command.Parameters.AddWithValue("@codigo", codigo);
                        command.Parameters.AddWithValue("@desc", descripcion);
                        command.Parameters.AddWithValue("@precio", (double)precioVenta);
                        command.Parameters.AddWithValue("@stock", stockActual);
                        command.Parameters.AddWithValue("@id", productoID);
                        command.ExecuteNonQuery();
                        return true;
                    }
                }
            }
            catch (Exception ex) { MessageBox.Show($"Error al guardar producto: {ex.Message}"); return false; }
        }
        public static bool EliminarProducto(int productoID)
        {
            try
            {
                using (var connection = new SQLiteConnection($"Data Source={_dbPath};Version=3;"))
                {
                    connection.Open();
                    string sql = "DELETE FROM Productos WHERE ProductoID = @id";
                    using (var command = new SQLiteCommand(sql, connection))
                    {
                        command.Parameters.AddWithValue("@id", productoID);
                        command.ExecuteNonQuery();
                        return true;
                    }
                }
            }
            catch (Exception ex) { MessageBox.Show($"Error al eliminar producto: {ex.Message}"); return false; }
        }
        public static DataRow BuscarProducto(string query)
        {
            DataRow row = null;
            try
            {
                using (var connection = new SQLiteConnection($"Data Source={_dbPath};Version=3;"))
                {
                    connection.Open();
                    string sql = "SELECT * FROM Productos WHERE Codigo = @query OR Descripcion LIKE @likeQuery LIMIT 1";
                    var dt = new DataTable();
                    using (var command = new SQLiteCommand(sql, connection))
                    {
                        command.Parameters.AddWithValue("@query", query);
                        command.Parameters.AddWithValue("@likeQuery", $"%{query}%");
                        using (var adapter = new SQLiteDataAdapter(command)) { adapter.Fill(dt); }
                    }
                    if (dt.Rows.Count > 0) row = dt.Rows[0];
                }
            }
            catch (Exception ex) { MessageBox.Show($"Error al buscar producto: {ex.Message}"); }
            return row;
        }
        public static DataTable BuscarProductosMultiples(string query)
        {
            var dt = new DataTable();
            try
            {
                using (var connection = new SQLiteConnection($"Data Source={_dbPath};Version=3;"))
                {
                    connection.Open();
                    string sql = "SELECT ProductoID, Codigo, Descripcion, PrecioVenta, StockActual FROM Productos WHERE (Codigo LIKE @likeQuery OR Descripcion LIKE @likeQuery) AND StockActual > 0 LIMIT 10";
                    using (var command = new SQLiteCommand(sql, connection))
                    {
                        command.Parameters.AddWithValue("@likeQuery", $"%{query}%");
                        using (var adapter = new SQLiteDataAdapter(command)) { adapter.Fill(dt); }
                    }
                }
            }
            catch (Exception ex) { MessageBox.Show($"Error al buscar productos: {ex.Message}"); }
            return dt;
        }
        #endregion
        #region Metodos Facturacion y Stock

        public static bool GuardarFactura(int clienteID, string tipoComprobante, decimal total, List<FacturaItem> items)
        {
            using (var connection = new SQLiteConnection($"Data Source={_dbPath};Version=3;"))
            {
                connection.Open();
                using (var transaction = connection.BeginTransaction())
                {
                    try
                    {
                        string fechaActual = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");

                        string sqlFactura = "INSERT INTO Facturas (ClienteID, Fecha, Total, TipoComprobante) VALUES (@ClienteID, @Fecha, @Total, @Tipo)";
                        using (var command = new SQLiteCommand(sqlFactura, connection, transaction))
                        {
                            command.Parameters.AddWithValue("@ClienteID", clienteID);
                            command.Parameters.AddWithValue("@Fecha", fechaActual);
                            command.Parameters.AddWithValue("@Total", (double)total);
                            command.Parameters.AddWithValue("@Tipo", tipoComprobante);
                            command.ExecuteNonQuery();
                        }

                        long facturaID = connection.LastInsertRowId;

                        foreach (var item in items)
                        {
                            string sqlDetalle = "INSERT INTO FacturaDetalle (FacturaID, ProductoID, Cantidad, PrecioUnitario) VALUES (@FacturaID, @ProductoID, @Cantidad, @Precio)";
                            using (var command = new SQLiteCommand(sqlDetalle, connection, transaction))
                            {
                                command.Parameters.AddWithValue("@FacturaID", facturaID);
                                command.Parameters.AddWithValue("@ProductoID", item.ProductoID);
                                command.Parameters.AddWithValue("@Cantidad", item.Cantidad);
                                command.Parameters.AddWithValue("@Precio", (double)item.PrecioUnitario);
                                command.ExecuteNonQuery();
                            }

                            string sqlStock = "UPDATE Productos SET StockActual = StockActual - @Cantidad WHERE ProductoID = @ProductoID";
                            using (var command = new SQLiteCommand(sqlStock, connection, transaction))
                            {
                                command.Parameters.AddWithValue("@Cantidad", item.Cantidad);
                                command.Parameters.AddWithValue("@ProductoID", item.ProductoID);
                                int rowsAffected = command.ExecuteNonQuery();
                                if (rowsAffected == 0) throw new Exception($"Error al actualizar stock del producto ID: {item.ProductoID}.");
                            }

                            string sqlMovimiento = @"
                                INSERT INTO MovimientosStock (ProductoID, FacturaID, Fecha, TipoMovimiento, Cantidad) 
                                VALUES (@ProductoID, @FacturaID, @Fecha, @Tipo, @Cantidad)";
                            using (var command = new SQLiteCommand(sqlMovimiento, connection, transaction))
                            {
                                command.Parameters.AddWithValue("@ProductoID", item.ProductoID);
                                command.Parameters.AddWithValue("@FacturaID", facturaID);
                                command.Parameters.AddWithValue("@Fecha", fechaActual);
                                command.Parameters.AddWithValue("@Tipo", "Venta");
                                command.Parameters.AddWithValue("@Cantidad", item.Cantidad * -1);
                                command.ExecuteNonQuery();
                            }
                        }

                        transaction.Commit();
                        return true;
                    }
                    catch (Exception ex)
                    {
                        transaction.Rollback();
                        MessageBox.Show($"Error al guardar la factura: {ex.Message}");
                        return false;
                    }
                }
            }
        }

        public static DataTable GetFacturasPorFecha(DateTime desde, DateTime hasta)
        {
            var dt = new DataTable();
            try
            {
                using (var connection = new SQLiteConnection($"Data Source={_dbPath};Version=3;"))
                {
                    connection.Open();
                    string sql = @"
                        SELECT 
                            f.FacturaID, 
                            f.Fecha, 
                            c.RazonSocial, 
                            f.TipoComprobante, 
                            f.Total 
                        FROM Facturas f
                        JOIN Clientes c ON f.ClienteID = c.ClienteID
                        WHERE f.Fecha BETWEEN @Desde AND @Hasta
                        ORDER BY f.Fecha DESC";

                    using (var command = new SQLiteCommand(sql, connection))
                    {
                        command.Parameters.AddWithValue("@Desde", desde.ToString("yyyy-MM-dd 00:00:00"));
                        command.Parameters.AddWithValue("@Hasta", hasta.ToString("yyyy-MM-dd 23:59:59"));
                        using (var adapter = new SQLiteDataAdapter(command)) { adapter.Fill(dt); }
                    }
                }
            }
            catch (Exception ex) { MessageBox.Show($"Error al cargar facturas: {ex.Message}"); }
            return dt;
        }

        public static DataTable GetFacturaDetalle(int facturaID)
        {
            var dt = new DataTable();
            try
            {
                using (var connection = new SQLiteConnection($"Data Source={_dbPath};Version=3;"))
                {
                    connection.Open();
                    string sql = @"
                        SELECT 
                            p.Codigo, 
                            p.Descripcion, 
                            fd.Cantidad, 
                            fd.PrecioUnitario,
                            (fd.Cantidad * fd.PrecioUnitario) AS Subtotal
                        FROM FacturaDetalle fd
                        JOIN Productos p ON fd.ProductoID = p.ProductoID
                        WHERE fd.FacturaID = @FacturaID";

                    using (var command = new SQLiteCommand(sql, connection))
                    {
                        command.Parameters.AddWithValue("@FacturaID", facturaID);
                        using (var adapter = new SQLiteDataAdapter(command)) { adapter.Fill(dt); }
                    }
                }
            }
            catch (Exception ex) { MessageBox.Show($"Error al cargar detalle de factura: {ex.Message}"); }
            return dt;
        }

        public static bool AjustarStock(int productoID, int cantidad, string tipoMovimiento)
        {
            if (cantidad == 0) return false;

            using (var connection = new SQLiteConnection($"Data Source={_dbPath};Version=3;"))
            {
                connection.Open();
                using (var transaction = connection.BeginTransaction())
                {
                    try
                    {
                        string sqlStock = "UPDATE Productos SET StockActual = StockActual + @Cantidad WHERE ProductoID = @ProductoID";
                        using (var command = new SQLiteCommand(sqlStock, connection, transaction))
                        {
                            command.Parameters.AddWithValue("@Cantidad", cantidad);
                            command.Parameters.AddWithValue("@ProductoID", productoID);
                            int rowsAffected = command.ExecuteNonQuery();
                            if (rowsAffected == 0) throw new Exception($"Producto ID {productoID} no encontrado.");
                        }

                        string sqlMovimiento = @"
                            INSERT INTO MovimientosStock (ProductoID, FacturaID, Fecha, TipoMovimiento, Cantidad) 
                            VALUES (@ProductoID, NULL, @Fecha, @Tipo, @Cantidad)";
                        using (var command = new SQLiteCommand(sqlMovimiento, connection, transaction))
                        {
                            command.Parameters.AddWithValue("@ProductoID", productoID);
                            command.Parameters.AddWithValue("@Fecha", DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"));
                            command.Parameters.AddWithValue("@Tipo", tipoMovimiento);
                            command.Parameters.AddWithValue("@Cantidad", cantidad);
                            command.ExecuteNonQuery();
                        }

                        transaction.Commit();
                        return true;
                    }
                    catch (Exception ex)
                    {
                        transaction.Rollback();
                        MessageBox.Show($"Error al ajustar el stock: {ex.Message}");
                        return false;
                    }
                }
            }
        }
        #endregion

        #region Metodos Permisos

        public static List<Permiso> GetPermisos()
        {
            var listaPermisos = new List<Permiso>();
            try
            {
                using (var connection = new SQLiteConnection($"Data Source={_dbPath};Version=3;"))
                {
                    connection.Open();
                    string sql = "SELECT PermisoID, NombrePermiso FROM Permisos ORDER BY NombrePermiso";
                    using (var command = new SQLiteCommand(sql, connection))
                    {
                        using (var reader = command.ExecuteReader())
                        {
                            while (reader.Read())
                            {
                                listaPermisos.Add(new Permiso
                                {
                                    PermisoId = Convert.ToInt32(reader["PermisoID"]),
                                    Nombre = reader["NombrePermiso"].ToString()
                                });
                            }
                        }
                    }
                }
            }
            catch (Exception ex) { MessageBox.Show($"Error al cargar permisos: {ex.Message}"); }
            return listaPermisos;
        }

        public static Dictionary<int, List<int>> GetPermisosPorRol()
        {
            var dict = new Dictionary<int, List<int>>();
            try
            {
                using (var connection = new SQLiteConnection($"Data Source={_dbPath};Version=3;"))
                {
                    connection.Open();
                    string sql = "SELECT RolID, PermisoID FROM Roles_Permisos";
                    using (var command = new SQLiteCommand(sql, connection))
                    {
                        using (var reader = command.ExecuteReader())
                        {
                            while (reader.Read())
                            {
                                int rolId = Convert.ToInt32(reader["RolID"]);
                                int permisoId = Convert.ToInt32(reader["PermisoID"]);

                                if (!dict.ContainsKey(rolId))
                                {
                                    dict[rolId] = new List<int>();
                                }
                                dict[rolId].Add(permisoId);
                            }
                        }
                    }
                }
            }
            catch (Exception ex) { MessageBox.Show($"Error al cargar permisos por rol: {ex.Message}"); }
            return dict;
        }

        public static void ActualizarPermisosParaRol(int rolId, List<int> permisosIds)
        {
            using (var connection = new SQLiteConnection($"Data Source={_dbPath};Version=3;"))
            {
                connection.Open();
                using (var transaction = connection.BeginTransaction())
                {
                    try
                    {
                        string sqlDelete = "DELETE FROM Roles_Permisos WHERE RolID = @RolID";
                        using (var command = new SQLiteCommand(sqlDelete, connection, transaction))
                        {
                            command.Parameters.AddWithValue("@RolID", rolId);
                            command.ExecuteNonQuery();
                        }

                        if (permisosIds != null)
                        {
                            string sqlInsert = "INSERT INTO Roles_Permisos (RolID, PermisoID) VALUES (@RolID, @PermisoID)";
                            foreach (int permisoId in permisosIds)
                            {
                                using (var command = new SQLiteCommand(sqlInsert, connection, transaction))
                                {
                                    command.Parameters.AddWithValue("@RolID", rolId);
                                    command.Parameters.AddWithValue("@PermisoID", permisoId);
                                    command.ExecuteNonQuery();
                                }
                            }
                        }

                        transaction.Commit();
                    }
                    catch (Exception)
                    {
                        transaction.Rollback();
                        throw;
                    }
                }
            }
        }


        // ===== INICIO DE CÓDIGO NUEVO (SESIÓN) =====
        /// <summary>
        /// Busca el RolID y los permisos de un usuario y los carga en la SesionUsuario estática.
        /// </summary>
        /// <param name="nombreUsuario">El usuario que inicia sesión.</param>
        /// <returns>True si tiene éxito, False si el usuario no se encuentra.</returns>
        public static bool CargarSesionUsuario(string nombreUsuario)
        {
            try
            {
                using (var connection = new SQLiteConnection($"Data Source={_dbPath};Version=3;"))
                {
                    connection.Open();

                    // 1. Obtener el RolID del usuario
                    int rolId = 0;
                    string sqlRol = "SELECT RolID FROM Usuarios WHERE NombreUsuario = @user";
                    using (var cmdRol = new SQLiteCommand(sqlRol, connection))
                    {
                        cmdRol.Parameters.AddWithValue("@user", nombreUsuario);
                        var result = cmdRol.ExecuteScalar();
                        if (result == null || result == DBNull.Value)
                        {
                            // Si el usuario no tiene RolID (caso raro), asignarle Vendedor por defecto
                            rolId = 2;
                        }
                        else
                        {
                            rolId = Convert.ToInt32(result);
                        }
                    }

                    // 2. Obtener la lista de nombres de permisos para ese RolID
                    var permisos = new List<string>();
                    string sqlPermisos = @"
                        SELECT p.NombrePermiso 
                        FROM Roles_Permisos rp
                        JOIN Permisos p ON rp.PermisoID = p.PermisoID
                        WHERE rp.RolID = @rolID";

                    using (var cmdPermisos = new SQLiteCommand(sqlPermisos, connection))
                    {
                        cmdPermisos.Parameters.AddWithValue("@rolID", rolId);
                        using (var reader = cmdPermisos.ExecuteReader())
                        {
                            while (reader.Read())
                            {
                                permisos.Add(reader["NombrePermiso"].ToString());
                            }
                        }
                    }

                    // 3. Iniciar la sesión global
                    SesionUsuario.Iniciar(nombreUsuario, rolId, permisos);
                    return true;
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error fatal al cargar permisos de usuario: {ex.Message}");
                return false;
            }
        }
        // ===== FIN DE CÓDIGO NUEVO (SESIÓN) =====

        #endregion
    }
}