using System;
using System.Security.Cryptography;

namespace ProyectoFinal
{
    // Nunca guardamos la contraseña en texto plano: guardamos un hash (PBKDF2 con SHA-256) y una sal aleatoria.
    // La sal hace que dos usuarios con la misma contraseña tengan hashes distintos.
    public static class PasswordHasher
    {
        private const int TamanioSal = 16;      // bytes
        private const int TamanioHash = 32;     // bytes
        private const int Iteraciones = 100_000;

        public static (string Hash, string Sal) Hashear(string password)
        {
            byte[] sal = RandomNumberGenerator.GetBytes(TamanioSal);
            byte[] hash = Rfc2898DeriveBytes.Pbkdf2(password, sal, Iteraciones, HashAlgorithmName.SHA256, TamanioHash);

            return (Convert.ToBase64String(hash), Convert.ToBase64String(sal));
        }

        public static bool Verificar(string password, string hashGuardado, string salGuardada)
        {
            try
            {
                byte[] sal = Convert.FromBase64String(salGuardada);
                byte[] hashEsperado = Convert.FromBase64String(hashGuardado);
                byte[] hashIngresado = Rfc2898DeriveBytes.Pbkdf2(password, sal, Iteraciones, HashAlgorithmName.SHA256, hashEsperado.Length);

                // Comparación en tiempo constante para no dar pistas por el tiempo de respuesta
                return CryptographicOperations.FixedTimeEquals(hashIngresado, hashEsperado);
            }
            catch (FormatException)
            {
                // Hash o sal dañados en la base: tratamos la contraseña como incorrecta
                return false;
            }
        }
    }
}
