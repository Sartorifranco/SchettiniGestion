using AdminLicencias.Api;
using AdminLicencias.Api.Contracts;
using AdminLicencias.Api.Middleware;
using AdminLicencias.Api.Security;
using AdminLicencias.Api.Services;
using AdminLicencias.Core;
using AdminLicencias.Core.Catalog;
using AdminLicencias.Core.Models;
using AdminLicencias.Core.Options;
using AdminLicencias.Core.Services;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Extensions.Options;
using System.Net;
using System.Reflection;
using System.Threading.RateLimiting;

var builder = WebApplication.CreateBuilder(args);

builder.Services.Configure<ForwardedHeadersOptions>(options =>
{
    options.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;
    options.KnownProxies.Add(IPAddress.Loopback);
    options.KnownProxies.Add(IPAddress.IPv6Loopback);
    // nginx en el mismo host
    options.KnownNetworks.Add(new Microsoft.AspNetCore.HttpOverrides.IPNetwork(IPAddress.Parse("127.0.0.0"), 8));
});

builder.Services.Configure<LicensingOptions>(builder.Configuration.GetSection(LicensingOptions.SectionName));
builder.Services.Configure<ApiSecurityOptions>(builder.Configuration.GetSection(ApiSecurityOptions.SectionName));
builder.Services.AddDataProtection();
builder.Services.AddSingleton<AdminSessionService>();
builder.Services.AddAdminLicenciasCore();
builder.Services.AddSingleton<AuditLogService>();

builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    options.AddPolicy("licenses", httpContext =>
        RateLimitPartition.GetFixedWindowLimiter(
            partitionKey: httpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown",
            factory: _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = 60,
                Window = TimeSpan.FromMinutes(1),
                QueueLimit = 0
            }));

    options.AddPolicy("auth", httpContext =>
        RateLimitPartition.GetFixedWindowLimiter(
            partitionKey: httpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown",
            factory: _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = 10,
                Window = TimeSpan.FromMinutes(1),
                QueueLimit = 0
            }));

    options.AddPolicy("validate", httpContext =>
        RateLimitPartition.GetFixedWindowLimiter(
            partitionKey: httpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown",
            factory: _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = 30,
                Window = TimeSpan.FromMinutes(1),
                QueueLimit = 0
            }));
});

var app = builder.Build();

