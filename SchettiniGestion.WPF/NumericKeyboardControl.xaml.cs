using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace SchettiniGestion.WPF
{
    public partial class NumericKeyboardControl : UserControl
    {
        // 🟢 1. AQUÍ DEFINIMOS EL EVENTO QUE FALTABA
        public event EventHandler<string> KeyPressed;

        public NumericKeyboardControl()
        {
            InitializeComponent();
        }

        // Método para simular la escritura en el elemento que tenga el foco
        private void SendKey(string key)
        {
            // Simulamos que el usuario presiona la tecla
            var target = Keyboard.FocusedElement;
            if (target is TextBox tb)
            {
                int caretIndex = tb.CaretIndex;
                tb.Text = tb.Text.Insert(caretIndex, key);
                tb.CaretIndex = caretIndex + 1;
            }
            else if (target is PasswordBox pb)
            {
                // PasswordBox es más difícil de manipular por seguridad
                pb.Password += key;
            }
        }

        private void BtnNum_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button btn)
            {
                string key = btn.Content.ToString();

                // Enviamos la tecla al control con foco
                SendKey(key);

                // 🟢 2. AVISAMOS A QUIEN ESTÉ ESCUCHANDO (PreciosControl)
                KeyPressed?.Invoke(this, key);
            }
        }

        private void BtnBackspace_Click(object sender, RoutedEventArgs e)
        {
            var target = Keyboard.FocusedElement;
            if (target is TextBox tb && tb.Text.Length > 0 && tb.CaretIndex > 0)
            {
                int caretIndex = tb.CaretIndex;
                tb.Text = tb.Text.Remove(caretIndex - 1, 1);
                tb.CaretIndex = caretIndex - 1;
            }

            // Avisamos del borrado (enviamos "BACK")
            KeyPressed?.Invoke(this, "BACK");
        }

        private void BtnClear_Click(object sender, RoutedEventArgs e)
        {
            var target = Keyboard.FocusedElement;
            if (target is TextBox tb)
            {
                tb.Text = "";
            }

            // Avisamos del limpiado (enviamos "CLEAR")
            KeyPressed?.Invoke(this, "CLEAR");
        }
    }
}