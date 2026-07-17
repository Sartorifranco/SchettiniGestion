using System;
using System.Collections.Generic;
using System.Data;
using System.IO;
using System.Linq;
using System.Security.AccessControl;
using System.Security.Cryptography.X509Certificates;
using System.Security.Principal;
using System.Text;
using Org.BouncyCastle.Asn1;
using Org.BouncyCastle.Asn1.X509;
using Org.BouncyCastle.Crypto;
using Org.BouncyCastle.Crypto.Generators;
using Org.BouncyCastle.OpenSsl;
using Org.BouncyCastle.Pkcs;
using Org.BouncyCastle.Security;
using Org.BouncyCastle.X509;

namespace SchettiniGestion
{
    /// <summary>
    /// Generación de CSR y gestión de certificados AFIP/ARCA (.key + .crt).
    /// </summary>
    public static class AfipActivacionFiscalService
    {
        private const string CarpetaAfip = "afip";
        private const string NombreCnGenerico = "SCHPOS";

        public sealed class ResultadoCsr
        {
            public bool Exito { get; set; }
            public string Error { get; set; }
            public string ContenidoCsr { get; set; }
            public string RutaClavePrivada { get; set; }
            public string NombreArchivoCsr { get; set; }
        }

        public sealed class ResultadoCertificado
        {
            public bool Exito { get; set; }
            public string Error { get; set; }
            public string RutaCertificado { get; set; }
        }

        /// <summary>
        /// Genera par RSA 2048, CSR PKCS#10 con subject AFIP y persiste la clave privada.
        /// </summary>
        public static ResultadoCsr GenerarCsr(string cuit, string razonSocial, string nombreFantasia)
        {
            var resultado = new ResultadoCsr();
            try
            {
                string cuitDigitos = LimpiarCuit(cuit);
                if (cuitDigitos.Length != 11)
                {
                    resultado.Error = "El CUIT debe tener 11 dígitos (sin guiones).";
                    return resultado;
                }

                if (string.IsNullOrWhiteSpace(razonSocial))
                {
                    resultado.Error = "La Razón Social es obligatoria.";
                    return resultado;
                }

                string commonName = string.IsNullOrWhiteSpace(nombreFantasia)
                    ? NombreCnGenerico
                    : nombreFantasia.Trim();

                var keyPair = GenerarParRsa2048();
                var subject = CrearSubjectAfip(razonSocial.Trim(), commonName, cuitDigitos);

                var csr = new Pkcs10CertificationRequest(
                    "SHA256withRSA",
                    subject,
                    keyPair.Public,
                    null,
                    keyPair.Private);

                string carpetaAfip = AsegurarCarpetaAfip();
                string rutaKey = Path.Combine(carpetaAfip, $"clave_privada_{cuitDigitos}.key");

                GuardarClavePrivadaPem(rutaKey, keyPair.Private);
                AplicarPermisosRestringidos(rutaKey);

                if (!DatabaseService.GuardarRutasActivacionAfip(rutaKey, null))
                {
                    resultado.Error = "No se pudo guardar la ruta de la clave privada en la base de datos.";
                    return resultado;
                }

                resultado.Exito = true;
                resultado.ContenidoCsr = ExportarPem(csr);
                resultado.RutaClavePrivada = rutaKey;
                resultado.NombreArchivoCsr = $"pedido_{cuitDigitos}.csr";
                return resultado;
            }
            catch (Exception ex)
            {
                resultado.Error = ex.Message;
                return resultado;
            }
        }

