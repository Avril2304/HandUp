using System;

namespace ProyectoFinal
{
    // Se lanza cuando el banco de señas (signs_database.json) falta, no se puede leer o no tiene datos válidos
    public class BancoSenasException : HandUpException
    {
        public BancoSenasException(string message) : base(message)
        {
        }

        public BancoSenasException(string message, Exception innerException) : base(message, innerException)
        {
        }
    }
}
