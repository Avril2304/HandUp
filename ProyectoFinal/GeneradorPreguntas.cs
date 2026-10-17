using System;
using System.Collections.Generic;
using System.Linq;

namespace ProyectoFinal
{
    // Fábrica que genera preguntas apoyándose en el repositorio de señas para buscar distractores
    public class GeneradorPreguntas
    {
        private readonly RepositorioSenas _repoSenas;
        private readonly Random _random = new();

        public GeneradorPreguntas(RepositorioSenas repoSenas)
        {
            _repoSenas = repoSenas ?? throw new ArgumentNullException(nameof(repoSenas));
        }

        // Genera una pregunta de opción múltiple para la seña dada
        public PreguntaOpcionMultiple CrearOpcionMultiple(Sena sena, string? categoriaId = null)
        {
            // Obtener 3 distractores aleatorios de la misma categoría
            var distractores = _repoSenas.ObtenerAleatorias(
                cantidad: 3,
                categoriaId: categoriaId ?? sena.CategoriaId,
                dificultad: null,
                excluirIds: new HashSet<int> { sena.Id }
            );

            // Si no hay 3 distractores en la categoría (caso raro), buscar de cualquier categoría
            if (distractores.Count < 3)
            {
                distractores = _repoSenas.ObtenerAleatorias(3, null, null, new HashSet<int> { sena.Id });
            }

            // Armar las 4 opciones con la correcta en posición aleatoria
            var opciones = new List<string>(distractores.Select(d => d.Palabra));
            int posicionCorrecta = _random.Next(0, opciones.Count + 1);
            opciones.Insert(posicionCorrecta, sena.Palabra);

            // Asegurar que haya exactamente 4 opciones por si sobraron
            while (opciones.Count > 4) opciones.RemoveAt(opciones.Count - 1);

            return new PreguntaOpcionMultiple(sena, opciones.AsReadOnly(), posicionCorrecta);
        }

        // Genera una pregunta de tipeo para la seña dada
        public PreguntaTipeo CrearTipeo(Sena sena) => new(sena);

        // Genera una pregunta de frase: la traducción correcta y 3 incorrectas de la misma plantilla.
        // Se prefieren las que cambian menos palabras ("Mi mamá cocina pizza" / "Mi tía cocina pizza"): así hay que mirar
        // bien cada seña. Entre las que cambian lo mismo, se elige al azar.
        public PreguntaFrase CrearFrase(Frase frase)
        {
            var opciones = frase.Alternativas
                .Where(a => a != frase.Texto)
                .Distinct()
                .OrderBy(a => PalabrasDistintas(frase.Texto, a))
                .ThenBy(_ => _random.Next())
                .Take(3)
                .ToList();
            if (opciones.Count < 3)
                throw new ArgumentException("La frase necesita al menos 3 opciones incorrectas.", nameof(frase));

            int posicionCorrecta = _random.Next(0, opciones.Count + 1);
            opciones.Insert(posicionCorrecta, frase.Texto);
            return new PreguntaFrase(frase, opciones.AsReadOnly(), posicionCorrecta);
        }

        // Cuántas palabras tiene una frase que la otra no (y viceversa): "Yo como pizza" / "Yo como pan" -> 2
        private static int PalabrasDistintas(string a, string b)
        {
            var palabrasA = a.Split(' ', StringSplitOptions.RemoveEmptyEntries).Select(p => p.Trim('.', ',', '¿', '?')).ToHashSet();
            var palabrasB = b.Split(' ', StringSplitOptions.RemoveEmptyEntries).Select(p => p.Trim('.', ',', '¿', '?')).ToHashSet();
            palabrasA.SymmetricExceptWith(palabrasB);
            return palabrasA.Count;
        }
    }
}
