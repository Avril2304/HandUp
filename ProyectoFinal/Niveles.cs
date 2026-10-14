using System;
using System.Collections.Generic;

namespace ProyectoFinal
{
    // Los 4 niveles de LSA y cuántas señas tiene cada uno según el banco multimedia (ver README).
    // Lo usan el menú, Mi perfil y ServicioProgreso, así la cuenta está en un solo lugar.
    public static class Niveles
    {
        public static readonly IReadOnlyList<(string Nombre, int Cantidad)> Todos = new[]
        {
            ("Principiante", 117),
            ("Básico", 185),
            ("Intermedio", 154),
            ("Avanzado", 107)
        };

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
