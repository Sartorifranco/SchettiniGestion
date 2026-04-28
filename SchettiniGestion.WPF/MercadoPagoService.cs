using System;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json;
using System.Data;
using System.Windows;
using System.Collections.Generic;

namespace SchettiniGestion.WPF
{
    public static class MercadoPagoService
    {
        private static readonly HttpClient client = new HttpClient();

        // Estructura para devolver Estado + ID al mismo tiempo
        public class InfoPagoMP
        {
            public string Estado { get; set; } = "waiting";
            public string IdOperacion { get; set; } = "";
        }

        // -------------------------------------------------------------------------
        // 1. GENERAR COBRO (QR) - Sin Cambios
        // -------------------------------------------------------------------------
        public static async Task<RespuestaOrdenMP> CrearOrdenQR(decimal total, string tituloVenta, string externalReference)
        {
            try
            {
                ServicePointManager.SecurityProtocol = SecurityProtocolType.Tls12;
                DataRow config = DatabaseService.GetConfiguracion();
                if (config == null) return new RespuestaOrdenMP { Exito = false, Error = "No hay configuración." };

                if (!config.Table.Columns.Contains("MPAccessToken"))
                    return new RespuestaOrdenMP { Exito = false, Error = "Base de datos desactualizada." };

                string accessToken = config["MPAccessToken"].ToString();
                string userId = config["MPUserId"].ToString();
                string posId = config["MPPosId"].ToString();

                if (string.IsNullOrEmpty(accessToken) || string.IsNullOrEmpty(userId) || string.IsNullOrEmpty(posId))
                    return new RespuestaOrdenMP { Exito = false, Error = "Faltan credenciales de MP." };

                string url = $"https://api.mercadopago.com/instore/orders/qr/seller/collectors/{userId}/pos/{posId}/qrs";

                var orden = new
                {
                    external_reference = externalReference,
                    title = tituloVenta,
                    description = "Venta POS",
                    notification_url = "https://www.google.com",
                    total_amount = total,
                    items = new[] {
                        new {
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
                    cash_out = new { amount = 0 }
                };

                string jsonBody = JsonConvert.SerializeObject(orden);
                var content = new StringContent(jsonBody, Encoding.UTF8, "application/json");

                client.DefaultRequestHeaders.Clear();
                client.DefaultRequestHeaders.Add("Authorization", $"Bearer {accessToken}");

                HttpResponseMessage response = await client.PostAsync(url, content);
                string responseBody = await response.Content.ReadAsStringAsync();

                if (response.IsSuccessStatusCode)
                {
                    dynamic json = JsonConvert.DeserializeObject(responseBody);
                    return new RespuestaOrdenMP { Exito = true, QRData = json.qr_data };
                }
                else
                {
                    return new RespuestaOrdenMP { Exito = false, Error = $"Error MP: {response.StatusCode} - {responseBody}" };
                }
            }
            catch (Exception ex)
            {
                return new RespuestaOrdenMP { Exito = false, Error = "Excepción: " + ex.Message };
            }
        }

        // -------------------------------------------------------------------------
        // 2. VERIFICAR PAGO (MODIFICADO PARA TRAER EL ID)
        // -------------------------------------------------------------------------
        public static async Task<InfoPagoMP> VerificarEstadoPago(string externalReference)
        {
            var resultado = new InfoPagoMP();
            try
            {
                DataRow config = DatabaseService.GetConfiguracion();
                if (config == null) return resultado;
                string accessToken = config["MPAccessToken"].ToString();

                string url = $"https://api.mercadopago.com/merchant_orders/search?external_reference={externalReference}";

                client.DefaultRequestHeaders.Clear();
                client.DefaultRequestHeaders.Add("Authorization", $"Bearer {accessToken}");

                HttpResponseMessage response = await client.GetAsync(url);

                if (response.IsSuccessStatusCode)
                {
                    string json = await response.Content.ReadAsStringAsync();
                    dynamic data = JsonConvert.DeserializeObject(json);

                    if (data.elements != null && data.elements.Count > 0)
                    {
                        var orden = data.elements[0];

                        if (orden.payments != null)
                        {
                            foreach (var p in orden.payments)
                            {
                                string status = p.status.ToString();
                                string id = p.id.ToString(); // ¡AQUÍ CAPTURAMOS EL ID!

                                if (status == "approved")
                                {
                                    resultado.Estado = "approved";
                                    resultado.IdOperacion = id;
                                    return resultado;
                                }
                                if (status == "in_process" || status == "pending") resultado.Estado = "in_process";
                                if (status == "rejected") resultado.Estado = "rejected";
                            }
                        }
                    }
                }
                return resultado;
            }
            catch { return resultado; }
        }

        // -------------------------------------------------------------------------
        // 3. OBTENER USER ID
        // -------------------------------------------------------------------------
        public static async Task<string> ObtenerUserId(string accessToken)
        {
            try
            {
                ServicePointManager.SecurityProtocol = SecurityProtocolType.Tls12;
                using (var client = new HttpClient())
                {
                    client.DefaultRequestHeaders.Add("Authorization", $"Bearer {accessToken}");
                    string url = "https://api.mercadopago.com/users/me";
                    var response = await client.GetAsync(url);
                    string jsonStr = await response.Content.ReadAsStringAsync();

                    if (response.IsSuccessStatusCode)
                    {
                        dynamic data = JsonConvert.DeserializeObject(jsonStr);
                        return data.id.ToString();
                    }
                }
            }
            catch { }
            return null;
        }

        // -------------------------------------------------------------------------
        // 4. DESCUBRIR CAJAS
        // -------------------------------------------------------------------------
        public static async Task DescubrirCajas(string accessToken)
        {
            try
            {
                ServicePointManager.SecurityProtocol = SecurityProtocolType.Tls12;
                using (var client = new HttpClient())
                {
                    client.DefaultRequestHeaders.Add("Authorization", $"Bearer {accessToken}");
                    string url = "https://api.mercadopago.com/pos";
                    var response = await client.GetAsync(url);
                    string jsonStr = await response.Content.ReadAsStringAsync();

                    if (response.IsSuccessStatusCode)
                    {
                        dynamic data = JsonConvert.DeserializeObject(jsonStr);
                        string reporte = "SUS CAJAS ENCONTRADAS:\n\n";
                        var lista = data.results ?? data;

                        if (lista != null)
                        {
                            foreach (var caja in lista)
                            {
                                string nombre = caja.name;
                                string idExterno = caja.external_id;
                                reporte += $"📂 {nombre}\n👉 ID: {idExterno}\n------------------\n";
                            }
                            MessageBox.Show(reporte, "Cajas Disponibles", MessageBoxButton.OK, MessageBoxImage.Information);
                        }
                    }
                    else
                    {
                        MessageBox.Show("Error MP: " + jsonStr);
                    }
                }
            }
            catch (Exception ex) { MessageBox.Show("Error: " + ex.Message); }
        }

        // -------------------------------------------------------------------------
        // 5. CREAR CAJA AUTOMÁTICA
        // -------------------------------------------------------------------------
        public static async Task<string> CrearCajaPorDefecto(string accessToken)
        {
            try
            {
                ServicePointManager.SecurityProtocol = SecurityProtocolType.Tls12;
                using (var client = new HttpClient())
                {
                    client.DefaultRequestHeaders.Add("Authorization", $"Bearer {accessToken}");

                    string urlPos = "https://api.mercadopago.com/pos";
                    var responsePos = await client.GetAsync(urlPos);
                    string jsonPos = await responsePos.Content.ReadAsStringAsync();
                    string storeId = "";

                    if (responsePos.IsSuccessStatusCode)
                    {
                        dynamic data = JsonConvert.DeserializeObject(jsonPos);
                        var lista = data.results ?? data;
                        if (lista != null && lista.Count > 0) storeId = lista[0].store_id;
                    }

                    if (string.IsNullOrEmpty(storeId))
                    {
                        // Lógica de fallback para buscar store_id si no hay cajas
                        string userUrl = "https://api.mercadopago.com/users/me";
                        var userResp = await client.GetAsync(userUrl);
                        dynamic userData = JsonConvert.DeserializeObject(await userResp.Content.ReadAsStringAsync());
                        string userId = userData.id.ToString();

                        string urlStores = $"https://api.mercadopago.com/stores/search?user_id={userId}";
                        var respStores = await client.GetAsync(urlStores);
                        dynamic dataStores = JsonConvert.DeserializeObject(await respStores.Content.ReadAsStringAsync());

                        if (dataStores.results != null && dataStores.results.Count > 0)
                            storeId = dataStores.results[0].id;
                        else
                            return "ERROR: No tienes 'Sucursales' creadas. Crea una en el panel web.";
                    }

                    string urlCreate = "https://api.mercadopago.com/pos";
                    var nuevaCaja = new
                    {
                        name = "Caja SchTec Principal",
                        fixed_amount = true,
                        store_id = Convert.ToInt64(storeId),
                        external_id = "SCH01"
                    };

                    string jsonBody = JsonConvert.SerializeObject(nuevaCaja);
                    var content = new StringContent(jsonBody, Encoding.UTF8, "application/json");

                    var responseCreate = await client.PostAsync(urlCreate, content);
                    string respStr = await responseCreate.Content.ReadAsStringAsync();

                    if (responseCreate.IsSuccessStatusCode) return "OK:SCH01";
                    else
                    {
                        if (respStr.Contains("already exists")) return "OK:SCH01";
                        return "ERROR AL CREAR: " + respStr;
                    }
                }
            }
            catch (Exception ex)
            {
                return "EXCEPCION: " + ex.Message;
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
