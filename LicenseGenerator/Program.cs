using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Text;

namespace LicenseGenerator
{
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
            Console.Title = "Generador de licencias — SchettiniGestion";
            Console.ForegroundColor = ConsoleColor.Cyan;
            Console.WriteLine("========================================");
            Console.WriteLine("   GENERADOR DE LICENCIAS SCHETTINI     ");
            Console.WriteLine("========================================");
            Console.ResetColor();
            Console.WriteLine();

            var licencia = new LicenseData();

            Console.Write("CUIT o nombre del cliente: ");
            licencia.CuitCliente = Console.ReadLine()?.Trim() ?? "CLIENTE-TEST";

            Console.Write("Días de validez (ej. 90 para testing): ");
            string diasStr = Console.ReadLine();
            int dias = int.TryParse(diasStr, out int d) ? d : 90;
            licencia.FechaExpiracion = DateTime.Now.Date.AddDays(dias);

            licencia.ModulosPermitidos = new List<string>();
            Console.WriteLine();
            Console.WriteLine("Módulos (s = incluir). Enter vacío en cada pregunta = No.");
            Console.WriteLine();

            if (Preguntar("Facturación / POS")) licencia.ModulosPermitidos.Add("ACCESO_FACTURACION");
            if (Preguntar("Productos")) licencia.ModulosPermitidos.Add("ACCESO_PRODUCTOS");
            if (Preguntar("Stock")) licencia.ModulosPermitidos.Add("ACCESO_STOCK");
            if (Preguntar("Ventas")) licencia.ModulosPermitidos.Add("ACCESO_VENTAS");
            if (Preguntar("Clientes")) licencia.ModulosPermitidos.Add("ACCESO_CLIENTES");
            if (Preguntar("Proveedores")) licencia.ModulosPermitidos.Add("ACCESO_PROVEEDORES");
            if (Preguntar("Compras")) licencia.ModulosPermitidos.Add("ACCESO_COMPRAS");
            if (Preguntar("Caja / Tesorería")) licencia.ModulosPermitidos.Add("ACCESO_CAJA");
            if (Preguntar("Presupuestos")) licencia.ModulosPermitidos.Add("ACCESO_PRESUPUESTOS");
            if (Preguntar("Precios")) licencia.ModulosPermitidos.Add("ACCESO_PRECIOS");
            if (Preguntar("Listas de precios")) licencia.ModulosPermitidos.Add("ACCESO_LISTASPRECIOS");
            if (Preguntar("Cuentas corrientes")) licencia.ModulosPermitidos.Add("ACCESO_CUENTASCORRIENTES");

            licencia.ModulosPermitidos.Add("ACCESO_USUARIOS");
            licencia.ModulosPermitidos.Add("ACCESO_PERMISOS");

            if (licencia.ModulosPermitidos.Contains("ACCESO_FACTURACION")
                && !licencia.ModulosPermitidos.Contains("ACCESO_PRODUCTOS"))
            {
                licencia.ModulosPermitidos.Add("ACCESO_PRODUCTOS");
            }

            string json = JsonConvert.SerializeObject(licencia, Formatting.Indented);
            string clave = Convert.ToBase64String(Encoding.UTF8.GetBytes(json));

            Console.WriteLine();
            Console.ForegroundColor = ConsoleColor.Green;
            Console.WriteLine("======== CLAVE PARA EL CLIENTE (copiar y enviar) ========");
            Console.ResetColor();
            Console.WriteLine(clave);
            Console.WriteLine();
            Console.WriteLine("Vence: " + licencia.FechaExpiracion.ToString("dd/MM/yyyy"));
            Console.WriteLine();
            Console.WriteLine("El tester puede:");
            Console.WriteLine("  1) Pegar esta clave en la pantalla de activación, o");
            Console.WriteLine("  2) Guardarla en un archivo licencia.key junto al .exe");
            Console.WriteLine();
            Console.WriteLine("JSON interno:");
            Console.WriteLine(json);
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
    }
}
