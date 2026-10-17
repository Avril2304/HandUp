using System.Collections.Generic;

namespace ProyectoFinal
{
    // Las secciones a las que se puede navegar desde el menú principal
    public enum Seccion
    {
        Inicio,
        Aprender,
        Desafio,
        Traductor,
        Diccionario,
        MiPerfil,
        Configuracion,
        Guia            // guía de uso: botón "i" del inicio, "Guía" en la barra lateral o F1
    }

    // Datos de presentación de cada sección: título, ícono (glifo de Segoe Fluent Icons) y descripción
    public record InfoSeccion(string Titulo, string Glifo, string Descripcion);

    public static class Secciones
    {
        // Colección genérica: a cada sección le corresponde su información de presentación
        public static readonly IReadOnlyDictionary<Seccion, InfoSeccion> Info = new Dictionary<Seccion, InfoSeccion>
        {
            [Seccion.Aprender] = new("Aprender", "",
                "Vas a elegir un nivel y una categoría, y practicar seña por seña con imagen o video."),
            [Seccion.Desafio] = new("Desafío", "",
                "Una partida contrarreloj con vidas, puntaje y racha de aciertos."),
            [Seccion.Traductor] = new("Traductor", "",
                "Vas a escribir una palabra o frase y ver cómo se dice en señas, o deletreada letra por letra."),
            [Seccion.Diccionario] = new("Diccionario", "",
                "Un buscador de todas las señas, con filtros y la descripción del movimiento de manos."),
            [Seccion.MiPerfil] = new("Mi perfil", "",
                "Tus datos, tus estadísticas y tus preferencias."),
            [Seccion.Configuracion] = new("Configuración", "",
                "Accesibilidad (alto contraste) y atajos de teclado para responder."),
        };
    }
}
