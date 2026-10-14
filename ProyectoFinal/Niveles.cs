using System;
using System.Collections.Generic;
using System.Linq;

namespace ProyectoFinal
{
    // Los 4 niveles de LSA y cuántas señas tiene cada uno.
    // Lo usan el menú, Mi perfil y ServicioProgreso, así la cuenta está en un solo lugar.
    public static class Niveles
    {
        // Respaldo por si la base no responde (cantidades del banco multimedia al 2026-10-14)
        private static readonly (string Nombre, int Cantidad)[] Respaldo =
        {
            ("Principiante", 145),
            ("Básico", 211),
            ("Intermedio", 174),
            ("Avanzado", 110)
        };

        private static IReadOnlyList<(string Nombre, int Cantidad)>? _todos;

        // Se cuentan una sola vez desde las señas importadas: si cambia el banco, los números siguen siendo reales
        public static IReadOnlyList<(string Nombre, int Cantidad)> Todos => _todos ??= ContarEnLaBase();

        private static IReadOnlyList<(string Nombre, int Cantidad)> ContarEnLaBase()
        {
            try
            {
                IReadOnlyList<NivelInfo> niveles = new RepositorioSenas().ObtenerNiveles();

                // Si falta algún nivel (banco vacío o incompleto), las cuentas no serían confiables
                if (niveles.Count == Respaldo.Length)
                    return niveles.Select(n => (n.Nombre, n.CantidadSenas)).ToList();
            }
            catch (DatabaseException)
            {
                // Sin base usamos el respaldo; la app sigue funcionando
            }
            return Respaldo;
        }

        // Avance del perfil dentro de su nivel actual (no del total de la app)
        public static ProgresoNivel ProgresoDe(Perfil perfil)
        {
            int indice = IndiceDe(perfil.Nivel);

            int senasNivelesAnteriores = 0;
            for (int i = 0; i < indice; i++)
                senasNivelesAnteriores += Todos[i].Cantidad;

            int total = Todos[indice].Cantidad;
            int dominadas = Math.Clamp(perfil.SenasDominadas - senasNivelesAnteriores, 0, total);
            string? siguiente = indice + 1 < Todos.Count ? Todos[indice + 1].Nombre : null;

            return new ProgresoNivel(Todos[indice].Nombre, dominadas, total, siguiente);
        }

        // Nivel que corresponde a una cantidad de señas dominadas en total
        public static string SegunSenasDominadas(int dominadas)
        {
            int acumuladas = 0;
            foreach (var (nombre, cantidad) in Todos)
            {
                acumuladas += cantidad;
                if (dominadas < acumuladas)
                    return nombre;
            }
            return Todos[Todos.Count - 1].Nombre;
        }

        // Si el nivel guardado no existe (un dato viejo o editado a mano), se toma Principiante
        private static int IndiceDe(string nivel)
        {
            for (int i = 0; i < Todos.Count; i++)
            {
                if (Todos[i].Nombre == nivel)
                    return i;
            }
            return 0;
        }
    }

    // Cuántas señas dominó el perfil dentro de su nivel y cuál es el nivel que sigue (null si ya está en el último)
    public record ProgresoNivel(string Nivel, int Dominadas, int Total, string? Siguiente)
    {
        public double Proporcion => Total == 0 ? 0 : (double)Dominadas / Total;
    }
}
