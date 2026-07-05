using AdminLicencias.Services;
using Microsoft.Win32;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace AdminLicencias.Views
{
    public partial class ConfigView : Page
    {
        private readonly MainWindow _main;

        public ConfigView(MainWindow main)
        {
            InitializeComponent();
            _main = main;
            txtRutaActual.Text = DataStore.RutaActual;
        }

        private void Examinar_Click(object s, RoutedEventArgs e)
        {
            var dlg = new SaveFileDialog
            {
                Title      = "Seleccionar ubicación del archivo de datos",
                Filter     = "JSON (*.json)|*.json",
                FileName   = "datos.json",
                OverwritePrompt = false
            };
            if (dlg.ShowDialog() == true)
                txtNuevaRuta.Text = dlg.FileName;
        }

        private void GuardarRuta_Click(object s, RoutedEventArgs e)
        {
            string nueva = txtNuevaRuta.Text.Trim();

            if (string.IsNullOrEmpty(nueva))
            {
                MostrarMsg("Ingresá una ruta o usá 'Restablecer default'.", false);
                return;
            }

            // Verificar que el directorio sea accesible
            string dir = Path.GetDirectoryName(nueva);
            if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
            {
                try { Directory.CreateDirectory(dir); }
                catch
                {
                    MostrarMsg($"No se puede acceder a la carpeta: {dir}\nVerificá que la ruta de red esté disponible.", false);
                    return;
                }
            }

            // Si ya existe un datos.json local y la nueva ruta está vacía, ofrecer migrar
            if (!File.Exists(nueva) && File.Exists(DataStore.RutaActual))
            {
                var res = MessageBox.Show(
                    "La nueva ruta no tiene datos todavía. ¿Querés copiar los datos actuales a la nueva ubicación?",
                    "Migrar datos", MessageBoxButton.YesNo, MessageBoxImage.Question);
                if (res == MessageBoxResult.Yes)
                {
                    try { File.Copy(DataStore.RutaActual, nueva); }
                    catch (System.Exception ex)
                    {
                        MostrarMsg("No se pudo copiar el archivo: " + ex.Message, false);
                        return;
                    }
                }
            }

            DataStore.CambiarRuta(nueva);
            DataStore.Cargar();
            txtRutaActual.Text = DataStore.RutaActual;
            txtNuevaRuta.Text  = "";
            MostrarMsg("✅ Ruta guardada. Los datos se cargan desde la nueva ubicación.", true);
        }

        private void Restablecer_Click(object s, RoutedEventArgs e)
        {
            string defaultPath = System.IO.Path.Combine(
                System.Environment.GetFolderPath(System.Environment.SpecialFolder.ApplicationData),
                "SCHPOSAdmin", "datos.json");

            DataStore.CambiarRuta(defaultPath);
            DataStore.Cargar();
            txtRutaActual.Text = DataStore.RutaActual;
            txtNuevaRuta.Text  = "";
            MostrarMsg("✅ Ruta restablecida al default local.", true);
        }

        private void MostrarMsg(string msg, bool ok)
        {
            txtMsgRuta.Text       = msg;
            txtMsgRuta.Foreground = ok
                ? (Brush)FindResource("GreenBrush")
                : (Brush)FindResource("RedBrush");
            txtMsgRuta.Visibility = Visibility.Visible;
        }
    }
}
