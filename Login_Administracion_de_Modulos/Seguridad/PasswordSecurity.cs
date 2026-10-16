using System;
using System.Security.Cryptography;
using System.Text;

namespace Login_Administracion_de_Modulos.Seguridad
{
    public static class PasswordSecurity
    {
        private const int IteracionesDefault = 100_000;
        private const int SaltSizeBytes = 16;
        private const int HashSizeBytes = 32;

        /// <summary>
        /// Genera un hash criptográficamente seguro utilizando PBKDF2 con HMAC-SHA256,
        /// salt aleatorio de 16 bytes y 100,000 iteraciones.
        /// Formato resultante: PBKDF2$iteraciones$saltBase64$hashBase64
        /// </summary>
        public static string HashearClave(string clavePlana)
        {
            if (string.IsNullOrEmpty(clavePlana))
            {
                throw new ArgumentException("La clave plana no puede ser nula ni vacía.", nameof(clavePlana));
            }

            byte[] salt = RandomNumberGenerator.GetBytes(SaltSizeBytes);
            byte[] hash = Rfc2898DeriveBytes.Pbkdf2(
                Encoding.UTF8.GetBytes(clavePlana),
                salt,
                IteracionesDefault,
                HashAlgorithmName.SHA256,
                HashSizeBytes
            );

            return $"PBKDF2${IteracionesDefault}${Convert.ToBase64String(salt)}${Convert.ToBase64String(hash)}";
        }

        /// <summary>
        /// Verifica si la clave ingresada coincide con la clave cifrada almacenada en la base de datos.
        /// Si la clave ingresada o la almacenada son nulas o vacías, devuelve SIEMPRE false.
        /// Utiliza CryptographicOperations.FixedTimeEquals para prevenir ataques de temporización (timing attacks).
        ///
        /// IMPORTANTE: el modo "legacy" (texto plano / hash simulado) es EXCLUSIVO de
        /// desarrollo y debe pasarse como false desde producción. Nunca habilitarlo
        /// en un deploy real: permitiría contraseñas sin hashear.
        /// </summary>
        public static bool VerificarClave(string? claveIngresada, string? claveCifradaDb, bool permitirLegacyDesarrollo = false)
        {
            // CRÍTICO: Si claveIngresada o claveCifradaDb es null o vacía, devuelve siempre false.
            if (string.IsNullOrEmpty(claveIngresada) || string.IsNullOrEmpty(claveCifradaDb))
            {
                return false;
            }

            // 1. Verificación del formato estándar PBKDF2 (PBKDF2$iteraciones$salt$hash)
            if (claveCifradaDb.StartsWith("PBKDF2$", StringComparison.OrdinalIgnoreCase))
            {
                var partes = claveCifradaDb.Split('$');
                if (partes.Length == 4 && int.TryParse(partes[1], out int iteraciones))
                {
                    try
                    {
                        byte[] salt = Convert.FromBase64String(partes[2]);
                        byte[] expectedHash = Convert.FromBase64String(partes[3]);

                        byte[] computedHash = Rfc2898DeriveBytes.Pbkdf2(
                            Encoding.UTF8.GetBytes(claveIngresada),
                            salt,
                            iteraciones,
                            HashAlgorithmName.SHA256,
                            expectedHash.Length
                        );

                        return CryptographicOperations.FixedTimeEquals(computedHash, expectedHash);
                    }
                    catch
                    {
                        return false;
                    }
                }
            }

            // 2. Compatibilidad con contraseñas de desarrollo / legacy si está habilitado
            if (permitirLegacyDesarrollo)
            {
                // Validación para hashes simulados de pruebas o texto plano temporal de desarrollo
                if (string.Equals(claveCifradaDb, claveIngresada, StringComparison.Ordinal))
                {
                    return true;
                }

                if (string.Equals(claveCifradaDb, "$2b$10$HashSimuladoDe" + claveIngresada, StringComparison.Ordinal))
                {
                    return true;
                }
            }

            return false;
        }
    }
}
