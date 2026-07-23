using System;
using System.Security.Cryptography;
using System.Text;

namespace SchettiniGestion
{
    /// <summary>
    /// Cifrado en reposo de la contraseña del certificado (.pfx) ARCA mediante DPAPI.
    /// Las filas antiguas en texto plano siguen funcionando hasta el próximo guardado.
    /// </summary>
    internal static class AfipCertPasswordDpapi
    {
        private const string Prefix = "DPAPI1:";

        public static string Encode(string plainPassword)
        {
            if (string.IsNullOrEmpty(plainPassword))
                return "";
            byte[] bytes = Encoding.UTF8.GetBytes(plainPassword);
            byte[] encrypted = ProtectedData.Protect(bytes, null, DataProtectionScope.CurrentUser);
            return Prefix + Convert.ToBase64String(encrypted);
        }

        public static string Decode(string storedFromDb)
        {
            if (string.IsNullOrEmpty(storedFromDb))
                return "";

            if (!storedFromDb.StartsWith(Prefix, StringComparison.Ordinal))
                return storedFromDb;

            try
            {
                byte[] blob = Convert.FromBase64String(storedFromDb.Substring(Prefix.Length));
                byte[] decrypted = ProtectedData.Unprotect(blob, null, DataProtectionScope.CurrentUser);
                return Encoding.UTF8.GetString(decrypted);
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException(
                    "No se pudo usar la contraseña del certificado ARCA guardada. " +
                    "Si cambió el usuario de Windows o restauró una copia de la base desde otra máquina, " +
                    "vuelva a ingresar la contraseña del .pfx en Configuración.", ex);
            }
        }
    }
}
