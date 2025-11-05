using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Text;

namespace LicenseGenerator
{
    // Esta es la MISMA estructura que definimos en el LicenseManager.
    // Es crucial que sean idénticas.
    public class LicenseData
    {
        public string CuitCliente { get; set; }
        public DateTime FechaExpiracion { get; set; }
        public List<string> ModulosPermitidos { get; set; } = new List<string>();
    }

    internal class Program
    {
        static void Main(string[] args)
        {
            // --- ACÁ DEFINÍS LA LICENCIA DEL CLIENTE ---

            Console.WriteLine("Generador de Licencias de SchettiniGestion");
            Console.WriteLine("-----------------------------------------");

            // 1. Definí los datos de la licencia que querés crear
            var datosLicencia = new LicenseData
            {
                CuitCliente = "30-71123456-1",
                FechaExpiracion = new DateTime(2026, 10, 24), // Año, Mes, Día
                ModulosPermitidos = new List<string> { "FACTURACION", "STOCK" } // Un cliente "LITE"
            };

            // 2. Convertimos esos datos a un string JSON
            string jsonLicencia = JsonConvert.SerializeObject(datosLicencia, Formatting.Indented);

            Console.WriteLine("\nDatos de la Licencia a generar:");
            Console.WriteLine(jsonLicencia);

            // 3. "Encriptamos" la licencia
            byte[] bytesLicencia = Encoding.UTF8.GetBytes(jsonLicencia);
            string claveLicenciaGenerada = Convert.ToBase64String(bytesLicencia);

            // 4. Mostramos la clave final
            Console.WriteLine("\n--- CLAVE DE LICENCIA GENERADA ---");
            Console.WriteLine("Copiá y pasale esta clave al cliente:");
            Console.WriteLine("\n" + claveLicenciaGenerada);


            Console.WriteLine("\nPresioná Enter para salir.");
            Console.ReadLine();
        }
    }
}