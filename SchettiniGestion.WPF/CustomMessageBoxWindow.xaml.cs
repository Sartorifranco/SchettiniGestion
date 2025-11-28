using System.Windows;

namespace SchettiniGestion.WPF
{
    public partial class CustomMessageBoxWindow : Window
    {
        public MessageBoxResult Result { get; private set; } = MessageBoxResult.None;

        public CustomMessageBoxWindow(string message, string title, MessageBoxButton buttons, MessageBoxImage icon)
        {
            InitializeComponent();
            lblTitulo.Text = title;
            lblMensaje.Text = message;

            // Configurar Botones según lo que pida el código
            ConfigurarBotones(buttons);

            // Configurar Color de Título según el icono (Error, Info, Warning)
            ConfigurarEstilo(icon);
        }

        private void ConfigurarBotones(MessageBoxButton buttons)
        {
            btnYes.Visibility = Visibility.Collapsed;
            btnNo.Visibility = Visibility.Collapsed;
            btnOk.Visibility = Visibility.Collapsed;

            switch (buttons)
            {
                case MessageBoxButton.YesNo:
                    btnYes.Visibility = Visibility.Visible;
                    btnNo.Visibility = Visibility.Visible;
                    btnYes.Content = "SÍ";
                    btnNo.Content = "NO";
                    break;
                case MessageBoxButton.OK:
                    btnOk.Visibility = Visibility.Visible;
                    break;
                case MessageBoxButton.OKCancel:
                    btnYes.Visibility = Visibility.Visible;
                    btnNo.Visibility = Visibility.Visible;
                    btnYes.Content = "ACEPTAR";
                    btnNo.Content = "CANCELAR";
                    break;
            }
        }

        private void ConfigurarEstilo(MessageBoxImage icon)
        {
            // Cambia el color del título según la gravedad
            switch (icon)
            {
                case MessageBoxImage.Error:
                    lblTitulo.Foreground = System.Windows.Media.Brushes.Red;
                    break;
                case MessageBoxImage.Warning:
                    lblTitulo.Foreground = System.Windows.Media.Brushes.Orange;
                    break;
                case MessageBoxImage.Information:
                    lblTitulo.Foreground = System.Windows.Media.Brushes.LightBlue;
                    break;
            }
        }

        private void btnYes_Click(object sender, RoutedEventArgs e)
        {
            Result = MessageBoxResult.Yes; // O OK
            this.Close();
        }

        private void btnNo_Click(object sender, RoutedEventArgs e)
        {
            Result = MessageBoxResult.No; // O Cancel
            this.Close();
        }

        private void btnOk_Click(object sender, RoutedEventArgs e)
        {
            Result = MessageBoxResult.OK;
            this.Close();
        }
    }
}