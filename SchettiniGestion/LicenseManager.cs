using System;
using System.Collections.Generic;
using System.Configuration;
using System.Text;
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

        /// <summary>Mensaje de error cuando ValidarLicencia() devuelve false.</summary>
        public static string UltimoMensajeError { get; private set; }

        public static void InvalidarCache()
        {
            _licenciaActual = null;
        }

        /// <summary>
        /// Orden: archivo licencia.key → ProgramData → base de datos → App.config → (solo DEBUG) licencia embebida.
        /// </summary>
        private static string ObtenerClaveLicencia()
        {
            string desdeArchivo = LicenseFileHelper.LeerClaveDesdeArchivos();
            if (!string.IsNullOrWhiteSpace(desdeArchivo))
                return desdeArchivo.Trim();

            try
            {
                string base64 = ConfigurationManager.AppSettings["LicenciaBase64"];
                if (!string.IsNullOrWhiteSpace(base64))
                    return base64.Trim();
            }
            catch { }

            try
            {
                string desdeBd = DatabaseService.ObtenerStringLicencia();
                if (!string.IsNullOrWhiteSpace(desdeBd))
                    return desdeBd.Trim();
            }
            catch { }

#if DEBUG
            return "eyJDdWl0Q2xpZW50ZSI6IjIwLTMzNDQ1NTY2LTUiLCJGZWNoYUV4cGlyYWNpb24iOiIyMDI2LTEyLTMxVDIzOjU5OjU5IiwiTW9kdWxvc1Blcm1pdGlkb3MiOlsiQUNDRVNPX0ZBQ1RVUkFDSU9OIiwiQUNDRVNPX1BST0RVQ1RPUyIsIkFDQ0VTT19DTElFTlRFUyIsIkFDQ0VTT19WRU5UQVMiLCJBQ0NFU09fU1RPQ0siLCJBQ0NFU09fVVNVQVJJT1MiLCJBQ0NFU09fUEVSTUlTT1MiLCJBQ0NFU09fUFJPVkVFRE9SRVMiLCJBQ0NFU09fQ09NUFJBUyIsIkFDQ0VTT19QUkVDSU9TIiwiQUNDRVNPX0NBSkEiLCJBQ0NFU09fUFJFU1VQVUVTVE9TIiwiQUNDRVNPX0NVRU5UQVNDT1JSSUVOVEVTIiwiQUNDRVNPX0xJU1RBU1BSRUNJT1MiXX0=";
#else
            return null;
#endif
        }

        private static bool CargarLicencia()
        {
            try
            {
                string claveLicencia = ObtenerClaveLicencia();
                if (string.IsNullOrWhiteSpace(claveLicencia))
                    return false;

                byte[] bytesLicencia = Convert.FromBase64String(claveLicencia);
                string jsonLicencia = Encoding.UTF8.GetString(bytesLicencia);
                _licenciaActual = JsonConvert.DeserializeObject<LicenseData>(jsonLicencia);
                return _licenciaActual != null;
            }
            catch
            {
                return false;
            }
        }

        public static bool ValidarLicencia()
        {
            UltimoMensajeError = null;
            InvalidarCache();

            if (!CargarLicencia())
            {
                UltimoMensajeError = "No hay licencia activa. Pegue la clave que le envió el proveedor o cargue el archivo licencia.key.";
                return false;
            }

            if (DateTime.Now > _licenciaActual.FechaExpiracion)
            {
                UltimoMensajeError = "Licencia expirada. Solicite una renovación al proveedor.";
                return false;
            }

            return true;
        }

        public static bool IsModuleEnabled(string moduleName)
        {
            if (_licenciaActual == null)
                CargarLicencia();
            if (_licenciaActual == null || _licenciaActual.ModulosPermitidos == null)
                return false;
            return _licenciaActual.ModulosPermitidos.Contains(moduleName.ToUpper());
        }

        public static string ObtenerFechaVencimiento()
        {
            if (_licenciaActual == null)
                CargarLicencia();
            if (_licenciaActual == null)
                return "-";
            return _licenciaActual.FechaExpiracion.ToShortDateString();
        }
    }
}
