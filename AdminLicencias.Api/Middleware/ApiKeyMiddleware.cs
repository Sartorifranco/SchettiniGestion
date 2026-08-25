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

        if (RequiereAdminAuth(path))
        {
            if (!TryAuthorizeAdmin(context, out string? userId))
            {
                context.Response.StatusCode = StatusCodes.Status401Unauthorized;
                await context.Response.WriteAsJsonAsync(new { error = "Sesión o API Key de administración inválida." });
                return;
            }

            if (string.IsNullOrWhiteSpace(userId))
            {
                context.Response.StatusCode = StatusCodes.Status400BadRequest;
                await context.Response.WriteAsJsonAsync(new { error = "Identificador de operador obligatorio." });
                return;
            }

            context.Items[ApiKeyConstants.UserIdentifierHeaderName] = userId;
        }
        else if (path.StartsWith("/api/licenses/validate", StringComparison.OrdinalIgnoreCase))
        {
            bool requirePos = _options.RequirePosApiKey || !string.IsNullOrWhiteSpace(_options.PosApiKey);
            if (requirePos)
            {
                if (string.IsNullOrWhiteSpace(_options.PosApiKey) ||
                    !SecureCompare.EqualsConstantTime(
                        context.Request.Headers[ApiKeyConstants.PosHeaderName].ToString(),
                        _options.PosApiKey))
                {
                    context.Response.StatusCode = StatusCodes.Status401Unauthorized;
                    await context.Response.WriteAsJsonAsync(new { error = "API Key del POS inválida o ausente." });
                    return;
                }
            }
        }

        await _next(context);
    }

    private bool TryAuthorizeAdmin(HttpContext context, out string? userIdentifier)
    {
        userIdentifier = null;

        if (string.IsNullOrWhiteSpace(_options.AdminApiKey))
            return false;

        // 1) Cookie de sesión HttpOnly (preferida por el panel web)
        if (context.Request.Cookies.TryGetValue(ApiKeyConstants.SessionCookieName, out string? cookie)
            && !string.IsNullOrWhiteSpace(cookie))
        {
            var sessions = context.RequestServices.GetRequiredService<AdminSessionService>();
            var ticket = sessions.TryRead(cookie);
            if (ticket != null)
            {
                userIdentifier = ticket.UserIdentifier;
                return true;
            }
        }

        // 2) Header X-Api-Key (scripts / herramientas)
        if (!SecureCompare.EqualsConstantTime(
                context.Request.Headers[ApiKeyConstants.AdminHeaderName].ToString(),
                _options.AdminApiKey))
            return false;

        if (!context.Request.Headers.TryGetValue(ApiKeyConstants.UserIdentifierHeaderName, out var provided)
            || string.IsNullOrWhiteSpace(provided.ToString()))
            return false;

        userIdentifier = provided.ToString().Trim();
        return true;
    }

    private static bool RequiereAdminAuth(string path) =>
        path.StartsWith("/api/licenses/generate", StringComparison.OrdinalIgnoreCase) ||
        path.StartsWith("/api/licenses/history", StringComparison.OrdinalIgnoreCase) ||
        path.StartsWith("/api/licenses/clients", StringComparison.OrdinalIgnoreCase) ||
        path.StartsWith("/api/licenses/dashboard", StringComparison.OrdinalIgnoreCase) ||
        path.StartsWith("/api/licenses/modules", StringComparison.OrdinalIgnoreCase) ||
        path.StartsWith("/api/licenses/revoke", StringComparison.OrdinalIgnoreCase) ||
        path.StartsWith("/api/licenses/audit", StringComparison.OrdinalIgnoreCase) ||
        path.StartsWith("/api/auth/me", StringComparison.OrdinalIgnoreCase);
}
