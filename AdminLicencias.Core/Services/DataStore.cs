using AdminLicencias.Core.Models;
using AdminLicencias.Core.Options;
using Newtonsoft.Json;
using System.Text;

namespace AdminLicencias.Core.Services;

public sealed class DataStore
{
    private readonly string _dataPath;
    private readonly string _configPath;
    private StoreData _data = new();

    public DataStore(LicensingOptions options)
    {
        _configPath = ResolverRutaConfig(options);
        _dataPath = ResolverRutaDatos(options);
    }

    public string RutaActual => _dataPath;

    public IReadOnlyList<Cliente> Clientes => _data.Clientes;
    public IReadOnlyList<Licencia> Licencias => _data.Licencias;

    public void Cargar()
    {
        try
        {
            if (!File.Exists(_dataPath))
            {
                _data = new StoreData();
                return;
            }

            string json = File.ReadAllText(_dataPath, Encoding.UTF8);
            _data = JsonConvert.DeserializeObject<StoreData>(json) ?? new StoreData();
        }
        catch
        {
            _data = new StoreData();
        }
    }

    public void Guardar()
    {
        string? dir = Path.GetDirectoryName(_dataPath);
        if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
            Directory.CreateDirectory(dir);

        string json = JsonConvert.SerializeObject(_data, Formatting.Indented);
        File.WriteAllText(_dataPath, json, Encoding.UTF8);
    }

    public void GuardarCliente(Cliente cliente)
    {
        int idx = _data.Clientes.FindIndex(x => x.Id == cliente.Id);
        if (idx >= 0) _data.Clientes[idx] = cliente;
        else _data.Clientes.Add(cliente);
        Guardar();
    }

    public Cliente? ObtenerCliente(Guid id) =>
        _data.Clientes.FirstOrDefault(c => c.Id == id);

    public Cliente? BuscarClientePorCuit(string cuit)
    {
        if (string.IsNullOrWhiteSpace(cuit)) return null;
        return _data.Clientes.FirstOrDefault(c =>
            string.Equals(c.CUIT?.Trim(), cuit.Trim(), StringComparison.OrdinalIgnoreCase));
    }

    public void GuardarLicencia(Licencia licencia)
    {
        int idx = _data.Licencias.FindIndex(x => x.Id == licencia.Id);
        if (idx >= 0) _data.Licencias[idx] = licencia;
        else _data.Licencias.Add(licencia);
        Guardar();
    }

    public Licencia? UltimaLicencia(Guid clienteId) =>
        _data.Licencias
            .Where(l => l.ClienteId == clienteId)
            .OrderByDescending(l => l.FechaEmision)
            .FirstOrDefault();

    public IEnumerable<HistorialLicenciaDto> ObtenerHistorial()
    {
        return _data.Licencias
            .OrderByDescending(l => l.FechaEmision)
            .Select(l =>
            {
                var c = _data.Clientes.FirstOrDefault(x => x.Id == l.ClienteId);
                return new HistorialLicenciaDto
                {
                    LicenciaId = l.Id,
                    ClienteId = l.ClienteId,
                    RazonSocial = c?.RazonSocial ?? "(sin cliente)",
                    CUIT = c?.CUIT ?? "",
                    HWID = l.HWID,
                    Plan = l.Plan,
                    FechaEmision = l.FechaEmision,
                    FechaVencimiento = l.FechaVencimiento,
                    DiasRestantes = l.DiasRestantes,
                    Estado = l.Estado.ToString(),
                    ModulosResumen = l.ModulosResumen,
                    MontoVenta = l.MontoVenta,
                    MetodoPago = l.MetodoPago,
                    VersionSchpos = l.VersionSchpos,
                    EsRenovacion = l.EsRenovacion,
                    Observaciones = l.Observaciones
                };
            });
    }

    private static string ResolverRutaDatos(LicensingOptions options)
    {
        if (!string.IsNullOrWhiteSpace(options.DataFilePath))
            return options.DataFilePath.Trim();

        string env = Environment.GetEnvironmentVariable("SCHPOS_LICENSE_DATA_PATH") ?? "";
        if (!string.IsNullOrWhiteSpace(env))
            return env.Trim();

        // Linux server default
        if (!OperatingSystem.IsWindows())
            return "/var/lib/schpos-licenses/datos.json";

        return Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "SCHPOSAdmin", "datos.json");
    }

    private static string ResolverRutaConfig(LicensingOptions options)
    {
        if (!string.IsNullOrWhiteSpace(options.ConfigFilePath))
            return options.ConfigFilePath.Trim();

        if (!OperatingSystem.IsWindows())
            return "/var/lib/schpos-licenses/config.txt";

        return Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "SCHPOSAdmin", "config.txt");
    }

    private sealed class StoreData
    {
        public List<Cliente> Clientes { get; set; } = new();
        public List<Licencia> Licencias { get; set; } = new();
    }
}

public sealed class HistorialLicenciaDto
{
    public Guid LicenciaId { get; set; }
    public Guid ClienteId { get; set; }
    public string RazonSocial { get; set; } = "";
    public string CUIT { get; set; } = "";
    public string HWID { get; set; } = "";
    public string Plan { get; set; } = "";
    public DateTime FechaEmision { get; set; }
    public DateTime FechaVencimiento { get; set; }
    public int DiasRestantes { get; set; }
    public string Estado { get; set; } = "";
    public string ModulosResumen { get; set; } = "";
    public decimal MontoVenta { get; set; }
    public string MetodoPago { get; set; } = "";
    public string VersionSchpos { get; set; } = "";
    public bool EsRenovacion { get; set; }
    public string Observaciones { get; set; } = "";
}
