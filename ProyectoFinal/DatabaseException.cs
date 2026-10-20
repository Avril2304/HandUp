using System;

namespace ProyectoFinal
{
    // Envuelve cualquier error de SQLite para que las ventanas solo tengan que conocer excepciones propias
    public class DatabaseException : HandUpException
    {
        public DatabaseException(string message) : base(message)
        {
        }

        public DatabaseException(string message, Exception innerException) : base(message, innerException)
        {
        }
    }
}
