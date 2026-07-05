using AdminLicencias.Views;
using AdminLicencias.Services;
using System.Windows;
using System.Windows.Controls;

namespace AdminLicencias
{
    public partial class MainWindow : Window
    {
        private Button _activeBtn;

        public MainWindow()
        {
            InitializeComponent();
            _activeBtn = btnDashboard;
            foreach (Button b in new[] { btnClientes, btnNuevaLic, btnHistorial, btnConfig })
                b.Style = (Style)FindResource("NavBtn");

            NavigateTo("dashboard");
        }

        private void Nav_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button btn)
                NavigateTo(btn.Tag?.ToString());
        }

        private void SetActive(Button btn)
        {
            if (_activeBtn != null)
                _activeBtn.Style = (Style)FindResource("NavBtn");
            btn.Style = (Style)FindResource("NavBtnActive");
            _activeBtn = btn;
        }

        public void NavigateTo(string section, object param = null)
        {
            switch (section)
            {
                case "dashboard":
                    SetActive(btnDashboard);
                    frameMain.Navigate(new DashboardView(this));
                    break;
                case "clientes":
                    SetActive(btnClientes);
                    frameMain.Navigate(new ClientesView(this));
                    break;
                case "nueva":
                    SetActive(btnNuevaLic);
                    frameMain.Navigate(new NuevaLicenciaView(this, param as Models.Cliente));
                    break;
                case "historial":
                    SetActive(btnHistorial);
                    frameMain.Navigate(new HistorialView());
                    break;
                case "config":
                    SetActive(btnConfig);
                    frameMain.Navigate(new ConfigView(this));
                    break;
            }
        }
    }
}
