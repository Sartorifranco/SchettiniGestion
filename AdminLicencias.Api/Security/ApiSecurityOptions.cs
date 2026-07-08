namespace AdminLicencias.Api.Security;

public static class ApiKeyConstants
{
    public const string AdminHeaderName = "X-Api-Key";
    public const string PosHeaderName = "X-Pos-Api-Key";
}

public sealed class ApiSecurityOptions
{
    public const string SectionName = "ApiSecurity";

    /// <summary>Protege POST /generate y GET /history.</summary>
    public string AdminApiKey { get; set; } = "";

    /// <summary>Opcional en POST /validate. Si está vacío, /validate es público.</summary>
    public string? PosApiKey { get; set; }
}
