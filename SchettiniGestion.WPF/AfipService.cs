using System;
using System.Collections.Generic;
using System.Data;
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace SchettiniGestion.WPF
{
    public static class AfipService
    {
        // Usamos HttpClient con un "Handler" que guarda Cookies automáticamente
        private static readonly HttpClientHandler handler = new HttpClientHandler { UseCookies = true, CookieContainer = new CookieContainer() };
        private static readonly HttpClient client = new HttpClient(handler);

        // ==============================================================================
        //  FACTURACIÓN (CAE) - SIMULADO (Sin cambios)
        // ==============================================================================
        public static ResultadoAfip Facturar(int tipoComprobante, int puntoVenta, double importeTotal, long cuitCliente, List<FacturaItem> items)
        {
            return Task.Run(() => FacturarAsync(tipoComprobante, puntoVenta, importeTotal, cuitCliente, items)).Result;
        }

        public static async Task<ResultadoAfip> FacturarAsync(int tipoComprobante, int puntoVenta, double importeTotal, long cuitCliente, List<FacturaItem> items)
        {
            var resultado = new ResultadoAfip();
            try
            {
                await Task.Delay(1000);
                resultado.Exito = true;
                resultado.CAE = "77441122334455";
                resultado.Vencimiento = DateTime.Now.AddDays(10).ToString("yyyyMMdd");
                resultado.NumeroComprobante = new Random().Next(100, 9999);
                return resultado;
            }
            catch (Exception ex)
            {
                resultado.Exito = false;
                resultado.Error = "Error Fiscal: " + ex.Message;
            }
            return resultado;
        }

        // ==============================================================================
        //  CONSULTA DE PADRÓN - VERSIÓN CON COOKIES
        // ==============================================================================

        public static async Task<PersonaAfip> ObtenerDatosPersonaAsync(long cuit)
        {
            try
            {
                // 1. Configuración de Seguridad TLS
                ServicePointManager.SecurityProtocol = SecurityProtocolType.Tls12 | SecurityProtocolType.Tls12;

                // 2. Limpiar cabeceras anteriores
                client.DefaultRequestHeaders.Clear();

                // 3. Establecer cabeceras de "Navegador Real"
                client.DefaultRequestHeaders.Add("User-Agent", "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/120.0.0.0 Safari/537.36");
                client.DefaultRequestHeaders.Add("Accept", "application/json, text/plain, */*");
                client.DefaultRequestHeaders.Add("Accept-Language", "es-ES,es;q=0.9,en;q=0.8");
                client.DefaultRequestHeaders.Add("Referer", "https://soa.afip.gob.ar/sr-padron/v2/");
                client.DefaultRequestHeaders.Add("Origin", "https://soa.afip.gob.ar");
                client.DefaultRequestHeaders.Add("Sec-Fetch-Dest", "empty");
                client.DefaultRequestHeaders.Add("Sec-Fetch-Mode", "cors");
                client.DefaultRequestHeaders.Add("Sec-Fetch-Site", "same-origin");

                // URL Oficial
                string url = $"https://soa.afip.gob.ar/sr-padron/v2/persona/{cuit}";

                // 4. Intentar petición directa (El Handler gestiona las cookies si hay redirección)
                HttpResponseMessage response = await client.GetAsync(url);

                if (response.StatusCode == HttpStatusCode.NotFound)
                {
                    return new PersonaAfip { Exito = false, Error = "CUIT no encontrado o bloqueado por AFIP." };
                }

                response.EnsureSuccessStatusCode();

                string jsonResponse = await response.Content.ReadAsStringAsync();
                JObject data = JObject.Parse(jsonResponse);

                if ((bool)data["success"] == true)
                {
                    JObject datosPersona = (JObject)data["data"];

                    string nombre = datosPersona["nombre"]?.ToString() ?? "";
                    string apellido = datosPersona["apellido"]?.ToString() ?? "";
                    string razonSocial = datosPersona["razonSocial"]?.ToString() ?? "";
                    string tipoPersona = datosPersona["tipoPersona"]?.ToString();
                    string condicionIVA = "Consumidor Final";
                    string direccion = "Domicilio no informado";

                    string nombreFinal = string.IsNullOrEmpty(razonSocial) ? $"{apellido} {nombre}".Trim() : razonSocial;

                    if (datosPersona["domicilioFiscal"] != null)
                    {
                        var dom = datosPersona["domicilioFiscal"];
                        string calle = dom["direccion"]?.ToString();
                        string localidad = dom["localidad"]?.ToString();
                        string provincia = dom["nombreProvincia"]?.ToString();
                        direccion = $"{calle}, {localidad}, {provincia}";
                    }

                    // Lógica IVA
                    if (datosPersona["impuestos"] != null)
                    {
                        foreach (var imp in datosPersona["impuestos"])
                        {
                            int idImpuesto = (int)imp;
                            if (idImpuesto == 30) condicionIVA = "Responsable Inscripto";
                            if (idImpuesto == 32) condicionIVA = "Exento";
                            if (idImpuesto == 20) condicionIVA = "Monotributo";
                        }
                    }
                    if (datosPersona["monotributo"] != null && datosPersona["monotributo"].HasValues)
                    {
                        condicionIVA = "Monotributo";
                    }

                    return new PersonaAfip
                    {
                        Exito = true,
                        RazonSocial = nombreFinal,
                        Domicilio = direccion,
                        CondicionIVA = condicionIVA,
                        TipoPersona = tipoPersona
                    };
                }
                else
                {
                    return new PersonaAfip { Exito = false, Error = "La respuesta de AFIP no contiene datos." };
                }
            }
            catch (Exception ex)
            {
                return new PersonaAfip { Exito = false, Error = "Error de conexión: " + ex.Message };
            }
        }
    }

    public class ResultadoAfip
    {
        public bool Exito { get; set; }
        public string CAE { get; set; }
        public string Vencimiento { get; set; }
        public int NumeroComprobante { get; set; }
        public string Error { get; set; }
    }

    public class PersonaAfip
    {
        public bool Exito { get; set; }
        public string RazonSocial { get; set; }
        public string Domicilio { get; set; }
        public string CondicionIVA { get; set; }
        public string TipoPersona { get; set; }
        public string Error { get; set; }
    }
}