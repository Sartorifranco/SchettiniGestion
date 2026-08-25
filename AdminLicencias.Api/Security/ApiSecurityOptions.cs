namespace AdminLicencias.Api.Security;

public static class ApiKeyConstants
{
    public const string AdminHeaderName = "X-Api-Key";
    public const string PosHeaderName = "X-Pos-Api-Key";
    public const string UserIdentifierHeaderName = "X-User-Identifier";
    public const string SessionCookieName = "schpos_admin_session";
}

public sealed class ApiSecurityOptions
{
    public const string SectionName = "ApiSecurity";

    public string AdminApiKey { get; set; } = "";

    /// <summary>Clave del POS para /validate. En Production es obligatoria si RequirePosApiKey=true.</summary>
    public string? PosApiKey { get; set; }

    /// <summary>Si true (recomendado en Production), /validate exige X-Pos-Api-Key.</summary>
    public bool RequirePosApiKey { get; set; }

    /// <summary>Orígenes CORS permitidos (vacío = mismo origen / sin CORS amplio).</summary>
    public string[] AllowedOrigins { get; set; } =
    [
        "https://licencias.schpos.com.ar",
        "http://localhost:5080",
        "http://127.0.0.1:5080"
    ];

    /// <summary>Horas de validez de la cookie de sesión del panel.</summary>
    public int SessionHours { get; set; } = 12;
}
