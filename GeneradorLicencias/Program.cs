using System;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using Newtonsoft.Json;

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

            // 3. Selección de Módulos
            licencia.ModulosPermitidos = new List<string>();
            Console.WriteLine("\n--- SELECCIÓN DE MÓDULOS (Responda 's' para Sí) ---");

            if (Ask("1. ¿Facturación (POS)?")) licencia.ModulosPermitidos.Add("ACCESO_FACTURACION");
            if (Ask("2. ¿Control de Stock y Productos?"))
            {
                licencia.ModulosPermitidos.Add("ACCESO_STOCK");
                licencia.ModulosPermitidos.Add("ACCESO_PRODUCTOS");
            }
            if (Ask("3. ¿Caja Diaria?")) licencia.ModulosPermitidos.Add("ACCESO_CAJA");
            if (Ask("4. ¿Clientes?")) licencia.ModulosPermitidos.Add("ACCESO_CLIENTES");
            if (Ask("5. ¿Proveedores y Compras?"))
            {
                licencia.ModulosPermitidos.Add("ACCESO_PROVEEDORES");
                licencia.ModulosPermitidos.Add("ACCESO_COMPRAS");
            }
            if (Ask("6. ¿Cuentas Corrientes?")) licencia.ModulosPermitidos.Add("ACCESO_CUENTASCORRIENTES");
            if (Ask("7. ¿Precios y Listas?"))
            {
                licencia.ModulosPermitidos.Add("ACCESO_PRECIOS");
                licencia.ModulosPermitidos.Add("ACCESO_LISTASPRECIOS");
            }
            if (Ask("8. ¿Reportes y Ventas?")) licencia.ModulosPermitidos.Add("ACCESO_VENTAS");
            if (Ask("9. ¿Presupuestos?")) licencia.ModulosPermitidos.Add("ACCESO_PRESUPUESTOS");

            licencia.ModulosPermitidos.Add("ACCESO_USUARIOS");
            licencia.ModulosPermitidos.Add("ACCESO_PERMISOS");
            licencia.ModulosPermitidos.Add("ACCESO_CONFIGURACION");

            if (licencia.ModulosPermitidos.Contains("ACCESO_FACTURACION") && !licencia.ModulosPermitidos.Contains("ACCESO_PRODUCTOS"))
            {
                licencia.ModulosPermitidos.Add("ACCESO_PRODUCTOS");
            }

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

        static bool Ask(string preg)
        {
            Console.Write(preg + " [s/n]: ");
            string resp = Console.ReadLine();
            return resp != null && resp.ToLower().StartsWith("s");
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