// Validación de configuración en Production
{
    var licOpts = app.Services.GetRequiredService<IOptions<LicensingOptions>>().Value;
    var secOpts = app.Services.GetRequiredService<IOptions<ApiSecurityOptions>>().Value;
    var logger = app.Services.GetRequiredService<ILoggerFactory>().CreateLogger("Startup");

    if (app.Environment.IsProduction())
    {
        if (string.IsNullOrWhiteSpace(licOpts.SecretKey))
            throw new InvalidOperationException(
                "Production: Licensing__SecretKey es obligatoria (misma clave que SCHPOS LicenseManager).");

        if (string.IsNullOrWhiteSpace(secOpts.AdminApiKey) ||
            secOpts.AdminApiKey.Contains("CAMBIAR", StringComparison.OrdinalIgnoreCase) ||
            secOpts.AdminApiKey.Contains("REEMPLAZAR", StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("Production: ApiSecurity__AdminApiKey debe ser una clave fuerte.");

        // Fail-closed: en Production siempre exigir PosApiKey
        if (string.IsNullOrWhiteSpace(secOpts.PosApiKey))
        {
            logger.LogWarning(
                "Production sin PosApiKey: se fuerza RequirePosApiKey. Configure ApiSecurity__PosApiKey para habilitar /validate.");
            secOpts.RequirePosApiKey = true;
        }
        else
        {
            secOpts.RequirePosApiKey = true;
        }
    }
    else if (string.IsNullOrWhiteSpace(licOpts.SecretKey))
    {
        logger.LogWarning("Licensing:SecretKey vacía — configure appsettings.Development.json o variables de entorno.");
    }
}

app.UseForwardedHeaders();

var allowedOrigins = app.Services.GetRequiredService<IOptions<ApiSecurityOptions>>().Value.AllowedOrigins
    ?? Array.Empty<string>();
app.UseCors(policy =>
{
    if (allowedOrigins.Length == 0)
    {
        policy.SetIsOriginAllowed(_ => false);
    }
    else
    {
        policy.WithOrigins(allowedOrigins)
            .AllowAnyHeader()
            .AllowAnyMethod()
            .AllowCredentials();
    }
});

app.Use(async (context, next) =>
{
    try
    {
        await next();
    }
    catch (Exception ex)
    {
        if (context.Response.HasStarted)
            throw;

        var logger = context.RequestServices.GetService<ILoggerFactory>()
            ?.CreateLogger("UnhandledException");
        logger?.LogError(ex, "Error no controlado en {Method} {Path}", context.Request.Method, context.Request.Path);

        context.Response.Clear();
        context.Response.StatusCode = StatusCodes.Status500InternalServerError;
        context.Response.ContentType = "application/json; charset=utf-8";

        bool reveal = app.Environment.IsDevelopment();
        await context.Response.WriteAsJsonAsync(new
        {
            error = reveal ? ex.Message : "Error interno del servidor.",
            type = reveal ? ex.GetType().Name : "ServerError"
        });
    }
});

app.UseRateLimiter();
app.UseMiddleware<ApiKeyMiddleware>();
app.UseMiddleware<AuditMiddleware>();
app.UseDefaultFiles();
app.UseStaticFiles();

app.MapGet("/panel", () => Results.Redirect("/", permanent: true));
app.MapGet("/panel/{*path}", (string path) => Results.Redirect("/" + path, permanent: true));

string buildId = Assembly.GetExecutingAssembly()
    .GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion
    ?? Assembly.GetExecutingAssembly().GetName().Version?.ToString()
    ?? "unknown";

app.MapGet("/api", () => Results.Ok(new
{
    servicio = "SCHPOS License API",
    version = "1.3",
    build = buildId,
    endpoints = new[]
    {
        "POST /api/auth/login", "POST /api/auth/logout", "GET /api/auth/me",
        "POST /api/licenses/generate", "POST /api/licenses/validate", "GET /api/licenses/history",
        "GET|POST /api/licenses/clients", "PUT|DELETE /api/licenses/clients/{id}",
        "GET /api/licenses/dashboard", "GET /api/licenses/modules", "POST /api/licenses/revoke",
        "GET /api/licenses/audit"
    }
}));

app.MapGet("/api/version", (IHostEnvironment env) =>
{
    if (env.IsProduction())
    {
        return Results.Ok(new
        {
            version = "1.3",
            build = buildId,
            environment = env.EnvironmentName,
            utc = DateTime.UtcNow
        });
    }

    var dataStore = app.Services.GetRequiredService<DataStore>();
    string dataPath = dataStore.RutaActual;
    string? dataDir = Path.GetDirectoryName(dataPath);
    return Results.Ok(new
    {
        version = "1.3",
        build = buildId,
        environment = env.EnvironmentName,
        dataFile = dataPath,
        dataFileExists = File.Exists(dataPath),
        dataDirWritable = dataDir != null && IsDirWritable(dataDir),
        utc = DateTime.UtcNow
    });
});

static bool IsDirWritable(string dir)
{
    try
    {
        if (!Directory.Exists(dir))
            return false;
        string probe = Path.Combine(dir, $".write-test-{Guid.NewGuid():N}");
        File.WriteAllText(probe, "ok");
        File.Delete(probe);
        return true;
    }
    catch
    {
        return false;
    }
}

var auth = app.MapGroup("/api/auth").RequireRateLimiting("auth");

auth.MapPost("/login", (
    LoginRequest req,
    AdminSessionService sessions,
    IOptions<ApiSecurityOptions> security,
    HttpContext http) =>
{
    var opts = security.Value;
    if (string.IsNullOrWhiteSpace(req.AdminApiKey) || string.IsNullOrWhiteSpace(req.UserIdentifier))
        return Results.BadRequest(new { error = "AdminApiKey y UserIdentifier son obligatorios." });

    if (!SecureCompare.EqualsConstantTime(req.AdminApiKey, opts.AdminApiKey))
        return Results.Unauthorized();

    string user = req.UserIdentifier.Trim();
    if (user.Length < 2 || user.Length > 80)
        return Results.BadRequest(new { error = "UserIdentifier inválido." });

    string cookieValue = sessions.CreateCookieValue(user);
    http.Response.Cookies.Append(ApiKeyConstants.SessionCookieName, cookieValue, sessions.BuildCookieOptions(http.Request));

    return Results.Ok(new { ok = true, userIdentifier = user });
});

auth.MapPost("/logout", (AdminSessionService sessions, HttpContext http) =>
{
    http.Response.Cookies.Delete(ApiKeyConstants.SessionCookieName, new CookieOptions
    {
        Path = "/",
        Secure = http.Request.IsHttps
            || string.Equals(http.Request.Headers["X-Forwarded-Proto"].ToString(), "https", StringComparison.OrdinalIgnoreCase),
        SameSite = SameSiteMode.Strict,
        HttpOnly = true
    });
    return Results.Ok(new { ok = true });
});

auth.MapGet("/me", (HttpContext http) =>
{
    if (http.Items.TryGetValue(ApiKeyConstants.UserIdentifierHeaderName, out var item)
        && item is string user
        && !string.IsNullOrWhiteSpace(user))
        return Results.Ok(new { authenticated = true, userIdentifier = user });

    return Results.Json(new { authenticated = false }, statusCode: StatusCodes.Status401Unauthorized);
});

var licenses = app.MapGroup("/api/licenses").RequireRateLimiting("licenses");

licenses.MapPost("/generate", (
    GenerateLicenseRequest req,
    LicenseService licenseService,
    DataStore dataStore,
    IHostEnvironment env) =>
{
    try
    {
        if (string.IsNullOrWhiteSpace(req.HardwareId))
            return Results.BadRequest(new { error = "HardwareId es obligatorio." });

        if (req.FechaVencimiento.Date <= DateTime.Today)
            return Results.BadRequest(new { error = "FechaVencimiento debe ser futura." });

        Cliente? cliente = null;
        if (req.ClienteId.HasValue)
            cliente = dataStore.ObtenerCliente(req.ClienteId.Value);

        if (cliente == null && !string.IsNullOrWhiteSpace(req.Cuit))
            cliente = dataStore.BuscarClientePorCuit(req.Cuit);

        if (cliente == null)
        {
            if (string.IsNullOrWhiteSpace(req.Cuit) || string.IsNullOrWhiteSpace(req.RazonSocial))
                return Results.BadRequest(new { error = "Indique ClienteId o bien CUIT + RazonSocial." });

            cliente = new Cliente
            {
                RazonSocial = req.RazonSocial.Trim(),
                CUIT = req.Cuit.Trim()
            };
            dataStore.GuardarCliente(cliente);
        }

        string hwid = req.HardwareId.Trim().ToUpperInvariant();
        var modulos = licenseService.ResolverModulosPorPlan(req.Plan, req.Modulos);
        string plan = (req.Plan ?? "lite").Trim().ToLowerInvariant();

        string clave;
        try
        {
            clave = licenseService.GenerarClave(cliente.CUIT, hwid, req.FechaVencimiento, modulos);
        }
        catch (Exception ex)
        {
            string msg = env.IsDevelopment() ? "Error al generar la clave: " + ex.Message : "Error al generar la clave.";
            return Results.Json(new { error = msg }, statusCode: 500);
        }

        var licencia = new Licencia
        {
            ClienteId = cliente.Id,
            HWID = hwid,
            LicenseKey = clave,
            HuellaClave = LicenseService.HuellaClave(clave),
            FechaEmision = DateTime.Today,
            FechaVencimiento = req.FechaVencimiento.Date,
            Modulos = modulos,
            MontoLicencia = req.MontoLicencia,
            AbonoMensual = req.AbonoMensual,
            VersionSchpos = string.IsNullOrWhiteSpace(req.VersionSchpos) ? "2.4.0" : req.VersionSchpos.Trim(),
            EsRenovacion = req.EsRenovacion,
            Observaciones = req.Observaciones ?? "",
            Plan = plan
        };

        if (licencia.EsRenovacion)
        {
            var anterior = dataStore.UltimaLicencia(cliente.Id);
            if (anterior != null)
                licencia.LicenciaAnteriorId = anterior.Id;
        }

        dataStore.GuardarLicencia(licencia);

        return Results.Ok(new GenerateLicenseResponse
        {
            LicenseKey = clave,
            LicenciaId = licencia.Id,
            ClienteId = cliente.Id,
            Plan = plan,
            FechaVencimiento = licencia.FechaVencimiento,
            Modulos = modulos,
            ModulosResumen = licencia.ModulosResumen
        });
    }
    catch (Exception ex)
    {
        string msg = env.IsDevelopment() ? "Error al guardar la licencia: " + ex.Message : "Error al guardar la licencia.";
        return Results.Json(new { error = msg }, statusCode: 500);
    }
});

licenses.MapPost("/validate", (
    ValidateLicenseRequest req,
    LicenseService licenseService,
    DataStore dataStore) =>
{
    if (string.IsNullOrWhiteSpace(req.LicenseKey))
        return Results.BadRequest(new { error = "LicenseKey es obligatorio." });

    if (string.IsNullOrWhiteSpace(req.HardwareId))
        return Results.BadRequest(new { error = "HardwareId es obligatorio." });

    bool revocada = dataStore.EstaClaveRevocada(req.LicenseKey);
    var result = licenseService.Validar(req.LicenseKey, req.HardwareId, revocada);

    return Results.Ok(new ValidateLicenseResponse
    {
        Valida = result.Valida,
        Mensaje = result.Mensaje,
        Estado = result.Estado,
        DiasRestantes = result.DiasRestantes,
        FechaExpiracion = result.Payload?.FechaExpiracion,
        CuitCliente = result.Payload?.CuitCliente,
        ModulosPermitidos = result.Payload?.ModulosPermitidos ?? new List<string>()
    });
}).RequireRateLimiting("validate");

licenses.MapGet("/history", (DataStore dataStore) =>
{
    ModulosCatalog.EnsureLoaded();
    return Results.Ok(dataStore.ObtenerHistorial());
});

licenses.MapGet("/clients", (DataStore dataStore) =>
    Results.Ok(dataStore.ObtenerClientesResumen()));

licenses.MapGet("/clients/{id:guid}", (Guid id, DataStore dataStore) =>
{
    var cliente = dataStore.ObtenerClienteDetalle(id);
    return cliente is null
        ? Results.NotFound(new { error = "Cliente no encontrado." })
        : Results.Ok(ClienteMapper.ToResponse(cliente));
});

licenses.MapPost("/clients", (UpsertClienteRequest req, DataStore dataStore) =>
{
    var error = ClienteMapper.Validar(req);
    if (error != null) return Results.BadRequest(new { error });

    if (!string.IsNullOrWhiteSpace(req.CUIT))
    {
        var existente = dataStore.BuscarClientePorCuit(req.CUIT);
        if (existente != null)
            return Results.Conflict(new { error = "Ya existe un cliente con ese CUIT." });
    }

    var entity = ClienteMapper.ToEntity(req);
    var saved = dataStore.GuardarClienteCompleto(entity);
    return Results.Created($"/api/licenses/clients/{saved.Id}", ClienteMapper.ToResponse(saved));
});

licenses.MapPut("/clients/{id:guid}", (Guid id, UpsertClienteRequest req, DataStore dataStore) =>
{
    var error = ClienteMapper.Validar(req);
    if (error != null) return Results.BadRequest(new { error });

    var existing = dataStore.ObtenerCliente(id);
    if (existing is null)
        return Results.NotFound(new { error = "Cliente no encontrado." });

    if (!string.IsNullOrWhiteSpace(req.CUIT))
    {
        var otro = dataStore.BuscarClientePorCuit(req.CUIT);
        if (otro != null && otro.Id != id)
            return Results.Conflict(new { error = "Ya existe otro cliente con ese CUIT." });
    }

    var entity = ClienteMapper.ToEntity(req, existing);
    var saved = dataStore.GuardarClienteCompleto(entity);
    return Results.Ok(ClienteMapper.ToResponse(saved));
});

licenses.MapDelete("/clients/{id:guid}", (Guid id, DataStore dataStore) =>
{
    var existing = dataStore.ObtenerCliente(id);
    if (existing is null)
        return Results.NotFound(new { error = "Cliente no encontrado." });

    dataStore.EliminarCliente(id);
    return Results.NoContent();
});

licenses.MapPost("/revoke", (RevokeLicenseRequest req, DataStore dataStore) =>
{
    if (req.LicenciaId == Guid.Empty)
        return Results.BadRequest(new { error = "LicenciaId es obligatorio." });

    if (!dataStore.RevocarLicencia(req.LicenciaId))
        return Results.NotFound(new { error = "Licencia no encontrada." });

    return Results.Ok(new
    {
        mensaje = "Licencia revocada. /validate rechazará la clave; SCHPOS offline sigue hasta renovar o consultar online.",
        licenciaId = req.LicenciaId
    });
});

licenses.MapGet("/dashboard", (DataStore dataStore) =>
    Results.Ok(dataStore.ObtenerDashboardStats()));

licenses.MapGet("/modules", () =>
{
    ModulosCatalog.EnsureLoaded();
    return Results.Ok(ModulosCatalog.ObtenerLicenciables()
        .Select(m => new
        {
            m.Codigo,
            m.Nombre,
            m.NombreCorto,
            m.Grupo,
            m.IncluidoEnLite,
            m.EsAbonoMensual,
            m.Descripcion,
            m.Orden,
            m.DependeDe
        }));
});

licenses.MapGet("/audit", (AuditLogService auditLog) =>
    Results.Ok(auditLog.ObtenerUltimos(100)));

app.Run();

public sealed class LoginRequest
{
    public string AdminApiKey { get; set; } = "";
    public string UserIdentifier { get; set; } = "";
}
