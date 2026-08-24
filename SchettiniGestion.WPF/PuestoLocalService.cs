using System;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;

namespace SchettiniGestion.WPF
{
    /// <summary>
    /// Nombre de esta caja (CAJA-01, MOSTRADOR, SERVIDOR). Vive en ProgramData,
    /// no en la base compartida: cada PC elige el suyo.
    /// </summary>
    internal static class PuestoLocalService
    {
        public static string RutaPuestoCfg => Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData),
            "SCHPOS", "puesto.cfg");

        public static string IdPuesto =>
            SanitizeId(Environment.MachineName);

        public static string Nombre
        {
            get
            {
                string n = Leer();
                if (!string.IsNullOrWhiteSpace(n)) return n;
                n = NombrePorDefecto();
                Guardar(n);
                return n;
            }
        }

        public static string NombrePorDefecto()
        {
            return SqlServerNetworkSetup.EsModoCliente() ? "CAJA-01" : "SERVIDOR";
        }

        public static string Leer()
        {
            try
            {
                if (!File.Exists(RutaPuestoCfg)) return "";
                string n = (File.ReadAllText(RutaPuestoCfg, Encoding.UTF8) ?? "").Trim();
                return SanitizeNombre(n);
            }
            catch
            {
                return "";
            }
        }

        public static void Guardar(string nombre)
        {
            string n = SanitizeNombre(nombre);
            if (string.IsNullOrWhiteSpace(n))
                n = NombrePorDefecto();
            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(RutaPuestoCfg));
                File.WriteAllText(RutaPuestoCfg, n, new UTF8Encoding(false));
            }
            catch { }
        }

        public static string SanitizeNombre(string nombre)
        {
            if (string.IsNullOrWhiteSpace(nombre)) return "";
            string n = nombre.Trim();
            n = Regex.Replace(n, @"\s+", " ");
            n = Regex.Replace(n, @"[^\w\s\-áéíóúÁÉÍÓÚñÑüÜ]", "", RegexOptions.CultureInvariant);
            if (n.Length > 40) n = n.Substring(0, 40).Trim();
            return n;
        }

        private static string SanitizeId(string machine)
        {
            if (string.IsNullOrWhiteSpace(machine)) return "PC";
            string n = Regex.Replace(machine.Trim(), @"[^\w\-]", "-", RegexOptions.CultureInvariant);
            if (n.Length > 80) n = n.Substring(0, 80);
            return string.IsNullOrWhiteSpace(n) ? "PC" : n;
        }
    }
}
