using System;
using System.Configuration;
using System.IO;

namespace SchettiniGestion
{
    /// <summary>
    /// Lectura/escritura de <c>licencia.key</c> junto al ejecutable y en ProgramData (escribible sin admin).
    /// </summary>
    public static class LicenseFileHelper
    {
        public static readonly string RutaLicenciaProgramData = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData),
            "SCHPOS",
            "licencia.key");

        public static string ObtenerRutaLicenciaEjecutable()
        {
            string baseDir = AppDomain.CurrentDomain.BaseDirectory ?? "";
            string rutaRelativa = null;
            try { rutaRelativa = ConfigurationManager.AppSettings["RutaLicencia"]; } catch { }
            if (string.IsNullOrWhiteSpace(rutaRelativa))
                rutaRelativa = "licencia.key";
            return Path.IsPathRooted(rutaRelativa) ? rutaRelativa : Path.Combine(baseDir, rutaRelativa);
        }

        public static string LeerClaveDesdeArchivos()
        {
            foreach (string path in new[] { ObtenerRutaLicenciaEjecutable(), RutaLicenciaProgramData })
            {
                try
                {
                    if (!File.Exists(path))
                        continue;
                    string texto = File.ReadAllText(path).Trim();
                    if (!string.IsNullOrWhiteSpace(texto))
                        return texto;
                }
                catch { /* siguiente ruta */ }
            }
            return null;
        }

        public static bool GuardarClave(string key)
        {
            if (string.IsNullOrWhiteSpace(key))
                return false;

            key = key.Trim();
            bool ok = false;

            try
            {
                string dir = Path.GetDirectoryName(RutaLicenciaProgramData);
                if (!string.IsNullOrEmpty(dir))
                    Directory.CreateDirectory(dir);
                File.WriteAllText(RutaLicenciaProgramData, key);
                ok = true;
            }
            catch { }

            try
            {
                File.WriteAllText(ObtenerRutaLicenciaEjecutable(), key);
                ok = true;
            }
            catch { }

            return ok;
        }
    }
}
