using System;
using System.Collections.Generic;

namespace AdminLicencias.Models
{
    public enum EstadoLicencia { Activa, PorVencer, Vencida, Revocada, Trial }

    public class Licencia
    {
        public Guid Id { get; set; } = Guid.NewGuid();
        public Guid ClienteId { get; set; }
        public string HWID { get; set; } = "";
        public string LicenseKey { get; set; } = "";
        public DateTime FechaEmision { get; set; } = DateTime.Today;
        public DateTime FechaVencimiento { get; set; }
        public List<string> Modulos { get; set; } = new List<string>();
        /// <summary>Monto cobrado por esta licencia/renovación</summary>
        public decimal MontoVenta { get; set; }
        public string MetodoPago { get; set; } = "Transferencia";
        /// <summary>Versión de SCHPOS que tiene instalada (manual)</summary>
        public string VersionSchpos { get; set; } = "1.0.0";
        public string Observaciones { get; set; } = "";
        public bool EsRenovacion { get; set; }
        /// <summary>ID de la licencia que esta reemplaza (para historial de renovaciones)</summary>
        public Guid? LicenciaAnteriorId { get; set; }

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

        public string ModulosResumen =>
            SchettiniGestion.ModulosCatalog.ObtenerResumenModulos(Modulos);
    }
}
