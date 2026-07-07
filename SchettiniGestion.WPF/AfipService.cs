using System;
using System.Collections.Generic;
using System.Data;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Security.Cryptography.Pkcs;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Xml;
using System.Xml.Linq;
using SchettiniGestion;

namespace SchettiniGestion.WPF
{
    public static class AfipService
    {
        private const string ARCHIVO_TOKEN = "afip_token.xml";
        private const string ARCHIVO_TOKEN_PADRON = "afip_token_padron.xml";

        // URLs
        private const string URL_WSAA_HOMO = "https://wsaahomo.afip.gov.ar/ws/services/LoginCms";
        private const string URL_WSAA_PROD = "https://wsaa.afip.gov.ar/ws/services/LoginCms";
        private const string URL_WSFE_HOMO = "https://wswhomo.afip.gov.ar/wsfev1/service.asmx";
        private const string URL_WSFE_PROD = "https://servicios1.afip.gov.ar/wsfev1/service.asmx";
        private const string URL_PADRON_HOMO = "https://awshomo.afip.gov.ar/sr-padron/webservices/personaServiceA4";
        private const string URL_PADRON_PROD = "https://aws.afip.gov.ar/sr-padron/webservices/personaServiceA4";

        public static async Task<ResultadoAfip> FacturarAsync(int tipoComprobante, int puntoVenta, double importeTotal, long cuitCliente, List<FacturaItem> items, string condicionIvaCliente = null)
        {
            var resultado = new ResultadoAfip();

            try
            {
                bool prod = DatabaseService.GetAfipAmbienteProduccion();
                DataRow config = DatabaseService.GetConfiguracion();
                string cuitRaw = config["CUIT"]?.ToString().Replace("-", "").Trim() ?? "";
                if (string.IsNullOrEmpty(cuitRaw) || !long.TryParse(cuitRaw, out long cuitEmpresa))
                {
                    resultado.Error = "CUIT de la empresa no configurado o inválido. Vaya a Configuración > Negocio y AFIP.";
                    return resultado;
                }
                string rutaCert = config["CertificadoPath"]?.ToString() ?? "";
                string passCert = config["PasswordAfip"]?.ToString() ?? "";

                // 1. LOGIN
                LoginTicket ticket;
                try
                {
                    ticket = await ObtenerTicketAcceso(rutaCert, passCert, prod);
                }
                catch
                {
                    if (File.Exists(ARCHIVO_TOKEN)) File.Delete(ARCHIVO_TOKEN);
                    ticket = await ObtenerTicketAcceso(rutaCert, passCert, prod);
                }

                // 2. ├ÜLTIMO COMPROBANTE
                int nroComprobante = await ObtenerUltimoComprobante(ticket, cuitEmpresa, puntoVenta, tipoComprobante, prod) + 1;

                // 3. DATOS VENTA (neto / IVA según alícuota por ítem)
                double neto = 0, iva = 0;
                var lineas = items ?? new List<FacturaItem>();
                if (tipoComprobante == 11)
                {
                    neto = importeTotal;
                    iva = 0;
                }
                else
                {
                    foreach (var it in lineas)
                    {
                        double line = (double)it.Subtotal;
                        double pct = (double)it.AlicuotaIvaPct;
                        if (pct <= 0.01)
                        {
                            neto += line;
                            continue;
                        }
                        double denom = 1 + pct / 100.0;
                        double nl = Math.Round(line / denom, 2);
                        double ivp = Math.Round(line - nl, 2);
                        neto += nl;
                        iva += ivp;
                    }
                }

                // --- Documento y condición IVA del receptor ---
                int docTipo = 99; // Consumidor Final (Anónimo)
                long docNro = 0;
                int condicionIvaReceptor = MapearCondicionIvaAfip(condicionIvaCliente, cuitCliente);

                if (prod && cuitCliente > 0)
                {
                    docTipo = 80;
                    docNro = cuitCliente;
                }

                string fecha = DateTime.Now.ToString("yyyyMMdd");
                string strTotal = importeTotal.ToString("0.00", System.Globalization.CultureInfo.InvariantCulture);
                string strNeto = neto.ToString("0.00", System.Globalization.CultureInfo.InvariantCulture);
                string strIva = iva.ToString("0.00", System.Globalization.CultureInfo.InvariantCulture);

                // XML CON EL CAMPO NUEVO <CondicionIVAReceptorId>
                string xmlBody = $@"
                    <FECAESolicitar xmlns=""http://ar.gov.afip.dif.FEV1/"">
                        <Auth>
                            <Token>{ticket.Token}</Token>
                            <Sign>{ticket.Sign}</Sign>
                            <Cuit>{cuitEmpresa}</Cuit>
                        </Auth>
                        <FeCAEReq>
                            <FeCabReq>
                                <CantReg>1</CantReg>
                                <PtoVta>{puntoVenta}</PtoVta>
                                <CbteTipo>{tipoComprobante}</CbteTipo>
                            </FeCabReq>
                            <FeDetReq>
                                <FECAEDetRequest>
                                    <Concepto>1</Concepto>
                                    <DocTipo>{docTipo}</DocTipo>
                                    <DocNro>{docNro}</DocNro>
                                    <CbteDesde>{nroComprobante}</CbteDesde>
                                    <CbteHasta>{nroComprobante}</CbteHasta>
                                    <CbteFch>{fecha}</CbteFch>
                                    <ImpTotal>{strTotal}</ImpTotal>
                                    <ImpTotConc>0</ImpTotConc>
                                    <ImpNeto>{strNeto}</ImpNeto>
                                    <ImpOpEx>0</ImpOpEx>
                                    <ImpTrib>0</ImpTrib>
                                    <ImpIVA>{strIva}</ImpIVA>
                                    <MonId>PES</MonId>
                                    <MonCotiz>1</MonCotiz>
                                    <CondicionIVAReceptorId>{condicionIvaReceptor}</CondicionIVAReceptorId>
                                    {(tipoComprobante != 11 ? $@"<Iva><AlicIva><Id>5</Id><BaseImp>{strNeto}</BaseImp><Importe>{strIva}</Importe></AlicIva></Iva>" : "")}
                                </FECAEDetRequest>
                            </FeDetReq>
                        </FeCAEReq>
                    </FECAESolicitar>";

                string respuestaXml = await EnviarSoap(prod ? URL_WSFE_PROD : URL_WSFE_HOMO, xmlBody, "http://ar.gov.afip.dif.FEV1/FECAESolicitar");

                var doc = XDocument.Parse(respuestaXml);
                XNamespace ns = "http://ar.gov.afip.dif.FEV1/";
                var resultadoCab = doc.Descendants(ns + "Resultado").FirstOrDefault()?.Value;

                if (resultadoCab == "A")
                {
                    resultado.Exito = true;
                    resultado.CAE = doc.Descendants(ns + "CAE").FirstOrDefault()?.Value;
                    resultado.Vencimiento = doc.Descendants(ns + "CAEFchVto").FirstOrDefault()?.Value;
                    resultado.NumeroComprobante = nroComprobante;
                }
                else
                {
                    resultado.Exito = false;
                    var errorMsg = doc.Descendants(ns + "Msg").FirstOrDefault()?.Value;
                    if (errorMsg == null) errorMsg = doc.Descendants(ns + "Obs").Descendants(ns + "Msg").FirstOrDefault()?.Value;
                    resultado.Error = "Rechazo AFIP: " + (errorMsg ?? respuestaXml);
                }
                return resultado;
            }
            catch (Exception ex)
            {
                return new ResultadoAfip { Exito = false, Error = "Error Sistema: " + ex.Message };
            }
        }

