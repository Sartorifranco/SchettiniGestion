using System.Data.SqlClient;

namespace SchettiniGestion.WPF
{
    public static class BackupService
    {
        public static bool RealizarBackup(string ruta)
        {
            try
            {
                using (var c = new SqlConnection(SchettiniGestion.DatabaseService.ConnectionString))
                {
                    c.Open();
                    string sql = $"BACKUP DATABASE SchPosDB TO DISK = N'{ruta}' WITH NOFORMAT, NOINIT, SKIP, NOREWIND, NOUNLOAD, STATS = 10";
                    new SqlCommand(sql, c).ExecuteNonQuery();
                    return true;
                }
            }
            catch { return false; }
        }
    }
}
