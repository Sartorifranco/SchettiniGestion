using System;
using System.Diagnostics;
using System.IO;
using System.Windows;

namespace SchettiniGestion.WPF
{
    public static class KeyboardHelper
    {
        /// <summary>
        /// Inicia el Teclado en Pantalla (OSK). Usa <c>sysnative</c> para evitar el redirect WoW64 que rompe <c>osk</c> en procesos de 32 bits.
        /// </summary>
        public static void ShowOnScreenKeyboard()
        {
            Process[] oskProcesses = Process.GetProcessesByName("osk");
            if (oskProcesses.Length > 0)
                return;

            try
            {
                string win = Environment.GetFolderPath(Environment.SpecialFolder.Windows);
                string oskPath = Path.Combine(win, "sysnative", "osk.exe");
                if (!File.Exists(oskPath))
                    oskPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.System), "osk.exe");

                Process.Start(new ProcessStartInfo
                {
                    FileName = oskPath,
                    UseShellExecute = true
                });
            }
            catch (Exception ex)
            {
                CustomMessageBox.Show($"No se pudo iniciar el teclado.\n\nError: {ex.Message}",
                    "Error de teclado", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
    }
}