        // --- M├ëTODOS AUXILIARES ---
        private static async Task<LoginTicket> ObtenerTicketAcceso(string rutaCert, string pass, bool produccion)
        {
            if (File.Exists(ARCHIVO_TOKEN)) { try { var doc = XDocument.Load(ARCHIVO_TOKEN); var expTime = DateTime.Parse(doc.Descendants("expirationTime").First().Value); if (expTime > DateTime.Now) return new LoginTicket { Token = doc.Descendants("token").First().Value, Sign = doc.Descendants("sign").First().Value }; } catch { } }
            var nuevoTicket = await AutenticarWSAA_Remoto(rutaCert, pass, produccion);
            try { File.WriteAllText(ARCHIVO_TOKEN, nuevoTicket.XmlRespuestaOriginal); } catch { }
            return nuevoTicket;
        }

        private static async Task<LoginTicket> AutenticarWSAA_Remoto(string rutaCert, string pass, bool produccion, string servicio = "wsfe")
        {
            uint uniqueId = (uint)(DateTime.Now.Ticks % 4294967295);
            string xmlTra = $"<?xml version=\"1.0\" encoding=\"UTF-8\"?><loginTicketRequest version=\"1.0\"><header><uniqueId>{uniqueId}</uniqueId><generationTime>{DateTime.Now.AddMinutes(-10):s}</generationTime><expirationTime>{DateTime.Now.AddMinutes(10):s}</expirationTime></header><service>{servicio}</service></loginTicketRequest>";
            X509Certificate2 cert = new X509Certificate2(rutaCert, pass, X509KeyStorageFlags.PersistKeySet);
            ContentInfo contentInfo = new ContentInfo(Encoding.UTF8.GetBytes(xmlTra));
            SignedCms signedCms = new SignedCms(contentInfo);
            CmsSigner signer = new CmsSigner(cert);
            signer.IncludeOption = X509IncludeOption.EndCertOnly;
            signedCms.ComputeSignature(signer);
            string cmsBase64 = Convert.ToBase64String(signedCms.Encode());
            string xmlSoap = $@"<soapenv:Envelope xmlns:soapenv=""http://schemas.xmlsoap.org/soap/envelope/"" xmlns:wsaa=""http://wsaa.view.sua.dnet.afip.gov.ar/xsd""><soapenv:Header/><soapenv:Body><wsaa:loginCms><wsaa:in0>{cmsBase64}</wsaa:in0></wsaa:loginCms></soapenv:Body></soapenv:Envelope>";
            string url = produccion ? URL_WSAA_PROD : URL_WSAA_HOMO;
            string resp = await EnviarSoap(url, xmlSoap, "");
            var docSoap = XDocument.Parse(resp);
            var loginReturnString = docSoap.Descendants().FirstOrDefault(n => n.Name.LocalName == "loginCmsReturn")?.Value;
            if (string.IsNullOrEmpty(loginReturnString)) { var fault = docSoap.Descendants().FirstOrDefault(n => n.Name.LocalName == "faultstring")?.Value; throw new Exception("Error Login AFIP: " + (fault ?? resp)); }
            var docTicket = XDocument.Parse(loginReturnString);
            return new LoginTicket { Token = docTicket.Descendants("token").First().Value, Sign = docTicket.Descendants("sign").First().Value, XmlRespuestaOriginal = loginReturnString };
        }

