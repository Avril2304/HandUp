using System;
using System.Collections.Generic;

namespace ProyectoFinal
{
    // Pregunta de frase (prueba final): se reproducen las señas de la frase una tras otra
    // y se elige su traducción entre 4. Las incorrectas cambian 1 o 2 palabras ("Yo como pizza" / "Yo como empanadas").
    public class PreguntaFrase : PreguntaOpcionMultiple
    {
        public Frase Frase { get; }

        public PreguntaFrase(Frase frase, IReadOnlyList<string> opciones, int indiceCorrecta)
            : base((frase ?? throw new ArgumentNullException(nameof(frase))).SenaPrincipal, opciones, indiceCorrecta)
        {
            Frase = frase;
        }

        public override string ToString() => $"Pregunta sobre la frase: {Frase.Texto}";
    }
}
