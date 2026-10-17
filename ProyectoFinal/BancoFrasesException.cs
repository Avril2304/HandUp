using System;

namespace ProyectoFinal
{
    // Se lanza cuando el banco de frases (frases.json) falta, no se puede leer o no tiene frases válidas
    public class BancoFrasesException : Exception
    {
        public BancoFrasesException(string message) : base(message)
        {
        }

        public BancoFrasesException(string message, Exception innerException) : base(message, innerException)
        {
        }
    }
}
