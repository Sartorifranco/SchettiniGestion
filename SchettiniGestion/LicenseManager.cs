using System;
using System.Collections.Generic;
using System.Text;
using System.Windows.Forms; // ¡IMPORTANTE para que funcione MessageBox!
using Newtonsoft.Json; // <-- ¡LA LIBRERÍA QUE INSTALAMOS!

namespace SchettiniGestion
{
    public static class LicenseManager
    {
        // 1. Clase interna para los datos de la licencia
        public class LicenseData
        {
            public string CuitCliente { get; set; }
            public DateTime FechaExpiracion { get; set; }
            public List<string> ModulosPermitidos { get; set; } = new List<string>();
        }

        // 2. Variable para guardar la licencia
        private static LicenseData _licenciaActual;

        // 3. Método para cargar la licencia
        private static bool CargarLicencia()
        {
            try
            {
                // --- ¡AQUÍ ESTÁ LA CLAVE "PRO" CORREGIDA Y FUNCIONAL! ---
                string claveLicencia = "eyJDdWl0Q2xpZW50ZSI6IjIwLTExMjIzMzQ0LTUiLCJGZWNoYUV4cGlyYWNpb24iOiIyMDI2LTEyLTMxVDIzOjU5OjU5IiwiTW9kdWxvc1Blcm1pdGlkb3MiOlsiQUNDRVNPX0ZBQ1RVUkFDSU9OIiwiQUNDRVNPX1BST0RVQ1RPUyJdfQ==";

                // 1. Decodificamos la clave (de Base64 a JSON)
                byte[] bytesLicencia = Convert.FromBase64String(claveLicencia);
                string jsonLicencia = Encoding.UTF8.GetString(bytesLicencia);

                // (El MessageBox de depuración se ha eliminado)

                // 2. Convertimos el JSON a nuestro objeto C#
                _licenciaActual = JsonConvert.DeserializeObject<LicenseData>(jsonLicencia);

                // Si no pudo decodificarla, _licenciaActual será null
                if (_licenciaActual == null)
                {
                    MessageBox.Show("La clave de licencia está corrupta.", "Error de Licencia", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    return false;
                }

                return true;
            }
            catch (Exception ex)
            {
                // Si falla (por ej. la clave está mal pegada), mostramos un error.
                MessageBox.Show($"Error al validar la licencia: {ex.Message}", "Error de Licencia", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return false;
            }
        }

        // 4. Método para validar (sin cambios)
        public static bool ValidarLicencia()
        {
            // 1. Intentamos cargar la licencia desde la clave.
            if (!CargarLicencia())
            {
                return false;
            }

            // 2. Simulación de anti-crackeo (Anti-Reloj)
            if (DateTime.Now < new DateTime(2024, 1, 1))
            {
                MessageBox.Show("Se detectó una fecha de sistema inválida. Por favor, corrija el reloj.", "Error de Licencia", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return false;
            }

            // 3. Simulación de licencia expirada
            if (DateTime.Now > _licenciaActual.FechaExpiracion)
            {
                MessageBox.Show($"Su licencia ha expirado el {_licenciaActual.FechaExpiracion.ToShortDateString()}. Por favor, contacte al proveedor.", "Licencia Expirada", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return false;
            }

            // Si llegamos acá, la licencia es válida.
            return true;
        }


        // 5. Método para chequear módulos (limpio)
        public static bool IsModuleEnabled(string moduleName)
        {
            if (_licenciaActual == null)
            {
                return false;
            }

            if (_licenciaActual.ModulosPermitidos == null)
            {
                return false;
            }

            return _licenciaActual.ModulosPermitidos.Contains(moduleName.ToUpper());
        }

    } // <-- ¡AQUÍ TERMINA LA CLASE LICENSEMANAGER!
} // <-- ¡AQUÍ TERMINA EL NAMESPACE!