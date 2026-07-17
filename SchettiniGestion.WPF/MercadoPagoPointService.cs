using System;
using System.Collections.Generic;
using System.Data;
using System.Globalization;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json;
using SchettiniGestion;

namespace SchettiniGestion.WPF
{
    public class TerminalPointInfo
    {
        public string Id { get; set; }
        public string ExternalPosId { get; set; }
        public string OperatingMode { get; set; }
        public string DisplayName =>
            $"{Id} — {(string.IsNullOrWhiteSpace(ExternalPosId) ? "sin caja" : ExternalPosId)} — {OperatingMode}";
    }

    public class RespuestaOrdenPoint
    {
        public bool Exito { get; set; }
        public string OrdenId { get; set; }
        public string Error { get; set; }
    }

    public class EstadoPagoPoint
    {
        public string Estado { get; set; } = "waiting";
        public string EstadoDetalle { get; set; } = "";
        public string OperacionId { get; set; } = "";
        public string MarcaTarjeta { get; set; } = "";
        public string UltimosDigitos { get; set; } = "";
        public int Cuotas { get; set; } = 1;
    }

    /// <summary>
    /// Integración con Point Smart/Smart 2 mediante Mercado Pago Orders API.
    /// El cobro manual queda fuera de este servicio y sigue disponible como fallback.
    /// </summary>
    public static class MercadoPagoPointService
    {
        private static readonly HttpClient Client = new HttpClient
        {
            Timeout = TimeSpan.FromSeconds(25)
        };

        private static string ObtenerToken()
        {
            DataRow config = DatabaseService.GetConfiguracion();
            return config != null && config.Table.Columns.Contains("MPAccessToken")
                ? config["MPAccessToken"]?.ToString()?.Trim()
                : "";
        }

        private static void PrepararHeaders(string token, bool idempotencia = false)
        {
            Client.DefaultRequestHeaders.Clear();
            Client.DefaultRequestHeaders.Add("Authorization", $"Bearer {token}");
            if (idempotencia)
                Client.DefaultRequestHeaders.Add("X-Idempotency-Key", Guid.NewGuid().ToString());
        }

        public static async Task<List<TerminalPointInfo>> ListarTerminales(string accessToken)
        {
            ServicePointManager.SecurityProtocol = SecurityProtocolType.Tls12;
            var terminales = new List<TerminalPointInfo>();
            if (string.IsNullOrWhiteSpace(accessToken)) return terminales;

            PrepararHeaders(accessToken);
            HttpResponseMessage response = await Client.GetAsync(
                "https://api.mercadopago.com/terminals/v1/list?limit=50&offset=0");
            string body = await response.Content.ReadAsStringAsync();
            if (!response.IsSuccessStatusCode)
                throw new InvalidOperationException($"Mercado Pago rechazó la consulta ({(int)response.StatusCode}): {body}");

            dynamic json = JsonConvert.DeserializeObject(body);
            if (json?.data?.terminals == null) return terminales;

            foreach (var terminal in json.data.terminals)
            {
                terminales.Add(new TerminalPointInfo
                {
                    Id = terminal.id?.ToString() ?? "",
                    ExternalPosId = terminal.external_pos_id?.ToString() ?? "",
                    OperatingMode = terminal.operating_mode?.ToString() ?? "UNDEFINED"
                });
            }
            return terminales;
        }

        public static async Task<bool> ConfigurarModoPdv(string accessToken, string terminalId)
        {
            if (string.IsNullOrWhiteSpace(accessToken) || string.IsNullOrWhiteSpace(terminalId))
                return false;

            PrepararHeaders(accessToken);
            var payload = new
            {
                terminals = new[] { new { id = terminalId, operating_mode = "PDV" } }
            };
            var request = new HttpRequestMessage(new HttpMethod("PATCH"),
                "https://api.mercadopago.com/terminals/v1/setup")
            {
                Content = new StringContent(JsonConvert.SerializeObject(payload), Encoding.UTF8, "application/json")
            };
            HttpResponseMessage response = await Client.SendAsync(request);
            string body = await response.Content.ReadAsStringAsync();
            if (!response.IsSuccessStatusCode)
                throw new InvalidOperationException($"No se pudo activar el modo PDV ({(int)response.StatusCode}): {body}");
            return true;
        }

