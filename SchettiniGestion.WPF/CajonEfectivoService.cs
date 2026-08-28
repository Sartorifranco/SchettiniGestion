using System;
using System.Collections.Generic;
using System.Linq;
using SchettiniGestion;

namespace SchettiniGestion.WPF
{
    /// <summary>
    /// Cajón de efectivo vía pulso ESC/POS (ESC p) por la impresora de tickets.
    /// El cajón típico va al RJ11 de la térmica; SCHPOS no habla con el cajón en forma directa.
    /// </summary>
    internal static class CajonEfectivoService
    {
        public static bool EsEfectivo(string nombre) =>
            (nombre ?? "").IndexOf("efectivo", StringComparison.OrdinalIgnoreCase) >= 0;

        public static bool IncluyeEfectivo(IEnumerable<CobranzaItem> cobranzas)
        {
            if (cobranzas == null) return false;
            return cobranzas.Any(c => c != null && c.monto > 0 && EsEfectivo(c.nombreMedio));
        }

        public static void Abrir()
        {
            try
            {
                if (!DatabaseService.GetAbrirCajonEfectivo())
                    return;
                var (ticket, _) = DatabaseService.GetImpresoras();
                TicketRawPrinter.EnviarAperturaCajon(ticket);
            }
            catch
            {
                // Sin impresora o sin cajón: no interrumpir la venta.
            }
        }

        public static void AbrirSiHayEfectivo(IEnumerable<CobranzaItem> cobranzas)
        {
            if (IncluyeEfectivo(cobranzas))
                Abrir();
        }

        /// <summary>Prueba inmediata, aunque el tilde esté desmarcado. Usa la impresora indicada (combo, no la guardada).</summary>
        public static bool Probar(string nombreImpresora)
        {
            return TicketRawPrinter.EnviarAperturaCajon(nombreImpresora);
        }
    }
}
