using Newtonsoft.Json;
using SchettiniGestion;
using System;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;
using System.Text;

namespace LicenseGenerator
{
    // ─────────────────────────────────────────────────────────────────────────
    //  GENERADOR DE LICENCIAS — SchettiniGestion
    //  Encriptación: AES-256, IV = 16 bytes cero, clave padded a 32 bytes.
    //  DEBE ser idéntico a GeneradorLicencias/Program.cs y a LicenseManager.cs.
    // ─────────────────────────────────────────────────────────────────────────
    public class LicenseData
    {
        public string CuitCliente { get; set; }
        public DateTime FechaExpiracion { get; set; }
        public string HardwareID { get; set; }
        public List<string> ModulosPermitidos { get; set; } = new List<string>();
    }

    internal class Program
    {
        private const string SECRET_KEY = "Soctech_Sistemas_Seguridad_2025!";

        static void Main(string[] args)
        {
            Console.Title = "Generador de licencias — SchettiniGestion";
            Console.ForegroundColor = ConsoleColor.Cyan;
            Console.WriteLine("========================================");
            Console.WriteLine("   GENERADOR DE LICENCIAS SCHETTINI     ");
            Console.WriteLine("========================================");
            Console.ResetColor();
            Console.WriteLine();

            var licencia = new LicenseData();

            // 1. CUIT / Nombre del cliente
            Console.Write("CUIT o nombre del cliente: ");
            licencia.CuitCliente = Console.ReadLine()?.Trim() ?? "CLIENTE-TEST";

            // 2. Hardware ID del cliente
            Console.ForegroundColor = ConsoleColor.Yellow;
            Console.WriteLine();
            Console.WriteLine("IMPORTANTE: Pídale al cliente que abra el sistema.");
            Console.WriteLine("En la pantalla de activación aparece el HARDWARE ID de su equipo.");
            Console.ResetColor();
            Console.Write("Hardware ID del cliente (copiar y pegar): ");
            licencia.HardwareID = Console.ReadLine()?.Trim() ?? "";

            // 3. Días de validez
            Console.Write("\nDías de validez (ej. 365): ");
            string diasStr = Console.ReadLine();
            int dias = int.TryParse(diasStr, out int d) ? d : 365;
            licencia.FechaExpiracion = DateTime.Now.Date.AddDays(dias);

            // 4. Módulos (desde ModulosCatalog.json)
            var seleccionados = new List<string>();
            Console.WriteLine();
            Console.Write("¿Aplicar paquete SCHPOS Lite (base) como punto de partida? [S/n]: ");
            if (Preguntar("", defaultSi: true))
            {
                foreach (var codigo in ModulosCatalog.ObtenerPresetLite())
                {
                    if (!seleccionados.Exists(c => string.Equals(c, codigo, StringComparison.OrdinalIgnoreCase)))
                        seleccionados.Add(codigo);
                }
            }

            PreguntarGrupo("PAQUETE LITE (BASE)", ModulosCatalog.GrupoLiteBase, seleccionados);
            PreguntarGrupo("MÓDULOS ADICIONALES", ModulosCatalog.GrupoModuloAdicional, seleccionados);
            PreguntarGrupo("EXTRAS — PAGO ÚNICO", ModulosCatalog.GrupoExtraUnico, seleccionados);
            PreguntarGrupo("ABONOS MENSUALES", ModulosCatalog.GrupoAbonoMensual, seleccionados);

            licencia.ModulosPermitidos = ModulosCatalog.ResolverLicencia(seleccionados);

            // 5. Generar y encriptar
            try
            {
                string json = JsonConvert.SerializeObject(licencia);
                string clave = Encriptar(json);

                Console.WriteLine();
                Console.ForegroundColor = ConsoleColor.Green;
                Console.WriteLine("======== CLAVE PARA EL CLIENTE (copiar y enviar) ========");
                Console.ResetColor();
                Console.WriteLine(clave);
                Console.WriteLine();
                Console.WriteLine($"Vence: {licencia.FechaExpiracion:dd/MM/yyyy}");
                Console.WriteLine($"Hardware ID vinculado: {(string.IsNullOrEmpty(licencia.HardwareID) ? "(ninguno)" : licencia.HardwareID)}");
            }
            catch (Exception ex)
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine("Error al generar la licencia: " + ex.Message);
                Console.ResetColor();
            }

            Console.WriteLine();
            Console.WriteLine("Presione Enter para salir.");
            Console.ReadLine();
        }

        static bool Preguntar(string texto, bool defaultSi = false)
        {
            if (!string.IsNullOrWhiteSpace(texto))
                Console.Write(texto + " [s/N]: ");
            else
                Console.Write("[s/N]: ");

            string r = Console.ReadLine();
            if (string.IsNullOrWhiteSpace(r))
                return defaultSi;
            return r.Trim().Equals("s", StringComparison.OrdinalIgnoreCase);
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
                if (Preguntar($"{i}. {etiqueta}"))
                {
                    if (!seleccionados.Exists(c => string.Equals(c, mod.Codigo, StringComparison.OrdinalIgnoreCase)))
                        seleccionados.Add(mod.Codigo);
                }
                i++;
            }
        }

        /// <summary>
        /// AES-256 CBC con IV = 16 bytes cero y clave padded a 32 bytes.
        /// IDÉNTICO al algoritmo de GeneradorLicencias y LicenseManager.
        /// </summary>
        public static string Encriptar(string plainText)
        {
            byte[] iv = new byte[16];
            using (Aes aes = Aes.Create())
            {
                byte[] keyBytes = new byte[32];
                byte[] secretBytes = Encoding.UTF8.GetBytes(SECRET_KEY);
                Array.Copy(secretBytes, keyBytes, Math.Min(keyBytes.Length, secretBytes.Length));
                aes.Key = keyBytes;
                aes.IV = iv;

                ICryptoTransform encryptor = aes.CreateEncryptor(aes.Key, aes.IV);
                using (MemoryStream ms = new MemoryStream())
                {
                    using (CryptoStream cs = new CryptoStream(ms, encryptor, CryptoStreamMode.Write))
                    using (StreamWriter sw = new StreamWriter(cs, Encoding.UTF8))
                        sw.Write(plainText);

                    return Convert.ToBase64String(ms.ToArray());
                }
            }
        }
    }
}
