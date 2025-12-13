using System;
using System.Diagnostics;
<<<<<<< HEAD
using System.Runtime.InteropServices; // ¡Importante para la API!
using System.Windows; // Para el MessageBox
=======
using System.IO;
using System.Windows;
using System.ComponentModel; // Para la Win32Exception
>>>>>>> 1507ba100efa8b3956af38cfc06c17be61c7bf61

namespace SchettiniGestion.WPF
{
    public static class KeyboardHelper
    {
<<<<<<< HEAD
        // --- Importamos las funciones de la API de Windows ---

        // Esta función desactiva la redirección de carpetas
        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern bool Wow64DisableWow64FsRedirection(ref IntPtr ptr);

        // Esta función vuelve a activar la redirección (¡CRÍTICO!)
        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern bool Wow64RevertWow64FsRedirection(IntPtr ptr);

        /// <summary>
        /// Inicia el Teclado en Pantalla (OSK) de forma segura,
        /// manejando la redirección de 32/64 bits.
        /// </summary>
        public static void ShowOnScreenKeyboard()
        {
            IntPtr ptr = new IntPtr();
            bool isRedirectionDisabled = false;

            try
            {
                // Verificamos si ya está abierto
                Process[] oskProcesses = Process.GetProcessesByName("osk");
                if (oskProcesses.Length > 0)
                {
                    // Si ya está abierto, no hacemos nada.
                    return;
                }

                // Definimos la ruta REAL del teclado
                string keyboardPath = @"C:\Windows\System32\osk.exe";

                // Si estamos en un SO de 64 bits (casi seguro)
                if (Environment.Is64BitOperatingSystem)
                {
                    // 1. Desactivamos la redirección.
                    isRedirectionDisabled = Wow64DisableWow64FsRedirection(ref ptr);
                }

                // 2. Iniciamos el proceso
                Process.Start(keyboardPath);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"No se pudo iniciar el teclado en pantalla: {ex.Message}", "Error de teclado", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                // 3. ¡CRÍTICO! Volvemos a activar la redirección SIEMPRE.
                if (isRedirectionDisabled)
                {
                    Wow64RevertWow64FsRedirection(ptr);
                }
=======
        /// <summary>
        /// Inicia el Teclado en Pantalla (OSK) de forma segura.
        /// </summary>
        public static void ShowOnScreenKeyboard()
        {
            // Verificamos si ya está abierto
            Process[] oskProcesses = Process.GetProcessesByName("osk");
            if (oskProcesses.Length > 0)
            {
                return; // Si ya está abierto, no hacemos nada.
            }

            try
            {
                // --- ¡ESTE ES EL PLAN QUE SUGERISTE (Plan F)! ---
                // Le pedimos al Símbolo del Sistema (cmd) que abra el teclado.
                // Como nuestra app ahora es x64, llamará al cmd.exe de 64 bits,
                // que SÍ sabe dónde está 'osk.exe'.

                ProcessStartInfo psi = new ProcessStartInfo();
                psi.FileName = "cmd.exe";

                // Usamos 'start osk' que es más robusto que solo 'osk'
                psi.Arguments = "/C start osk"; // /C = "ejecuta el comando y cierra"

                // Ocultamos la ventana de cmd.exe para que no se vea
                psi.WindowStyle = ProcessWindowStyle.Hidden;
                psi.CreateNoWindow = true;
                psi.UseShellExecute = false; // Le decimos que no use el "Shell"

                Process.Start(psi);
            }
            catch (Exception ex)
            {
                // Si esto falla, ya es un problema del SO
                CustomMessageBox.Show($"No se pudo iniciar el teclado.\n\nError: {ex.Message}",
                    "Error de teclado", MessageBoxButton.OK, MessageBoxImage.Error);
>>>>>>> 1507ba100efa8b3956af38cfc06c17be61c7bf61
            }
        }
    }
}