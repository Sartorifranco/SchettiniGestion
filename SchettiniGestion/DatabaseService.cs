using System;
using System.Data.SQLite;
using System.IO;
using System.Windows.Forms;
using System.Data;

namespace SchettiniGestion
{
    public static class DatabaseService
    {
        private static string _dbPath;

        public static void InitializeDatabase()
        {
            try
            {
                string appDataFolder = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
                string appFolder = Path.Combine(appDataFolder, "SchettiniGestion");

                if (!Directory.Exists(appFolder))
                {
                    Directory.CreateDirectory(appFolder);
                }

                _dbPath = Path.Combine(appFolder, "SchettiniGestion.sqlite");

                using (var connection = new SQLiteConnection($"Data Source={_dbPath};Version=3;"))
                {
                    connection.Open();

                    // Tabla Usuarios
                    string sqlUsuarios = @"
                    CREATE TABLE IF NOT EXISTS Usuarios (
                        UsuarioID INTEGER PRIMARY KEY AUTOINCREMENT,
                        NombreUsuario TEXT NOT NULL UNIQUE,
                        PasswordHash TEXT NOT NULL,
                        Rol TEXT NOT NULL
                    );";
                    using (var command = new SQLiteCommand(sqlUsuarios, connection))
                    {
                        command.ExecuteNonQuery();
                    }

                    string adminPassHash = PasswordHasher.HashPassword("12345");

                    string sqlAdmin = @"
                    INSERT OR IGNORE INTO Usuarios (NombreUsuario, PasswordHash, Rol) 
                    VALUES ('admin', @pass, 'Admin');";

                    using (var command = new SQLiteCommand(sqlAdmin, connection))
                    {
                        command.Parameters.AddWithValue("@pass", adminPassHash);
                        command.ExecuteNonQuery();
                    }

                    // Tabla Productos
                    string sqlProductos = @"
                    CREATE TABLE IF NOT EXISTS Productos (
                        ProductoID INTEGER PRIMARY KEY AUTOINCREMENT,
                        Codigo TEXT,
                        Descripcion TEXT NOT NULL,
                        PrecioVenta REAL NOT NULL,
                        StockActual REAL DEFAULT 0
                    );";
                    using (var command = new SQLiteCommand(sqlProductos, connection))
                    {
                        command.ExecuteNonQuery();
                    }

                    // Tabla Clientes
                    string sqlClientes = @"
                    CREATE TABLE IF NOT EXISTS Clientes (
                        ClienteID INTEGER PRIMARY KEY AUTOINCREMENT,
                        CUIT TEXT NOT NULL UNIQUE,
                        RazonSocial TEXT NOT NULL,
                        CondicionIVA TEXT
                    );";
                    using (var command = new SQLiteCommand(sqlClientes, connection))
                    {
                        command.ExecuteNonQuery();
                    }
                }
            }
            catch (Exception ex)
            {
                System.Windows.Forms.MessageBox.Show($"Error fatal al inicializar la base de datos: {ex.Message}");
                Environment.Exit(1);
            }
        }

        // --- MÉTODOS DE USUARIOS ---

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

