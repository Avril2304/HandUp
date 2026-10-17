using System;

namespace ProyectoFinal
{
    // Se lanza cuando se le pide la siguiente seña a un mazo que no tiene ninguna (ej. una categoría vacía en el Desafío)
    public class MazoVacioException : Exception
    {
        public MazoVacioException()
            : base("No hay señas para armar la partida.") { }
    }
}
