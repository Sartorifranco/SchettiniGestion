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
        private static string _hardwareIdCache;
        private static readonly object _hardwareIdLock = new object();

        public static string ObtenerHardwareId()
        {
            if (!string.IsNullOrEmpty(_hardwareIdCache)) return _hardwareIdCache;
            lock (_hardwareIdLock)
            {
                if (!string.IsNullOrEmpty(_hardwareIdCache)) return _hardwareIdCache;
                _hardwareIdCache = CalcularHardwareIdWmi();
                return _hardwareIdCache;
            }
        }

        private static string CalcularHardwareIdWmi()
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
        //  Obtención de la clave cruda
        //  Orden: ProgramData → BD → exe → App.config
        //  (el .key junto al exe puede ser viejo si no se pudo sobrescribir en Program Files)
        // ─────────────────────────────────────────────────────────────
        private static string ObtenerClaveLicencia()
        {
            try
            {
                if (File.Exists(LicenseFileHelper.RutaLicenciaProgramData))
                {
                    string desdePd = File.ReadAllText(LicenseFileHelper.RutaLicenciaProgramData).Trim();
                    if (!string.IsNullOrWhiteSpace(desdePd))
                        return desdePd;
                }
            }
            catch { }

            try
            {
                string desdeBd = DatabaseService.ObtenerStringLicencia();
                if (!string.IsNullOrWhiteSpace(desdeBd))
                    return desdeBd.Trim();
            }
            catch { }

            try
            {
                string rutaExe = LicenseFileHelper.ObtenerRutaLicenciaEjecutable();
                if (File.Exists(rutaExe))
                {
                    string desdeExe = File.ReadAllText(rutaExe).Trim();
                    if (!string.IsNullOrWhiteSpace(desdeExe))
                        return desdeExe;
                }
            }
            catch { }

            try
            {
                string fromConfig = ConfigurationManager.AppSettings["LicenciaBase64"];
                if (!string.IsNullOrWhiteSpace(fromConfig))
                    return fromConfig.Trim();
            }
            catch { }

            return null;
        }

        private static bool CargarLicenciaDesdeClave(string claveLicencia)
        {
            if (string.IsNullOrWhiteSpace(claveLicencia))
                return false;
            try
            {
                string json = Desencriptar(claveLicencia.Trim());
                _licenciaActual = JsonConvert.DeserializeObject<LicenseData>(json);
                return _licenciaActual != null;
            }
            catch
            {
                return false;
            }
        }

        // ─────────────────────────────────────────────────────────────
        //  Carga y desencriptación de la licencia
        // ─────────────────────────────────────────────────────────────
        private static bool CargarLicencia()
        {
            try
            {
                if (CargarLicenciaDesdeClave(ObtenerClaveLicencia()))
                {
#if DEBUG
                    // Si hay licencia.key (p. ej. Lite) sin Compras/Proveedores, en Debug
                    // se suman módulos de avance para poder probar v2.2 sin regenerar clave.
                    AsegurarModulosDebugParaPruebasAvances();
#endif
                    return true;
                }

#if DEBUG
                // Licencia de desarrollo embebida: solo activa en compilaciones Debug,
                // sin restricción de Hardware ID para facilitar el trabajo en distintos equipos.
                _licenciaActual = new LicenseData
                {
                    CuitCliente = "DEBUG-DEVELOPER",
                    FechaExpiracion = DateTime.Now.AddYears(10),
                    HardwareID = "",
                    ModulosPermitidos = ModulosCatalog.ResolverLicencia(new List<string>
                    {
                        "ACCESO_VENTAS", "ACCESO_FACTURACION", "ACCESO_PRODUCTOS", "ACCESO_STOCK",
                        "ACCESO_CLIENTES", "ACCESO_CAJA", "ACCESO_LISTASPRECIOS", "ACCESO_PRECIOS",
                        "ACCESO_PRESUPUESTOS", "ACCESO_ESTADISTICAS", "ACCESO_COMPRAS", "ACCESO_PROVEEDORES",
                        "ACCESO_CUENTASCORRIENTES", "ACCESO_RED", "ACCESO_AFIP", "ACCESO_ETIQUETAS", "ACCESO_VISOR_CLIENTE",
                        "ACCESO_MERCADOPAGO_QR", "ACCESO_MERCADOPAGO_POINT", "ACCESO_SOPORTE"
                    })
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

#if DEBUG
        /// <summary>
        /// Solo Debug: garantiza módulos de avances v2.2 aunque la licencia.key del equipo sea Lite.
        /// No aplica en Release / instaladores de clientes.
        /// </summary>
        private static void AsegurarModulosDebugParaPruebasAvances()
        {
            if (_licenciaActual == null) return;

            var baseMods = _licenciaActual.ModulosPermitidos ?? new List<string>();
            var unidos = new List<string>(baseMods)
            {
                "ACCESO_PROVEEDORES", "ACCESO_COMPRAS", "ACCESO_ESTADISTICAS",
                "ACCESO_ETIQUETAS", "ACCESO_CUENTASCORRIENTES"
            };
            _licenciaActual.ModulosPermitidos = ModulosCatalog.ResolverLicencia(unidos);
        }
#endif

        // ─────────────────────────────────────────────────────────────
        //  Validación completa: desencripta → verifica vencimiento →
        //  verifica Hardware ID (solo en Release)
        // ─────────────────────────────────────────────────────────────
        /// <param name="claveForzada">
        /// Si se indica (p. ej. al activar), valida esa clave en memoria
        /// en lugar de releer un licencia.key posiblemente desactualizado.
        /// </param>
        public static bool ValidarLicencia(string claveForzada = null)
        {
            UltimoMensajeError = null;
            InvalidarCache();

            bool cargada = !string.IsNullOrWhiteSpace(claveForzada)
                ? CargarLicenciaDesdeClave(claveForzada)
                : CargarLicencia();

            if (!cargada)
            {
                UltimoMensajeError = "No hay licencia activa. Pegue la clave que le envió el proveedor o cargue el archivo licencia.key.";
                return false;
            }

#if DEBUG
            AsegurarModulosDebugParaPruebasAvances();
#endif

            // Comparar por día de calendario (la clave guarda medianoche del día de vencimiento).
            if (DateTime.Now.Date > _licenciaActual.FechaExpiracion.Date)
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

        /// <summary>Módulos que indican licencia «completa» anterior a extras monetizables (no quitar funciones).</summary>
        private static readonly string[] MarcadoresLicenciaLegacy = new[]
        {
            "ACCESO_LISTASPRECIOS", "ACCESO_PRECIOS", "ACCESO_PRESUPUESTOS",
            "ACCESO_COMPRAS", "ACCESO_PROVEEDORES", "ACCESO_CUENTASCORRIENTES"
        };


        private static HashSet<string> ObtenerModulosImplicitos()
        {
            return new HashSet<string>(ModulosCatalog.ObtenerImplicitos(), StringComparer.OrdinalIgnoreCase);
        }

        /// <summary>Licencias emitidas antes del esquema Lite/Pro conservan todos los extras.</summary>
        public static bool EsLicenciaLegacyCompleta()
        {
            if (_licenciaActual == null)
                CargarLicencia();
            if (_licenciaActual?.ModulosPermitidos == null)
                return false;

            foreach (var marcador in MarcadoresLicenciaLegacy)
            {
                if (_licenciaActual.ModulosPermitidos.Contains(marcador))
                    return true;
            }
            return false;
        }

        public static bool IsModuleEnabled(string moduleName)
        {
            if (string.IsNullOrWhiteSpace(moduleName))
                return false;
            string mod = moduleName.ToUpperInvariant();
            if (ObtenerModulosImplicitos().Contains(mod))
                return true;

            if (_licenciaActual == null)
                CargarLicencia();
            if (_licenciaActual?.ModulosPermitidos == null)
                return false;
            return _licenciaActual.ModulosPermitidos.Contains(mod);
        }

        /// <summary>
        /// Extras monetizables (RED, ARCA, etiquetas, visor, MP QR/Point, soporte).
        /// Solo se habilitan si el código está en la licencia.
        /// Ya no se regalan por «licencia legacy/Pro»: si no compró RED, no ve ni usa red.
        /// </summary>
        public static bool IsExtraEnabled(string extraCode)
        {
            if (string.IsNullOrWhiteSpace(extraCode))
                return false;

            return IsModuleEnabled(extraCode.ToUpperInvariant());
        }

        /// <summary>True solo con el extra ACCESO_RED contratado en la licencia.</summary>
        public static bool TieneConexionRed() => IsExtraEnabled("ACCESO_RED");
        public static bool TieneAfip() => IsExtraEnabled("ACCESO_AFIP");
        public static bool TieneEtiquetas() => IsExtraEnabled("ACCESO_ETIQUETAS");
        public static bool TieneVisorCliente() => IsExtraEnabled("ACCESO_VISOR_CLIENTE");
        /// <summary>Abono independiente: cobro con código QR de Mercado Pago.</summary>
        public static bool TieneMercadoPagoQr() => IsExtraEnabled("ACCESO_MERCADOPAGO_QR");
        /// <summary>Abono independiente: terminal Point Smart / Smart 2.</summary>
        public static bool TieneMercadoPagoPoint() => IsExtraEnabled("ACCESO_MERCADOPAGO_POINT");
        public static bool TieneSoporte() => IsExtraEnabled("ACCESO_SOPORTE");
        /// <summary>Módulo adicional: Gráficos y Estadísticas (no es abono mensual).</summary>
        public static bool TieneEstadisticas() => IsModuleEnabled("ACCESO_ESTADISTICAS");

        public static string ObtenerFechaVencimiento()
        {
            if (_licenciaActual == null)
                CargarLicencia();
            return _licenciaActual?.FechaExpiracion.ToShortDateString() ?? "-";
        }
    }
}
