using System;

namespace ProyectoFinal
{
    public class SenaNotFoundException : HandUpException
    {
        public SenaNotFoundException(int id)
            : base($"No se encontró la seña con Id {id}.") { }

        public SenaNotFoundException(string palabra)
            : base($"No se encontró la seña '{palabra}'.") { }
    }
}
