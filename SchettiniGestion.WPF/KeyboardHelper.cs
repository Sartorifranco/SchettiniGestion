using System.Windows;

namespace SchettiniGestion.WPF
{
    /// <summary>
    /// Antes forzaba el teclado en pantalla de Windows (osk.exe).
    /// Ya no: las cajas táctiles usan el teclado nativo del sistema al tocar un campo.
    /// </summary>
    public static class KeyboardHelper
    {
        public static void ShowOnScreenKeyboard()
        {
        }

        public static void AttachTouchKeyboard(DependencyObject root)
        {
        }

        public static void AttachTouchKeyboardOnPointer(DependencyObject root)
        {
        }
    }
}
