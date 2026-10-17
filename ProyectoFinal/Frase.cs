using System;
using System.Collections.Generic;
using System.Linq;

namespace ProyectoFinal
{
    // Una frase del banco (ya armada a partir de su plantilla de frases.json):
    // las señas en orden, cómo se dice en español y con qué otras frases se puede confundir.
    public class Frase
    {
        public string PlantillaId { get; }
        public int Nivel { get; }

        // La traducción correcta: "Yo como pizza."
        public string Texto { get; }

        // Lo que se reproduce, en orden (un nombre deletreado aporta una seña por letra)
        public IReadOnlyList<Sena> Senas { get; }

        // Otras traducciones de la misma plantilla ("Yo como empanadas."): son las opciones incorrectas de la pregunta
        public IReadOnlyList<string> Alternativas { get; internal set; } = Array.Empty<string>();

        public Frase(string plantillaId, int nivel, string texto, IReadOnlyList<Sena> senas)
        {
            if (senas.Count == 0)
                throw new ArgumentException("Una frase necesita al menos una seña.", nameof(senas));

            PlantillaId = plantillaId;
            Nivel = nivel;
            Texto = texto;
            Senas = senas;
        }

        // La seña que la hace de su nivel (la primera de ese nivel). La usa la pregunta, que siempre gira en torno a una seña.
        public Sena SenaPrincipal => Senas.FirstOrDefault(s => s.Dificultad == Nivel) ?? Senas[0];

        public override string ToString() => Texto;
    }
}
