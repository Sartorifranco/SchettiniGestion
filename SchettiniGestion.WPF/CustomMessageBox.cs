using System.Windows;

namespace SchettiniGestion.WPF
{
    public static class CustomMessageBox
    {
        public static MessageBoxResult Show(string message, string title = "Mensaje", MessageBoxButton buttons = MessageBoxButton.OK, MessageBoxImage icon = MessageBoxImage.None)
        {
            // Creamos nuestra ventana personalizada
            var msgWindow = new CustomMessageBoxWindow(message, title, buttons, icon);
            if (Application.Current?.MainWindow != null && Application.Current.MainWindow.IsLoaded)
                msgWindow.Owner = Application.Current.MainWindow;

            msgWindow.ShowDialog();

            // Devolvemos qué botón apretó el usuario
            return msgWindow.Result;
        }
    }
}