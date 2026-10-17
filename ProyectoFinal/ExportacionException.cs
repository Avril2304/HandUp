using System;

namespace ProyectoFinal
{
    // No se pudo generar el video de una traducción (archivo en uso, sin permisos, falta una seña, etc.)
    public class ExportacionException : Exception
    {
        public ExportacionException() { }

        public ExportacionException(string message) : base(message) { }

        public ExportacionException(string message, Exception inner) : base(message, inner) { }
    }
}
