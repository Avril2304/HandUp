using System;
using System.Collections.Generic;
using System.Linq;

namespace ProyectoFinal
{
    // Reglas de la prueba final de un nivel (fase 2 del plan de niveles, por ahora solo con palabras).
    // Con PorcentajeParaAprobar o más se desbloquea el nivel siguiente (ver ServicioProgreso.RegistrarPruebaFinal).
    public static class PruebaFinal
    {
        public const int CantidadPreguntas = 20;
        public const int PorcentajeParaAprobar = 80;

        // Las señas de la prueba: CantidadPreguntas del nivel, repartidas parejo entre sus categorías.
        // Se dejan afuera los homónimos (NARANJA, PAPA/PAPÁ, UNO, MERIENDA, MAÑANA): con una sola palabra
        // como respuesta no se sabe de cuál de los significados se trata.
        public static List<Sena> ElegirSenas(int nivel, Random random)
        {
            var repositorio = new RepositorioSenas();

            HashSet<string> homonimos = repositorio.ObtenerTodos()
                .GroupBy(s => s.PalabraNormalizada)
                .Where(g => g.Count() > 1)
                .Select(g => g.Key)
                .ToHashSet();

            IEnumerable<Sena> candidatas = repositorio.BuscarPorDificultad(nivel)
                .Where(s => !homonimos.Contains(s.PalabraNormalizada));

            return Seleccion.ElegirRepartido(candidatas, s => s.CategoriaId, CantidadPreguntas, random);
        }

        // 17 de 20 -> 85
        public static int Porcentaje(int aciertos, int total) =>
            total == 0 ? 0 : (int)Math.Round(100.0 * aciertos / total, MidpointRounding.AwayFromZero);

        public static bool Aprueba(int porcentaje) => porcentaje >= PorcentajeParaAprobar;
    }

    // Cómo salió una prueba final. MejorAnterior es null si era la primera vez que la rendía.
    public record ResultadoPruebaFinal(int Nivel, int Aciertos, int Total, int Porcentaje, int? MejorAnterior, bool Desbloqueo)
    {
        public bool Aprobada => PruebaFinal.Aprueba(Porcentaje);
        public int Mejor => Math.Max(Porcentaje, MejorAnterior ?? 0);
    }
}
