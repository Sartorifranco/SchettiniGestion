using System;
using System.Diagnostics;
using System.IO;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Win32;

namespace SchettiniGestion.WPF
{
    /// <summary>
    /// Detecta e instala SQL Server Express (instancia SQLEXPRESS) cuando falta en la PC servidor.
    /// </summary>
    internal static class SqlExpressInstaller
    {
        public const string InstanciaDefault = @".\SQLEXPRESS";
        public const string NombreServicio = "MSSQL$SQLEXPRESS";

        /// <summary>Bootstrapper oficial SQL Server 2022 Express (redirige al instalador SSEI).</summary>
        private const string UrlBootstrapper = "https://go.microsoft.com/fwlink/?linkid=2215158";

        public static bool PuedeConectarExpress()
        {
            // En PCs cliente (sin Express) .\SQLEXPRESS puede colgar el hilo varios minutos
            // aunque Connect Timeout sea 3. Cortar sí o sí a los 2,5 s.
            try
            {
                var t = Task.Run(() =>
                {
                    using (var c = new System.Data.SqlClient.SqlConnection(
                        "Server=.\\SQLEXPRESS;Database=master;Integrated Security=True;Encrypt=False;TrustServerCertificate=True;Connect Timeout=2;Pooling=False;"))
                    {
                        c.Open();
                        return true;
                    }
                });
                return t.Wait(TimeSpan.FromMilliseconds(2500)) && t.Status == TaskStatus.RanToCompletion && t.Result;
            }
            catch { return false; }
        }

        /// <summary>
        /// Detección local sin abrir SQL. Nunca usar SqlConnection acá: en una notebook cliente
        /// .\SQLEXPRESS no existe y el driver se queda esperando SQL Browser / named pipes.
        /// </summary>
        public static bool EstaInstalado()
        {
            if (ServicioExpressRegistrado()) return true;
            return InstanciaExpressEnRegistro();
        }

        private static bool ServicioExpressRegistrado()
        {
            try
            {
                string sc = RutaSc64();
                var psi = new ProcessStartInfo(sc, "query " + NombreServicio)
                {
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    CreateNoWindow = true
                };
                using (var p = Process.Start(psi))
                {
                    if (p == null) return false;
                    string o = p.StandardOutput.ReadToEnd() ?? "";
                    if (!p.WaitForExit(2000))
                    {
                        try { p.Kill(); } catch { }
                        return false;
                    }
                    if (o.IndexOf("RUNNING", StringComparison.OrdinalIgnoreCase) >= 0
                        || o.IndexOf("STOPPED", StringComparison.OrdinalIgnoreCase) >= 0)
                        return true;
                }
            }
            catch { }
            return false;
        }

        private static bool InstanciaExpressEnRegistro()
        {
            try
            {
                using (var k = Registry.LocalMachine.OpenSubKey(@"SOFTWARE\Microsoft\Microsoft SQL Server\Instance Names\SQL"))
                {
                    if (k?.GetValue("SQLEXPRESS") != null) return true;
                }
            }
            catch { }
            return false;
        }

        public static bool ServicioEnEjecucion()
        {
            try
            {
                string sc = RutaSc64();
                var psi = new ProcessStartInfo(sc, "start " + NombreServicio)
                {
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    CreateNoWindow = true
                };
                using (var p = Process.Start(psi))
                    p?.WaitForExit(15000);
            }
            catch { }

            for (int i = 0; i < 20; i++)
            {
                if (PuedeConectarExpress()) return true;
                Thread.Sleep(1000);
            }
            return false;
        }

        /// <summary>
        /// Descarga el instalador Express e intenta instalación.
        /// Primero quiet; si no queda Express, abre el instalador con UI.
        /// Retorna null si OK.
        /// </summary>
        public static string InstalarSilencioso(Action<string> progreso = null)
        {
            try
            {
                if (PuedeConectarExpress())
                    return null;

                string dir = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData),
                    "SCHPOS", "prereq");
                Directory.CreateDirectory(dir);
                string exe = Path.Combine(dir, "SQL2022-SSEI-Expr.exe");

                progreso?.Invoke("Descargando SQL Server Express (puede tardar varios minutos)...");
                string errDl = AsegurarBootstrapper(exe, progreso);
                if (errDl != null) return errDl;

                if (!ElevacionHelper.ConfirmarAntesDeUac("instalar SQL Server Express"))
                    return ElevacionHelper.MensajeUacCancelado("instalar SQL Express");

