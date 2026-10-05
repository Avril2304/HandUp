using System;
using System.Collections.Generic;
using System.IO;

namespace ProyectoFinal
{
    // Clase base abstracta para todas las señas (Aplica polimorfismo)
    public abstract class Sena
    {
        public int Id { get; set; }
        public string Codigo { get; set; } = string.Empty;        // "sign_036"
        public string Palabra { get; set; } = string.Empty;        // "Hola"
        public string CategoriaId { get; set; } = string.Empty;    // "saludos"
        public int Dificultad { get; set; }                         // 1-4
        public string Descripcion { get; set; } = string.Empty;    // Movimiento de manos
        public string? Instrucciones { get; set; }                  // Pasos separados por \n
        public string RutaImagen { get; set; } = string.Empty;     // Relativa al .exe
        public string? RutaVideo { get; set; }                      // Relativa al .exe, null si no tiene

        // Devuelve la ruta absoluta de la imagen (resolviendo contra el directorio del .exe)
        public string RutaImagenAbsoluta =>
            Path.Combine(AppDomain.CurrentDomain.BaseDirectory, RutaImagen);

        // Devuelve la ruta absoluta del video (null si no tiene)
        public string? RutaVideoAbsoluta =>
            RutaVideo is null ? null : Path.Combine(AppDomain.CurrentDomain.BaseDirectory, RutaVideo);

        // Lista de pasos de instrucciones (vacía si no hay)
        public IReadOnlyList<string> ListaInstrucciones =>
            string.IsNullOrWhiteSpace(Instrucciones)
                ? Array.Empty<string>()
                : Instrucciones.Split('\n', StringSplitOptions.RemoveEmptyEntries);

        // Método polimórfico: cada subclase decide cómo se "reproduce"
        // Devuelve la ruta absoluta del medio a mostrar (imagen o video)
        public abstract string ObtenerRutaMedio();

        // Para normalizar las palabras al comparar respuestas (quita tildes, pasa a minúsculas)
        public string PalabraNormalizada => NormalizarTexto(Palabra);

        // Utilidad estática de normalización para reusar en las preguntas
        public static string NormalizarTexto(string texto)
        {
            if (string.IsNullOrWhiteSpace(texto)) return string.Empty;
            string normalizado = texto.Trim().ToLowerInvariant();
            // Quitar tildes comunes del español
            normalizado = normalizado
                .Replace("á", "a").Replace("é", "e").Replace("í", "i")
                .Replace("ó", "o").Replace("ú", "u").Replace("ü", "u").Replace("ñ", "n");
            return normalizado;
        }

        public override string ToString() => Palabra;
    }
}
