using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace SchettiniGestion.WPF
{
    public partial class CustomMessageBoxWindow : Window
    {
        public MessageBoxResult Result { get; private set; } = MessageBoxResult.None;
        private List<Button> _botonesActivos = new List<Button>();

        public CustomMessageBoxWindow(string message, string title, MessageBoxButton buttons, MessageBoxImage icon)
        {
            InitializeComponent();
            lblTitulo.Text = title;
            lblMensaje.Text = message;

            ConfigurarBotones(buttons);
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

        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            _botonesActivos = ObtenerBotonesVisibles();
            if (_botonesActivos.Count > 0)
            {
                var primero = _botonesActivos[0];
                primero.Focus();
                Keyboard.Focus(primero);
            }
        }

        private List<Button> ObtenerBotonesVisibles()
        {
            var lista = new List<Button>();
            if (btnYes.Visibility == Visibility.Visible) lista.Add(btnYes);
            if (btnNo.Visibility == Visibility.Visible) lista.Add(btnNo);
            if (btnOk.Visibility == Visibility.Visible) lista.Add(btnOk);
            return lista;
        }

        private void Window_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            if (_botonesActivos == null || _botonesActivos.Count == 0)
                _botonesActivos = ObtenerBotonesVisibles();
            if (_botonesActivos.Count == 0) return;

            int idx = _botonesActivos.IndexOf(Keyboard.FocusedElement as Button);
            if (idx < 0) idx = 0;

            switch (e.Key)
            {
                case Key.Left:
                case Key.Up:
                    MoverFoco(idx - 1);
                    e.Handled = true;
                    break;

                case Key.Right:
                case Key.Down:
                    MoverFoco(idx + 1);
                    e.Handled = true;
                    break;

                case Key.Enter:
                    ActivarBoton(Keyboard.FocusedElement as Button ?? _botonesActivos[0]);
                    e.Handled = true;
                    break;

                case Key.Escape:
                    if (btnNo.Visibility == Visibility.Visible)
                        ActivarBoton(btnNo);
                    else if (btnOk.Visibility == Visibility.Visible)
                        ActivarBoton(btnOk);
                    e.Handled = true;
                    break;

                case Key.Y:
                    if (btnYes.Visibility == Visibility.Visible)
                    {
                        ActivarBoton(btnYes);
                        e.Handled = true;
                    }
                    break;

                case Key.N:
                    if (btnNo.Visibility == Visibility.Visible)
                    {
                        ActivarBoton(btnNo);
                        e.Handled = true;
                    }
                    break;
            }
        }

        private void MoverFoco(int nuevoIndice)
        {
            if (_botonesActivos.Count == 0) return;
            if (nuevoIndice < 0) nuevoIndice = _botonesActivos.Count - 1;
            if (nuevoIndice >= _botonesActivos.Count) nuevoIndice = 0;
            var btn = _botonesActivos[nuevoIndice];
            btn.Focus();
            Keyboard.Focus(btn);
        }

        private void ActivarBoton(Button btn)
        {
            if (btn == null) return;
            if (btn == btnYes) btnYes_Click(btn, new RoutedEventArgs());
            else if (btn == btnNo) btnNo_Click(btn, new RoutedEventArgs());
            else if (btn == btnOk) btnOk_Click(btn, new RoutedEventArgs());
        }

        private void btnYes_Click(object sender, RoutedEventArgs e)
        {
            Result = MessageBoxResult.Yes;
            Close();
        }

        private void btnNo_Click(object sender, RoutedEventArgs e)
        {
            Result = MessageBoxResult.No;
            Close();
        }

        private void btnOk_Click(object sender, RoutedEventArgs e)
        {
            Result = MessageBoxResult.OK;
            Close();
        }
    }
}
