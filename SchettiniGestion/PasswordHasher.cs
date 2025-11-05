// --- ¡ARCHIVO 100% NUEVO! ---
// Usamos las librerías de criptografía de .NET
using System;
using System.Security.Cryptography;

namespace SchettiniGestion
{
    public static class PasswordHasher
    {
        // Define cuántas iteraciones usará el algoritmo. Más es más seguro, pero más lento.
        // 10000 es un buen balance para 2024.
        private const int Iterations = 10000;
        private const int SaltSize = 16; // 16 bytes (128 bits)
        private const int HashSize = 32; // 32 bytes (256 bits)

        /// <summary>
        /// Crea un hash (con salt) a partir de una contraseña.
        /// </summary>
        /// <param name="password">La contraseña en texto plano.</param>
        /// <returns>Un string que combina iteraciones, salt y hash.</returns>
        public static string HashPassword(string password)
        {
            // 1. Crear el "salt" (una clave aleatoria única para este hash)
            byte[] salt;
            using (var rng = new RNGCryptoServiceProvider())
            {
                rng.GetBytes(salt = new byte[SaltSize]);
            }

            // 2. Crear el hash usando el algoritmo PBKDF2
            var pbkdf2 = new Rfc2898DeriveBytes(password, salt, Iterations);
            byte[] hash = pbkdf2.GetBytes(HashSize);

            // 3. Combinar todo en un solo string para guardarlo
            // Formato: [Iteraciones]:[Salt en Base64]:[Hash en Base64]
            return $"{Iterations}:{Convert.ToBase64String(salt)}:{Convert.ToBase64String(hash)}";
        }

        /// <summary>
        /// Verifica una contraseña en texto plano contra un hash guardado.
        /// </summary>
        /// <param name="password">La contraseña en texto plano (la que escribe el usuario).</param>
        /// <param name="hashedPassword">El hash guardado en la base de datos.</param>
        /// <returns>True si la contraseña es correcta, False si no lo es.</returns>
        public static bool VerifyPassword(string password, string hashedPassword)
        {
            try
            {
                // 1. Separar el hash guardado en sus 3 partes
                var parts = hashedPassword.Split(':');
                if (parts.Length != 3)
                {
                    // El hash está corrupto o no tiene el formato esperado
                    return false;
                }

                int iterations = int.Parse(parts[0]);
                byte[] salt = Convert.FromBase64String(parts[1]);
                byte[] hash = Convert.FromBase64String(parts[2]);

                // 2. Generar un hash nuevo usando la contraseña y el MISMO salt
                var pbkdf2 = new Rfc2898DeriveBytes(password, salt, iterations);
                byte[] testHash = pbkdf2.GetBytes(hash.Length);

                // 3. Comparar los dos hashes byte por byte
                // (Una comparación de tiempo constante para evitar "timing attacks")
                uint diff = (uint)hash.Length ^ (uint)testHash.Length;
                for (int i = 0; i < hash.Length && i < testHash.Length; i++)
                {
                    diff |= (uint)(hash[i] ^ testHash[i]);
                }

                // Si diff es 0, los hashes son idénticos
                return diff == 0;
            }
            catch
            {
                // Si algo falla (ej: formato Base64 inválido), la contraseña es incorrecta.
                return false;
            }
        }
    }
}
