using System;
using System.Runtime.InteropServices;

namespace SchettiniGestion.WPF
{
    /// <summary>Envía bytes crudos a una impresora Windows (comandos ESC/POS, TSPL, ZPL, etc.).</summary>
    internal static class RawPrinterHelper
    {
        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
        private class DOCINFO
        {
            [MarshalAs(UnmanagedType.LPWStr)]
            public string pDocName;
            [MarshalAs(UnmanagedType.LPWStr)]
            public string pOutputFile;
            [MarshalAs(UnmanagedType.LPWStr)]
            public string pDataType;
        }

        [DllImport("winspool.drv", EntryPoint = "OpenPrinterW", SetLastError = true, CharSet = CharSet.Unicode)]
        private static extern bool OpenPrinter(string pPrinterName, out IntPtr phPrinter, IntPtr pDefault);

        [DllImport("winspool.drv", EntryPoint = "ClosePrinter", SetLastError = true)]
        private static extern bool ClosePrinter(IntPtr hPrinter);

        [DllImport("winspool.drv", EntryPoint = "StartDocPrinterW", SetLastError = true, CharSet = CharSet.Unicode)]
        private static extern int StartDocPrinter(IntPtr hPrinter, int level, [In] DOCINFO di);

        [DllImport("winspool.drv", EntryPoint = "EndDocPrinter", SetLastError = true)]
        private static extern bool EndDocPrinter(IntPtr hPrinter);

        [DllImport("winspool.drv", EntryPoint = "StartPagePrinter", SetLastError = true)]
        private static extern bool StartPagePrinter(IntPtr hPrinter);

        [DllImport("winspool.drv", EntryPoint = "EndPagePrinter", SetLastError = true)]
        private static extern bool EndPagePrinter(IntPtr hPrinter);

        [DllImport("winspool.drv", EntryPoint = "WritePrinter", SetLastError = true)]
        private static extern bool WritePrinter(IntPtr hPrinter, IntPtr pBytes, int dwCount, out int dwWritten);

        public static bool EnviarBytes(string nombreImpresora, byte[] datos, string nombreDocumento = "SCHPOS Corte")
        {
            if (string.IsNullOrWhiteSpace(nombreImpresora) || datos == null || datos.Length == 0)
                return false;

            IntPtr hPrinter = IntPtr.Zero;
            IntPtr pUnmanaged = IntPtr.Zero;
            try
            {
                if (!OpenPrinter(nombreImpresora.Trim(), out hPrinter, IntPtr.Zero))
                    return false;

                var di = new DOCINFO
                {
                    pDocName = nombreDocumento,
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
                        pUnmanaged = Marshal.AllocCoTaskMem(datos.Length);
                        Marshal.Copy(datos, 0, pUnmanaged, datos.Length);
                        if (!WritePrinter(hPrinter, pUnmanaged, datos.Length, out int written))
                            return false;
                        return written == datos.Length;
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
            catch
            {
                return false;
            }
            finally
            {
                if (pUnmanaged != IntPtr.Zero)
                    Marshal.FreeCoTaskMem(pUnmanaged);
                if (hPrinter != IntPtr.Zero)
                    ClosePrinter(hPrinter);
            }
        }
    }
}
