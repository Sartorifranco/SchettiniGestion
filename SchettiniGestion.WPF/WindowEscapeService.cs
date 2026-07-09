using System;
using System.Windows;
using System.Windows.Input;

namespace SchettiniGestion.WPF
{
    /// <summary>
    /// Cierra ventanas modales con Escape (excepto la ventana principal).
    /// </summary>
    public static class WindowEscapeService
    {
        private static bool _inicializado;

        public static void Initialize()
        {
            if (_inicializado) return;
            _inicializado = true;

            EventManager.RegisterClassHandler(
                typeof(Window),
                UIElement.PreviewKeyDownEvent,
                new KeyEventHandler(OnWindowPreviewKeyDown),
                handledEventsToo: false);
        }

        private static void OnWindowPreviewKeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key != Key.Escape || e.Handled) return;
            if (!(sender is Window win) || !win.IsVisible) return;

            if (win is PrincipalWindow || win is LoginWindow) return;
            if (e.Handled) return;

            e.Handled = true;
            try { win.DialogResult = false; } catch { }
            win.Close();
        }
    }
}
