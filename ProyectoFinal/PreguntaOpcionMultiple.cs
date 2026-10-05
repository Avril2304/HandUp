using System;
using System.Collections.Generic;

namespace ProyectoFinal
{
    // Pregunta donde el usuario elige entre 4 opciones dadas
    public class PreguntaOpcionMultiple : Pregunta
    {
        // Las 4 opciones (una es la correcta)
        public IReadOnlyList<string> Opciones { get; }

        // Índice (0-3) de la opción correcta dentro de Opciones
        public int IndiceCorrecta { get; }

        public PreguntaOpcionMultiple(Sena sena, IReadOnlyList<string> opciones, int indiceCorrecta)
            : base(sena)
        {
            if (opciones.Count != 4)
                throw new ArgumentException("Debe haber exactamente 4 opciones.");
            if (indiceCorrecta < 0 || indiceCorrecta >= opciones.Count)
                throw new ArgumentOutOfRangeException(nameof(indiceCorrecta));

            Opciones = opciones;
            IndiceCorrecta = indiceCorrecta;
        }

        // respuesta es el índice elegido ("0", "1", "2", "3")
        protected override bool Verificar(string respuesta)
        {
            if (int.TryParse(respuesta, out int indice))
                return indice == IndiceCorrecta;
            return false;
        }
    }
}
