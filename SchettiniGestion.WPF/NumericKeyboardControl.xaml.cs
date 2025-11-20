using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace SchettiniGestion.WPF
{
    public partial class NumericKeyboardControl : UserControl
    {
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
                string numero = button.Content.ToString();

                // --- DEBUG ---
                // MessageBox.Show($"TECLADO: Click en {numero}", "Debug Teclado"); 
                // --- FIN DEBUG ---

                KeyPressed?.Invoke(this, numero);
            }
        }

        private void BackspaceButton_Click(object sender, RoutedEventArgs e)
        {
            // --- DEBUG ---
            // MessageBox.Show("TECLADO: Click en Borrar", "Debug Teclado");
            // --- FIN DEBUG ---

            KeyPressed?.Invoke(this, "Back");
        }

        private void EnterButton_Click(object sender, RoutedEventArgs e)
        {
            // --- DEBUG ---
            // MessageBox.Show("TECLADO: Click en Enter", "Debug Teclado");
            // --- FIN DEBUG ---

            KeyPressed?.Invoke(this, "Enter");
        }
    }
}