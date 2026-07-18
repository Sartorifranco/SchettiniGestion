using AdminLicencias.Api.Security;
using AdminLicencias.Api.Services;

namespace AdminLicencias.Api.Middleware;

public sealed class AuditMiddleware
{
    private readonly RequestDelegate _next;

    public AuditMiddleware(RequestDelegate next) => _next = next;

    public async Task InvokeAsync(HttpContext context, AuditLogService auditLog)
    {
        await _next(context);

        if (!DebeAuditar(context))
            return;

        int status = context.Response.StatusCode;
        if (status < 200 || status >= 300)
            return;

        string? accion = ResolverAccion(context.Request);
        if (accion is null)
            return;

        // Nunca debe romper la respuesta exitosa (p. ej. permisos de logs.json).
        try
        {
            auditLog.Registrar(new AuditLogEntry
            {
                Fecha = DateTime.Now,
                Usuario = ObtenerUsuario(context),
                Accion = accion,
                Ip = ObtenerIpCliente(context),
                Navegador = ResumirUserAgent(context.Request.Headers.UserAgent.ToString()),
                Metodo = context.Request.Method,
                Ruta = context.Request.Path.Value ?? ""
            });
        }
        catch (Exception ex)
        {
            var logger = context.RequestServices.GetService<ILogger<AuditMiddleware>>();
            logger?.LogWarning(ex, "No se pudo registrar auditoría para {Accion} {Ruta}", accion, context.Request.Path.Value);
        }
    }

    private static bool DebeAuditar(HttpContext context)
    {
        string path = context.Request.Path.Value ?? "";
        return path.StartsWith("/api/licenses/", StringComparison.OrdinalIgnoreCase) &&
               !path.StartsWith("/api/licenses/validate", StringComparison.OrdinalIgnoreCase) &&
               !path.StartsWith("/api/licenses/modules", StringComparison.OrdinalIgnoreCase);
    }

    private static string? ResolverAccion(HttpRequest request)
    {
        string path = request.Path.Value ?? "";
        string method = request.Method.ToUpperInvariant();

        if (path.Equals("/api/licenses/history", StringComparison.OrdinalIgnoreCase) && method == "GET")
            return "Ver historial";

        if (path.Equals("/api/licenses/dashboard", StringComparison.OrdinalIgnoreCase) && method == "GET")
            return "Ver dashboard";

        if (path.Equals("/api/licenses/audit", StringComparison.OrdinalIgnoreCase) && method == "GET")
            return "Ver auditoría";

        if (path.Equals("/api/licenses/generate", StringComparison.OrdinalIgnoreCase) && method == "POST")
            return "Generar licencia";

        if (path.Equals("/api/licenses/revoke", StringComparison.OrdinalIgnoreCase) && method == "POST")
            return "Revocar licencia";

        if (path.Equals("/api/licenses/clients", StringComparison.OrdinalIgnoreCase) && method == "GET")
            return "Listar clientes";

        if (path.StartsWith("/api/licenses/clients/", StringComparison.OrdinalIgnoreCase) && method == "GET")
            return "Ver cliente";

        if (path.Equals("/api/licenses/clients", StringComparison.OrdinalIgnoreCase) && method == "POST")
            return "Alta cliente";

        if (path.StartsWith("/api/licenses/clients/", StringComparison.OrdinalIgnoreCase) && method == "PUT")
            return "Modificar cliente";

        if (path.StartsWith("/api/licenses/clients/", StringComparison.OrdinalIgnoreCase) && method == "DELETE")
            return "Eliminar cliente";

        return null;
    }

    internal static string ObtenerUsuario(HttpContext context)
    {
        if (context.Request.Headers.TryGetValue(ApiKeyConstants.UserIdentifierHeaderName, out var value))
        {
            string id = value.ToString().Trim();
            if (!string.IsNullOrWhiteSpace(id))
                return id;
        }

        return "Desconocido";
    }

    internal static string ObtenerIpCliente(HttpContext context)
    {
        if (context.Request.Headers.TryGetValue("X-Forwarded-For", out var xff))
        {
            string first = xff.ToString().Split(',')[0].Trim();
            if (!string.IsNullOrWhiteSpace(first))
                return first;
        }

        return context.Connection.RemoteIpAddress?.ToString() ?? "Desconocida";
    }

    internal static string ResumirUserAgent(string? userAgent)
    {
        if (string.IsNullOrWhiteSpace(userAgent))
            return "Desconocido";

        string ua = userAgent.Trim();

        string browser = "Navegador";
        if (ua.Contains("Edg/", StringComparison.OrdinalIgnoreCase)) browser = "Edge";
        else if (ua.Contains("Chrome/", StringComparison.OrdinalIgnoreCase)) browser = "Chrome";
        else if (ua.Contains("Firefox/", StringComparison.OrdinalIgnoreCase)) browser = "Firefox";
        else if (ua.Contains("Safari/", StringComparison.OrdinalIgnoreCase)) browser = "Safari";

        string os = "SO desconocido";
        if (ua.Contains("Windows", StringComparison.OrdinalIgnoreCase)) os = "Windows";
        else if (ua.Contains("Android", StringComparison.OrdinalIgnoreCase)) os = "Android";
        else if (ua.Contains("iPhone", StringComparison.OrdinalIgnoreCase) ||
                 ua.Contains("iPad", StringComparison.OrdinalIgnoreCase)) os = "iOS";
        else if (ua.Contains("Mac OS", StringComparison.OrdinalIgnoreCase)) os = "macOS";
        else if (ua.Contains("Linux", StringComparison.OrdinalIgnoreCase)) os = "Linux";

        return $"{browser} · {os}";
    }
}
