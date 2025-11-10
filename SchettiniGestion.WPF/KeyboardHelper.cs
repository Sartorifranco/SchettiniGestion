using System;
using System.Diagnostics;
using System.IO;
using System.Windows;
using System.ComponentModel; // Para la Win32Exception

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
                // --- ¡ESTE ES EL NUEVO INTENTO! ---
                // "ms-inputapp:" es el "protocolo" de Windows 10/11
                // para llamar a las aplicaciones de teclado/entrada.
                // Es nuestra mejor apuesta.
                Process.Start("ms-inputapp:showallupview");
            }
            catch (Exception ex)
            {
                // Si falla (ej: "ms-inputapp" no está registrado),
                // probamos el "Plan E" (llamar a 'osk' sin ruta)
                try
                {
                    Process.Start("osk");
                }
                catch (Exception ex2)
                {
                    // Si ambos fallan, mostramos el error
                    MessageBox.Show($"No se pudo iniciar el teclado en pantalla.\n\nError 1 (Moderno): {ex.Message}\n\nError 2 (Clásico): {ex2.Message}",
                        "Error de teclado", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }
    }
}