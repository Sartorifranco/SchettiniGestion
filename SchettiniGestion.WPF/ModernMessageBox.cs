using System.Windows;

namespace SchettiniGestion.WPF
{
    /// <summary>Alias del cuadro de mensajes del sistema (misma implementación que CustomMessageBox).</summary>
    public static class ModernMessageBox
    {
        public static MessageBoxResult Show(string message, string title = "Mensaje", MessageBoxButton buttons = MessageBoxButton.OK, MessageBoxImage icon = MessageBoxImage.None)
            => CustomMessageBox.Show(message, title, buttons, icon);
    }
}