        /// <summary>
        /// Copia el certificado .crt emitido por AFIP/ARCA junto a la clave privada del sistema.
        /// </summary>
        public static ResultadoCertificado GuardarCertificadoAfip(string rutaArchivoOrigen, string cuitPantalla = null)
        {
            var resultado = new ResultadoCertificado();
            try
            {
                if (string.IsNullOrWhiteSpace(rutaArchivoOrigen) || !File.Exists(rutaArchivoOrigen))
                {
                    resultado.Error = "Seleccione un archivo de certificado (.crt) válido.";
                    return resultado;
                }

                string ext = Path.GetExtension(rutaArchivoOrigen).ToLowerInvariant();
                if (ext != ".crt" && ext != ".cer")
                {
                    resultado.Error = "Solo se admiten archivos .crt o .cer.";
                    return resultado;
                }

                DataRow config = DatabaseService.GetConfiguracion();
                if (config == null)
                {
                    resultado.Error = "No hay configuración de negocio cargada.";
                    return resultado;
                }

                string cuitDigitos = DatabaseService.ObtenerCuitEmpresaSoloDigitos(config);
                if (cuitDigitos.Length != 11)
                    cuitDigitos = LimpiarCuit(cuitPantalla);
                if (cuitDigitos.Length != 11)
                {
                    resultado.Error = "Configure un CUIT válido (11 dígitos) en Datos de la Empresa y presione Guardar antes de subir el certificado.";
                    return resultado;
                }

                string keyPath = config.Table.Columns.Contains("AfipClavePrivadaPath")
                    ? config["AfipClavePrivadaPath"]?.ToString()
                    : null;

                if (string.IsNullOrWhiteSpace(keyPath) || !File.Exists(keyPath))
                {
                    resultado.Error = "Primero debe generar el CSR desde esta pantalla. Falta la clave privada (.key) asociada.";
                    return resultado;
                }

                ValidarCertificadoCorresponde(rutaArchivoOrigen, keyPath);

                string carpetaAfip = AsegurarCarpetaAfip();
                string destino = Path.Combine(carpetaAfip, $"certificado_{cuitDigitos}.crt");
                File.Copy(rutaArchivoOrigen, destino, overwrite: true);
                AplicarPermisosRestringidos(destino);

                if (!DatabaseService.GuardarRutasActivacionAfip(keyPath, destino))
                {
                    resultado.Error = "No se pudo registrar la ruta del certificado en la base de datos.";
                    return resultado;
                }

                resultado.Exito = true;
                resultado.RutaCertificado = destino;
                return resultado;
            }
            catch (Exception ex)
            {
                resultado.Error = ex.Message;
                return resultado;
            }
        }

        /// <summary>
        /// Combina certificado público (.crt) y clave privada (.key) para firmar el TRA de WSAA.
        /// </summary>
        public static X509Certificate2 CargarCertificadoConClave(string rutaCertificado, string rutaClavePrivada)
        {
            if (string.IsNullOrWhiteSpace(rutaCertificado) || !File.Exists(rutaCertificado))
                throw new FileNotFoundException("Certificado AFIP no encontrado.", rutaCertificado ?? "");
            if (string.IsNullOrWhiteSpace(rutaClavePrivada) || !File.Exists(rutaClavePrivada))
                throw new FileNotFoundException("Clave privada AFIP no encontrada.", rutaClavePrivada ?? "");

            var certParser = new X509CertificateParser();
            Org.BouncyCastle.X509.X509Certificate bcCert = certParser.ReadCertificate(File.ReadAllBytes(rutaCertificado));
            AsymmetricKeyParameter privateKey = LeerClavePrivadaPem(rutaClavePrivada);

            var store = new Pkcs12StoreBuilder().Build();
            var certEntry = new X509CertificateEntry(bcCert);
            store.SetCertificateEntry("cert", certEntry);
            store.SetKeyEntry("key", new AsymmetricKeyEntry(privateKey), new[] { certEntry });

            using (var ms = new MemoryStream())
            {
                store.Save(ms, Array.Empty<char>(), new SecureRandom());
                // UserKeySet: el almacén de máquina (MachineKeySet) requiere permisos de
                // administrador y provoca "El conjunto de claves no existe" al firmar.
                return new X509Certificate2(ms.ToArray(), string.Empty,
                    X509KeyStorageFlags.Exportable | X509KeyStorageFlags.PersistKeySet | X509KeyStorageFlags.UserKeySet);
            }
        }

        public static string ObtenerEstadoActivacion(DataRow config)
        {
            if (config == null) return "Sin configurar";

            string certPath = config["CertificadoPath"]?.ToString();
            string keyPath = config.Table.Columns.Contains("AfipClavePrivadaPath")
                ? config["AfipClavePrivadaPath"]?.ToString()
                : null;

            bool tieneKey = !string.IsNullOrWhiteSpace(keyPath) && File.Exists(keyPath);
            bool tieneCert = !string.IsNullOrWhiteSpace(certPath) && File.Exists(certPath);

            if (tieneKey && tieneCert) return "Certificado AFIP listo (.key + .crt)";
            if (tieneKey) return "CSR generado — falta subir el certificado .crt de AFIP/ARCA";
            if (!string.IsNullOrWhiteSpace(certPath) && certPath.EndsWith(".pfx", StringComparison.OrdinalIgnoreCase))
                return "Certificado .pfx configurado";

            return "Sin certificado fiscal";
        }

