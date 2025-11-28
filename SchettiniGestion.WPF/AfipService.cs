using System;
using System.Collections.Generic;
using System.Data;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json;
using System.Windows;

namespace SchettiniGestion.WPF
{
    public static class AfipService
    {
        // Cliente HTTP para futura conexión real con AfipSDK
        private static readonly HttpClient client = new HttpClient();
        private const string URL_API = "https://app.afipsdk.com/api/v1/afip";

        // --- MÉTODO SÍNCRONO (Puente para evitar errores si alguien lo llama sin await) ---
        public static ResultadoAfip Facturar(int tipoComprobante, int puntoVenta, double importeTotal, long cuitCliente, List<FacturaItem> items)
        {
            return Task.Run(() => FacturarAsync(tipoComprobante, puntoVenta, importeTotal, cuitCliente, items)).Result;
        }

        // --- MÉTODO ASÍNCRONO PRINCIPAL ---
        public static async Task<ResultadoAfip> FacturarAsync(int tipoComprobante, int puntoVenta, double importeTotal, long cuitCliente, List<FacturaItem> items)
        {
            var resultado = new ResultadoAfip();

            try
            {
                // 1. Obtener Configuración
                DataRow config = DatabaseService.GetConfiguracion();
                if (config == null) throw new Exception("Falta configuración del negocio en la base de datos.");

                // Validar CUIT del emisor
                string cuitStr = config["CUIT"].ToString().Replace("-", "").Replace(" ", "");
                if (!long.TryParse(cuitStr, out long cuitEmisor)) throw new Exception("El CUIT del negocio es inválido.");

                // ========================================================================
                // MODO SIMULACIÓN: ACTIVADO
                // (Esto te permitirá ver el ticket con CAE aunque no tengas certificado real aún)
                // ========================================================================
                bool modoSimulacion = true;

                if (modoSimulacion)
                {
                    // Simulamos una espera de conexión...
                    await Task.Delay(1500);

                    // ¡Devolvemos éxito falso!
                    resultado.Exito = true;
                    resultado.CAE = "77441122334455"; // CAE DE PRUEBA
                    resultado.Vencimiento = DateTime.Now.AddDays(10).ToString("yyyyMMdd");

                    // Simulamos un número de comprobante aleatorio
                    resultado.NumeroComprobante = new Random().Next(100, 5000);

                    return resultado; // Salimos aquí.
                }
                // ========================================================================

                // AQUÍ IRÍA EL CÓDIGO DE CONEXIÓN REAL CON AFIPSDK
                // (Se activará cuando configures modoSimulacion = false en el futuro)

                throw new Exception("No se ha configurado la conexión real con AFIP.");
            }
            catch (Exception ex)
            {
                resultado.Exito = false;
                resultado.Error = "Error Fiscal: " + ex.Message;
            }

            return resultado;
        }
    }

    // Clase para devolver la respuesta a la pantalla
    public class ResultadoAfip
    {
        public bool Exito { get; set; }
        public string CAE { get; set; }
        public string Vencimiento { get; set; }
        public int NumeroComprobante { get; set; }
        public string Error { get; set; }
    }
}