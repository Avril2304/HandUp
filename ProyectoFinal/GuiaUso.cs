using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.Json;

namespace ProyectoFinal
{
    // Contenido de la guía de uso (botón "i" del menú y PDF de la presentación).
    // Sale de Guia/guia.json, junto al ejecutable: el mismo archivo lo usa el script que arma el PDF,
    // así la app y el PDF dicen siempre lo mismo.
    public class GuiaUso
    {
        public string Titulo { get; set; } = string.Empty;
        public string Subtitulo { get; set; } = string.Empty;
        public List<SeccionGuia> Secciones { get; set; } = new();

        public static string Carpeta => Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Guia");
        public static string RutaArchivo => Path.Combine(Carpeta, "guia.json");
        public static string RutaPdf => Path.Combine(Carpeta, "HandUp - Guia de uso.pdf");

        // Lanza GuiaException si el archivo falta, está dañado o no tiene secciones
        public static GuiaUso Cargar()
        {
            if (!File.Exists(RutaArchivo))
                throw new GuiaException("No se encontró la guía de uso (Guia/guia.json) junto al ejecutable.");

            try
            {
                string json = File.ReadAllText(RutaArchivo);
                GuiaUso? guia = JsonSerializer.Deserialize<GuiaUso>(json, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

                // Se descartan las secciones sin título: no se podrían mostrar en el índice
                if (guia is not null)
                    guia.Secciones = guia.Secciones.Where(s => !string.IsNullOrWhiteSpace(s.Titulo)).ToList();

                if (guia is null || guia.Secciones.Count == 0)
                    throw new GuiaException("La guía de uso está vacía.");
                return guia;
            }
            catch (Exception ex) when (ex is JsonException || ex is IOException || ex is UnauthorizedAccessException)
            {
                throw new GuiaException("No se pudo leer la guía de uso: el archivo está dañado o no se puede abrir.", ex);
            }
        }
    }

    // Una sección de la guía: qué es, cómo se usa paso a paso, sus atajos y sus capturas
    public class SeccionGuia
    {
        public string Id { get; set; } = string.Empty;
        public string Titulo { get; set; } = string.Empty;
        public string Glifo { get; set; } = "E946";     // código del ícono en Segoe Fluent Icons (por defecto, "información")
        public string Resumen { get; set; } = string.Empty;
        public List<string> Pasos { get; set; } = new();
        public string TituloPasos { get; set; } = "Cómo se usa";  // título de la lista de pasos (en Fuentes es "Fuentes")
        public List<AtajoGuia> Atajos { get; set; } = new();
        public string? Consejo { get; set; }
        public List<CapturaGuia> Capturas { get; set; } = new();

        // "E7BE" -> el carácter del ícono; si el código no es válido, el de información
        public string Icono =>
            int.TryParse(Glifo, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out int codigo)
                ? char.ConvertFromUtf32(codigo)
                : "";
    }

    public class AtajoGuia
    {
        public string Teclas { get; set; } = string.Empty;
        public string Accion { get; set; } = string.Empty;
    }

    public class CapturaGuia
    {
        public string Archivo { get; set; } = string.Empty;
        public string Pie { get; set; } = string.Empty;

        public string RutaAbsoluta => Path.Combine(GuiaUso.Carpeta, "capturas", Archivo);
    }
}