                progreso?.Invoke("Instalando SQL Server Express (aceptá el UAC de Windows)...");
                string errQuiet = EjecutarInstalador(exe,
                    "/ACTION=Install /IACCEPTSQLSERVERLICENSETERMS /QUIET /HIDEPROGRESSBAR /ENU",
                    20 * 60 * 1000);
                if (errQuiet != null && (errQuiet.IndexOf("permiso", StringComparison.OrdinalIgnoreCase) >= 0
                    || errQuiet.IndexOf("administrador", StringComparison.OrdinalIgnoreCase) >= 0))
                    return errQuiet;

                progreso?.Invoke("Esperando que SQLEXPRESS quede listo...");
                if (EsperarExpressListo(90))
                    return null;

                // Quiet a menudo no instala nada: abrir instalador con interfaz
                progreso?.Invoke("La instalación silenciosa no alcanzó. Abriendo el instalador de SQL Express...");
                if (!ElevacionHelper.ConfirmarAntesDeUac("abrir el instalador de SQL Express"))
                    return ElevacionHelper.MensajeUacCancelado("instalar SQL Express");
                string errUi = EjecutarInstalador(exe,
                    "/ACTION=Install /IACCEPTSQLSERVERLICENSETERMS /ENU",
                    45 * 60 * 1000);
                if (errUi != null && (errUi.IndexOf("permiso", StringComparison.OrdinalIgnoreCase) >= 0
                    || errUi.IndexOf("administrador", StringComparison.OrdinalIgnoreCase) >= 0))
                    return errUi;

                progreso?.Invoke("Esperando SQLEXPRESS después del instalador...");
                if (EsperarExpressListo(120))
                    return null;

                return "No quedó instalada la instancia .\\SQLEXPRESS.\n\n" +
                       "Instalá «Express» desde https://www.microsoft.com/es-es/sql-server/sql-server-downloads\n" +
                       "con nombre de instancia SQLEXPRESS, reiniciá si pide, y reintentá Preparar servidor.";
            }
            catch (Exception ex)
            {
                return ex.Message;
            }
        }

        private static string AsegurarBootstrapper(string exe, Action<string> progreso)
        {
            try
            {
                if (File.Exists(exe) && new FileInfo(exe).Length >= 1_000_000)
                    return null;

                ServicePointManager.SecurityProtocol |= SecurityProtocolType.Tls12;
                using (var wc = new WebClient())
                    wc.DownloadFile(UrlBootstrapper, exe);

                if (!File.Exists(exe) || new FileInfo(exe).Length < 1_000_000)
                {
                    try { if (File.Exists(exe)) File.Delete(exe); } catch { }
                    return "No se pudo descargar el instalador de SQL Server Express. Verificá Internet e intentá de nuevo.";
                }
                return null;
            }
            catch (Exception ex)
            {
                return "Error al descargar SQL Express: " + ex.Message;
            }
        }

        private static string EjecutarInstalador(string exe, string args, int timeoutMs)
        {
            string errElev;
            using (var p = ElevacionHelper.StartElevado(exe, args, out errElev))
            {
                if (p == null)
                    return errElev ?? ElevacionHelper.MensajeUacCancelado("instalar SQL Express");
                if (!p.WaitForExit(timeoutMs))
                    return "El instalador de SQL Express sigue en curso. Cuando termine, reintentá Preparar servidor.";
                return null;
            }
        }

        private static bool EsperarExpressListo(int segundos)
        {
            for (int i = 0; i < segundos; i++)
            {
                if (PuedeConectarExpress()) return true;
                try
                {
                    string sc = RutaSc64();
                    var psi = new ProcessStartInfo(sc, "start " + NombreServicio)
                    {
                        UseShellExecute = false,
                        RedirectStandardOutput = true,
                        CreateNoWindow = true
                    };
                    using (var p = Process.Start(psi))
                        p?.WaitForExit(3000);
                }
                catch { }
                Thread.Sleep(1000);
            }
            return PuedeConectarExpress();
        }

        private static string RutaSc64()
        {
            string windir = Environment.GetFolderPath(Environment.SpecialFolder.Windows);
            if (Environment.Is64BitOperatingSystem && !Environment.Is64BitProcess)
            {
                string sysnative = Path.Combine(windir, "Sysnative", "sc.exe");
                if (File.Exists(sysnative)) return sysnative;
            }
            return "sc.exe";
        }

        public static void AbrirPaginaDescargaManual()
        {
            try
            {
                Process.Start(new ProcessStartInfo
                {
                    FileName = "https://www.microsoft.com/es-es/sql-server/sql-server-downloads",
                    UseShellExecute = true
                });
            }
            catch { }
        }
    }
}
