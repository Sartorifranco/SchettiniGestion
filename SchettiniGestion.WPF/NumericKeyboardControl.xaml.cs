using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input; // Este Input es de WPF, está bien

namespace SchettiniGestion.WPF
{
    public partial class NumericKeyboardControl : UserControl
    {
        // El evento ahora envía un simple string con el botón presionado.
        public event EventHandler<string> KeyPressed;

        public NumericKeyboardControl()
        {
            InitializeComponent();
        }

        private void KeyboardButton_Click(object sender, RoutedEventArgs e)
        {
            Button button = sender as Button;
            if (button != null)
            {
                // Dispara el evento con el contenido literal del botón (ej: "7", "8", ".")
                KeyPressed?.Invoke(this, button.Content.ToString());
            }
        }

        private void BackspaceButton_Click(object sender, RoutedEventArgs e)
        {
            // Dispara el evento con la palabra clave "Back"
            KeyPressed?.Invoke(this, "Back");
        }

        private void EnterButton_Click(object sender, RoutedEventArgs e)
        {
            // Dispara el evento con la palabra clave "Enter"
            KeyPressed?.Invoke(this, "Enter");
        }
    }
}