        private static AsymmetricCipherKeyPair GenerarParRsa2048()
        {
            var generator = new RsaKeyPairGenerator();
            generator.Init(new KeyGenerationParameters(new SecureRandom(), 2048));
            return generator.GenerateKeyPair();
        }

        private static X509Name CrearSubjectAfip(string razonSocial, string commonName, string cuitDigitos)
        {
            var orden = new List<DerObjectIdentifier>
            {
                X509Name.C,
                X509Name.O,
                X509Name.CN,
                X509Name.SerialNumber
            };

            var valores = new Dictionary<DerObjectIdentifier, string>
            {
                [X509Name.C] = "AR",
                [X509Name.O] = razonSocial,
                [X509Name.CN] = commonName,
                [X509Name.SerialNumber] = "CUIT " + cuitDigitos
            };

            return new X509Name(orden, valores);
        }

        private static string ExportarPem(object obj)
        {
            using (var sw = new StringWriter())
            {
                var pemWriter = new PemWriter(sw);
                pemWriter.WriteObject(obj);
                pemWriter.Writer.Flush();
                return sw.ToString();
            }
        }

        private static void GuardarClavePrivadaPem(string ruta, AsymmetricKeyParameter privateKey)
        {
            using (var sw = new StreamWriter(ruta, false, new UTF8Encoding(false)))
            using (var pemWriter = new PemWriter(sw))
            {
                pemWriter.WriteObject(privateKey);
            }
        }

        private static AsymmetricKeyParameter LeerClavePrivadaPem(string ruta)
        {
            using (var reader = File.OpenText(ruta))
            {
                var pemReader = new PemReader(reader);
                object obj = pemReader.ReadObject();
                if (obj is AsymmetricCipherKeyPair pair)
                    return pair.Private;
                if (obj is AsymmetricKeyParameter key)
                    return key;
                throw new InvalidOperationException("El archivo .key no contiene una clave privada RSA válida.");
            }
        }

        private static void ValidarCertificadoCorresponde(string rutaCertificado, string rutaClavePrivada)
        {
            try
            {
                using (var cert = CargarCertificadoConClave(rutaCertificado, rutaClavePrivada))
                {
                    if (!cert.HasPrivateKey)
                        throw new InvalidOperationException("El certificado no pudo vincularse con la clave privada generada.");
                }
            }
            catch (Exception ex) when (!(ex is InvalidOperationException))
            {
                throw new InvalidOperationException(
                    "El certificado .crt no corresponde a la clave privada generada en este sistema. " +
                    "Use el certificado emitido por AFIP/ARCA para el CSR que descargó aquí.", ex);
            }
        }

        private static string AsegurarCarpetaAfip()
        {
            string baseDir = DatabaseService.AsegurarCarpetaDatosSchpos();
            string dir = Path.Combine(baseDir, CarpetaAfip);
            if (!Directory.Exists(dir))
                Directory.CreateDirectory(dir);
            return dir;
        }

        private static void AplicarPermisosRestringidos(string rutaArchivo)
        {
            try
            {
                var identity = WindowsIdentity.GetCurrent();
                if (identity?.User == null) return;

                var reglas = new FileSecurity();
                reglas.AddAccessRule(new FileSystemAccessRule(
                    identity.User,
                    FileSystemRights.FullControl,
                    AccessControlType.Allow));

                reglas.AddAccessRule(new FileSystemAccessRule(
                    new SecurityIdentifier(WellKnownSidType.BuiltinAdministratorsSid, null),
                    FileSystemRights.FullControl,
                    AccessControlType.Allow));

                File.SetAccessControl(rutaArchivo, reglas);
            }
            catch
            {
                // Permisos restringidos son best-effort; no bloquear el flujo en entornos sin ACL.
            }
        }

        private static string LimpiarCuit(string cuit)
        {
            if (string.IsNullOrWhiteSpace(cuit)) return "";
            return new string(cuit.Where(char.IsDigit).ToArray());
        }
    }
}
