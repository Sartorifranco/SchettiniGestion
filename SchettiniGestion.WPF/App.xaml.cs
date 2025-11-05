using System;
using System.Collections.Generic;
using System.Configuration;
using System.Data;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
// --- ¡INICIO DEL CÓDIGO NUEVO! ---
// Importamos la lógica de nuestro otro proyecto
using SchettiniGestion;
// --- ¡FIN DEL CÓDIGO NUEVO! ---

namespace SchettiniGestion.WPF
{
    /// <summary>
    /// Lógica de interacción para App.xaml
    /// </summary>
    public partial class App : Application
    {
        // --- ¡INICIO DEL CÓDIGO NUEVO! ---
        protected override void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);

            // ¡Validamos la licencia y preparamos la base de datos!
            // Esto es exactamente lo mismo que hacíamos en el Program.cs
            // del proyecto viejo.
            bool licenciaValida = LicenseManager.ValidarLicencia();
            if (licenciaValida)
            {
                DatabaseService.InitializeDatabase();
            }
            else
            {
                // Si la licencia no es válida, cerramos la app.
                MessageBox.Show("Error de licencia. La aplicación se cerrará.", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                Application.Current.Shutdown();
            }
        }
        // --- ¡FIN DEL CÓDIGO NUEVO! ---
    }
}
