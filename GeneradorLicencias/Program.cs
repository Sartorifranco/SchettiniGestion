using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using Newtonsoft.Json;
using SchettiniGestion;

namespace GeneradorLicencias
{
    class Program
    {
        private static readonly string SECRET_KEY = "Soctech_Sistemas_Seguridad_2025!";

        public class LicenseData
        {
            public string CuitCliente { get; set; }
            public DateTime FechaExpiracion { get; set; }
            public string HardwareID { get; set; } // <--- NUEVO CAMPO IMPORTANTE
            public List<string> ModulosPermitidos { get; set; } = new List<string>();
        }

        static void Main(string[] args)
        {
            if (args != null && args.Length > 0 && args.Any(a => a.Equals("--auto", StringComparison.OrdinalIgnoreCase)))
            {
                GenerarAutomatica(args);
                return;
            }

            Console.Title = "Generador de Licencias Soctech";
            Console.ForegroundColor = ConsoleColor.Cyan;
            Console.WriteLine("========================================");
            Console.WriteLine("    GENERADOR DE LICENCIAS - SCHETTINI    ");
            Console.WriteLine("========================================");
            Console.ResetColor();
            Console.WriteLine("");

            var licencia = new LicenseData();

            // 1. Datos del Cliente
            Console.Write(">> Ingrese CUIT/Nombre del Cliente: ");
            licencia.CuitCliente = Console.ReadLine();

            // 2. ID de Hardware (Anti-Copia)
            Console.ForegroundColor = ConsoleColor.Yellow;
            Console.WriteLine("\nIMPORTANTE: Pídale al cliente que abra el sistema.");
            Console.WriteLine("Si no tiene licencia, le aparecerá un CODIGO DE HARDWARE.");
            Console.ResetColor();
            Console.Write(">> Ingrese el ID DE HARDWARE del Cliente (Copiar y Pegar): ");
            licencia.HardwareID = Console.ReadLine()?.Trim() ?? ""; // Quitamos espacios por si acaso

            Console.Write("\n>> Días de validez (Ej: 365): ");
            string diasStr = Console.ReadLine();
            int dias = int.TryParse(diasStr, out int d) ? d : 30;
            licencia.FechaExpiracion = DateTime.Now.AddDays(dias);

            // 4. Módulos (desde ModulosCatalog.json)
            var seleccionados = new List<string>();
            Console.WriteLine();
            Console.ForegroundColor = ConsoleColor.Green;
            Console.Write("¿Aplicar paquete SCHPOS Lite (base) como punto de partida? [S/n]: ");
            Console.ResetColor();
            if (Ask("", defaultSi: true))
            {
                foreach (var codigo in ModulosCatalog.ObtenerPresetLite())
                {
                    if (!seleccionados.Contains(codigo, StringComparer.OrdinalIgnoreCase))
                        seleccionados.Add(codigo);
                }
            }

            PreguntarGrupo("PAQUETE LITE (BASE)", ModulosCatalog.GrupoLiteBase, seleccionados);
            PreguntarGrupo("MÓDULOS ADICIONALES", ModulosCatalog.GrupoModuloAdicional, seleccionados);
            PreguntarGrupo("EXTRAS — PAGO ÚNICO", ModulosCatalog.GrupoExtraUnico, seleccionados);
            PreguntarGrupo("ABONOS MENSUALES", ModulosCatalog.GrupoAbonoMensual, seleccionados);

            licencia.ModulosPermitidos = ModulosCatalog.ResolverLicencia(seleccionados);

            // 4. Generar y Encriptar
            try
            {
                string json = JsonConvert.SerializeObject(licencia);
                string claveFinal = Encriptar(json);

                Console.ForegroundColor = ConsoleColor.Green;
                Console.WriteLine("\n================================================");
                Console.WriteLine(" CLAVE GENERADA (Copiar y enviar al cliente):");
                Console.WriteLine("================================================\n");
                Console.ResetColor();
                Console.WriteLine(claveFinal);
                Console.WriteLine("\n================================================");
                Console.WriteLine($"Vence el: {licencia.FechaExpiracion:dd/MM/yyyy}");
                Console.WriteLine($"Hardware ID vinculado: {licencia.HardwareID}");
            }
            catch (Exception ex)
            {
                Console.WriteLine("Error al generar: " + ex.Message);
            }

            Console.WriteLine("Presione ENTER para salir...");
            Console.ReadLine();
        }

