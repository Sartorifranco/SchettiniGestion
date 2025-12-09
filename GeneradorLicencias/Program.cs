using System;
using System.Collections.Generic;
using System.Text;
using Newtonsoft.Json;

namespace GeneradorLicencias
{
    class Program
    {
        // Esta estructura debe ser IGUAL a la de tu sistema principal
        public class LicenseData
        {
            public string CuitCliente { get; set; }
            public DateTime FechaExpiracion { get; set; }
            public List<string> ModulosPermitidos { get; set; } = new List<string>();
        }

        static void Main(string[] args)
        {
            Console.WriteLine("========================================");
            Console.WriteLine("   GENERADOR DE LICENCIAS - SCHETTINI   ");
            Console.WriteLine("========================================");
            Console.WriteLine("");

            var licencia = new LicenseData();

            // 1. Datos del Cliente
            Console.Write(">> Ingrese CUIT del Cliente (sin guiones): ");
            licencia.CuitCliente = Console.ReadLine();

            Console.Write(">> Días de validez (Ej: 365 para un año): ");
            string diasStr = Console.ReadLine();
            int dias = int.TryParse(diasStr, out int d) ? d : 30; // 30 días por defecto si falla
            licencia.FechaExpiracion = DateTime.Now.AddDays(dias);

            // 2. Selección de Módulos
            licencia.ModulosPermitidos = new List<string>();
            Console.WriteLine("\n--- SELECCIÓN DE MÓDULOS (Responda 's' para Sí) ---");

            // Módulos Principales
            if (Ask("1. ¿Facturación (POS)?")) licencia.ModulosPermitidos.Add("ACCESO_FACTURACION");
            if (Ask("2. ¿Control de Stock?")) licencia.ModulosPermitidos.Add("ACCESO_STOCK");
            if (Ask("3. ¿Caja Diaria?")) licencia.ModulosPermitidos.Add("ACCESO_CAJA");
            if (Ask("4. ¿Clientes?")) licencia.ModulosPermitidos.Add("ACCESO_CLIENTES");

            // Gestión Avanzada
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

            // Reportes y Admin
            if (Ask("8. ¿Reportes y Ventas?")) licencia.ModulosPermitidos.Add("ACCESO_VENTAS");
            if (Ask("9. ¿Presupuestos?")) licencia.ModulosPermitidos.Add("ACCESO_PRESUPUESTOS");

            // Admin siempre va incluido para que puedan configurar usuarios
            licencia.ModulosPermitidos.Add("ACCESO_USUARIOS");
            licencia.ModulosPermitidos.Add("ACCESO_PERMISOS");

            // 3. Generar Clave
            string json = JsonConvert.SerializeObject(licencia);
            byte[] bytes = Encoding.UTF8.GetBytes(json);
            string claveFinal = Convert.ToBase64String(bytes);

            Console.WriteLine("\n================================================");
            Console.WriteLine(" CLAVE GENERADA (Copiar y enviar al cliente):");
            Console.WriteLine("================================================\n");
            Console.WriteLine(claveFinal);
            Console.WriteLine("\n================================================");
            Console.WriteLine("Presione ENTER para salir...");
            Console.ReadLine();
        }

        static bool Ask(string preg)
        {
            Console.Write(preg + " [s/n]: ");
            string resp = Console.ReadLine();
            return resp != null && resp.ToLower().StartsWith("s");
        }
    }
}