                        if (result != null)
                        {
                            storedHash = result.ToString();
                        }
                        else
                        {
                            return false;
                        }
                    }
                    return PasswordHasher.VerifyPassword(password, storedHash);
                }
            }
            catch (Exception ex)
            {
                System.Windows.Forms.MessageBox.Show($"Error al validar usuario: {ex.Message}");
                return false;
            }
        }

        public static System.Data.DataTable GetUsuarios()
        {
            var dt = new System.Data.DataTable();
            try
            {
                using (var connection = new SQLiteConnection($"Data Source={_dbPath};Version=3;"))
                {
                    connection.Open();
                    string sql = "SELECT UsuarioID, NombreUsuario, Rol FROM Usuarios";
                    using (var command = new SQLiteCommand(sql, connection))
                    {
                        using (var adapter = new SQLiteDataAdapter(command))
                        {
                            adapter.Fill(dt);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                System.Windows.Forms.MessageBox.Show($"Error al cargar usuarios: {ex.Message}");
            }
            return dt;
        }

        public static bool GuardarUsuario(int usuarioID, string nombreUsuario, string password, string rol)
        {
            string passHash = "";
            if (!string.IsNullOrEmpty(password))
            {
                passHash = PasswordHasher.HashPassword(password);
            }

            try
            {
                using (var connection = new SQLiteConnection($"Data Source={_dbPath};Version=3;"))
                {
                    connection.Open();
                    string sql = "";

                    if (usuarioID == 0)
                    {
                        if (string.IsNullOrEmpty(passHash))
                        {
                            MessageBox.Show("Para un usuario nuevo, la contraseña es obligatoria.", "Error", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                            return false;
                        }
                        sql = "INSERT INTO Usuarios (NombreUsuario, PasswordHash, Rol) VALUES (@user, @pass, @rol)";
                    }
                    else
                    {
                        if (string.IsNullOrEmpty(passHash))
                        {
                            sql = "UPDATE Usuarios SET NombreUsuario = @user, Rol = @rol WHERE UsuarioID = @id";
                        }
                        else
                        {
                            sql = "UPDATE Usuarios SET NombreUsuario = @user, PasswordHash = @pass, Rol = @rol WHERE UsuarioID = @id";
                        }
                    }

                    using (var command = new SQLiteCommand(sql, connection))
                    {
                        command.Parameters.AddWithValue("@user", nombreUsuario);
                        command.Parameters.AddWithValue("@rol", rol);
                        command.Parameters.AddWithValue("@id", usuarioID);

                        if (sql.Contains("@pass"))
                        {
                            command.Parameters.AddWithValue("@pass", passHash);
                        }

                        command.ExecuteNonQuery();
                        return true;
                    }
                }
            }
            catch (SQLiteException ex)
            {
                if (ex.ErrorCode == 19)
                {
                    MessageBox.Show("Error: El nombre de usuario ya existe.", "Error al guardar", MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
                else
                {
                    MessageBox.Show($"Error de base de datos: {ex.Message}", "Error al guardar", MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
                return false;
            }
            catch (Exception ex)
            {
                System.Windows.Forms.MessageBox.Show($"Error al guardar usuario: {ex.Message}");
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
            catch (Exception ex)
            {
                System.Windows.Forms.MessageBox.Show($"Error al eliminar usuario: {ex.Message}");
                return false;
            }
        }

        // --- MÉTODOS DE CLIENTES ---

        public static System.Data.DataTable GetClientes()
        {
            var dt = new System.Data.DataTable();
            try
            {
                using (var connection = new SQLiteConnection($"Data Source={_dbPath};Version=3;"))
                {
                    connection.Open();
                    string sql = "SELECT ClienteID, CUIT, RazonSocial, CondicionIVA FROM Clientes";
                    using (var command = new SQLiteCommand(sql, connection))
                    {
                        using (var adapter = new SQLiteDataAdapter(command))
                        {
                            adapter.Fill(dt);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                System.Windows.Forms.MessageBox.Show($"Error al cargar clientes: {ex.Message}");
            }
            return dt;
        }

        public static bool GuardarCliente(int clienteID, string cuit, string razonSocial, string condicionIVA)
        {
            try
            {
                using (var connection = new SQLiteConnection($"Data Source={_dbPath};Version=3;"))
                {
                    connection.Open();
                    string sql = "";

                    if (clienteID == 0)
                    {
                        // --- CREAR NUEVO CLIENTE ---
                        sql = "INSERT INTO Clientes (CUIT, RazonSocial, CondicionIVA) VALUES (@cuit, @razon, @iva)";
                    }
                    else
                    {
                        // --- ACTUALIZAR CLIENTE EXISTENTE ---
                        sql = "UPDATE Clientes SET CUIT = @cuit, RazonSocial = @razon, CondicionIVA = @iva WHERE ClienteID = @id";
                    }

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
            catch (SQLiteException ex)
            {
                if (ex.ErrorCode == 19)
                {
                    MessageBox.Show("Error: El CUIT ingresado ya existe.", "Error al guardar", MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
                else
                {
                    MessageBox.Show($"Error de base de datos: {ex.Message}", "Error al guardar", MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
                return false;
            }
            catch (Exception ex)
            {
                System.Windows.Forms.MessageBox.Show($"Error al guardar cliente: {ex.Message}");
                return false;
            }
        }

        // --- ¡INICIO DEL CÓDIGO NUEVO (PASO 32)! ---
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
            catch (Exception ex)
            {
                // NOTA: Si en el futuro un cliente tiene facturas asociadas,
                // esto podría fallar (lo cual es bueno, previene borrar datos).
                // Por ahora, un mensaje de error genérico es suficiente.
                System.Windows.Forms.MessageBox.Show($"Error al eliminar cliente: {ex.Message}");
                return false;
            }
        }
        // --- ¡FIN DEL CÓDIGO NUEVO (PASO 32)! ---
    }
}

