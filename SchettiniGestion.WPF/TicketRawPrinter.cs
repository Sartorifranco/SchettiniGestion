using System;
using System.Runtime.InteropServices;

namespace SchettiniGestion.WPF
{
    /// <summary>
    /// Envía corte ESC/POS en crudo a la impresora térmica.
    /// El ticket se dibuja por GDI; el autocorte no lo hace Windows, lo tiene que mandar el programa
    /// (o el driver, si está marcado “cortar al finalizar”).
    /// </summary>
    internal static class TicketRawPrinter
    {
        /// <summary>
        /// Pulso de cajón ESC/POS (ESC p). Pin 2 y pin 5: el cajón suele estar en uno solo.
        /// </summary>
        public static bool EnviarAperturaCajon(string nombreImpresora)
        {
            if (string.IsNullOrWhiteSpace(nombreImpresora) || EsImpresoraDeArchivo(nombreImpresora))
                return false;

            // ESC p m t1 t2 — t1/t2 en unidades de 2 ms. 0x19/0xFA ≈ 50 ms on / 500 ms off.
            byte[] pulso =
            {
                0x1B, 0x70, 0x00, 0x19, 0xFA,
                0x1B, 0x70, 0x01, 0x19, 0xFA
            };

            try
            {
                return EnviarRaw(nombreImpresora, pulso, "SCHPOS cajon");
            }
            catch
            {
                return false;
            }
        }

        public static void EnviarCorte(string nombreImpresora)
        {
            if (string.IsNullOrWhiteSpace(nombreImpresora) || EsImpresoraDeArchivo(nombreImpresora))
                return;

            // Avance + corte completo (Epson GS V) + ESC i (Star / clones).
            byte[] corte =
            {
                0x0A, 0x0A, 0x0A, 0x0A, 0x0A,
                0x1B, 0x64, 0x04,
                0x1D, 0x56, 0x00,
                0x1D, 0x56, 0x41, 0x00,
                0x1B, 0x69
            };

            try
            {
                EnviarRaw(nombreImpresora, corte, "SCHPOS corte ticket");
            }
            catch
            {
                // Si el driver no acepta RAW, el corte queda a cargo de la impresora.
            }
        }

        private static bool EsImpresoraDeArchivo(string nombre)
        {
            string n = nombre.ToUpperInvariant();
            return n.Contains("PDF") || n.Contains("XPS") || n.Contains("ONENOTE")
                || n.Contains("FAX") || n.Contains("MICROSOFT PRINT")
                || n.Contains("SNAGIT") || n.Contains("ONENOTE");
        }

        private static bool EnviarRaw(string impresora, byte[] datos, string documento)
        {
            IntPtr hPrinter = IntPtr.Zero;
            if (!OpenPrinter(impresora, out hPrinter, IntPtr.Zero) || hPrinter == IntPtr.Zero)
                return false;

            try
            {
                var di = new DOCINFOA
                {
                    pDocName = documento,
                    pDataType = "RAW"
                };
                if (StartDocPrinter(hPrinter, 1, di) == 0)
                    return false;
                try
                {
                    if (!StartPagePrinter(hPrinter))
                        return false;
                    try
                    {
                        IntPtr unmanaged = Marshal.AllocHGlobal(datos.Length);
                        try
                        {
                            Marshal.Copy(datos, 0, unmanaged, datos.Length);
                            int written;
                            WritePrinter(hPrinter, unmanaged, datos.Length, out written);
                            return written > 0;
                        }
                        finally
                        {
                            Marshal.FreeHGlobal(unmanaged);
                        }
                    }
                    finally
                    {
                        EndPagePrinter(hPrinter);
                    }
                }
                finally
                {
                    EndDocPrinter(hPrinter);
                }
            }
            finally
            {
                ClosePrinter(hPrinter);
            }
        }

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
        private class DOCINFOA
        {
            [MarshalAs(UnmanagedType.LPWStr)]
            public string pDocName;
            [MarshalAs(UnmanagedType.LPWStr)]
            public string pOutputFile;
            [MarshalAs(UnmanagedType.LPWStr)]
            public string pDataType;
        }

        [DllImport("winspool.Drv", EntryPoint = "OpenPrinterW", SetLastError = true, CharSet = CharSet.Unicode)]
        private static extern bool OpenPrinter(string pPrinterName, out IntPtr phPrinter, IntPtr pDefault);

        [DllImport("winspool.Drv", EntryPoint = "ClosePrinter", SetLastError = true)]
        private static extern bool ClosePrinter(IntPtr hPrinter);

        [DllImport("winspool.Drv", EntryPoint = "StartDocPrinterW", SetLastError = true, CharSet = CharSet.Unicode)]
        private static extern int StartDocPrinter(IntPtr hPrinter, int level, [In] DOCINFOA di);

        [DllImport("winspool.Drv", EntryPoint = "EndDocPrinter", SetLastError = true)]
        private static extern bool EndDocPrinter(IntPtr hPrinter);

        [DllImport("winspool.Drv", EntryPoint = "StartPagePrinter", SetLastError = true)]
        private static extern bool StartPagePrinter(IntPtr hPrinter);

        [DllImport("winspool.Drv", EntryPoint = "EndPagePrinter", SetLastError = true)]
        private static extern bool EndPagePrinter(IntPtr hPrinter);

        [DllImport("winspool.Drv", EntryPoint = "WritePrinter", SetLastError = true)]
        private static extern bool WritePrinter(IntPtr hPrinter, IntPtr pBytes, int dwCount, out int dwWritten);
    }
}
