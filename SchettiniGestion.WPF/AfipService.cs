using System;
using System.Collections.Generic;
using System.Data;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net;
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
        private const string ARCHIVO_TOKEN_LEGACY = "afip_token.xml";
        private const string ARCHIVO_TOKEN_PADRON_LEGACY = "afip_token_padron.xml";

        // URLs
        private const string URL_WSAA_HOMO = "https://wsaahomo.afip.gov.ar/ws/services/LoginCms";
        private const string URL_WSAA_PROD = "https://wsaa.afip.gov.ar/ws/services/LoginCms";
        private const string URL_WSFE_HOMO = "https://wswhomo.afip.gov.ar/wsfev1/service.asmx";
        private const string URL_WSFE_PROD = "https://servicios1.afip.gov.ar/wsfev1/service.asmx";
        private const string URL_PADRON_HOMO = "https://awshomo.afip.gov.ar/sr-padron/webservices/personaServiceA4";
        private const string URL_PADRON_PROD = "https://aws.afip.gov.ar/sr-padron/webservices/personaServiceA4";

        public static async Task<ResultadoAfip> FacturarAsync(
            int tipoComprobante,
            int puntoVenta,
            double importeTotal,
            long cuitCliente,
            List<FacturaItem> items,
            string condicionIvaCliente = null,
            int? cbteAsocTipo = null,
            int? cbteAsocPtoVta = null,
            long? cbteAsocNro = null)
        {
            var resultado = new ResultadoAfip();

            try
            {
                bool prod = DatabaseService.GetAfipAmbienteProduccion();
                DataRow config = DatabaseService.GetConfiguracion();
                string cuitRaw = config["CUIT"]?.ToString().Replace("-", "").Trim() ?? "";
                if (string.IsNullOrEmpty(cuitRaw) || !long.TryParse(cuitRaw, out long cuitEmpresa))
                {
                    resultado.Error = "CUIT de la empresa no configurado o inválido. Vaya a Configuración > Negocio y ARCA.";
                    return resultado;
                }
                string rutaCert = config["CertificadoPath"]?.ToString() ?? "";
                string passCert = DatabaseService.DecodeAfipCertificatePasswordStored(config["PasswordAfip"]);

                // 1. LOGIN (reutiliza TA en disco; no borrar el archivo si ARCA ya tiene uno vigente)
                LoginTicket ticket = await ObtenerTicketAcceso(rutaCert, passCert, prod);

                // 2. ÚLTIMO COMPROBANTE
                int nroComprobante = await ObtenerUltimoComprobante(ticket, cuitEmpresa, puntoVenta, tipoComprobante, prod) + 1;

                // 3. DATOS VENTA (neto / IVA según alícuota por ítem)
                // Tipos C (11 Factura C, 12 ND C, 13 NC C): sin discriminación de IVA
                bool esTipoC = tipoComprobante == 11 || tipoComprobante == 12 || tipoComprobante == 13;
                double neto = 0, iva = 0;
                var lineas = items ?? new List<FacturaItem>();
                if (esTipoC)
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
                    if (lineas.Count == 0)
                    {
                        // NC/ND sin ítems tipados: asumir IVA 21 % incluido
                        neto = Math.Round(importeTotal / 1.21, 2);
                        iva = Math.Round(importeTotal - neto, 2);
                    }
                }

                // --- Documento y condición IVA del receptor ---
                int docTipo = 99; // Consumidor Final (Anónimo)
                long docNro = 0;
                int condicionIvaReceptor = MapearCondicionIvaAfip(condicionIvaCliente, cuitCliente);

                if (cuitCliente > 0 && (prod || tipoComprobante == 1 || tipoComprobante == 2 || tipoComprobante == 3))
                {
                    docTipo = 80;
                    docNro = cuitCliente;
                }

                string fecha = DateTime.Now.ToString("yyyyMMdd");
                string strTotal = importeTotal.ToString("0.00", System.Globalization.CultureInfo.InvariantCulture);
                string strNeto = neto.ToString("0.00", System.Globalization.CultureInfo.InvariantCulture);
                string strIva = iva.ToString("0.00", System.Globalization.CultureInfo.InvariantCulture);

                // Comprobante asociado obligatorio en NC/ND electrónicas
                string xmlCbteAsoc = "";
                if (cbteAsocTipo.HasValue && cbteAsocPtoVta.HasValue && cbteAsocNro.HasValue
                    && cbteAsocTipo.Value > 0 && cbteAsocPtoVta.Value > 0 && cbteAsocNro.Value > 0)
                {
                    xmlCbteAsoc = $@"
                                    <CbtesAsoc>
                                        <CbteAsoc>
                                            <Tipo>{cbteAsocTipo.Value}</Tipo>
                                            <PtoVta>{cbteAsocPtoVta.Value}</PtoVta>
                                            <Nro>{cbteAsocNro.Value}</Nro>
                                            <Cuit>{cuitEmpresa}</Cuit>
                                        </CbteAsoc>
                                    </CbtesAsoc>";
                }

                string xmlIva = esTipoC
                    ? ""
                    : $@"<Iva><AlicIva><Id>5</Id><BaseImp>{strNeto}</BaseImp><Importe>{strIva}</Importe></AlicIva></Iva>";

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
                                    {xmlCbteAsoc}
                                    {xmlIva}
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
                    // Motivo real del rechazo: Observaciones del detalle o Errors.
                    // (Events trae avisos informativos de ARCA que no son el motivo.)
                    var errorMsg = doc.Descendants(ns + "Observaciones").Descendants(ns + "Msg").FirstOrDefault()?.Value
                        ?? doc.Descendants(ns + "Obs").Descendants(ns + "Msg").FirstOrDefault()?.Value
                        ?? doc.Descendants(ns + "Errors").Descendants(ns + "Msg").FirstOrDefault()?.Value
                        ?? doc.Descendants(ns + "Msg").FirstOrDefault()?.Value;
                    resultado.Error = "Rechazo ARCA: " + (errorMsg ?? respuestaXml);
                }
                return resultado;
            }
            catch (Exception ex)
            {
                return new ResultadoAfip { Exito = false, Error = "Error Sistema: " + ex.Message };
            }
        }

        /// <summary>
        /// Reintento de CAE: primero consulta si ARCA ya autorizó (timeout), si no pide uno nuevo.
        /// </summary>
        public static async Task<ResultadoAfip> ReintentarCaePendienteAsync(
            int tipoComprobante,
            int puntoVenta,
            double importeTotal,
            long cuitCliente,
            List<FacturaItem> items,
            string condicionIvaCliente)
        {
            var recuperado = await IntentarRecuperarCaePerdido(tipoComprobante, puntoVenta, importeTotal);
            if (recuperado != null && recuperado.Exito)
                return recuperado;
            return await FacturarAsync(tipoComprobante, puntoVenta, importeTotal, cuitCliente, items, condicionIvaCliente);
        }

        private static async Task<ResultadoAfip> IntentarRecuperarCaePerdido(int tipoComprobante, int puntoVenta, double importeTotal)
        {
            try
            {
                bool prod = DatabaseService.GetAfipAmbienteProduccion();
                DataRow config = DatabaseService.GetConfiguracion();
                string cuitRaw = config["CUIT"]?.ToString().Replace("-", "").Trim() ?? "";
                if (string.IsNullOrEmpty(cuitRaw) || !long.TryParse(cuitRaw, out long cuitEmpresa))
                    return null;
                string rutaCert = config["CertificadoPath"]?.ToString() ?? "";
                string passCert = DatabaseService.DecodeAfipCertificatePasswordStored(config["PasswordAfip"]);
                LoginTicket ticket = await ObtenerTicketAcceso(rutaCert, passCert, prod);

                int ultimo = await ObtenerUltimoComprobante(ticket, cuitEmpresa, puntoVenta, tipoComprobante, prod);
                string hoy = DateTime.Now.ToString("yyyyMMdd");
                foreach (int nro in new[] { ultimo, ultimo + 1 })
                {
                    if (nro <= 0) continue;
                    var cons = await ConsultarComprobante(ticket, cuitEmpresa, puntoVenta, tipoComprobante, nro, prod);
                    if (cons == null || !cons.Exito) continue;
                    if (!string.Equals(cons.FechaCbte, hoy, StringComparison.Ordinal)) continue;
                    if (Math.Abs(cons.ImporteTotal - importeTotal) > 0.05) continue;
                    return cons;
                }
            }
            catch { }
            return null;
        }

        private static async Task<ResultadoAfip> ConsultarComprobante(
            LoginTicket ticket, long cuit, int ptoVta, int tipoCbte, int nro, bool produccion)
        {
            string xmlBody = $@"<FECompConsultar xmlns=""http://ar.gov.afip.dif.FEV1/"">
                <Auth><Token>{ticket.Token}</Token><Sign>{ticket.Sign}</Sign><Cuit>{cuit}</Cuit></Auth>
                <FeCompConsReq>
                    <PtoVta>{ptoVta}</PtoVta>
                    <CbteTipo>{tipoCbte}</CbteTipo>
                    <CbteNro>{nro}</CbteNro>
                </FeCompConsReq>
            </FECompConsultar>";
            string url = produccion ? URL_WSFE_PROD : URL_WSFE_HOMO;
            string resp = await EnviarSoap(url, xmlBody, "http://ar.gov.afip.dif.FEV1/FECompConsultar");
            var doc = XDocument.Parse(resp);
            XNamespace ns = "http://ar.gov.afip.dif.FEV1/";
            string cae = doc.Descendants(ns + "CodAutorizacion").FirstOrDefault()?.Value
                ?? doc.Descendants(ns + "CAE").FirstOrDefault()?.Value;
            if (string.IsNullOrWhiteSpace(cae)) return null;
            string imp = doc.Descendants(ns + "ImpTotal").FirstOrDefault()?.Value;
            double.TryParse(imp, System.Globalization.NumberStyles.Any,
                System.Globalization.CultureInfo.InvariantCulture, out double total);
            return new ResultadoAfip
            {
                Exito = true,
                CAE = cae,
                Vencimiento = doc.Descendants(ns + "FchVto").FirstOrDefault()?.Value
                    ?? doc.Descendants(ns + "CAEFchVto").FirstOrDefault()?.Value,
                NumeroComprobante = nro,
                FechaCbte = doc.Descendants(ns + "CbteFch").FirstOrDefault()?.Value,
                ImporteTotal = total
            };
        }

        /// <summary>
        /// Prueba autenticación contra WSAA usando el certificado configurado.
        /// Reutiliza ticket en caché si sigue vigente; si ARCA responde que ya hay TA válido,
        /// se interpreta como éxito (el certificado está autorizado).
        /// </summary>
        public static async Task<ResultadoPruebaWsaa> ProbarConexionWsaaAsync()
        {
            var resultado = new ResultadoPruebaWsaa();
            bool produccion = DatabaseService.GetAfipAmbienteProduccion();
            string ambiente = produccion ? "producción" : "homologación";

            try
            {
                DataRow config = DatabaseService.GetConfiguracion();
                if (config == null)
                {
                    resultado.Mensaje = "No hay configuración de negocio cargada. Complete los datos del negocio y guarde.";
                    return resultado;
                }

                string rutaCert = config["CertificadoPath"]?.ToString() ?? "";
                if (string.IsNullOrWhiteSpace(rutaCert) || !File.Exists(rutaCert))
                {
                    resultado.Mensaje = "No hay certificado ARCA configurado. Genere el CSR, obtenga el .crt en ARCA e impórtelo en esta pantalla.";
                    return resultado;
                }

                string ext = Path.GetExtension(rutaCert).ToLowerInvariant();
                if (ext == ".crt" || ext == ".cer")
                {
                    string keyPath = DatabaseService.ObtenerAfipClavePrivadaPath();
                    if (string.IsNullOrWhiteSpace(keyPath) || !File.Exists(keyPath))
                    {
                        resultado.Mensaje = "Falta la clave privada (.key). Debe generar el CSR desde esta pantalla antes de subir el certificado .crt.";
                        return resultado;
                    }
                }
                else if ((ext == ".pfx" || ext == ".p12") && !DatabaseService.TienePasswordAfipPersistida(config))
                {
                    resultado.Mensaje = "Ingrese la contraseña del certificado .pfx y guarde la configuración antes de probar la conexión.";
                    return resultado;
                }

                string passCert = DatabaseService.DecodeAfipCertificatePasswordStored(config["PasswordAfip"]);

                // Para validar vigencia alcanza con la parte pública del certificado.
                using (X509Certificate2 cert = (ext == ".crt" || ext == ".cer")
                    ? new X509Certificate2(rutaCert)
                    : new X509Certificate2(rutaCert, passCert ?? ""))
                {
                    if (DateTime.Now > cert.NotAfter)
                    {
                        resultado.Mensaje = $"El certificado ARCA está vencido (venció el {cert.NotAfter:dd/MM/yyyy}). Solicite uno nuevo en ARCA.";
                        return resultado;
                    }
                    if (DateTime.Now < cert.NotBefore)
                    {
                        resultado.Mensaje = $"El certificado ARCA aún no es válido (válido desde {cert.NotBefore:dd/MM/yyyy}).";
                        return resultado;
                    }
                }

                LoginTicket ticket;
                try
                {
                    // Preferir ticket en caché; evita el rechazo de WSAA por TA ya vigente.
                    ticket = await ObtenerTicketAcceso(rutaCert, passCert, produccion);
                }
                catch (Exception exAuth) when (EsErrorTaYaVigente(exAuth))
                {
                    resultado.Exito = true;
                    resultado.Mensaje =
                        $"Conexión OK con ARCA ({ambiente}).\n\n" +
                        "ARCA ya tenía un ticket de acceso válido para este certificado (es normal si acabás de probar o facturar).\n" +
                        "El certificado está autorizado: el sistema puede emitir facturas electrónicas.";
                    return resultado;
                }

                if (ticket == null || string.IsNullOrWhiteSpace(ticket.Token) || string.IsNullOrWhiteSpace(ticket.Sign))
                {
                    resultado.Mensaje = $"ARCA ({ambiente}) respondió pero no devolvió un ticket válido. Verifique la delegación del servicio wsfe en ARCA.";
                    return resultado;
                }

                resultado.Exito = true;
                resultado.Mensaje = $"Conexión exitosa con WSAA en {ambiente}. El sistema está listo para emitir facturas electrónicas.";
                return resultado;
            }
            catch (Exception ex)
            {
                if (EsErrorTaYaVigente(ex))
                {
                    resultado.Exito = true;
                    resultado.Mensaje =
                        $"Conexión OK con ARCA ({ambiente}).\n\n" +
                        "ARCA ya tenía un ticket de acceso válido para este certificado. El sistema está listo.";
                    return resultado;
                }
                resultado.Mensaje = FormatearErrorWsaaParaUsuario(ex, produccion);
                return resultado;
            }
        }

        private static bool EsErrorTaYaVigente(Exception ex)
        {
            string t = ObtenerMensajeExcepcionCompleto(ex).ToLowerInvariant();
            return t.Contains("ya posee un ta")
                || t.Contains("ya posee un t.a")
                || t.Contains("already has a valid ta")
                || (t.Contains("ta valido") && t.Contains("wsn"))
                || (t.Contains("ta válido") && t.Contains("wsn"));
        }

        // --- M├ëTODOS AUXILIARES ---
        private static string CarpetaTokensWsaa()
        {
            return Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "SCHPOS");
        }

        private static string RutaTokenWsaa(bool produccion, bool padron)
        {
            string nombre = padron
                ? (produccion ? "afip_token_padron_prod.xml" : "afip_token_padron_homo.xml")
                : (produccion ? "afip_token_prod.xml" : "afip_token_homo.xml");
            return Path.Combine(CarpetaTokensWsaa(), nombre);
        }

        private static IEnumerable<string> RutasTokenLegacy(bool padron)
        {
            string nombre = padron ? ARCHIVO_TOKEN_PADRON_LEGACY : ARCHIVO_TOKEN_LEGACY;
            yield return Path.Combine(AppDomain.CurrentDomain.BaseDirectory ?? "", nombre);
            yield return Path.Combine(Directory.GetCurrentDirectory(), nombre);
            yield return nombre;
        }

        private static bool TryParseFechaWsaa(string raw, out DateTime fecha)
        {
            fecha = default;
            if (string.IsNullOrWhiteSpace(raw)) return false;
            if (DateTime.TryParse(raw, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out fecha))
                return true;
            if (DateTime.TryParse(raw, CultureInfo.GetCultureInfo("es-AR"), DateTimeStyles.None, out fecha))
                return true;
            try
            {
                fecha = XmlConvert.ToDateTime(raw.Trim(), XmlDateTimeSerializationMode.Local);
                return true;
            }
            catch { return false; }
        }

        private static LoginTicket CargarTicketDesdeArchivo(string path, bool exigirVigente)
        {
            if (string.IsNullOrWhiteSpace(path) || !File.Exists(path)) return null;
            try
            {
                var doc = XDocument.Load(path);
                string token = doc.Descendants("token").FirstOrDefault()?.Value;
                string sign = doc.Descendants("sign").FirstOrDefault()?.Value;
                if (string.IsNullOrWhiteSpace(token) || string.IsNullOrWhiteSpace(sign)) return null;
                if (exigirVigente)
                {
                    string expRaw = doc.Descendants("expirationTime").FirstOrDefault()?.Value;
                    // 2 minutos de holgura por desfasaje de reloj; si no se puede parsear, igual se usa (WSAA no re-emite).
                    if (TryParseFechaWsaa(expRaw, out DateTime exp) && exp <= DateTime.Now.AddMinutes(1))
                        return null;
                }
                return new LoginTicket
                {
                    Token = token,
                    Sign = sign,
                    XmlRespuestaOriginal = doc.Root != null ? doc.ToString() : File.ReadAllText(path)
                };
            }
            catch { return null; }
        }

        private static LoginTicket CargarTicketGuardado(bool produccion, bool padron, bool exigirVigente)
        {
            var ticket = CargarTicketDesdeArchivo(RutaTokenWsaa(produccion, padron), exigirVigente);
            if (ticket != null) return ticket;
            foreach (string legacy in RutasTokenLegacy(padron))
            {
                ticket = CargarTicketDesdeArchivo(legacy, exigirVigente);
                if (ticket != null) return ticket;
            }
            return null;
        }

        private static void GuardarTicket(bool produccion, bool padron, LoginTicket ticket)
        {
            if (ticket == null || string.IsNullOrWhiteSpace(ticket.XmlRespuestaOriginal)) return;
            try
            {
                string dir = CarpetaTokensWsaa();
                if (!Directory.Exists(dir)) Directory.CreateDirectory(dir);
                File.WriteAllText(RutaTokenWsaa(produccion, padron), ticket.XmlRespuestaOriginal, Encoding.UTF8);
            }
            catch { /* Program Files / permisos: no abortar la facturación si el TA ya está en memoria */ }
        }

        private static async Task<LoginTicket> ObtenerTicketAcceso(string rutaCert, string pass, bool produccion)
        {
            var vigente = CargarTicketGuardado(produccion, false, exigirVigente: true);
            if (vigente != null)
            {
                GuardarTicket(produccion, false, vigente);
                return vigente;
            }

            try
            {
                var nuevoTicket = await AutenticarWSAA_Remoto(rutaCert, pass, produccion);
                GuardarTicket(produccion, false, nuevoTicket);
                return nuevoTicket;
            }
            catch (Exception ex) when (EsErrorTaYaVigente(ex))
            {
                // Caché local perdida o vencida según el reloj, pero ARCA aún tiene TA vigente:
                // hay que reutilizar la copia, no pedir otra ni borrar el archivo.
                var guardado = CargarTicketGuardado(produccion, false, exigirVigente: false);
                if (guardado != null)
                {
                    GuardarTicket(produccion, false, guardado);
                    return guardado;
                }
                throw new Exception(
                    "ARCA todavía tiene un ticket de acceso vigente (suele durar hasta ~12 horas) y no quedó una copia usable en esta PC. " +
                    "Esperá a que venza y volvé a intentar. No regeneres el certificado.",
                    ex);
            }
        }

        private static async Task<LoginTicket> AutenticarWSAA_Remoto(string rutaCert, string pass, bool produccion, string servicio = "wsfe")
        {
            uint uniqueId = (uint)(DateTime.Now.Ticks % 4294967295);
            string xmlTra = $"<?xml version=\"1.0\" encoding=\"UTF-8\"?><loginTicketRequest version=\"1.0\"><header><uniqueId>{uniqueId}</uniqueId><generationTime>{DateTime.Now.AddMinutes(-10):s}</generationTime><expirationTime>{DateTime.Now.AddMinutes(10):s}</expirationTime></header><service>{servicio}</service></loginTicketRequest>";
            string cmsBase64 = Convert.ToBase64String(FirmarTra(Encoding.UTF8.GetBytes(xmlTra), rutaCert, pass));
            string xmlSoap = $@"<soapenv:Envelope xmlns:soapenv=""http://schemas.xmlsoap.org/soap/envelope/"" xmlns:wsaa=""http://wsaa.view.sua.dnet.afip.gov.ar/xsd""><soapenv:Header/><soapenv:Body><wsaa:loginCms><wsaa:in0>{cmsBase64}</wsaa:in0></wsaa:loginCms></soapenv:Body></soapenv:Envelope>";
            string url = produccion ? URL_WSAA_PROD : URL_WSAA_HOMO;
            string resp = await EnviarSoap(url, xmlSoap, "");
            var docSoap = XDocument.Parse(resp);
            var loginReturnString = docSoap.Descendants().FirstOrDefault(n => n.Name.LocalName == "loginCmsReturn")?.Value;
            if (string.IsNullOrEmpty(loginReturnString)) { var fault = docSoap.Descendants().FirstOrDefault(n => n.Name.LocalName == "faultstring")?.Value; throw new Exception("Error Login ARCA: " + (fault ?? resp)); }
            var docTicket = XDocument.Parse(loginReturnString);
            return new LoginTicket { Token = docTicket.Descendants("token").First().Value, Sign = docTicket.Descendants("sign").First().Value, XmlRespuestaOriginal = loginReturnString };
        }

        /// <summary>Firma el TRA para WSAA. Con .crt+.key usa BouncyCastle (evita el error
        /// "El conjunto de claves no existe" de los contenedores de claves de Windows).</summary>
        private static byte[] FirmarTra(byte[] tra, string rutaCert, string pass)
        {
            if (string.IsNullOrWhiteSpace(rutaCert) || !File.Exists(rutaCert))
                throw new FileNotFoundException("Certificado ARCA no encontrado.", rutaCert ?? "");

            string ext = Path.GetExtension(rutaCert).ToLowerInvariant();
            if (ext == ".crt" || ext == ".cer")
            {
                string keyPath = DatabaseService.ObtenerAfipClavePrivadaPath();
                return AfipActivacionFiscalService.FirmarTraCms(tra, rutaCert, keyPath);
            }

            if (ext == ".pfx" || ext == ".p12")
            {
                var cert = new X509Certificate2(rutaCert, pass ?? "", X509KeyStorageFlags.Exportable | X509KeyStorageFlags.PersistKeySet);
                var signedCms = new SignedCms(new ContentInfo(tra));
                var signer = new CmsSigner(cert) { IncludeOption = X509IncludeOption.EndCertOnly };
                signedCms.ComputeSignature(signer);
                return signedCms.Encode();
            }

            throw new NotSupportedException("Formato de certificado ARCA no soportado: " + ext);
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

        // --- CONSULTA PADR├ôN (CUIT en ARCA) ---
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
                if (config == null) { resultado.Error = "No hay configuraci├│n de negocio. Vaya a Configuraci├│n > Negocio y ARCA."; return resultado; }
                string cuitEmpresaStr = LimpiarCuit(config["CUIT"]?.ToString());
                if (string.IsNullOrEmpty(cuitEmpresaStr) || !long.TryParse(cuitEmpresaStr, out long cuitEmpresa))
                {
                    resultado.Error = "CUIT de la empresa no configurado o inv├ílido. Configure el CUIT en Configuraci├│n > Negocio y ARCA.";
                    return resultado;
                }
                string rutaCert = config["CertificadoPath"]?.ToString();
                string passCert = DatabaseService.DecodeAfipCertificatePasswordStored(config["PasswordAfip"]);
                if (string.IsNullOrEmpty(rutaCert) || !File.Exists(rutaCert)) { resultado.Error = "Certificado ARCA no configurado o no encontrado"; return resultado; }

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
                        if (fault != null) { resultado.Error = "ARCA: " + (fault.Value ?? fault.ToString()); return resultado; }

                        var personaReturn = doc.Descendants().FirstOrDefault(n => n.Name.LocalName == "personaReturn");
                        if (personaReturn == null) { resultado.Error = "ARCA: Respuesta sin datos de persona"; return resultado; }
                        var persona = personaReturn.Descendants().FirstOrDefault(n => n.Name.LocalName == "persona" || n.Name.LocalName == "datosGenerales");
                        if (persona == null) { resultado.Error = "ARCA: No se encontr├│ el CUIT en el padr├│n"; return resultado; }

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
                    msg += "\n\nDebe autorizar su IP en ARCA: ingrese a afip.gob.ar con Clave Fiscal, vaya a Administraci├│n de Relaciones / Web Services, adhiera el servicio ws_sr_padron_a4 y registre la IP de su computadora.";
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

        /// <summary>Mapea la condición IVA del cliente al código ARCA (CondicionIVAReceptorId).</summary>
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
            var vigente = CargarTicketGuardado(produccion, true, exigirVigente: true);
            if (vigente != null)
            {
                GuardarTicket(produccion, true, vigente);
                return vigente;
            }

            try
            {
                var nuevoTicket = await AutenticarWSAA_Remoto(rutaCert, pass, produccion, "ws_sr_padron_a4");
                GuardarTicket(produccion, true, nuevoTicket);
                return nuevoTicket;
            }
            catch (Exception ex) when (EsErrorTaYaVigente(ex))
            {
                var guardado = CargarTicketGuardado(produccion, true, exigirVigente: false);
                if (guardado != null)
                {
                    GuardarTicket(produccion, true, guardado);
                    return guardado;
                }
                throw;
            }
        }

        private static string FormatearErrorWsaaParaUsuario(Exception ex, bool produccion)
        {
            string ambiente = produccion ? "producción" : "homologación";
            string texto = ObtenerMensajeExcepcionCompleto(ex).ToLowerInvariant();

            if (ex is FileNotFoundException || texto.Contains("no se encuentra") || texto.Contains("not found"))
                return "No se encontró el certificado o la clave privada en disco. Vuelva a importar el .crt o verifique la ruta del .pfx.";

            if (texto.Contains("venc") || texto.Contains("expir") || texto.Contains("caduc") || texto.Contains("not yet valid"))
                return "El certificado ARCA está vencido o aún no es válido. Solicite uno nuevo en ARCA.";

            if (texto.Contains("contraseña") || texto.Contains("password") || texto.Contains("bad password") || texto.Contains("incorrect password"))
                return "La contraseña del certificado .pfx es incorrecta. Verifíquela en Configuración y vuelva a probar.";

            if (texto.Contains("computador") || texto.Contains("no autorizado") || texto.Contains("not authorized")
                || texto.Contains("no está autorizado") || texto.Contains("coe.notauthorized")
                || texto.Contains("no registered") || texto.Contains("servicio no") || texto.Contains("deleg"))
            {
                return "ARCA rechazó la autorización del certificado.\n\n" +
                       "Verifique en ARCA (Clave Fiscal) que:\n" +
                       "• El certificado esté asociado al CUIT del negocio.\n" +
                       "• El servicio wsfe esté delegado al «Computador Fiscal» correcto.\n" +
                       "• Esté usando el ambiente correcto (" + ambiente + ").";
            }

            if (texto.Contains("cms") || texto.Contains("firma") || texto.Contains("signature")
                || texto.Contains("certificado no") || texto.Contains("ac de confianza"))
            {
                return "ARCA no pudo validar la firma del certificado. Asegúrese de usar el .crt emitido por ARCA para el CSR generado en este sistema.";
            }

            if (texto.Contains("clave privada") || texto.Contains("private key") || texto.Contains("does not match"))
                return "El certificado .crt no corresponde a la clave privada (.key) guardada en este equipo. Importe el .crt correcto o genere un nuevo CSR.";

            if (ex is HttpRequestException || ex is WebException || ex is TaskCanceledException
                || texto.Contains("timeout") || texto.Contains("timed out") || texto.Contains("conexión")
                || texto.Contains("connection") || texto.Contains("host") || texto.Contains("ssl"))
            {
                return "No se pudo conectar con los servidores de ARCA (" + ambiente + ").\n\n" +
                       "Verifique su conexión a Internet, firewall o proxy, e intente nuevamente en unos segundos.";
            }

            if (texto.Contains("error login afip"))
                return "ARCA respondió con error: " + ex.Message.Replace("Error Login ARCA: ", "").Trim();

            return "No se pudo autenticar con ARCA (" + ambiente + "): " + ex.Message;
        }

        private static string ObtenerMensajeExcepcionCompleto(Exception ex)
        {
            if (ex == null) return "";
            var sb = new StringBuilder(ex.Message);
            var inner = ex.InnerException;
            while (inner != null)
            {
                sb.Append(' ').Append(inner.Message);
                inner = inner.InnerException;
            }
            return sb.ToString();
        }

        public class LoginTicket { public string Token; public string Sign; public string XmlRespuestaOriginal; }
    }

    public class ResultadoPruebaWsaa { public bool Exito; public string Mensaje; }
    public class ResultadoAfip
    {
        public bool Exito;
        public string CAE;
        public string Vencimiento;
        public int NumeroComprobante;
        public string Error;
        public string FechaCbte;
        public double ImporteTotal;
    }
    public class PersonaAfip { public bool Exito; public string Error; public string RazonSocial; public string CondicionIVA; }
}
