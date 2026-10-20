using System;

namespace ProyectoFinal
{
    // Se lanza cuando la guía de uso (Guia/guia.json) falta, no se puede leer o no tiene secciones
    public class GuiaException : HandUpException
    {
        public GuiaException(string message) : base(message)
        {
        }

        public GuiaException(string message, Exception innerException) : base(message, innerException)
        {
        }
    }
}
