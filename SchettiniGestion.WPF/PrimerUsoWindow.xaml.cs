using System;
using System.Data.SqlClient;
using System.Windows;
using System.Windows.Media;
using SchettiniGestion;

namespace SchettiniGestion.WPF
{
    public partial class PrimerUsoWindow : Window
    {
        private string _cadenaTesteada = null;

        public PrimerUsoWindow()
        {
            InitializeComponent();
        }

        private void Header_MouseLeftButtonDown(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            if (e.ButtonState == System.Windows.Input.MouseButtonState.Pressed) DragMove();
        }

        private void rbTipo_Checked(object sender, RoutedEventArgs e)
        {
            if (panelCustom == null) return;

            bool esCustom = rbCustom.IsChecked == true;
            panelCustom.Visibility = esCustom ? Visibility.Visible : Visibility.Collapsed;
            panelInfoLocalDB.Visibility = rbLocalDB.IsChecked == true ? Visibility.Visible : Visibility.Collapsed;

            // Resetear estado del test al cambiar tipo
            _cadenaTesteada = null;
            btnContinuar.IsEnabled = false;
            SetTestStatus("⚪", "Listo para probar la conexión", "Presione 'Probar conexión' para verificar antes de continuar.", "neutral");
        }

        private string ObtenerCadenaSeleccionada()
        {
            if (rbLocalDB.IsChecked == true)
                return DatabaseService.CS_LOCALDB;
            if (rbExpress.IsChecked == true)
                return DatabaseService.CS_SQLEXPRESS;
            return txtConexion.Text.Trim();
        }

        private void btnTestear_Click(object sender, RoutedEventArgs e)
        {
            string cs = ObtenerCadenaSeleccionada();
            SetTestStatus("⏳", "Probando conexión...", "Conectando al servidor de base de datos...", "pending");

            try
            {
                // Intentar conectar al servidor (master)
                var builder = new SqlConnectionStringBuilder(cs);
                string dbName = builder.InitialCatalog;
                builder.InitialCatalog = "master";

                using (var conn = new SqlConnection(builder.ConnectionString))
                {
                    conn.Open();

                    // Verificar si la BD ya existe o podemos crearla
                    object result = new SqlCommand($"SELECT db_id(N'{dbName}')", conn).ExecuteScalar();
                    bool dbExiste = result != DBNull.Value;

                    string desc = dbExiste
                        ? $"Conexión exitosa. La base de datos '{dbName}' ya existe y será usada."
                        : $"Conexión exitosa. La base de datos '{dbName}' será creada automáticamente.";

                    SetTestStatus("✔", "¡Conexión exitosa!", desc, "success");
                    _cadenaTesteada = cs;
                    btnContinuar.IsEnabled = true;
                }
            }
            catch (Exception ex)
            {
                string ayuda = rbLocalDB.IsChecked == true
                    ? "LocalDB no está instalado. Descargue 'SQL Server LocalDB' desde microsoft.com/sql (gratuito) o elija otra opción."
                    : "Verifique que SQL Server esté corriendo y que la cadena de conexión sea correcta.";

                SetTestStatus("✖", "Error de conexión", $"{ex.Message}\n\n{ayuda}", "error");
                _cadenaTesteada = null;
                btnContinuar.IsEnabled = false;
            }
        }

        private void btnContinuar_Click(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrWhiteSpace(_cadenaTesteada))
            {
                MessageBox.Show("Primero pruebe la conexión correctamente.", "Atención", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            try
            {
                // Guardar la conexión elegida
                DatabaseService.ActualizarConexion(_cadenaTesteada);

                // Crear BD y tablas
                SetTestStatus("⏳", "Configurando base de datos...", "Creando tablas y datos iniciales...", "pending");

                var app = (App)Application.Current;
                // Llamar la inicialización con la cadena testeada
                typeof(App)
                    .GetMethod("InicializarBaseDeDatosCompleta",
                        System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)
                    ?.Invoke(app, new object[] { _cadenaTesteada });

                DatabaseService.InitializeDatabase();

                SetTestStatus("✔", "¡Base de datos configurada!", "El sistema está listo para usar.", "success");

                MessageBox.Show("¡Configuración completada!\n\nEl sistema se iniciará ahora.", "Listo", MessageBoxButton.OK, MessageBoxImage.Information);
                DialogResult = true;
                Close();
            }
            catch (Exception ex)
            {
                SetTestStatus("✖", "Error al configurar", ex.Message, "error");
            }
        }

        private void SetTestStatus(string icon, string titulo, string desc, string tone)
        {
            iconTest.Text = icon;
            lblTestTitle.Text = titulo;
            lblTestDesc.Text = desc;

            Brush accent;
            switch (tone)
            {
                case "success":
                    accent = BrushFromResource("SuccessColor");
                    break;
                case "error":
                    accent = BrushFromResource("DangerColor");
                    break;
                case "pending":
                    accent = BrushFromResource("WarningColor");
                    break;
                default:
                    accent = BrushFromResourceDyn("TextSecondary");
                    break;
            }
            iconTest.Foreground = accent;
            lblTestTitle.Foreground = accent;
            borderTest.Background = BrushFromResourceDyn("SurfaceDark");
        }

        private static Brush BrushFromResource(string key) =>
            Application.Current?.TryFindResource(key) as Brush ?? Brushes.Gray;

        private static Brush BrushFromResourceDyn(string key) =>
            Application.Current?.TryFindResource(key) as Brush ?? Brushes.DimGray;
    }
}
