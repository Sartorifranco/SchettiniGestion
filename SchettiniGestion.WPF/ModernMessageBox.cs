using System.Windows;

namespace SchettiniGestion.WPF
{
    /// <summary>Diálogo modal consistente con la UI; reemplaza <see cref="MessageBox"/>.</summary>
    public static class ModernMessageBox
    {
        public static MessageBoxResult Show(string messageBoxText) =>
            Show(messageBoxText, "Mensaje", MessageBoxButton.OK, MessageBoxImage.None, null);

        public static MessageBoxResult Show(string messageBoxText, string caption) =>
            Show(messageBoxText, caption, MessageBoxButton.OK, MessageBoxImage.None, null);

        public static MessageBoxResult Show(string messageBoxText, string caption, MessageBoxButton button) =>
            Show(messageBoxText, caption, button, MessageBoxImage.None, null);

        public static MessageBoxResult Show(string messageBoxText, string caption, MessageBoxButton button, MessageBoxImage icon) =>
            Show(messageBoxText, caption, button, icon, null);

        public static MessageBoxResult Show(string messageBoxText, string caption, MessageBoxButton button, MessageBoxImage icon, Window owner)
        {
            var w = new CustomMessageBoxWindow(messageBoxText, caption, button, icon, owner);
            w.ShowDialog();
            return w.Result;
        }
    }
}
