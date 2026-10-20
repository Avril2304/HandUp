using System;
using System.Collections.Generic;
using System.Globalization;
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
            catch (DatabaseException ex)
            {
                // Sin base usamos el respaldo; la app sigue funcionando
                RegistroErrores.Registrar(ex, "Contar las señas de cada nivel");
            }
            return Respaldo;
        }

        // Nombres de los 4 niveles, en orden. No dependen de la base: los usa Perfil para pasar del número al nombre.
        public static IReadOnlyList<string> Nombres { get; } = Respaldo.Select(n => n.Nombre).ToArray();

        // "Básico" -> 2. No distingue mayúsculas ni tildes; si el nivel no existe (un dato viejo o editado a mano) se toma Principiante.
        public static int NumeroDe(string? nivel)
        {
            for (int i = 0; i < Nombres.Count; i++)
            {
                if (string.Compare(Nombres[i], nivel?.Trim(), CultureInfo.InvariantCulture,
                        CompareOptions.IgnoreCase | CompareOptions.IgnoreNonSpace) == 0)
                    return i + 1;
            }
            return 1;
        }

        // 2 -> "Básico"
        public static string NombreDe(int numero) => Nombres[Math.Clamp(numero, 1, Nombres.Count) - 1];

        // Avance del perfil dentro de su nivel actual: señas de ese nivel que ya dominó (según ProgresoSenas)
        public static ProgresoNivel ProgresoDe(Perfil perfil)
        {
            int indice = perfil.NivelDesbloqueado - 1;
            int total = Todos[indice].Cantidad;

            int dominadas = 0;
            try
            {
                dominadas = new RepositorioPerfiles().ContarDominadasDelNivel(perfil.Id, perfil.NivelDesbloqueado);
            }
            catch (DatabaseException ex)
            {
                // Sin base mostramos la barra vacía; la app sigue funcionando
                RegistroErrores.Registrar(ex, "Contar las señas dominadas del nivel");
            }

            string? siguiente = indice + 1 < Todos.Count ? Todos[indice + 1].Nombre : null;
            return new ProgresoNivel(Todos[indice].Nombre, Math.Clamp(dominadas, 0, total), total, siguiente);
        }
    }

    // Cuántas señas dominó el perfil dentro de su nivel y cuál es el nivel que sigue (null si ya está en el último)
    public record ProgresoNivel(string Nivel, int Dominadas, int Total, string? Siguiente)
    {
        public double Proporcion => Total == 0 ? 0 : (double)Dominadas / Total;
    }
}