        private static async Task<int> ObtenerUltimoComprobante(LoginTicket ticket, long cuit, int ptoVta, int tipoCbte, bool produccion)
        {
            string xmlBody = $@"<FECompUltimoAutorizado xmlns=""http://ar.gov.afip.dif.FEV1/""><Auth><Token>{ticket.Token}</Token><Sign>{ticket.Sign}</Sign><Cuit>{cuit}</Cuit></Auth><PtoVta>{ptoVta}</PtoVta><CbteTipo>{tipoCbte}</CbteTipo></FECompUltimoAutorizado>";
            string url = produccion ? URL_WSFE_PROD : URL_WSFE_HOMO;
            string resp = await EnviarSoap(url, xmlBody, "http://ar.gov.afip.dif.FEV1/FECompUltimoAutorizado");
            var doc = XDocument.Parse(resp);
            var cbteNro = doc.Descendants(XNamespace.Get("http://ar.gov.afip.dif.FEV1/") + "CbteNro").FirstOrDefault()?.Value;
            return string.IsNullOrEmpty(cbteNro) ? 0 : int.Parse(cbteNro);
        }

        private static async Task<string> EnviarSoap(string url, string bodyContent, string soapAction)
        {
            string envelope = bodyContent;
            if (!bodyContent.StartsWith("<soapenv:Envelope")) envelope = $@"<soapenv:Envelope xmlns:soapenv=""http://schemas.xmlsoap.org/soap/envelope/"" xmlns:def=""http://ar.gov.afip.dif.FEV1/""><soapenv:Header/><soapenv:Body>{bodyContent}</soapenv:Body></soapenv:Envelope>";
            using (var client = new HttpClient()) { var content = new StringContent(envelope, Encoding.UTF8, "text/xml"); client.DefaultRequestHeaders.TryAddWithoutValidation("SOAPAction", soapAction ?? ""); var response = await client.PostAsync(url, content); return await response.Content.ReadAsStringAsync(); }
        }

