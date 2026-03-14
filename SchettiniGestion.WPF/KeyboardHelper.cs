using System;
using System.Diagnostics;
using System.Windows;

namespace SchettiniGestion.WPF
{
    public static class KeyboardHelper
    {
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
                // Le pedimos al Símbolo del Sistema (cmd) que abra el teclado.
                // Como nuestra app es x86, llamará al cmd.exe que sabe dónde está 'osk.exe'.

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
            }
        }
    }
}
