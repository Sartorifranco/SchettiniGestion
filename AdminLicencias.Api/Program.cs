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

var builder = WebApplication.CreateBuilder(args);

builder.Services.Configure<ForwardedHeadersOptions>(options =>
{
    options.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;
    options.KnownNetworks.Clear();
    options.KnownProxies.Clear();
});

builder.Services.Configure<LicensingOptions>(builder.Configuration.GetSection(LicensingOptions.SectionName));
builder.Services.Configure<ApiSecurityOptions>(builder.Configuration.GetSection(ApiSecurityOptions.SectionName));
builder.Services.AddAdminLicenciasCore();
builder.Services.AddSingleton<AuditLogService>();

builder.Services.AddCors(options =>
{
    options.AddPolicy("LicensePanel", policy =>
        policy.SetIsOriginAllowed(_ => true)
              .AllowAnyHeader()
              .AllowAnyMethod());
});

var app = builder.Build();

app.UseForwardedHeaders();
app.UseCors("LicensePanel");
app.UseMiddleware<ApiKeyMiddleware>();
app.UseMiddleware<AuditMiddleware>();
app.UseDefaultFiles();
app.UseStaticFiles();

app.MapGet("/panel", () => Results.Redirect("/", permanent: true));
app.MapGet("/panel/{*path}", (string path) => Results.Redirect("/" + path, permanent: true));

app.MapGet("/api", () => Results.Ok(new
{
    servicio = "SCHPOS License API",
    version = "1.0",
    endpoints = new[]
    {
        "POST /api/licenses/generate", "POST /api/licenses/validate", "GET /api/licenses/history",
        "GET|POST /api/licenses/clients", "PUT|DELETE /api/licenses/clients/{id}",
        "GET /api/licenses/dashboard", "GET /api/licenses/modules", "POST /api/licenses/revoke",
        "GET /api/licenses/audit"
    }
}));

var licenses = app.MapGroup("/api/licenses");

licenses.MapPost("/generate", (
    GenerateLicenseRequest req,
    LicenseService licenseService,
    DataStore dataStore) =>
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
        return Results.Problem("Error al generar la clave: " + ex.Message, statusCode: 500);
    }

    var licencia = new Licencia
    {
        ClienteId = cliente.Id,
        HWID = hwid,
        LicenseKey = clave,
        FechaEmision = DateTime.Today,
        FechaVencimiento = req.FechaVencimiento.Date,
        Modulos = modulos,
        MontoLicencia = req.MontoLicencia,
        AbonoMensual = req.AbonoMensual,
        VersionSchpos = req.VersionSchpos ?? "2.0.8",
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
});

licenses.MapPost("/validate", (
    ValidateLicenseRequest req,
    LicenseService licenseService) =>
{
    if (string.IsNullOrWhiteSpace(req.LicenseKey))
        return Results.BadRequest(new { error = "LicenseKey es obligatorio." });

    if (string.IsNullOrWhiteSpace(req.HardwareId))
        return Results.BadRequest(new { error = "HardwareId es obligatorio." });

    var result = licenseService.Validar(req.LicenseKey, req.HardwareId);

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
});

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

    return Results.Ok(new { mensaje = "Licencia revocada.", licenciaId = req.LicenciaId });
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
            m.Orden
        }));
});

licenses.MapGet("/audit", (AuditLogService auditLog) =>
    Results.Ok(auditLog.ObtenerUltimos(100)));

app.Run();