        private static string LimpiarCuit(string cuit)
        {
            if (string.IsNullOrEmpty(cuit)) return "";
            return new string(cuit.Where(char.IsDigit).ToArray());
        }

        // --- CONSULTA PADR├ôN (CUIT en AFIP) ---
        public static async Task<PersonaAfip> ObtenerPersonaPorCuitAsync(string cuit)
        {
            var resultado = new PersonaAfip { Exito = false };
            try
            {
                string cuitLimpio = LimpiarCuit(cuit);
                if (string.IsNullOrEmpty(cuitLimpio) || cuitLimpio.Length < 10) { resultado.Error = "CUIT inv├ílido (m├¡nimo 10 d├¡gitos)"; return resultado; }
                if (!long.TryParse(cuitLimpio, out long idPersona)) { resultado.Error = "CUIT debe contener solo n├║meros"; return resultado; }

                bool prod = DatabaseService.GetAfipAmbienteProduccion();
                DataRow config = DatabaseService.GetConfiguracion();
                if (config == null) { resultado.Error = "No hay configuraci├│n de negocio. Vaya a Configuraci├│n > Negocio y AFIP."; return resultado; }
                string cuitEmpresaStr = LimpiarCuit(config["CUIT"]?.ToString());
                if (string.IsNullOrEmpty(cuitEmpresaStr) || !long.TryParse(cuitEmpresaStr, out long cuitEmpresa))
                {
                    resultado.Error = "CUIT de la empresa no configurado o inv├ílido. Configure el CUIT en Configuraci├│n > Negocio y AFIP.";
                    return resultado;
                }
                string rutaCert = config["CertificadoPath"]?.ToString();
                string passCert = config["PasswordAfip"]?.ToString();
                if (string.IsNullOrEmpty(rutaCert) || !File.Exists(rutaCert)) { resultado.Error = "Certificado AFIP no configurado o no encontrado"; return resultado; }

                LoginTicket ticket = await ObtenerTicketAccesoPadron(rutaCert, passCert ?? "", prod);
                string urlPadron = prod ? URL_PADRON_PROD : URL_PADRON_HOMO;

                // SOAP getPersona para Padr├│n A4
                string ns = "http://ar.gov.afip.dif.sr_padron_a4/";
                string xmlBody = $@"<getPersona xmlns=""{ns}""><token>{ticket.Token}</token><sign>{ticket.Sign}</sign><cuitRepresentada>{cuitEmpresa}</cuitRepresentada><idPersona>{idPersona}</idPersona></getPersona>";
                string soapAction = $"\"{ns}getPersona\"";
                string envelope = $@"<soapenv:Envelope xmlns:soapenv=""http://schemas.xmlsoap.org/soap/envelope/"" xmlns:pad=""{ns}""><soapenv:Header/><soapenv:Body><pad:getPersona><pad:token>{ticket.Token}</pad:token><pad:sign>{ticket.Sign}</pad:sign><pad:cuitRepresentada>{cuitEmpresa}</pad:cuitRepresentada><pad:idPersona>{idPersona}</pad:idPersona></pad:getPersona></soapenv:Body></soapenv:Envelope>";

                using (var client = new HttpClient())
                {
                    var content = new StringContent(envelope, Encoding.UTF8, "text/xml");
                    client.DefaultRequestHeaders.TryAddWithoutValidation("SOAPAction", soapAction);
                    using (var response = await client.PostAsync(urlPadron, content))
                    {
                        string respXml = await response.Content.ReadAsStringAsync();
                        var doc = XDocument.Parse(respXml);
                        var fault = doc.Descendants().FirstOrDefault(n => n.Name.LocalName == "Fault" || n.Name.LocalName == "faultstring");
                        if (fault != null) { resultado.Error = "AFIP: " + (fault.Value ?? fault.ToString()); return resultado; }

                        var personaReturn = doc.Descendants().FirstOrDefault(n => n.Name.LocalName == "personaReturn");
                        if (personaReturn == null) { resultado.Error = "AFIP: Respuesta sin datos de persona"; return resultado; }
                        var persona = personaReturn.Descendants().FirstOrDefault(n => n.Name.LocalName == "persona" || n.Name.LocalName == "datosGenerales");
                        if (persona == null) { resultado.Error = "AFIP: No se encontr├│ el CUIT en el padr├│n"; return resultado; }

                        string razonSocial = persona.Descendants().FirstOrDefault(n => n.Name.LocalName == "razonSocial")?.Value ?? persona.Descendants().FirstOrDefault(n => n.Name.LocalName == "denominacion")?.Value;
                        if (string.IsNullOrEmpty(razonSocial))
                        {
                            string apellido = persona.Descendants().FirstOrDefault(n => n.Name.LocalName == "apellido")?.Value ?? "";
                            string nombre = persona.Descendants().FirstOrDefault(n => n.Name.LocalName == "nombre")?.Value ?? "";
                            razonSocial = $"{apellido}, {nombre}".Trim(',', ' ');
                        }
                        string condicionIva = persona.Descendants().FirstOrDefault(n => n.Name.LocalName == "impuestoIVA")?.Value ?? persona.Descendants().FirstOrDefault(n => n.Name.LocalName == "catIVA")?.Value ?? "";

                        resultado.Exito = true;
                        resultado.RazonSocial = razonSocial ?? "";
                        resultado.CondicionIVA = MapearCondicionIva(condicionIva);
                        return resultado;
                    }
                }
            }
            catch (Exception ex)
            {
                string msg = ex.Message;
                if (msg.Contains("no autorizado") || msg.Contains("Computador"))
                    msg += "\n\nDebe autorizar su IP en AFIP: ingrese a afip.gob.ar con Clave Fiscal, vaya a Administraci├│n de Relaciones / Web Services, adhiera el servicio ws_sr_padron_a4 y registre la IP de su computadora.";
                resultado.Error = "Error: " + msg;
                return resultado;
            }
        }