        /// <summary>
        /// Modo no interactivo para generar licencias de prueba/soporte por script.
        /// Uso:
        ///   GeneradorLicencias.exe --auto [--cuit "TEXTO"] [--hardware "ID" | --hardware-local]
        ///                            [--dias N] [--modulos "TODOS"|"COD1,COD2,..."] [--out "ruta.key"]
        /// Sin --hardware ni --hardware-local, la licencia queda sin atar a un equipo (HardwareID vacío).
        /// </summary>
        static void GenerarAutomatica(string[] args)
        {
            string ObtenerValor(string nombre, string porDefecto = "")
            {
                for (int i = 0; i < args.Length - 1; i++)
                    if (args[i].Equals(nombre, StringComparison.OrdinalIgnoreCase))
                        return args[i + 1];
                return porDefecto;
            }

            var licencia = new LicenseData
            {
                CuitCliente = ObtenerValor("--cuit", "PRUEBA-INTERNA")
            };

            if (args.Any(a => a.Equals("--hardware-local", StringComparison.OrdinalIgnoreCase)))
                licencia.HardwareID = ObtenerHardwareIdLocal();
            else
                licencia.HardwareID = ObtenerValor("--hardware", "");

            int dias = int.TryParse(ObtenerValor("--dias", "90"), out int d) ? d : 90;
            licencia.FechaExpiracion = DateTime.Now.AddDays(dias);

            string modulosArg = ObtenerValor("--modulos", "TODOS");
            List<string> seleccionados = modulosArg.Equals("TODOS", StringComparison.OrdinalIgnoreCase)
                ? ModulosCatalog.ObtenerTodosCodigos()
                : modulosArg.Split(',').Select(s => s.Trim()).Where(s => s.Length > 0).ToList();

            licencia.ModulosPermitidos = ModulosCatalog.ResolverLicencia(seleccionados);

            string json = JsonConvert.SerializeObject(licencia);
            string claveFinal = Encriptar(json);

            Console.WriteLine(claveFinal);

            string outPath = ObtenerValor("--out", "");
            if (!string.IsNullOrWhiteSpace(outPath))
            {
                File.WriteAllText(outPath, claveFinal);
                Console.Error.WriteLine($"[OK] Licencia guardada en: {outPath}");
            }

            Console.Error.WriteLine($"[INFO] CUIT/Cliente: {licencia.CuitCliente}");
            Console.Error.WriteLine($"[INFO] Vence: {licencia.FechaExpiracion:dd/MM/yyyy}");
            Console.Error.WriteLine($"[INFO] HardwareID: {(string.IsNullOrWhiteSpace(licencia.HardwareID) ? "(sin atar a equipo)" : licencia.HardwareID)}");
            Console.Error.WriteLine($"[INFO] Módulos: {ModulosCatalog.ObtenerResumenModulos(licencia.ModulosPermitidos)}");
        }

        /// <summary>
        /// Réplica exacta del algoritmo de SchettiniGestion.LicenseManager.ObtenerHardwareId(),
        /// para poder calcular el Hardware ID de la PC donde corre este generador sin
        /// depender de LicenseManager.cs (que no se compila en este proyecto net8.0).
        /// </summary>
        static string ObtenerHardwareIdLocal()
        {
            string cpuId = "";
            string boardSerial = "";

            try
            {
                using (var mc = new System.Management.ManagementClass("Win32_Processor"))
                using (var instances = mc.GetInstances())
                    foreach (System.Management.ManagementObject obj in instances)
                    {
                        cpuId = obj["ProcessorId"]?.ToString()?.Trim() ?? "";
                        break;
                    }
            }
            catch { }

            try
            {
                using (var mc = new System.Management.ManagementClass("Win32_BaseBoard"))
                using (var instances = mc.GetInstances())
                    foreach (System.Management.ManagementObject obj in instances)
                    {
                        boardSerial = obj["SerialNumber"]?.ToString()?.Trim() ?? "";
                        break;
                    }
            }
            catch { }

            try
            {
                string raw = $"{cpuId}|{boardSerial}";
                using (var sha = SHA256.Create())
                {
                    byte[] hash = sha.ComputeHash(Encoding.UTF8.GetBytes(raw));
                    return BitConverter.ToString(hash, 0, 8).Replace("-", "").ToUpperInvariant();
                }
            }
            catch
            {
                return Environment.MachineName.ToUpperInvariant();
            }
        }

        static bool Ask(string preg, bool defaultSi = false)
        {
            if (!string.IsNullOrWhiteSpace(preg))
                Console.Write(preg + " [s/n]: ");
            else
                Console.Write("[s/n]: ");

            string resp = Console.ReadLine();
            if (string.IsNullOrWhiteSpace(resp))
                return defaultSi;
            return resp.Trim().ToLowerInvariant().StartsWith("s");
        }

        static void PreguntarGrupo(string titulo, string grupo, List<string> seleccionados)
        {
            var mods = ModulosCatalog.ObtenerPorGrupo(grupo);
            if (mods.Count == 0) return;

            Console.WriteLine();
            Console.ForegroundColor = ConsoleColor.Cyan;
            Console.WriteLine("--- " + titulo + " ---");
            Console.ResetColor();

            int i = 1;
            foreach (var mod in mods)
            {
                string etiqueta = mod.EsAbonoMensual ? mod.Nombre + " (abono)" : mod.Nombre;
                if (Ask($"{i}. ¿{etiqueta}?"))
                {
                    if (!seleccionados.Contains(mod.Codigo, StringComparer.OrdinalIgnoreCase))
                        seleccionados.Add(mod.Codigo);
                }
                i++;
            }
        }

        public static string Encriptar(string plainText)
        {
            byte[] iv = new byte[16];
            byte[] array;
            using (Aes aes = Aes.Create())
            {
                byte[] keyBytes = new byte[32];
                byte[] secretBytes = Encoding.UTF8.GetBytes(SECRET_KEY);
                Array.Copy(secretBytes, keyBytes, Math.Min(keyBytes.Length, secretBytes.Length));
                aes.Key = keyBytes;
                aes.IV = iv;
                ICryptoTransform encryptor = aes.CreateEncryptor(aes.Key, aes.IV);
                using (MemoryStream memoryStream = new MemoryStream())
                {
                    using (CryptoStream cryptoStream = new CryptoStream((Stream)memoryStream, encryptor, CryptoStreamMode.Write))
                    {
                        using (StreamWriter streamWriter = new StreamWriter((Stream)cryptoStream))
                        {
                            streamWriter.Write(plainText);
                        }
                        array = memoryStream.ToArray();
                    }
                }
            }
            return Convert.ToBase64String(array);
        }
    }
}