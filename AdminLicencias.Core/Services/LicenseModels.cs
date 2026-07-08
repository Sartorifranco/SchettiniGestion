namespace AdminLicencias.Core.Services;

public sealed class LicensePayload
{
    public string CuitCliente { get; set; } = "";
    public DateTime FechaExpiracion { get; set; }
    public string HardwareID { get; set; } = "";
    public List<string> ModulosPermitidos { get; set; } = new();
}

public sealed class LicenseValidationResult
{
    public bool Valida { get; init; }
    public string Mensaje { get; init; } = "";
    public LicensePayload? Payload { get; init; }
    public int DiasRestantes { get; init; }
    public string Estado { get; init; } = "";
}
