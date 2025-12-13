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

            if (licenciaValida)
            {
                // 3. Inicializar y Actualizar Base de Datos
                // Esto ejecutará los "ALTER TABLE" para agregar las columnas nuevas (Código de Barras, etc.)
                DatabaseService.InitializeDatabase();
            }
            else
            {
                CustomMessageBox.Show("Error de licencia. La aplicación se cerrará.", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                Application.Current.Shutdown();
            }
        }
    }
}