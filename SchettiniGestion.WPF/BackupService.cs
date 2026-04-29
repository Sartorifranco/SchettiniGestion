using System.Data.SqlClient;

namespace SchettiniGestion.WPF
{
    public static class BackupService
    {
        public static bool RealizarBackup(string ruta)
        {
            try
            {
                var b = new SqlConnectionStringBuilder(SchettiniGestion.DatabaseService.ConnectionString);
                string dbName = b.InitialCatalog;
                if (string.IsNullOrWhiteSpace(dbName)) return false;
                string seguro = dbName.Replace("]", "]]");

                using (var c = new SqlConnection(SchettiniGestion.DatabaseService.ConnectionString))
                {
                    c.Open();
                    string sql = $"BACKUP DATABASE [{seguro}] TO DISK = N'{ruta.Replace("'", "''")}' WITH NOFORMAT, NOINIT, SKIP, NOREWIND, NOUNLOAD, STATS = 10";
                    new SqlCommand(sql, c).ExecuteNonQuery();
                    return true;
                }
            }
            catch { return false; }
        }
    }
}
