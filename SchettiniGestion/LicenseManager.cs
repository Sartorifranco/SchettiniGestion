using System;
using System.Collections.Generic;
using System.Text;
using System.Windows.Forms;
using Newtonsoft.Json; // <-- ¡LA LIBRERÍA QUE INSTALAMOS!

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

        // --- FUNCIÓN NUEVA: PARA CARGAR LA LICENCIA ---
        private static bool CargarLicencia()
        {
            try
            {
                // --- ACÁ PEGAMOS LA CLAVE DEL CLIENTE ---
                // (En el futuro, la leeremos del Registro de Windows,
                // pero por ahora, la pegamos acá para probar)
                string claveLicencia = "eyJDdWl0Q2xpZW50ZSI6IjMwLTcxMTIzNDU2LTEiLCJGZWNoYUV4cGlyYWNpb24iOiIyMDI2LTEwLTI0VDAwOjAwOjAwIiwiTW9kdWxvblBlcm1pdGlkb3MiOlsiRkFDVFVSQUNJT04iLCJTVE9DSyJdfQ==";


                // 1. Decodificamos la clave (de Base64 a JSON)
                byte[] bytesLicencia = Convert.FromBase64String(claveLicencia);
                string jsonLicencia = Encoding.UTF8.GetString(bytesLicencia);

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

        // --- FUNCIÓN MODIFICADA: ValidarLicencia ---
        public static bool ValidarLicencia()
        {
            // 1. Intentamos cargar la licencia desde la clave.
            if (!CargarLicencia())
            {
                // Si CargarLicencia devuelve false, es porque falló
                // y ya mostró un error. Salimos.
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


        // --- Esta función queda igual ---
        public static bool IsModuleEnabled(string moduleName)
        {
            if (_licenciaActual == null)
            {
                return false;
            }

            return _licenciaActual.ModulosPermitidos.Contains(moduleName.ToUpper());
        }
    }
}