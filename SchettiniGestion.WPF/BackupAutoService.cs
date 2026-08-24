using System;
using System.Data.SqlClient;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using SchettiniGestion;

namespace SchettiniGestion.WPF
{
    /// <summary>
    /// Backup automático programado: genera el .bak localmente (donde vive el motor SQL)
    /// y lo copia a una carpeta externa (pendrive, disco de red, carpeta sincronizada con la nube).
    /// La programación diaria se hace con el Programador de tareas de Windows, invocando
    /// este mismo ejecutable con el argumento "/autobackup".
    /// </summary>
    public static class BackupAutoService
    {
        private const string NombreTarea = "SCHPOS_BackupAutomatico";

        private static string CarpetaStaging =>
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData), "SCHPOS", "BackupsAuto");

        private static string RutaLog =>
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData), "SCHPOS", "backup_auto.log");

        /// <summary>
        /// Devuelve true si la base de datos configurada vive en esta misma PC (LocalDB, "." o el propio
        /// nombre de equipo/IP). Solo tiene sentido programar el backup automático en esa PC: ahí es donde
        /// corre el motor SQL Server que genera el archivo .bak.
        /// </summary>
        public static bool EsEstaPCElHostDeLaBaseDeDatos()
        {
            try
            {
                var b = new SqlConnectionStringBuilder(DatabaseService.ConnectionString);
                string src = (b.DataSource ?? "").Trim();
                if (src.Length == 0) return true;

                string host = src.Split(',')[0]; // sacar ",puerto"
                host = host.Split('\\')[0];       // sacar "\instancia"
                host = host.Trim();

                if (host.IndexOf("(localdb)", StringComparison.OrdinalIgnoreCase) >= 0) return true;
                if (host == "." || host.Equals("localhost", StringComparison.OrdinalIgnoreCase) || host.Equals("(local)", StringComparison.OrdinalIgnoreCase))
                    return true;
                if (host == "127.0.0.1" || host == "::1") return true;
                if (host.Equals(Environment.MachineName, StringComparison.OrdinalIgnoreCase)) return true;

                // No usar Dns.GetHostAddresses: en notebooks con DNS lento/roto congela Configuración.
                if (IPAddress.TryParse(host, out IPAddress ipObjetivo))
                    return IpEsLocal(ipObjetivo);

                return false;
            }
            catch { return true; }
        }

        private static bool IpEsLocal(IPAddress ip)
        {
            if (IPAddress.IsLoopback(ip)) return true;
            try
            {
                foreach (var ni in NetworkInterface.GetAllNetworkInterfaces())
                {
                    if (ni.OperationalStatus != OperationalStatus.Up) continue;
                    if (ni.NetworkInterfaceType == NetworkInterfaceType.Loopback) continue;
                    foreach (var ua in ni.GetIPProperties().UnicastAddresses)
                    {
                        if (ua.Address.AddressFamily != ip.AddressFamily) continue;
                        if (ua.Address.Equals(ip)) return true;
                    }
                }
            }
            catch { }
            return false;
        }

        /// <summary>
        /// Ejecuta el backup completo: BACKUP DATABASE local + copia a la carpeta externa + purga de backups viejos.
        /// Devuelve un mensaje descriptivo del resultado (éxito o error). También deja registro en la BD y en un log local.
        /// </summary>
        public static string EjecutarBackupAutomatico()
        {
            DateTime inicio = DateTime.Now;
            string resultado;
            try
            {
                var cfg = DatabaseService.ObtenerConfigBackupAuto();
                if (!cfg.Habilitado)
                {
                    resultado = "Backup automático deshabilitado, no se ejecutó nada.";
                    EscribirLog(resultado);
                    return resultado;
                }

                Directory.CreateDirectory(CarpetaStaging);
                string nombreArchivo = $"SCHPOS_Backup_{inicio:yyyyMMdd_HHmmss}.bak";
                string rutaLocal = Path.Combine(CarpetaStaging, nombreArchivo);

                string errorBackup = BackupService.RealizarBackup(rutaLocal);
                if (errorBackup != null)
                {
                    resultado = "❌ Falló la generación del backup: " + errorBackup;
                    EscribirLog(resultado);
                    DatabaseService.RegistrarResultadoBackupAuto(inicio, resultado);
                    return resultado;
                }

                PurgarBackupsViejos(CarpetaStaging, cfg.RetencionCantidad);

                if (string.IsNullOrWhiteSpace(cfg.CarpetaExterna))
                {
                    resultado = $"⚠ Backup generado en {rutaLocal}, pero no hay carpeta externa configurada (no se copió afuera de esta PC).";
                    EscribirLog(resultado);
                    DatabaseService.RegistrarResultadoBackupAuto(inicio, resultado);
                    return resultado;
                }

                try
                {
                    Directory.CreateDirectory(cfg.CarpetaExterna);
                    string rutaExterna = Path.Combine(cfg.CarpetaExterna, nombreArchivo);
                    File.Copy(rutaLocal, rutaExterna, overwrite: true);
                    PurgarBackupsViejos(cfg.CarpetaExterna, cfg.RetencionCantidad);

                    long sizeMb = new FileInfo(rutaExterna).Length / (1024 * 1024);
                    resultado = $"✔ Backup OK ({sizeMb} MB) copiado a: {rutaExterna}";
                    EscribirLog(resultado);
                    DatabaseService.RegistrarResultadoBackupAuto(inicio, resultado);
                    return resultado;
                }
                catch (Exception exCopia)
                {
                    resultado = $"⚠ Backup generado localmente, pero falló la copia a la carpeta externa ({cfg.CarpetaExterna}): {exCopia.Message}";
                    EscribirLog(resultado);
                    DatabaseService.RegistrarResultadoBackupAuto(inicio, resultado);
                    return resultado;
                }
            }
            catch (Exception ex)
            {
                resultado = "❌ Error inesperado en backup automático: " + ex.Message;
                EscribirLog(resultado);
                try { DatabaseService.RegistrarResultadoBackupAuto(inicio, resultado); } catch { }
                return resultado;
            }
        }

        private static void PurgarBackupsViejos(string carpeta, int cantidadAConservar)
        {
            try
            {
                var archivos = new DirectoryInfo(carpeta)
                    .GetFiles("SCHPOS_Backup_*.bak")
                    .OrderByDescending(f => f.CreationTime)
                    .Skip(Math.Max(1, cantidadAConservar))
                    .ToList();
                foreach (var f in archivos)
                {
                    try { f.Delete(); } catch { }
                }
            }
            catch { }
        }

        private static void EscribirLog(string linea)
        {
            try
            {
                string dir = Path.GetDirectoryName(RutaLog);
                if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);
                File.AppendAllText(RutaLog, $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] {linea}{Environment.NewLine}");
            }
            catch { }
        }

        /// <summary>Crea o actualiza la tarea diaria en el Programador de tareas de Windows.</summary>
        public static string ProgramarTareaWindows(string hora)
        {
            try
            {
                if (!System.Text.RegularExpressions.Regex.IsMatch(hora ?? "", @"^\d{1,2}:\d{2}$"))
                    return "Hora inválida. Usá el formato HH:mm, por ejemplo 02:00.";

                string exePath = Process.GetCurrentProcess().MainModule.FileName;
                string args = $"/Create /TN \"{NombreTarea}\" /TR \"\\\"{exePath}\\\" /autobackup\" /SC DAILY /ST {hora} /RL HIGHEST /F";
                var (ok, salida) = EjecutarSchtasks(args);
                return ok ? null : ("No se pudo crear la tarea programada: " + salida);
            }
            catch (Exception ex)
            {
                return "No se pudo crear la tarea programada: " + ex.Message;
            }
        }

        public static string QuitarTareaWindows()
        {
            try
            {
                var (ok, salida) = EjecutarSchtasks($"/Delete /TN \"{NombreTarea}\" /F");
                // Si la tarea no existía, tampoco es un error real.
                if (ok || salida.IndexOf("no existe", StringComparison.OrdinalIgnoreCase) >= 0
                        || salida.IndexOf("cannot find", StringComparison.OrdinalIgnoreCase) >= 0)
                    return null;
                return "No se pudo quitar la tarea programada: " + salida;
            }
            catch (Exception ex)
            {
                return "No se pudo quitar la tarea programada: " + ex.Message;
            }
        }

        public static bool ExisteTareaWindows()
        {
            try
            {
                var (ok, _) = EjecutarSchtasks($"/Query /TN \"{NombreTarea}\"");
                return ok;
            }
            catch { return false; }
        }

        private static (bool ok, string salida) EjecutarSchtasks(string argumentos)
        {
            var psi = new ProcessStartInfo
            {
                FileName = "schtasks.exe",
                Arguments = argumentos,
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true
            };
            using (var p = Process.Start(psi))
            {
                string stdout = p.StandardOutput.ReadToEnd();
                string stderr = p.StandardError.ReadToEnd();
                p.WaitForExit(15000);
                return (p.ExitCode == 0, string.IsNullOrWhiteSpace(stderr) ? stdout : stderr);
            }
        }
    }
}
