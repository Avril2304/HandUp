using System;
using System.Collections.Generic;
using System.Linq;

namespace ProyectoFinal
{
    // Una regla de seguridad de la contraseña: un texto corto para el checklist, el mensaje de error
    // y la condición (un delegado genérico Func<string, bool>) que dice si la contraseña la cumple.
    public record ReglaPassword(string Descripcion, string MensajeError, Func<string, bool> SeCumple);

    // Reglas que tiene que cumplir una contraseña nueva. Las usan el repositorio (para validar)
    // y la ventana de registro (para mostrar el checklist en vivo), así están definidas en un solo lugar.
    public static class PoliticaPassword
    {
        public const int LongitudMinima = 8;

        public static IReadOnlyList<ReglaPassword> Reglas { get; } = new List<ReglaPassword>
        {
            new($"{LongitudMinima}+ caracteres",
                $"tener al menos {LongitudMinima} caracteres",
                p => p.Length >= LongitudMinima),

            new("Una mayúscula",
                "tener al menos una letra mayúscula",
                p => p.Any(char.IsUpper)),

            new("Sin espacios",
                "no tener espacios",
                p => p.Length > 0 && !p.Any(char.IsWhiteSpace)),
        };

        // Lanza InvalidInputException indicando todas las reglas que no se cumplen
        public static void Validar(string password)
        {
            List<string> incumplidas = Reglas
                .Where(r => !r.SeCumple(password ?? string.Empty))
                .Select(r => r.MensajeError)
                .ToList();

            if (incumplidas.Count == 0)
                return;

            // "tener X, tener Y y no tener Z"
            string detalle = incumplidas.Count == 1
                ? incumplidas[0]
                : $"{string.Join(", ", incumplidas.Take(incumplidas.Count - 1))} y {incumplidas[^1]}";

            throw new InvalidInputException($"La contraseña debe {detalle}.");
        }
    }
}
