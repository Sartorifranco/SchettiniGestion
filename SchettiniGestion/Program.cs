using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Forms; // <-- Importante para Application.Run

namespace SchettiniGestion
{
    static class Program
    {
        /// <summary>
        /// Punto de entrada principal para la aplicación.
        /// </summary>
        [STAThread]
        static void Main()
        {
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);

            // 1. Validamos la licencia
            bool licenciaValida = LicenseManager.ValidarLicencia();

            if (licenciaValida)
            {
                // 2. Preparamos la base de datos
                DatabaseService.InitializeDatabase();

                // 3. Abrimos la aplicación
                Application.Run(new FormLogin());
            }
        }
    }
}