using System;
using System.Data.SqlClient;
using System.IO;

namespace SchettiniGestion.WPF
{
    public static class BackupService
    {
        /// <summary>
        /// Realiza BACKUP DATABASE a la ruta indicada.
        /// La ruta debe ser accesible por la cuenta del servicio SQL Server.
        /// Retorna null si ok, o el mensaje de error si falla.
        /// </summary>
        public static string RealizarBackup(string ruta)
        {
            try
            {
                var b = new SqlConnectionStringBuilder(SchettiniGestion.DatabaseService.ConnectionString);
                string dbName = b.InitialCatalog;
                if (string.IsNullOrWhiteSpace(dbName))
                    return "No se encontró el nombre de la base de datos en la cadena de conexión.";

                string seguro = dbName.Replace("]", "]]");
                string rutaSql = ruta.Replace("'", "''");

                using (var c = new SqlConnection(SchettiniGestion.DatabaseService.ConnectionString))
                {
                    c.Open();
                    // Timeout extendido: backups grandes pueden tardar
                    using (var cmd = new SqlCommand(
                        $"BACKUP DATABASE [{seguro}] TO DISK = N'{rutaSql}' WITH FORMAT, INIT, SKIP, NOREWIND, NOUNLOAD, STATS = 10",
                        c))
                    {
                        cmd.CommandTimeout = 300; // 5 minutos
                        cmd.ExecuteNonQuery();
                    }
                }
                return null; // éxito
            }
            catch (Exception ex)
            {
                return ex.Message;
            }
        }

        /// <summary>
        /// Restaura la base de datos desde un archivo .bak.
        /// ¡REEMPLAZA TODOS LOS DATOS ACTUALES!
        /// Retorna null si ok, o el mensaje de error si falla.
        /// </summary>
        public static string RestaurarBackup(string rutaBak)
        {
            string stagedPath = null;
            try
            {
                if (!File.Exists(rutaBak))
                    return $"El archivo no existe: {rutaBak}";

                var b = new SqlConnectionStringBuilder(SchettiniGestion.DatabaseService.ConnectionString);
                string dbName = b.InitialCatalog;
                if (string.IsNullOrWhiteSpace(dbName))
                    return "No se encontró el nombre de la base de datos en la cadena de conexión.";

                // SQL Server debe poder leer el archivo: copiar a carpeta local accesible
                stagedPath = PrepararRutaRestauracionSql(rutaBak);
                string seguro   = dbName.Replace("]", "]]");
                string rutaSql  = stagedPath.Replace("'", "''");

                // Conectar a 'master' para poder cerrar conexiones y restaurar
                var builderMaster = new SqlConnectionStringBuilder(SchettiniGestion.DatabaseService.ConnectionString);
                builderMaster.InitialCatalog = "master";

                using (var c = new SqlConnection(builderMaster.ConnectionString))
                {
                    c.Open();
                    c.InfoMessage += (s, e) => System.Diagnostics.Debug.WriteLine("RESTORE: " + e.Message);

                    // Cerrar todas las conexiones activas a la BD
                    using (var cmd = new SqlCommand(
                        $"ALTER DATABASE [{seguro}] SET SINGLE_USER WITH ROLLBACK IMMEDIATE", c))
                    {
                        cmd.CommandTimeout = 30;
                        cmd.ExecuteNonQuery();
                    }

                    // Restaurar
                    using (var cmd = new SqlCommand(
                        $"RESTORE DATABASE [{seguro}] FROM DISK = N'{rutaSql}' WITH REPLACE, RECOVERY, STATS = 10",
                        c))
                    {
                        cmd.CommandTimeout = 600; // 10 minutos
                        cmd.ExecuteNonQuery();
                    }

                    // Volver a multi-user
                    using (var cmd = new SqlCommand(
                        $"ALTER DATABASE [{seguro}] SET MULTI_USER", c))
                    {
                        cmd.CommandTimeout = 30;
                        cmd.ExecuteNonQuery();
                    }
                }
                return null; // éxito
            }
            catch (Exception ex)
            {
                // Intentar restaurar MULTI_USER si falló en medio del restore
                try
                {
                    var b2 = new SqlConnectionStringBuilder(SchettiniGestion.DatabaseService.ConnectionString);
                    string dbName2 = b2.InitialCatalog;
                    b2.InitialCatalog = "master";
                    using (var c2 = new SqlConnection(b2.ConnectionString))
                    {
                        c2.Open();
                        new SqlCommand($"ALTER DATABASE [{dbName2.Replace("]", "]]")}] SET MULTI_USER", c2)
                            { CommandTimeout = 15 }.ExecuteNonQuery();
                    }
                }
                catch { }

                return ex.Message;
            }
            finally
            {
                if (stagedPath != null)
                {
                    try { File.Delete(stagedPath); } catch { }
                }
            }
        }

        /// <summary>
        /// Copia el .bak a una carpeta que SQL Server (LocalDB o servicio local) pueda leer.
        /// </summary>
        private static string PrepararRutaRestauracionSql(string rutaOrigen)
        {
            string stagingDir = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData),
                "SCHPOS", "Restore");
            Directory.CreateDirectory(stagingDir);

            string destino = Path.Combine(stagingDir,
                "restore_" + DateTime.Now.ToString("yyyyMMdd_HHmmss") + ".bak");

            File.Copy(rutaOrigen, destino, overwrite: true);
            return destino;
        }
    }
}
