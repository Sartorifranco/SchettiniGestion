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

    public void EliminarCliente(Guid id)
    {
        _data.Clientes.RemoveAll(x => x.Id == id);
        _data.Licencias.RemoveAll(x => x.ClienteId == id);
        Guardar();
    }

    public bool RevocarLicencia(Guid licenciaId)
    {
        var lic = _data.Licencias.FirstOrDefault(x => x.Id == licenciaId);
        if (lic == null) return false;

        lic.FechaVencimiento = DateTime.Today.AddDays(-1);
        Guardar();
        return true;
    }

    public ClienteDetalleDto? ObtenerClienteDetalle(Guid id)
    {
        var c = ObtenerCliente(id);
        return c == null ? null : MapClienteDetalle(c);
    }

    public ClienteDetalleDto GuardarClienteCompleto(Cliente cliente)
    {
        GuardarCliente(cliente);
        return MapClienteDetalle(cliente);
    }

    private static ClienteDetalleDto MapClienteDetalle(Cliente c) => new()
    {
        Id = c.Id,
        RazonSocial = c.RazonSocial,
        CUIT = c.CUIT,
        Contacto = c.Contacto,
        Email = c.Email,
        Telefono = c.Telefono,
        Ciudad = c.Ciudad,
        Provincia = c.Provincia,
        IPServidor = c.IPServidor,
        PuertoServidor = c.PuertoServidor,
        CantidadPuestos = c.CantidadPuestos,
        CanalContacto = c.CanalContacto,
        Notas = c.Notas,
        FechaAlta = c.FechaAlta,
        Activo = c.Activo
    };

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
                    MontoLicencia = l.MontoLicencia,
                    AbonoMensual = l.AbonoMensual,
                    VersionSchpos = l.VersionSchpos,
                    EsRenovacion = l.EsRenovacion,
                    Observaciones = l.Observaciones
                };
            });
    }

    public IEnumerable<ClienteResumenDto> ObtenerClientesResumen()
    {
        return _data.Clientes
            .Where(c => c.Activo)
            .OrderBy(c => c.RazonSocial)
            .Select(c =>
            {
                var lic = UltimaLicencia(c.Id);
                return new ClienteResumenDto
                {
                    Id = c.Id,
                    RazonSocial = c.RazonSocial,
                    CUIT = c.CUIT,
                    Ciudad = c.Ciudad,
                    Contacto = c.Contacto,
                    Telefono = c.Telefono,
                    Email = c.Email,
                    IPServidor = c.IPServidor,
                    PuertoServidor = c.PuertoServidor,
                    UltimoHwid = lic?.HWID ?? "",
                    UltimoVencimiento = lic?.FechaVencimiento,
                    UltimoEstado = lic?.Estado.ToString() ?? "Sin licencia",
                    DiasRestantes = lic?.DiasRestantes
                };
            });
    }

    public DashboardStatsDto ObtenerDashboardStats()
    {
        var proximos = _data.Clientes
            .Where(c => c.Activo)
            .Select(c =>
            {
                var lic = UltimaLicencia(c.Id);
                return new ClienteProximoVencerDto
                {
                    RazonSocial = c.RazonSocial,
                    CUIT = c.CUIT,
                    Ciudad = c.Ciudad,
                    Vencimiento = lic?.FechaVencimiento,
                    DiasRestantes = lic?.DiasRestantes,
                    Estado = lic?.Estado.ToString() ?? "Vencida"
                };
            })
            .Where(x => x.DiasRestantes is null or <= 30)
            .OrderBy(x => x.DiasRestantes ?? int.MinValue)
            .ToList();

        return new DashboardStatsDto
        {
            ClientesActivos = _data.Clientes.Count(c =>
                c.Activo && UltimaLicencia(c.Id)?.Estado == EstadoLicencia.Activa),
            ClientesPorVencer = _data.Clientes.Count(c =>
                c.Activo && UltimaLicencia(c.Id)?.Estado == EstadoLicencia.PorVencer),
            ClientesVencidos = _data.Clientes.Count(c =>
            {
                var lic = UltimaLicencia(c.Id);
                return lic == null || lic.Estado == EstadoLicencia.Vencida;
            }),
            TotalClientes = _data.Clientes.Count,
            IngresosInstalacionesTotal = _data.Licencias.Sum(l => l.MontoLicencia),
            IngresosInstalacionesMes = _data.Licencias
                .Where(l => l.FechaEmision.Year == DateTime.Today.Year &&
                            l.FechaEmision.Month == DateTime.Today.Month)
                .Sum(l => l.MontoLicencia),
            IngresosInstalacionesAnio = _data.Licencias
                .Where(l => l.FechaEmision.Year == DateTime.Today.Year)
                .Sum(l => l.MontoLicencia),
            IngresoRecurrenteAbonos = _data.Clientes
                .Where(c => c.Activo)
                .Select(c => UltimaLicencia(c.Id))
                .Where(l => l != null && l.Estado != EstadoLicencia.Vencida)
                .Sum(l => l!.AbonoMensual),
            ProximosAVencer = proximos
        };
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
    public decimal MontoLicencia { get; set; }
    public decimal AbonoMensual { get; set; }
    public string VersionSchpos { get; set; } = "";
    public bool EsRenovacion { get; set; }
    public string Observaciones { get; set; } = "";
}

public sealed class ClienteResumenDto
{
    public Guid Id { get; set; }
    public string RazonSocial { get; set; } = "";
    public string CUIT { get; set; } = "";
    public string Ciudad { get; set; } = "";
    public string Contacto { get; set; } = "";
    public string Telefono { get; set; } = "";
    public string Email { get; set; } = "";
    public string IPServidor { get; set; } = "";
    public int PuertoServidor { get; set; } = 1433;
    public string UltimoHwid { get; set; } = "";
    public DateTime? UltimoVencimiento { get; set; }
    public string UltimoEstado { get; set; } = "";
    public int? DiasRestantes { get; set; }
}

public sealed class DashboardStatsDto
{
    public int ClientesActivos { get; set; }
    public int ClientesPorVencer { get; set; }
    public int ClientesVencidos { get; set; }
    public int TotalClientes { get; set; }
    public decimal IngresosInstalacionesTotal { get; set; }
    public decimal IngresosInstalacionesMes { get; set; }
    public decimal IngresosInstalacionesAnio { get; set; }
    /// <summary>Suma de abonos mensuales de clientes activos con licencia vigente.</summary>
    public decimal IngresoRecurrenteAbonos { get; set; }
    public List<ClienteProximoVencerDto> ProximosAVencer { get; set; } = new();
}

public sealed class ClienteProximoVencerDto
{
    public string RazonSocial { get; set; } = "";
    public string CUIT { get; set; } = "";
    public string Ciudad { get; set; } = "";
    public DateTime? Vencimiento { get; set; }
    public int? DiasRestantes { get; set; }
    public string Estado { get; set; } = "";
}

public sealed class ClienteDetalleDto
{
    public Guid Id { get; set; }
    public string RazonSocial { get; set; } = "";
    public string CUIT { get; set; } = "";
    public string Contacto { get; set; } = "";
    public string Email { get; set; } = "";
    public string Telefono { get; set; } = "";
    public string Ciudad { get; set; } = "";
    public string Provincia { get; set; } = "";
    public string IPServidor { get; set; } = "";
    public int PuertoServidor { get; set; } = 1433;
    public int CantidadPuestos { get; set; } = 1;
    public string CanalContacto { get; set; } = "WhatsApp";
    public string Notas { get; set; } = "";
    public DateTime FechaAlta { get; set; }
    public bool Activo { get; set; } = true;
}
