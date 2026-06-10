using System;
using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
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
            AttachTouchKeyboardInternal(root, onFocus: true);
        }

        /// <summary>Abre el teclado solo cuando el usuario toca o hace clic en el campo (no al abrir ventanas ni al foco programático).</summary>
        public static void AttachTouchKeyboardOnPointer(DependencyObject root)
        {
            AttachTouchKeyboardInternal(root, onFocus: false);
        }

        private static void AttachTouchKeyboardInternal(DependencyObject root, bool onFocus)
        {
            if (root == null) return;

            if (root is TextBox tb)
            {
                if (onFocus) tb.GotFocus += TouchInput_GotFocus;
                else RegisterPointerHandlers(tb);
            }
            else if (root is ComboBox cb && cb.IsEditable)
            {
                if (onFocus) cb.GotFocus += TouchInput_GotFocus;
                else RegisterPointerHandlers(cb);
            }
            else if (root is DecimalUpDown dud)
            {
                if (onFocus) dud.GotFocus += TouchInput_GotFocus;
                else RegisterPointerHandlers(dud);
            }
            else if (root is IntegerUpDown iud)
            {
                if (onFocus) iud.GotFocus += TouchInput_GotFocus;
                else RegisterPointerHandlers(iud);
            }

            for (int i = 0, n = VisualTreeHelper.GetChildrenCount(root); i < n; i++)
                AttachTouchKeyboardInternal(VisualTreeHelper.GetChild(root, i), onFocus);
        }

        private static void RegisterPointerHandlers(UIElement element)
        {
            element.PreviewMouseDown += TouchInput_PointerActivate;
            element.PreviewTouchDown += TouchInput_PointerActivate;
        }

        private static void TouchInput_PointerActivate(object sender, InputEventArgs e)
        {
            ShowOnScreenKeyboard();
        }

        private static void TouchInput_GotFocus(object sender, RoutedEventArgs e)
        {
            ShowOnScreenKeyboard();
        }
    }
}
