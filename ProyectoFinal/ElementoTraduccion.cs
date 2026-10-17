using System;
using System.Collections.Generic;

namespace ProyectoFinal
{
    public enum TipoElemento
    {
        Sena,       // la palabra (o frase) está en el banco
        Deletreo,   // no está: se deletrea letra por letra (story 15)
        Omitida     // un artículo ("el", "la", "un"...): en LSA no se seña
    }

    // Una palabra (o grupo de palabras) del texto escrito y cómo se dice en señas
    public class ElementoTraduccion
    {
        public TipoElemento Tipo { get; }

        // Como lo escribió el usuario: "Ana", "buenas noches"
        public string Texto { get; }

        // Señas posibles para el texto. Más de una = homónimo (ej. "naranja" fruta y color); la primera es la preferida
        public IReadOnlyList<Sena> Opciones { get; }

        // Letras del deletreo (vacía si no se deletrea)
        public IReadOnlyList<Sena> Letras { get; }

        // Caracteres que no tienen seña en el abecedario (ej. "@"), se avisan en pantalla
        public IReadOnlyList<string> SinSena { get; }

        public int OpcionElegida { get; private set; }

        public bool EsHomonimo => Opciones.Count > 1;
        public Sena? SenaElegida => Tipo == TipoElemento.Sena ? Opciones[OpcionElegida] : null;

        // Lo que se muestra, en orden: la seña elegida, cada letra, o nada si se omite
        public IReadOnlyList<Sena> Pasos => Tipo switch
        {
            TipoElemento.Sena => new[] { Opciones[OpcionElegida] },
            TipoElemento.Deletreo => Letras,
            _ => Array.Empty<Sena>()
        };

        private ElementoTraduccion(TipoElemento tipo, string texto, IReadOnlyList<Sena> opciones,
                                   IReadOnlyList<Sena> letras, IReadOnlyList<string> sinSena)
        {
            Tipo = tipo;
            Texto = texto;
            Opciones = opciones;
            Letras = letras;
            SinSena = sinSena;
        }

        public static ElementoTraduccion ConSena(string texto, IReadOnlyList<Sena> opciones)
        {
            if (opciones.Count == 0)
                throw new ArgumentException("Una palabra con seña necesita al menos una opción.", nameof(opciones));
            return new(TipoElemento.Sena, texto, opciones, Array.Empty<Sena>(), Array.Empty<string>());
        }

        public static ElementoTraduccion Deletreada(string texto, IReadOnlyList<Sena> letras, IReadOnlyList<string> sinSena) =>
            new(TipoElemento.Deletreo, texto, Array.Empty<Sena>(), letras, sinSena);

        public static ElementoTraduccion Omitida(string texto) =>
            new(TipoElemento.Omitida, texto, Array.Empty<Sena>(), Array.Empty<Sena>(), Array.Empty<string>());

        // Homónimos: pasa al siguiente significado (ej. de "Naranja · Colores" a "Naranja · Alimentos")
        public void CambiarOpcion()
        {
            if (EsHomonimo)
                OpcionElegida = (OpcionElegida + 1) % Opciones.Count;
        }
    }
}
