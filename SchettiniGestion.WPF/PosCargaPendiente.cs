using System.Collections.Generic;
using SchettiniGestion;

namespace SchettiniGestion.WPF
{
    /// <summary>Carga pendiente del POS (presupuesto convertido a venta).</summary>
    public static class PosCargaPendiente
    {
        public static List<FacturaItem> Items { get; set; }
        public static int ClienteID { get; set; }
        public static int? PresupuestoID { get; set; }

        public static bool HayCarga => Items != null && Items.Count > 0;

        public static void Limpiar()
        {
            Items = null;
            ClienteID = 0;
            PresupuestoID = null;
        }
    }
}
