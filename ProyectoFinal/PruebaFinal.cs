using System;
using System.Collections.Generic;
using System.Linq;

namespace ProyectoFinal
{
    // Reglas de la prueba final de un nivel (fases 2 y 4 del plan de niveles): 20 preguntas, 12 de frases (60%)
    // y 8 de palabras sueltas. Con PorcentajeParaAprobar o más se desbloquea el nivel siguiente
    // (ver ServicioProgreso.RegistrarPruebaFinal).
    public static class PruebaFinal
    {
        public const int CantidadPreguntas = 20;
        public const int CantidadFrases = 12;
        public const int PorcentajeParaAprobar = 80;

        // Las frases de la prueba: distintas entre sí y repartidas entre las plantillas del nivel
        // (así no salen dos "Yo como…" si hay otras). Lanza BancoFrasesException si no se puede cargar el banco.
        public static List<Frase> ElegirFrases(int nivel, Random random, int cantidad = CantidadFrases) =>
            Seleccion.ElegirRepartido(BancoFrases.Cargar().ObtenerPorNivel(nivel), f => f.PlantillaId, cantidad, random);

        // Las palabras sueltas de la prueba: 'cantidad' señas del nivel, repartidas parejo entre sus categorías.
        // Se dejan afuera los homónimos (NARANJA, PAPA/PAPÁ, UNO, MERIENDA, MAÑANA): con una sola palabra
        // como respuesta no se sabe de cuál de los significados se trata.
        public static List<Sena> ElegirSenas(int nivel, Random random, int cantidad = CantidadPreguntas)
        {
            var repositorio = new RepositorioSenas();

            HashSet<string> homonimos = repositorio.ObtenerTodos()
                .GroupBy(s => s.PalabraNormalizada)
                .Where(g => g.Count() > 1)
                .Select(g => g.Key)
                .ToHashSet();

            IEnumerable<Sena> candidatas = repositorio.BuscarPorDificultad(nivel)
                .Where(s => !homonimos.Contains(s.PalabraNormalizada));

            return Seleccion.ElegirRepartido(candidatas, s => s.CategoriaId, cantidad, random);
        }

        // 17 de 20 -> 85
        public static int Porcentaje(int aciertos, int total) =>
            total == 0 ? 0 : (int)Math.Round(100.0 * aciertos / total, MidpointRounding.AwayFromZero);

        public static bool Aprueba(int porcentaje) => porcentaje >= PorcentajeParaAprobar;

        // La prueba del último nivel (Avanzado) es el examen final de HandUp: no desbloquea nada, da el certificado
        public static bool EsExamenFinal(int nivel) => nivel == Niveles.Nombres.Count;

        // "Prueba final de Básico" / "Examen final de HandUp"
        public static string Nombre(int nivel) =>
            EsExamenFinal(nivel) ? "Examen final de HandUp" : $"Prueba final de {Niveles.NombreDe(nivel)}";
    }

    // Cómo salió una prueba final. MejorAnterior es null si era la primera vez que la rendía.
    // CompletoHandUp: aprobó el examen final por primera vez.
    public record ResultadoPruebaFinal(int Nivel, int Aciertos, int Total, int Porcentaje, int? MejorAnterior, bool Desbloqueo,
                                       bool CompletoHandUp = false)
    {
        public bool Aprobada => PruebaFinal.Aprueba(Porcentaje);
        public int Mejor => Math.Max(Porcentaje, MejorAnterior ?? 0);
    }
}