        public static async Task<RespuestaOrdenPoint> CrearOrden(decimal total, string referencia, string descripcion)
        {
            try
            {
                ServicePointManager.SecurityProtocol = SecurityProtocolType.Tls12;
                DataRow config = DatabaseService.GetConfiguracion();
                if (config == null)
                    return new RespuestaOrdenPoint { Error = "No hay configuración del negocio." };

                string token = ObtenerToken();
                string terminalId = config.Table.Columns.Contains("MPPointTerminalId")
                    ? config["MPPointTerminalId"]?.ToString()?.Trim()
                    : "";
                if (string.IsNullOrWhiteSpace(token) || string.IsNullOrWhiteSpace(terminalId))
                    return new RespuestaOrdenPoint { Error = "Falta configurar el Access Token o la terminal Point." };
                if (total <= 0)
                    return new RespuestaOrdenPoint { Error = "El monto debe ser mayor a cero." };

                string monto = total.ToString("0.00", CultureInfo.InvariantCulture);
                var payload = new
                {
                    type = "point",
                    external_reference = referencia,
                    expiration_time = "PT10M",
                    transactions = new { payments = new[] { new { amount = monto } } },
                    config = new
                    {
                        point = new
                        {
                            terminal_id = terminalId,
                            print_on_terminal = "seller_ticket",
                            ticket_number = referencia.Length > 20 ? referencia.Substring(0, 20) : referencia
                        },
                        payment_method = new { default_type = "credit_card" }
                    },
                    description = descripcion
                };

                PrepararHeaders(token, idempotencia: true);
                var content = new StringContent(JsonConvert.SerializeObject(payload), Encoding.UTF8, "application/json");
                HttpResponseMessage response = await Client.PostAsync("https://api.mercadopago.com/v1/orders", content);
                string body = await response.Content.ReadAsStringAsync();
                if (!response.IsSuccessStatusCode)
                    return new RespuestaOrdenPoint
                    {
                        Error = TraducirError(response.StatusCode, body)
                    };

                dynamic json = JsonConvert.DeserializeObject(body);
                return new RespuestaOrdenPoint
                {
                    Exito = true,
                    OrdenId = json.id?.ToString()
                };
            }
            catch (Exception ex)
            {
                return new RespuestaOrdenPoint { Error = ex.Message };
            }
        }

        public static async Task<EstadoPagoPoint> ConsultarEstado(string ordenId)
        {
            var resultado = new EstadoPagoPoint();
            try
            {
                string token = ObtenerToken();
                if (string.IsNullOrWhiteSpace(token) || string.IsNullOrWhiteSpace(ordenId)) return resultado;

                PrepararHeaders(token);
                HttpResponseMessage response = await Client.GetAsync($"https://api.mercadopago.com/v1/orders/{ordenId}");
                if (!response.IsSuccessStatusCode) return resultado;

                dynamic data = JsonConvert.DeserializeObject(await response.Content.ReadAsStringAsync());
                string estado = data.status?.ToString() ?? "";
                resultado.EstadoDetalle = data.status_detail?.ToString() ?? estado;

                if (estado == "processed") resultado.Estado = "approved";
                else if (estado == "at_terminal" || estado == "action_required") resultado.Estado = "in_process";
                else if (estado == "failed" || estado == "canceled" || estado == "expired") resultado.Estado = "rejected";

                var pagos = data.transactions?.payments;
                if (pagos != null && pagos.Count > 0)
                {
                    var pago = pagos[0];
                    resultado.OperacionId = (pago.reference_id ?? pago.id)?.ToString() ?? "";
                    resultado.MarcaTarjeta = pago.payment_method?.id?.ToString() ?? "";
                    resultado.UltimosDigitos = pago.card?.last_digits?.ToString() ?? "";
                    int cuotas;
                    if (int.TryParse(pago.payment_method?.installments?.ToString(), out cuotas) && cuotas > 0)
                        resultado.Cuotas = cuotas;
                    if (resultado.Estado == "waiting" && pago.status?.ToString() == "at_terminal")
                        resultado.Estado = "in_process";
                }
            }
            catch { }
            return resultado;
        }

        public static async Task CancelarOrden(string ordenId)
        {
            try
            {
                string token = ObtenerToken();
                if (string.IsNullOrWhiteSpace(token) || string.IsNullOrWhiteSpace(ordenId)) return;
                PrepararHeaders(token, idempotencia: true);
                await Client.PostAsync($"https://api.mercadopago.com/v1/orders/{ordenId}/cancel",
                    new StringContent("", Encoding.UTF8, "application/json"));
            }
            catch { }
        }

        private static string TraducirError(HttpStatusCode status, string body)
        {
            if (body != null && body.IndexOf("already_queued_order_for_terminal", StringComparison.OrdinalIgnoreCase) >= 0)
                return "La terminal ya tiene un cobro pendiente. Finalícelo o cancélelo desde el Point.";
            if (body != null && body.IndexOf("forbidden_checking_terminal_owner", StringComparison.OrdinalIgnoreCase) >= 0)
                return "La terminal seleccionada no pertenece a esta cuenta de Mercado Pago.";
            if (status == HttpStatusCode.Unauthorized)
                return "El Access Token de Mercado Pago no es válido.";
            return $"Mercado Pago respondió {(int)status}: {body}";
        }
    }
}
