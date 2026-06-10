using System;
using System.Windows;
using System.Windows.Controls;

namespace SchettiniGestion.WPF
{
    public partial class CustomMessageBoxWindow : Window
    {
        public MessageBoxResult Result { get; private set; } = MessageBoxResult.None;

        private readonly MessageBoxButton _buttonPreset;
        private bool _handledResult;

        public CustomMessageBoxWindow(string message, string caption, MessageBoxButton buttons, MessageBoxImage icon)
            : this(message, caption, buttons, icon, null)
        {
        }

        public CustomMessageBoxWindow(string message, string caption, MessageBoxButton buttons, MessageBoxImage icon, Window owner)
        {
            InitializeComponent();

            try
            {
                if (owner != null)
                    Owner = owner;
                else if (Application.Current?.MainWindow != null && Application.Current.MainWindow.IsLoaded)
                    Owner = Application.Current.MainWindow;
            }
            catch { /* sin owner */ }

            lblTitulo.Text = string.IsNullOrWhiteSpace(caption) ? " " : caption.Trim();
            lblMensaje.Text = message ?? "";

            _buttonPreset = buttons;
            ApplySeverityToTitle(icon);
            BuildButtons(buttons);
        }

        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            try
            {
                MoveFocus(new System.Windows.Input.TraversalRequest(System.Windows.Input.FocusNavigationDirection.First));
            }
            catch { }
        }

        private void ApplySeverityToTitle(MessageBoxImage icon)
        {
            try
            {
                // En WPF, Error/Hand/Stop, Warning/Exclamation e Information/Asterisk comparten el mismo valor numérico.
                switch ((uint)icon)
                {
                    case 16: // Hand, Stop, Error
                        lblTitulo.Foreground = System.Windows.Media.Brushes.Firebrick;
                        break;
                    case 48: // Exclamation, Warning
                        lblTitulo.Foreground = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(0xD9, 0x7A, 0x00));
                        break;
                    case 64: // Asterisk, Information
                        {
                            var b = TryFindResource("PrimaryColor") as System.Windows.Media.Brush;
                            if (b != null) lblTitulo.Foreground = b;
                        }
                        break;
                    case 32: // Question
                    default:
                        // Sin icono/question: deja caer al Foreground del XAML ({DynamicResource TextPrimary}).
                        // No sobreescribir con un color fijo; si la ventana usa DynamicResource ya es correcto.
                        break;
                }
            }
            catch { }
        }

        private void BuildButtons(MessageBoxButton preset)
        {
            panelBotones.Children.Clear();

            switch (preset)
            {
                case MessageBoxButton.OK:
                    AddFooterButton("Aceptar", MessageBoxResult.OK, isPrimary: true);
                    break;

                case MessageBoxButton.OKCancel:
                    AddFooterButton("Cancelar", MessageBoxResult.Cancel, isOutline: true, marginLeft: 0);
                    AddFooterButton("Aceptar", MessageBoxResult.OK, isPrimary: true, marginLeft: 10);
                    break;

                case MessageBoxButton.YesNo:
                    AddFooterButton("No", MessageBoxResult.No, isOutline: true, marginLeft: 0);
                    AddFooterButton("Sí", MessageBoxResult.Yes, isPrimary: true, marginLeft: 10);
                    break;

                case MessageBoxButton.YesNoCancel:
                    AddFooterButton("Cancelar", MessageBoxResult.Cancel, isOutline: true, marginLeft: 0);
                    AddFooterButton("No", MessageBoxResult.No, isOutline: true, marginLeft: 10);
                    AddFooterButton("Sí", MessageBoxResult.Yes, isPrimary: true, marginLeft: 10);
                    break;

                default:
                    AddFooterButton("Aceptar", MessageBoxResult.OK, isPrimary: true);
                    break;
            }
        }

        private void AddFooterButton(string text, MessageBoxResult res, bool isPrimary = false, bool isOutline = false, double marginLeft = 0)
        {
            var b = new Button
            {
                Content = text,
                Margin = new Thickness(marginLeft, 0, 0, 0)
            };

            if (isPrimary)
                b.Style = (Style)FindResource("ModernDlgPrimaryButton");
            else if (isOutline)
                b.Style = (Style)FindResource("ModernDlgOutlineButton");
            else
                b.Style = (Style)FindResource("ModernDlgOutlineButton");

            b.Click += (_, __) => CloseWith(res);

            panelBotones.Children.Add(b);
        }

        private void CloseWith(MessageBoxResult r)
        {
            _handledResult = true;
            Result = r;
            DialogResult = true;
            Close();
        }

        private void btnCerrar_Click(object sender, RoutedEventArgs e)
        {
            if (_handledResult) return;
            CloseWith(DefaultDismissResult());
        }

        private MessageBoxResult DefaultDismissResult()
        {
            switch (_buttonPreset)
            {
                case MessageBoxButton.OK:
                    return MessageBoxResult.OK;
                case MessageBoxButton.OKCancel:
                case MessageBoxButton.YesNoCancel:
                    return MessageBoxResult.Cancel;
                case MessageBoxButton.YesNo:
                    return MessageBoxResult.No;
                default:
                    return MessageBoxResult.None;
            }
        }

        protected override void OnKeyDown(System.Windows.Input.KeyEventArgs e)
        {
            if (e.Key == System.Windows.Input.Key.Escape)
            {
                if (_handledResult) return;
                CloseWith(DefaultDismissResult());
                e.Handled = true;
                return;
            }

            base.OnKeyDown(e);
        }
    }
}
