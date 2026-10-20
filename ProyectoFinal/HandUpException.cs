using System;

namespace ProyectoFinal
{
    // Base de todas las excepciones propias de HandUp. Las ventanas pueden atrapar cualquier error
    // del dominio con un solo catch (HandUpException) y dejar pasar los errores inesperados del sistema.
    public abstract class HandUpException : Exception
    {
        protected HandUpException() { }

        protected HandUpException(string message) : base(message) { }

        protected HandUpException(string message, Exception innerException) : base(message, innerException) { }
    }
}
