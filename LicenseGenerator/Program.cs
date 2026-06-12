using Newtonsoft.Json;
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

            // 4. Módulos
            licencia.ModulosPermitidos = new List<string>();
            Console.WriteLine();
            Console.WriteLine("Módulos (s = incluir, Enter vacío = No):");
            Console.WriteLine();

            if (Preguntar("1. Facturación / POS"))         licencia.ModulosPermitidos.Add("ACCESO_FACTURACION");
            if (Preguntar("2. Productos"))                 licencia.ModulosPermitidos.Add("ACCESO_PRODUCTOS");
            if (Preguntar("3. Stock"))                     licencia.ModulosPermitidos.Add("ACCESO_STOCK");
            if (Preguntar("4. Ventas (historial)"))        licencia.ModulosPermitidos.Add("ACCESO_VENTAS");
            if (Preguntar("5. Clientes"))                  licencia.ModulosPermitidos.Add("ACCESO_CLIENTES");
            if (Preguntar("6. Proveedores"))               licencia.ModulosPermitidos.Add("ACCESO_PROVEEDORES");
            if (Preguntar("7. Compras"))                   licencia.ModulosPermitidos.Add("ACCESO_COMPRAS");
            if (Preguntar("8. Caja / Tesorería"))          licencia.ModulosPermitidos.Add("ACCESO_CAJA");
            if (Preguntar("9. Presupuestos"))              licencia.ModulosPermitidos.Add("ACCESO_PRESUPUESTOS");
            if (Preguntar("10. Precios"))                  licencia.ModulosPermitidos.Add("ACCESO_PRECIOS");
            if (Preguntar("11. Listas de precios"))        licencia.ModulosPermitidos.Add("ACCESO_LISTASPRECIOS");
            if (Preguntar("12. Cuentas corrientes"))       licencia.ModulosPermitidos.Add("ACCESO_CUENTASCORRIENTES");

            // Módulos implícitos (siempre incluidos)
            licencia.ModulosPermitidos.Add("ACCESO_USUARIOS");
            licencia.ModulosPermitidos.Add("ACCESO_PERMISOS");

            // Si tiene Facturación, aseguramos que también tiene Productos
            if (licencia.ModulosPermitidos.Contains("ACCESO_FACTURACION")
                && !licencia.ModulosPermitidos.Contains("ACCESO_PRODUCTOS"))
                licencia.ModulosPermitidos.Add("ACCESO_PRODUCTOS");

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

        static bool Preguntar(string texto)
        {
            Console.Write(texto + " [s/N]: ");
            string r = Console.ReadLine();
            return r != null && r.Trim().Equals("s", StringComparison.OrdinalIgnoreCase);
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
