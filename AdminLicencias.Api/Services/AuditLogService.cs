using AdminLicencias.Core.Options;
using Microsoft.Extensions.Options;
using Newtonsoft.Json;
using System.Text;

namespace AdminLicencias.Api.Services;

public sealed class AuditLogService
{
    private const int MaxStoredEntries = 500;
    private readonly string _logPath;
    private readonly object _lock = new();

    public AuditLogService(IOptions<LicensingOptions> options)
    {
        _logPath = ResolverRutaLogs(options.Value);
    }

    public void Registrar(AuditLogEntry entry)
    {
        lock (_lock)
        {
            var entries = CargarInterno();
            entries.Add(entry);

            if (entries.Count > MaxStoredEntries)
                entries = entries.OrderByDescending(e => e.Fecha).Take(MaxStoredEntries).ToList();

            GuardarInterno(entries);
        }
    }

    public IReadOnlyList<AuditLogEntryDto> ObtenerUltimos(int cantidad = 100)
    {
        lock (_lock)
        {
            return CargarInterno()
                .OrderByDescending(e => e.Fecha)
                .Take(cantidad)
                .Select(e => new AuditLogEntryDto
                {
                    Id = e.Id,
                    Fecha = e.Fecha,
                    Usuario = e.Usuario,
                    Accion = e.Accion,
                    Ip = e.Ip,
                    Navegador = e.Navegador
                })
                .ToList();
        }
    }

    private List<AuditLogEntry> CargarInterno()
    {
        try
        {
            if (!File.Exists(_logPath))
                return new List<AuditLogEntry>();

            string json = File.ReadAllText(_logPath, Encoding.UTF8);
            return JsonConvert.DeserializeObject<List<AuditLogEntry>>(json) ?? new List<AuditLogEntry>();
        }
        catch
        {
            return new List<AuditLogEntry>();
        }
    }

    private void GuardarInterno(List<AuditLogEntry> entries)
    {
        string? dir = Path.GetDirectoryName(_logPath);
        if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
            Directory.CreateDirectory(dir);

        string json = JsonConvert.SerializeObject(entries, Formatting.Indented);
        File.WriteAllText(_logPath, json, Encoding.UTF8);
    }

    private static string ResolverRutaLogs(LicensingOptions options)
    {
        if (!string.IsNullOrWhiteSpace(options.AuditLogFilePath))
            return options.AuditLogFilePath.Trim();

        string env = Environment.GetEnvironmentVariable("SCHPOS_AUDIT_LOG_PATH") ?? "";
        if (!string.IsNullOrWhiteSpace(env))
            return env.Trim();

        if (!string.IsNullOrWhiteSpace(options.DataFilePath))
        {
            string? dir = Path.GetDirectoryName(options.DataFilePath);
            if (!string.IsNullOrEmpty(dir))
                return Path.Combine(dir, "logs.json");
        }

        if (!OperatingSystem.IsWindows())
            return "/var/lib/schpos-licenses/logs.json";

        return Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "SCHPOSAdmin", "logs.json");
    }
}
