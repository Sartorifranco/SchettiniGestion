using System;
using System.Windows;
using System.Windows.Controls;

namespace SchettiniGestion.WPF
{
    public partial class NumericKeyboardControl : UserControl
    {
        // Evento simple que envía el texto de la tecla
        public event EventHandler<string> KeyPressed;

        public NumericKeyboardControl()
        {
            InitializeComponent();
        }

        private void Button_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button btn)
            {
                string key = btn.Content.ToString();

                // Normalizamos nombres
                if (key == "⬅") key = "BACKSPACE";

                // Enviamos la tecla pulsada a la ventana principal
                KeyPressed?.Invoke(this, key);
            }
        }
    }
}