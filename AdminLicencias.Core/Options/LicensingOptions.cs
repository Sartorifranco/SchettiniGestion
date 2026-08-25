namespace AdminLicencias.Core.Options;

public sealed class LicensingOptions
{
    public const string SectionName = "Licensing";

    /// <summary>Ruta del archivo datos.json (clientes + historial).</summary>
    public string DataFilePath { get; set; } = "";

    /// <summary>Ruta del archivo logs.json (auditoría del panel).</summary>
    public string AuditLogFilePath { get; set; } = "";

    /// <summary>Ruta opcional del archivo de configuración de ruta (compat WPF).</summary>
    public string ConfigFilePath { get; set; } = "";

    /// <summary>
    /// Clave AES compartida con SCHPOS / LicenseManager.
    /// Obligatorio vía configuración o variable de entorno (nunca hardcodear en Production).
    /// </summary>
    public string SecretKey { get; set; } = "";
}
