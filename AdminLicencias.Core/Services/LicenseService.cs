using AdminLicencias.Core.Catalog;
using AdminLicencias.Core.Models;
using AdminLicencias.Core.Options;
using Newtonsoft.Json;
using System.Security.Cryptography;
using System.Text;

namespace AdminLicencias.Core.Services;

public sealed class LicenseService
{
    private readonly string _secretKey;

    public LicenseService(LicensingOptions options)
    {
        _secretKey = string.IsNullOrWhiteSpace(options.SecretKey)
            ? "Soctech_Sistemas_Seguridad_2025!"
            : options.SecretKey;
    }

    public string GenerarClave(string cuitCliente, string hwid, DateTime fechaVencimiento, List<string> modulos)
    {
        var payload = new LicensePayload
        {
            CuitCliente = cuitCliente ?? "",
            FechaExpiracion = fechaVencimiento.Date,
            HardwareID = (hwid ?? "").Trim().ToUpperInvariant(),
            ModulosPermitidos = modulos ?? new List<string>()
        };
        string json = JsonConvert.SerializeObject(payload);
        return Encriptar(json);
    }

    public LicensePayload? DesencriptarClave(string licenseKeyBase64)
    {
        if (string.IsNullOrWhiteSpace(licenseKeyBase64)) return null;
        try
        {
            string json = Desencriptar(licenseKeyBase64.Trim());
            return JsonConvert.DeserializeObject<LicensePayload>(json);
        }
        catch
        {
            return null;
        }
    }

    public LicenseValidationResult Validar(string licenseKeyBase64, string hardwareId)
    {
        var payload = DesencriptarClave(licenseKeyBase64);
        if (payload == null)
        {
            return new LicenseValidationResult
            {
                Valida = false,
                Mensaje = "Clave de licencia inválida o corrupta.",
                Estado = "invalida"
            };
        }

        if (DateTime.Now.Date > payload.FechaExpiracion.Date)
        {
            return new LicenseValidationResult
            {
                Valida = false,
                Mensaje = "Licencia expirada. Solicite una renovación al proveedor.",
                Payload = payload,
                DiasRestantes = (int)(payload.FechaExpiracion.Date - DateTime.Today).TotalDays,
                Estado = "vencida"
            };
        }

        string hwEsperado = (payload.HardwareID ?? "").Trim();
        string hwActual = (hardwareId ?? "").Trim().ToUpperInvariant();

        if (!string.IsNullOrWhiteSpace(hwEsperado) &&
            !string.Equals(hwActual, hwEsperado, StringComparison.OrdinalIgnoreCase))
        {
            return new LicenseValidationResult
            {
                Valida = false,
                Mensaje = "Esta licencia no es válida para este equipo.",
                Payload = payload,
                DiasRestantes = (int)(payload.FechaExpiracion.Date - DateTime.Today).TotalDays,
                Estado = "hwid_invalido"
            };
        }

        int dias = (int)(payload.FechaExpiracion.Date - DateTime.Today).TotalDays;
        string estado = dias <= 30 ? "por_vencer" : "activa";

        return new LicenseValidationResult
        {
            Valida = true,
            Mensaje = "Licencia válida.",
            Payload = payload,
            DiasRestantes = dias,
            Estado = estado
        };
    }

    public List<string> ResolverModulosPorPlan(string? plan, IEnumerable<string>? modulosOverride)
    {
        if (modulosOverride != null)
        {
            var lista = modulosOverride.Where(m => !string.IsNullOrWhiteSpace(m)).ToList();
            if (lista.Count > 0)
                return ModulosCatalog.ResolverLicencia(lista);
        }

        return (plan ?? "lite").Trim().ToLowerInvariant() switch
        {
            "pro" => ModulosCatalog.ObtenerPresetPro(),
            _ => ModulosCatalog.ObtenerPresetLite()
        };
    }

    private string Encriptar(string plainText)
    {
        byte[] iv = new byte[16];
        using var aes = Aes.Create();
        aes.Key = ObtenerKeyBytes();
        aes.IV = iv;

        using var encryptor = aes.CreateEncryptor(aes.Key, aes.IV);
        using var ms = new MemoryStream();
        using (var cs = new CryptoStream(ms, encryptor, CryptoStreamMode.Write))
        using (var sw = new StreamWriter(cs, Encoding.UTF8))
            sw.Write(plainText);

        return Convert.ToBase64String(ms.ToArray());
    }

    private string Desencriptar(string cipherBase64)
    {
        byte[] iv = new byte[16];
        byte[] cipherBytes = Convert.FromBase64String(cipherBase64);

        using var aes = Aes.Create();
        aes.Key = ObtenerKeyBytes();
        aes.IV = iv;

        using var decryptor = aes.CreateDecryptor(aes.Key, aes.IV);
        using var ms = new MemoryStream(cipherBytes);
        using var cs = new CryptoStream(ms, decryptor, CryptoStreamMode.Read);
        using var sr = new StreamReader(cs, Encoding.UTF8);
        return sr.ReadToEnd();
    }

    private byte[] ObtenerKeyBytes()
    {
        byte[] keyBytes = new byte[32];
        byte[] secretBytes = Encoding.UTF8.GetBytes(_secretKey);
        Array.Copy(secretBytes, keyBytes, Math.Min(keyBytes.Length, secretBytes.Length));
        return keyBytes;
    }
}
