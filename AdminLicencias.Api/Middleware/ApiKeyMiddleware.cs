using AdminLicencias.Api.Security;
using Microsoft.Extensions.Options;

namespace AdminLicencias.Api.Middleware;

public sealed class ApiKeyMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ApiSecurityOptions _options;

    public ApiKeyMiddleware(RequestDelegate next, IOptions<ApiSecurityOptions> options)
    {
        _next = next;
        _options = options.Value;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        string path = context.Request.Path.Value ?? "";

        if (path.StartsWith("/api/licenses/generate", StringComparison.OrdinalIgnoreCase) ||
            path.StartsWith("/api/licenses/history", StringComparison.OrdinalIgnoreCase))
        {
            if (!ValidarClave(context, _options.AdminApiKey, ApiKeyConstants.AdminHeaderName))
            {
                context.Response.StatusCode = StatusCodes.Status401Unauthorized;
                await context.Response.WriteAsJsonAsync(new { error = "API Key de administración inválida o ausente." });
                return;
            }
        }
        else if (path.StartsWith("/api/licenses/validate", StringComparison.OrdinalIgnoreCase))
        {
            if (!string.IsNullOrWhiteSpace(_options.PosApiKey) &&
                !ValidarClave(context, _options.PosApiKey, ApiKeyConstants.PosHeaderName))
            {
                context.Response.StatusCode = StatusCodes.Status401Unauthorized;
                await context.Response.WriteAsJsonAsync(new { error = "API Key del POS inválida o ausente." });
                return;
            }
        }

        await _next(context);
    }

    private static bool ValidarClave(HttpContext context, string? expected, string headerName)
    {
        if (string.IsNullOrWhiteSpace(expected))
            return false;

        if (!context.Request.Headers.TryGetValue(headerName, out var provided))
            return false;

        return string.Equals(provided.ToString().Trim(), expected.Trim(), StringComparison.Ordinal);
    }
}
