using AdminLicencias.Models;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;
using System.Net.Sockets;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;

namespace AdminLicencias.Services
{
    public static class LicenseService
    {
        private const string SECRET_KEY = "Soctech_Sistemas_Seguridad_2025!";

        public static string GenerarClave(string cuitCliente, string hwid,
            DateTime fechaVencimiento, List<string> modulos)
        {
            var payload = new
            {
                CuitCliente     = cuitCliente,
                FechaExpiracion = fechaVencimiento,
                HardwareID      = hwid,
                ModulosPermitidos = modulos
            };
            string json = JsonConvert.SerializeObject(payload);
            return Encriptar(json);
        }

        private static string Encriptar(string plainText)
        {
            byte[] iv = new byte[16];
            using (var aes = Aes.Create())
            {
                byte[] keyBytes = new byte[32];
                byte[] secretBytes = Encoding.UTF8.GetBytes(SECRET_KEY);
                Array.Copy(secretBytes, keyBytes, Math.Min(keyBytes.Length, secretBytes.Length));
                aes.Key = keyBytes;
                aes.IV  = iv;

                var encryptor = aes.CreateEncryptor(aes.Key, aes.IV);
                using (var ms = new MemoryStream())
                {
                    using (var cs = new CryptoStream(ms, encryptor, CryptoStreamMode.Write))
                    using (var sw = new StreamWriter(cs, Encoding.UTF8))
                        sw.Write(plainText);
                    return Convert.ToBase64String(ms.ToArray());
                }
            }
        }

        /// <summary>
        /// Prueba conexión TCP al servidor SQL del cliente.
        /// Retorna (ok, mensaje).
        /// </summary>
        public static async Task<(bool ok, string mensaje)> TestConexionAsync(string ip, int puerto)
        {
            if (string.IsNullOrWhiteSpace(ip))
                return (false, "IP no configurada para este cliente.");

            try
            {
                using (var tcp = new TcpClient())
                {
                    var cts = new System.Threading.CancellationTokenSource(3000);
                    await tcp.ConnectAsync(ip, puerto);
                    return (true, $"✅ Puerto {puerto} abierto en {ip} — servidor accesible.");
                }
            }
            catch (SocketException)
            {
                return (false, $"❌ No se pudo conectar a {ip}:{puerto} — servidor apagado, puerto cerrado o IP incorrecta.");
            }
            catch (Exception ex)
            {
                return (false, $"❌ Error: {ex.Message}");
            }
        }

        /// <summary>Exporta todas las licencias a CSV.</summary>
        public static void ExportarCSV(string ruta, IEnumerable<(Cliente c, Licencia l)> filas)
        {
            var sb = new StringBuilder();
            sb.AppendLine("Razón Social;CUIT;Ciudad;Versión;Fecha Emisión;Fecha Vencimiento;Días Restantes;Estado;Módulos;Monto;Método Pago;HWID;Notas");

            foreach (var (c, l) in filas)
            {
                if (l == null) continue;
                sb.AppendLine(string.Join(";",
                    Csv(c.RazonSocial), Csv(c.CUIT), Csv(c.Ciudad),
                    Csv(l.VersionSchpos),
                    l.FechaEmision.ToString("dd/MM/yyyy"),
                    l.FechaVencimiento.ToString("dd/MM/yyyy"),
                    l.DiasRestantes.ToString(),
                    l.Estado.ToString(),
                    Csv(l.ModulosResumen),
                    l.MontoVenta.ToString("F2"),
                    Csv(l.MetodoPago),
                    Csv(l.HWID),
                    Csv(l.Observaciones)));
            }

            File.WriteAllText(ruta, sb.ToString(), Encoding.UTF8);
        }

        private static string Csv(string v) => $"\"{(v ?? "").Replace("\"", "\"\"")}\"";
    }
}
