using System;
using System.Collections.Generic;
using System.Configuration;
using System.Data;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Media;
// Importamos la lógica de nuestro proyecto
using SchettiniGestion;

namespace SchettiniGestion.WPF
{
    public partial class App : Application
    {
        protected override void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);

            // 1. ¡CRÍTICO! Configurar licencia de Excel (EPPlus)
            // Si no pones esta línea, la App se cierra al intentar usar funciones de Excel.
            OfficeOpenXml.ExcelPackage.LicenseContext = OfficeOpenXml.LicenseContext.NonCommercial;

            // 2. Validar Licencia del Sistema
            bool licenciaValida = LicenseManager.ValidarLicencia();

            if (!licenciaValida)
            {
                string mensaje = SchettiniGestion.LicenseManager.UltimoMensajeError ?? "Error de licencia. La aplicación se cerrará.";
                CustomMessageBox.Show(mensaje, "Error de licencia", MessageBoxButton.OK, MessageBoxImage.Error);
                Application.Current.Shutdown();
                return;
            }

            // 3. Probar conexión a la base de datos
            bool conexionOk = DatabaseService.InitializeDatabase();
            if (!conexionOk)
            {
                CustomMessageBox.Show(
                    "No se pudo conectar a la base de datos. Verifique que SQL Server esté en ejecución y que la cadena de conexión en App.config (SchPosDB) sea correcta.",
                    "Error de conexión",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
                Application.Current.Shutdown();
                return;
            }
        }
    }
}