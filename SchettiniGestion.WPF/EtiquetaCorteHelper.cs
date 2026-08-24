using SchettiniGestion;
using System;
using System.Text;

namespace SchettiniGestion.WPF
{
    /// <summary>
    /// Comandos de auto-corte para impresoras de etiquetas en modo rollo.
    /// Solo aplica si la impresora tiene cuchilla mecánica.
    /// </summary>
    internal static class EtiquetaCorteHelper
    {
        public const string TextoAyuda =
            "Al marcar Auto-corte, SCHPOS envía un comando de corte al terminar el trabajo en modo Rollo " +
            "(igual que muchas térmicas de ticket cortan al final del recibo).\n\n" +
            "Requisitos:\n" +
            "• La impresora debe tener cortador mecánico (cuchilla).\n" +
            "• Solo se usa en modo Rollo (no A4 / cartel / góndola).\n" +
            "• Hay que elegir el protocolo correcto; si no coincide, puede no cortar o imprimir caracteres raros.\n\n" +
            "Protocolos programados en SCHPOS:\n" +
            "• ESC/POS — Epson, Rongta, Xprinter, Gprinter, Bixolon y muchas térmicas chinas con cortador.\n" +
            "• TSPL — TSC y compatibles; también muchas Argox / Godex en modo TSPL.\n" +
            "• ZPL — Zebra (series ZD / ZT / GK, etc.).\n" +
            "• EPL — Zebra / Eltron más antiguas.\n" +
            "• Automático — intenta detectar por el nombre de la impresora en Windows; si no reconoce, usa TSPL.\n\n" +
            "Si no corta: probá otro protocolo o activá el cutter en las propiedades del driver de Windows.";

        public static void IntentarCorteTrasImpresion(string nombreImpresora, OpcionesEtiqueta opciones)
        {
            if (opciones == null || !opciones.AutoCorte) return;
            if (!string.Equals(opciones.ModoImpresion, "Rollo", StringComparison.OrdinalIgnoreCase)) return;
            if (string.IsNullOrWhiteSpace(nombreImpresora)) return;

            string protocolo = ResolverProtocolo(opciones.ProtocoloCorte, nombreImpresora);
            byte[] cmd = ConstruirComandoCorte(protocolo);
            if (cmd == null || cmd.Length == 0) return;

            // El trabajo GDI ya está en cola; un breve delay reduce el riesgo de intercalado.
            try { System.Threading.Thread.Sleep(350); } catch { /* ignore */ }
            RawPrinterHelper.EnviarBytes(nombreImpresora, cmd, "SCHPOS Auto-corte etiquetas");
        }

        public static string ResolverProtocolo(string configurado, string nombreImpresora)
        {
            string p = (configurado ?? "Auto").Trim();
            if (!string.Equals(p, "Auto", StringComparison.OrdinalIgnoreCase))
            {
                if (string.Equals(p, "ESC/POS", StringComparison.OrdinalIgnoreCase)) return "ESCPOS";
                return p.ToUpperInvariant();
            }

            string n = (nombreImpresora ?? "").ToLowerInvariant();
            if (n.Contains("zebra") || n.Contains("zdesigner") || n.Contains(" zd") || n.Contains("zt") || n.Contains("gk420"))
                return "ZPL";
            if (n.Contains("eltron") || n.Contains("lp2824") || n.Contains("tlp2844"))
                return "EPL";
            if (n.Contains("epson") || n.Contains("rongta") || n.Contains("xprinter") || n.Contains("gprinter")
                || n.Contains("bixolon") || n.Contains("pos-") || n.Contains("pos58") || n.Contains("pos80")
                || n.Contains("star ") || n.Contains("tsp"))
                return "ESCPOS";
            if (n.Contains("tsc") || n.Contains("argox") || n.Contains("godex") || n.Contains("honeywell")
                || n.Contains("intermec") || n.Contains("citizen cl"))
                return "TSPL";

            // Muchas impresoras de etiquetas de rollo en el mercado local hablan TSPL.
            return "TSPL";
        }

        public static byte[] ConstruirComandoCorte(string protocolo)
        {
            switch ((protocolo ?? "").ToUpperInvariant())
            {
                case "ESCPOS":
                    // Avance de líneas + corte total (GS V 0)
                    return new byte[] { 0x1B, 0x64, 0x03, 0x1D, 0x56, 0x00 };
                case "TSPL":
                    return Encoding.ASCII.GetBytes("CUT\r\n");
                case "ZPL":
                    return Encoding.ASCII.GetBytes("~JC");
                case "EPL":
                    return Encoding.ASCII.GetBytes("C\n");
                default:
                    return Encoding.ASCII.GetBytes("CUT\r\n");
            }
        }
    }
}
