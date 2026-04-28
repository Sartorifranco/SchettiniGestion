using System.Windows;

namespace SchettiniGestion.WPF
{
    public partial class AyudaAtajosWindow : Window
    {
        public AyudaAtajosWindow()
        {
            InitializeComponent();
        }

        public AyudaAtajosWindow(object param) : this() { }
        public AyudaAtajosWindow(object p1, object p2) : this() { }
        public AyudaAtajosWindow(object p1, object p2, object p3) : this() { }

        private void btnCerrar_Click(object sender, RoutedEventArgs e) => Close();
    }
}
