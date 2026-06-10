using System;
using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using Xceed.Wpf.Toolkit;

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

        /// <summary>Al enfocar un campo editable, abre el teclado en pantalla (OSK).</summary>
        public static void AttachTouchKeyboard(DependencyObject root)
        {
            if (root == null) return;

            if (root is TextBox tb)
                tb.GotFocus += TouchInput_GotFocus;
            else if (root is ComboBox cb && cb.IsEditable)
                cb.GotFocus += TouchInput_GotFocus;
            else if (root is DecimalUpDown dud)
                dud.GotFocus += TouchInput_GotFocus;
            else if (root is IntegerUpDown iud)
                iud.GotFocus += TouchInput_GotFocus;

            for (int i = 0, n = VisualTreeHelper.GetChildrenCount(root); i < n; i++)
                AttachTouchKeyboard(VisualTreeHelper.GetChild(root, i));
        }

        private static void TouchInput_GotFocus(object sender, RoutedEventArgs e)
        {
            ShowOnScreenKeyboard();
        }
    }
}
