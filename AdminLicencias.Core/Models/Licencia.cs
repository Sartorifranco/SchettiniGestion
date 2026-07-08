using AdminLicencias.Core.Catalog;

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
    public decimal MontoVenta { get; set; }
    public string MetodoPago { get; set; } = "Transferencia";
    public string VersionSchpos { get; set; } = "2.0.0";
    public string Observaciones { get; set; } = "";
    public bool EsRenovacion { get; set; }
    public Guid? LicenciaAnteriorId { get; set; }
    public string Plan { get; set; } = "lite";

    public EstadoLicencia Estado
    {
        get
        {
            if (FechaVencimiento < DateTime.Today) return EstadoLicencia.Vencida;
            if ((FechaVencimiento - DateTime.Today).TotalDays <= 30) return EstadoLicencia.PorVencer;
            return EstadoLicencia.Activa;
        }
    }

    public int DiasRestantes => (int)(FechaVencimiento - DateTime.Today).TotalDays;

    public string ModulosResumen => ModulosCatalog.ObtenerResumenModulos(Modulos);
}
