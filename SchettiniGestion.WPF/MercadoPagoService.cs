using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json;
using System.Data;

namespace SchettiniGestion.WPF
{
    public static class MercadoPagoService
    {
        private static readonly HttpClient client = new HttpClient();

        public static async Task<RespuestaOrdenMP> CrearOrdenQR(decimal total, string tituloVenta)
        {
            try
            {
                // 1. OBTENER CREDENCIALES DE LA BASE DE DATOS
                DataRow config = DatabaseService.GetConfiguracion();

                if (config == null)
                {
                    return new RespuestaOrdenMP { Exito = false, Error = "No se pudo leer la configuración de la base de datos." };
                }

                // Verificamos si existen las columnas (por si la migración falló, aunque el InitDB lo cubre)
                if (!config.Table.Columns.Contains("MPAccessToken"))
                {
                    return new RespuestaOrdenMP { Exito = false, Error = "La base de datos no tiene configurado Mercado Pago. Reinicie el sistema." };
                }

                string accessToken = config["MPAccessToken"].ToString();
                string userId = config["MPUserId"].ToString();
                string posId = config["MPPosId"].ToString();

                // 2. VALIDAR QUE HAYA DATOS CARGADOS
                if (string.IsNullOrEmpty(accessToken) || string.IsNullOrEmpty(userId) || string.IsNullOrEmpty(posId))
                {
                    return new RespuestaOrdenMP
                    {
                        Exito = false,
                        Error = "Faltan configurar las credenciales de Mercado Pago (Token, User ID o POS) en la pantalla de Configuración."
                    };
                }

                // 3. ARMAR LA URL CON LOS DATOS DINÁMICOS
                string url = $"https://api.mercadopago.com/instore/orders/qr/seller/collectors/{userId}/pos/{posId}/qrs";

                // 4. PREPARAR ORDEN
                var orden = new
                {
                    external_reference = "Venta_" + DateTime.Now.Ticks.ToString(),
                    title = tituloVenta,
                    description = "Compra en Casa Schettini",
                    notification_url = "https://www.tu-sitio.com/webhook", // Opcional
                    total_amount = total,
                    items = new[]
                    {
                        new
                        {
                            sku_number = "GEN",
                            category = "GENERAL",
                            title = tituloVenta,
                            description = "Venta General",
                            unit_price = total,
                            quantity = 1,
                            unit_measure = "unit",
                            total_amount = total
                        }
                    },
                    cash_out = new
                    {
                        amount = 0
                    }
                };

                string jsonBody = JsonConvert.SerializeObject(orden);
                var content = new StringContent(jsonBody, Encoding.UTF8, "application/json");

                // 5. HEADERS CON TOKEN DINÁMICO
                client.DefaultRequestHeaders.Clear();
                client.DefaultRequestHeaders.Add("Authorization", $"Bearer {accessToken}");

                // 6. ENVIAR
                HttpResponseMessage response = await client.PostAsync(url, content);
                string responseBody = await response.Content.ReadAsStringAsync();

                if (response.IsSuccessStatusCode)
                {
                    dynamic json = JsonConvert.DeserializeObject(responseBody);
                    return new RespuestaOrdenMP
                    {
                        Exito = true,
                        QRData = json.qr_data
                    };
                }
                else
                {
                    return new RespuestaOrdenMP { Exito = false, Error = "Rechazo de MP: " + response.ReasonPhrase + " // " + responseBody };
                }
            }
            catch (Exception ex)
            {
                return new RespuestaOrdenMP { Exito = false, Error = "Excepción interna: " + ex.Message };
            }
        }
    }

    public class RespuestaOrdenMP
    {
        public bool Exito { get; set; }
        public string QRData { get; set; }
        public string Error { get; set; }
    }
}