using System;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Security.Principal;
using System.Windows;

namespace SchettiniGestion.WPF
{
    /// <summary>Ejecuta procesos elevados (UAC) con avisos claros al usuario.</summary>
    internal static class ElevacionHelper
    {
        public const int ErrorUacCancelado = 1223; // ERROR_CANCELLED
        public const int ErrorRutaNoEncontrada = 3; // ERROR_PATH_NOT_FOUND

        /// <summary>Si false, no muestra MessageBox previo (el asistente ya explicó el UAC).</summary>
        public static bool PedirConfirmacionUac { get; set; } = true;

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern bool Wow64DisableWow64FsRedirection(ref IntPtr ptr);

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern bool Wow64RevertWow64FsRedirection(IntPtr ptr);

        public static bool ProcesoEsAdministrador()
        {
            try
            {
                using (var id = WindowsIdentity.GetCurrent())
                {
                    var principal = new WindowsPrincipal(id);
                    return principal.IsInRole(WindowsBuiltInRole.Administrator);
                }
            }
            catch { return false; }
        }

        public static string MensajeUacCancelado(string paraQue)
        {
            return
                "Windows pidió permiso de administrador y no se aceptó " +
                "(o el cartel quedó detrás de otra ventana).\n\n" +
                "Para " + paraQue + " necesitás tocar «Sí» en el aviso de Windows.\n\n" +
                "Cómo reintentar:\n" +
                "1. Configuración → Red y Servidor → Asistente de servidor\n" +
                "2. Cuando aparezca el cartel de Windows con el escudo, tocá Sí\n" +
                "3. Si no lo ves, mirá la barra de tareas (icono de escudo) y abrilo";
        }

        public static bool ConfirmarAntesDeUac(string paraQue)
        {
            if (ProcesoEsAdministrador())
                return true;
            if (!PedirConfirmacionUac)
                return true;

            var r = MessageBox.Show(
                "A continuación Windows va a pedir permiso de administrador (escudo).\n\n" +
                "Es para: " + paraQue + "\n\n" +
                "Tocá «Sí» en ese cartel. Si no lo ves, mirá la barra de tareas.\n\n" +
                "¿Continuar?",
                "Permiso de administrador",
                MessageBoxButton.YesNo,
                MessageBoxImage.Warning);
            return r == MessageBoxResult.Yes;
        }

        /// <summary>
        /// Ruta a PowerShell 64-bit. No usa Sysnative (rompe Verb=runas con error 3).
        /// </summary>
        public static string RutaPowerShellParaElevar()
        {
            string windir = Environment.GetFolderPath(Environment.SpecialFolder.Windows);
            // Con redirección deshabilitada, System32 es el real de 64 bits.
            string system32 = Path.Combine(windir, "System32", "WindowsPowerShell", "v1.0", "powershell.exe");
            string syswow = Path.Combine(windir, "SysWOW64", "WindowsPowerShell", "v1.0", "powershell.exe");
            if (File.Exists(system32)) return system32;
            if (File.Exists(syswow)) return syswow;
            return "powershell.exe";
        }

        /// <summary>
        /// Inicia un proceso elevado. Si fileName es null, usa PowerShell 64-bit.
        /// Evita Sysnative+runas (código 3: ruta no encontrada).
        /// </summary>
        public static Process StartElevado(string fileName, string arguments, out string errorOut)
        {
            errorOut = null;

            string[] candidatos;
            if (!string.IsNullOrWhiteSpace(fileName)
                && fileName.IndexOf("Sysnative", StringComparison.OrdinalIgnoreCase) < 0)
            {
                candidatos = new[] { fileName, RutaPowerShellParaElevar(), "powershell.exe" };
            }
            else
            {
                // Sysnative no sirve con UseShellExecute+runas
                candidatos = new[] { RutaPowerShellParaElevar(), "powershell.exe" };
            }

            Exception ultimo = null;
            foreach (string candidato in candidatos)
            {
                try
                {
                    var p = StartElevadoInterno(candidato, arguments);
                    if (p != null) return p;
                }
                catch (Win32Exception ex)
                {
                    ultimo = ex;
                    if (ex.NativeErrorCode == ErrorUacCancelado)
                    {
                        errorOut = MensajeUacCancelado("continuar");
                        return null;
                    }
                    // código 3 u otro: probar siguiente candidato
                }
                catch (Exception ex)
                {
                    ultimo = ex;
                }
            }

            if (ultimo is Win32Exception w32)
            {
                if (w32.NativeErrorCode == ErrorUacCancelado)
                    errorOut = MensajeUacCancelado("continuar");
                else if (w32.NativeErrorCode == ErrorRutaNoEncontrada)
                    errorOut =
                        "No se encontró PowerShell para pedir permiso de administrador.\n" +
                        "Probá reiniciar la PC o ejecutar SCHPOS como administrador " +
                        "(click derecho → Ejecutar como administrador) y repetir el asistente.\n\n" +
                        "Detalle: " + w32.Message + " (código " + w32.NativeErrorCode + ")";
                else
                    errorOut = "No se pudo pedir permiso de administrador.\nDetalle: " + w32.Message +
                               " (código " + w32.NativeErrorCode + ")";
            }
            else
                errorOut = ultimo?.Message ?? MensajeUacCancelado("continuar");

            return null;
        }

        private static Process StartElevadoInterno(string fileName, string arguments)
        {
            IntPtr cookie = IntPtr.Zero;
            bool redirOff = false;
            try
            {
                // Permite ver C:\Windows\System32 real desde proceso 32-bit (evita SysWOW64 / Sysnative).
                if (Environment.Is64BitOperatingSystem && !Environment.Is64BitProcess)
                    redirOff = Wow64DisableWow64FsRedirection(ref cookie);

                var psi = new ProcessStartInfo
                {
                    FileName = fileName,
                    Arguments = arguments ?? "",
                    UseShellExecute = true,
                };
                if (!ProcesoEsAdministrador())
                    psi.Verb = "runas";

                return Process.Start(psi);
            }
            finally
            {
                if (redirOff)
                    Wow64RevertWow64FsRedirection(cookie);
            }
        }
    }
}
