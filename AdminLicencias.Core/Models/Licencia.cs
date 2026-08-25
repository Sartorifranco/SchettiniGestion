using AdminLicencias.Core.Catalog;
using Newtonsoft.Json;

namespace AdminLicencias.Core.Models;

public enum EstadoLicencia { Activa, PorVencer, Vencida, Revocada, Trial }

public class Licencia
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid ClienteId { get; set; }
    public string HWID { get; set; } = "";
    public string LicenseKey { get; set; } = "";
    public DateTime FechaEmision { get; set; } = DateTime.Today;
    public DateTime FechaVencimiento { get; set; }
    public List<string> Modulos { get; set; } = new();

    /// <summary>Cobro único por instalación / licencia del sistema.</summary>
    [JsonProperty("MontoLicencia")]
    public decimal MontoLicencia { get; set; }

    /// <summary>Compatibilidad con datos.json antiguos (MontoVenta).</summary>
    [JsonProperty("MontoVenta")]
    private decimal MontoVentaLegacy
    {
        set
        {
            if (MontoLicencia == 0)
                MontoLicencia = value;
        }
    }

    /// <summary>Abono mensual según módulos contratados.</summary>
    public decimal AbonoMensual { get; set; }

    public string VersionSchpos { get; set; } = "2.4.0";
    public string Observaciones { get; set; } = "";
    public bool EsRenovacion { get; set; }
    public Guid? LicenciaAnteriorId { get; set; }
    public string Plan { get; set; } = "lite";

    /// <summary>Marca administrativa de revoke (además de blacklist por huella).</summary>
    public bool Revocada { get; set; }

    public DateTime? FechaRevocacion { get; set; }

    /// <summary>SHA-256 hex de la LicenseKey al emitir/revocar.</summary>
    public string HuellaClave { get; set; } = "";

    [JsonIgnore]
    public EstadoLicencia Estado
    {
        get
        {
            if (Revocada) return EstadoLicencia.Revocada;
            if (FechaVencimiento < DateTime.Today) return EstadoLicencia.Vencida;
            if ((FechaVencimiento - DateTime.Today).TotalDays <= 30) return EstadoLicencia.PorVencer;
            return EstadoLicencia.Activa;
        }
    }

    [JsonIgnore]
    public int DiasRestantes => (int)(FechaVencimiento - DateTime.Today).TotalDays;

    [JsonIgnore]
    public string ModulosResumen => ModulosCatalog.ObtenerResumenModulos(Modulos);
}
