using System;
using System.Collections.Generic;
using System.Configuration;
using System.IO;
using System.Management;
using System.Security.Cryptography;
using System.Text;
using Newtonsoft.Json;

namespace SchettiniGestion
{
    public static class LicenseManager
    {
        // Clave AES — debe ser IDÉNTICA a la de GeneradorLicencias/Program.cs.
        private const string SECRET_KEY = "Soctech_Sistemas_Seguridad_2025!";

        public class LicenseData
        {
            public string CuitCliente { get; set; }
            public DateTime FechaExpiracion { get; set; }
            public string HardwareID { get; set; }
            public List<string> ModulosPermitidos { get; set; } = new List<string>();
        }

        private static LicenseData _licenciaActual;

        /// <summary>Mensaje descriptivo cuando ValidarLicencia() devuelve false.</summary>
        public static string UltimoMensajeError { get; private set; }

        public static void InvalidarCache() => _licenciaActual = null;

        // ─────────────────────────────────────────────────────────────
        //  Hardware ID (WMI — CPU + Placa madre)
        //  El mismo algoritmo se usa en ActivationWindow para mostrárselo
        //  al usuario y en ValidarLicencia() para compararlo.
        // ─────────────────────────────────────────────────────────────
        public static string ObtenerHardwareId()
        {
            string cpuId = "";
            string boardSerial = "";

            try
            {
                using (var mc = new ManagementClass("Win32_Processor"))
                using (var instances = mc.GetInstances())
                    foreach (ManagementObject obj in instances)
                    {
                        cpuId = obj["ProcessorId"]?.ToString()?.Trim() ?? "";
                        break;
                    }
            }
            catch { }

            try
            {
                using (var mc = new ManagementClass("Win32_BaseBoard"))
                using (var instances = mc.GetInstances())
                    foreach (ManagementObject obj in instances)
                    {
                        boardSerial = obj["SerialNumber"]?.ToString()?.Trim() ?? "";
                        break;
                    }
            }
            catch { }

            try
            {
                string raw = $"{cpuId}|{boardSerial}";
                using (var sha = SHA256.Create())
                {
                    byte[] hash = sha.ComputeHash(Encoding.UTF8.GetBytes(raw));
                    // 16 caracteres hex — corto, fácil de copiar/pegar al generador.
                    return BitConverter.ToString(hash, 0, 8).Replace("-", "").ToUpperInvariant();
                }
            }
            catch
            {
                return Environment.MachineName.ToUpperInvariant();
            }
        }

        // ─────────────────────────────────────────────────────────────
        //  Desencriptación AES
        //  IV = 16 bytes todo ceros (idéntico a GeneradorLicencias).
        //  Key = SECRET_KEY en UTF-8, padded a 32 bytes con ceros.
        // ─────────────────────────────────────────────────────────────
        private static string Desencriptar(string cipherBase64)
        {
            byte[] iv = new byte[16];
            byte[] cipherBytes = Convert.FromBase64String(cipherBase64);

            using (Aes aes = Aes.Create())
            {
                byte[] keyBytes = new byte[32];
                byte[] secretBytes = Encoding.UTF8.GetBytes(SECRET_KEY);
                Array.Copy(secretBytes, keyBytes, Math.Min(keyBytes.Length, secretBytes.Length));
                aes.Key = keyBytes;
                aes.IV = iv;

                ICryptoTransform decryptor = aes.CreateDecryptor(aes.Key, aes.IV);
                using (MemoryStream ms = new MemoryStream(cipherBytes))
                using (CryptoStream cs = new CryptoStream(ms, decryptor, CryptoStreamMode.Read))
                using (StreamReader sr = new StreamReader(cs, Encoding.UTF8))
                    return sr.ReadToEnd();
            }
        }

