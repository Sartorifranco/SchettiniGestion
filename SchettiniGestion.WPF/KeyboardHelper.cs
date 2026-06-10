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
            try
            {
                Process.Start(new ProcessStartInfo { FileName = "osk.exe", UseShellExecute = true });
            }
            catch
            {
                try
                {
                    Process.Start(new ProcessStartInfo
                    {
                        FileName = @"C:\Program Files\Common Files\microsoft shared\ink\TabTip.exe",
                        UseShellExecute = true
                    });
                }
                catch (Exception ex)
                {
                    ModernMessageBox.Show("No se pudo iniciar el teclado táctil.\n" + ex.Message,
                        "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }
    }
}
