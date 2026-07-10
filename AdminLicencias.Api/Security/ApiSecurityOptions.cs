namespace AdminLicencias.Api.Security;

public static class ApiKeyConstants
{
    public const string AdminHeaderName = "X-Api-Key";
    public const string PosHeaderName = "X-Pos-Api-Key";
    public const string UserIdentifierHeaderName = "X-User-Identifier";
}

public sealed class ApiSecurityOptions
{
    public const string SectionName = "ApiSecurity";

    public string AdminApiKey { get; set; } = "";
    public string? PosApiKey { get; set; }
}