        // ─────────────────────────────────────────────────────────────
        //  Obtención de la clave cruda (archivo → config → base de datos)
        // ─────────────────────────────────────────────────────────────
        private static string ObtenerClaveLicencia()
        {
            string desdeArchivo = LicenseFileHelper.LeerClaveDesdeArchivos();
            if (!string.IsNullOrWhiteSpace(desdeArchivo))
                return desdeArchivo.Trim();

            try
            {
                string fromConfig = ConfigurationManager.AppSettings["LicenciaBase64"];
                if (!string.IsNullOrWhiteSpace(fromConfig))
                    return fromConfig.Trim();
            }
            catch { }

            try
            {
                string desdeBd = DatabaseService.ObtenerStringLicencia();
                if (!string.IsNullOrWhiteSpace(desdeBd))
                    return desdeBd.Trim();
            }
            catch { }

            return null;
        }

        // ─────────────────────────────────────────────────────────────
        //  Carga y desencriptación de la licencia
        // ─────────────────────────────────────────────────────────────
        private static bool CargarLicencia()
        {
            try
            {
                string claveLicencia = ObtenerClaveLicencia();

                if (!string.IsNullOrWhiteSpace(claveLicencia))
                {
                    string json = Desencriptar(claveLicencia);
                    _licenciaActual = JsonConvert.DeserializeObject<LicenseData>(json);
                    return _licenciaActual != null;
                }

#if DEBUG
                // Licencia de desarrollo embebida: solo activa en compilaciones Debug,
                // sin restricción de Hardware ID para facilitar el trabajo en distintos equipos.
                _licenciaActual = new LicenseData
                {
                    CuitCliente = "DEBUG-DEVELOPER",
                    FechaExpiracion = DateTime.Now.AddYears(10),
                    HardwareID = "",
                    ModulosPermitidos = new List<string>
                    {
                        "ACCESO_FACTURACION", "ACCESO_PRODUCTOS", "ACCESO_CLIENTES",
                        "ACCESO_VENTAS",      "ACCESO_STOCK",     "ACCESO_USUARIOS",
                        "ACCESO_PERMISOS",    "ACCESO_PROVEEDORES","ACCESO_COMPRAS",
                        "ACCESO_PRECIOS",     "ACCESO_CAJA",      "ACCESO_PRESUPUESTOS",
                        "ACCESO_CUENTASCORRIENTES", "ACCESO_LISTASPRECIOS",
                        "ACCESO_CONFIGURACION"
                    }
                };
                return true;
#else
                return false;
#endif
            }
            catch
            {
                return false;
            }
        }

        // ─────────────────────────────────────────────────────────────
        //  Validación completa: desencripta → verifica vencimiento →
        //  verifica Hardware ID (solo en Release)
        // ─────────────────────────────────────────────────────────────
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

#if !DEBUG
            // Validación de Hardware ID — activa solo en producción.
            if (!string.IsNullOrWhiteSpace(_licenciaActual.HardwareID))
            {
                string hwActual = ObtenerHardwareId();
                if (!string.Equals(hwActual, _licenciaActual.HardwareID.Trim(),
                                   StringComparison.OrdinalIgnoreCase))
                {
                    UltimoMensajeError = "Esta licencia no es válida para este equipo. Contacte al proveedor para reactivar.";
                    return false;
                }
            }
#endif

            return true;
        }

        private static readonly HashSet<string> ModulosImplicitos = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "ACCESO_USUARIOS",
            "ACCESO_PERMISOS",
            "ACCESO_CONFIGURACION"
        };

        public static bool IsModuleEnabled(string moduleName)
        {
            if (string.IsNullOrWhiteSpace(moduleName))
                return false;
            string mod = moduleName.ToUpperInvariant();
            if (ModulosImplicitos.Contains(mod))
                return true;

            if (_licenciaActual == null)
                CargarLicencia();
            if (_licenciaActual?.ModulosPermitidos == null)
                return false;
            return _licenciaActual.ModulosPermitidos.Contains(mod);
        }

        public static string ObtenerFechaVencimiento()
        {
            if (_licenciaActual == null)
                CargarLicencia();
            return _licenciaActual?.FechaExpiracion.ToShortDateString() ?? "-";
        }
    }
}
