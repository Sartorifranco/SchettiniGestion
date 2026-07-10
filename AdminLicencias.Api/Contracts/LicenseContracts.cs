namespace AdminLicencias.Api.Contracts;

public sealed class GenerateLicenseRequest
{
    public string HardwareId { get; set; } = "";
    public Guid? ClienteId { get; set; }
    public string? Cuit { get; set; }
    public string? RazonSocial { get; set; }
    /// <summary>lite | pro. Ignorado si Modulos tiene ítems.</summary>
    public string Plan { get; set; } = "lite";
    public List<string>? Modulos { get; set; }
    public DateTime FechaVencimiento { get; set; }
    /// <summary>Cobro único por instalación / licencia.</summary>
    public decimal MontoLicencia { get; set; }
    /// <summary>Abono mensual según módulos contratados.</summary>
    public decimal AbonoMensual { get; set; }
    public string VersionSchpos { get; set; } = "2.0.8";
    public bool EsRenovacion { get; set; }
    public string Observaciones { get; set; } = "";
}

public sealed class GenerateLicenseResponse
{
    public string LicenseKey { get; set; } = "";
    public Guid LicenciaId { get; set; }
    public Guid ClienteId { get; set; }
    public string Plan { get; set; } = "";
    public DateTime FechaVencimiento { get; set; }
    public List<string> Modulos { get; set; } = new();
    public string ModulosResumen { get; set; } = "";
}

public sealed class ValidateLicenseRequest
{
    public string LicenseKey { get; set; } = "";
    public string HardwareId { get; set; } = "";
}

public sealed class ValidateLicenseResponse
{
    public bool Valida { get; set; }
    public string Mensaje { get; set; } = "";
    public string Estado { get; set; } = "";
    public int DiasRestantes { get; set; }
    public DateTime? FechaExpiracion { get; set; }
    public string? CuitCliente { get; set; }
    public List<string> ModulosPermitidos { get; set; } = new();
}
