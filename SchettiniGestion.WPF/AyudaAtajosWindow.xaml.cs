using System.Windows;
using System.Windows.Input;

namespace SchettiniGestion.WPF
{
    public partial class AyudaAtajosWindow : Window
    {
        public AyudaAtajosWindow()
        {
            InitializeComponent();
            PreviewKeyDown += AyudaAtajosWindow_PreviewKeyDown;
        }

        private void AyudaAtajosWindow_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Escape)
            {
                Close();
                e.Handled = true;
            }
        }

        public AyudaAtajosWindow(object param) : this() { }
        public AyudaAtajosWindow(object p1, object p2) : this() { }
        public AyudaAtajosWindow(object p1, object p2, object p3) : this() { }

        private void btnCerrar_Click(object sender, RoutedEventArgs e) => Close();
    }
}