        private static string MapearCondicionIva(string codigo)
        {
            if (string.IsNullOrEmpty(codigo)) return "Consumidor Final";
            var c = codigo.ToUpper();
            if (c.Contains("RI") || c == "INSCRIPTO" || c == "1") return "Responsable Inscripto";
            if (c.Contains("EX") || c == "4") return "Exento";
            if (c.Contains("MT") || c == "MONOTRIBUTO" || c == "6") return "Monotributo";
            return "Consumidor Final";
        }

        /// <summary>Mapea la condición IVA del cliente al código AFIP (CondicionIVAReceptorId).</summary>
        private static int MapearCondicionIvaAfip(string condicionIva, long cuitCliente)
        {
            if (cuitCliente <= 0) return 5;
            string c = (condicionIva ?? "").Trim().ToLowerInvariant();
            if (string.IsNullOrEmpty(c)) return 5;
            if (c.Contains("inscripto") && !c.Contains("no")) return 1;
            if (c.Contains("monotrib")) return 6;
            if (c.Contains("exento")) return 4;
            if (c.Contains("no alcanz") || c.Contains("no responsable")) return 15;
            if (c.Contains("consumidor")) return 5;
            return 5;
        }

        private static async Task<LoginTicket> ObtenerTicketAccesoPadron(string rutaCert, string pass, bool produccion)
        {
            if (File.Exists(ARCHIVO_TOKEN_PADRON))
            {
                try
                {
                    var doc = XDocument.Load(ARCHIVO_TOKEN_PADRON);
                    var expTime = DateTime.Parse(doc.Descendants("expirationTime").First().Value);
                    if (expTime > DateTime.Now)
                        return new LoginTicket { Token = doc.Descendants("token").First().Value, Sign = doc.Descendants("sign").First().Value };
                }
                catch { }
            }
            var nuevoTicket = await AutenticarWSAA_Remoto(rutaCert, pass, produccion, "ws_sr_padron_a4");
            try { File.WriteAllText(ARCHIVO_TOKEN_PADRON, nuevoTicket.XmlRespuestaOriginal); } catch { }
            return nuevoTicket;
        }

        public class LoginTicket { public string Token; public string Sign; public string XmlRespuestaOriginal; }
    }

    public class ResultadoAfip { public bool Exito; public string CAE; public string Vencimiento; public int NumeroComprobante; public string Error; }
    public class PersonaAfip { public bool Exito; public string Error; public string RazonSocial; public string CondicionIVA; }
}
