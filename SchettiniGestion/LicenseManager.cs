using System;
using System.Collections.Generic;
using System.Text;
using System.Windows.Forms;
using Newtonsoft.Json;

namespace SchettiniGestion
{
    public static class LicenseManager
    {
        public class LicenseData
        {
            public string CuitCliente { get; set; }
            public DateTime FechaExpiracion { get; set; }
            public List<string> ModulosPermitidos { get; set; } = new List<string>();
        }

        private static LicenseData _licenciaActual;

        private static bool CargarLicencia()
        {
            try
            {
                // CAMBIO: Leemos desde la base de datos en lugar de texto fijo
                string claveLicencia = DatabaseService.ObtenerStringLicencia();

                if (string.IsNullOrEmpty(claveLicencia)) return false;

                byte[] bytesLicencia = Convert.FromBase64String(claveLicencia);
                string jsonLicencia = Encoding.UTF8.GetString(bytesLicencia);
                _licenciaActual = JsonConvert.DeserializeObject<LicenseData>(jsonLicencia);

                if (_licenciaActual == null) return false;
                return true;
            }
            catch { return false; }
        }

        public static bool ValidarLicencia()
        {
            if (!CargarLicencia()) return false;

            if (DateTime.Now > _licenciaActual.FechaExpiracion)
            {
                // Opcional: Descomentar si quieres que avise con un popup al iniciar
                // MessageBox.Show("Su licencia ha expirado. Contacte a soporte.", "Licencia Vencida", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return false;
            }
            return true;
        }

        public static bool IsModuleEnabled(string moduleName)
        {
            if (_licenciaActual == null) CargarLicencia(); // Intentar cargar si es null

            if (_licenciaActual == null || _licenciaActual.ModulosPermitidos == null) return false;
            return _licenciaActual.ModulosPermitidos.Contains(moduleName.ToUpper());
        }

        // Método extra para mostrar fecha en la pantalla de configuración
        public static string ObtenerFechaVencimiento()
        {
            if (_licenciaActual == null) return "-";
            return _licenciaActual.FechaExpiracion.ToShortDateString();
        }
    }
}