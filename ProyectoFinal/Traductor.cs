using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;

namespace ProyectoFinal
{
    // Motor del Traductor (stories 14 y 15): pasa un texto en español a una lista de señas.
    //  - Busca primero la frase más larga que esté en el banco ("buenas noches" antes que "buenas").
    //  - Si una palabra no está (ej. un nombre propio), la deletrea con el abecedario.
    //  - CH y Ñ son letras propias del abecedario LSA; LL y RR no, así que se deletrean como dos letras.
    public class Traductor
    {
        private const string CategoriaAbecedario = "abecedario";

        private static readonly CultureInfo Espanol = new("es-AR");

        // En LSA los artículos no se señan: se muestran en la traducción, pero se saltean
        private static readonly HashSet<string> Articulos = new(StringComparer.Ordinal)
        {
            "el", "la", "los", "las", "lo", "un", "una", "unos", "unas"
        };

        // Para los números escritos con cifras ("5" -> seña de "cinco")
        private static readonly Dictionary<int, string> NumerosEnPalabras = new()
        {
            [0] = "cero", [1] = "uno", [2] = "dos", [3] = "tres", [4] = "cuatro", [5] = "cinco",
            [6] = "seis", [7] = "siete", [8] = "ocho", [9] = "nueve", [10] = "diez", [11] = "once",
            [12] = "doce", [13] = "trece", [14] = "catorce", [15] = "quince", [16] = "dieciseis",
            [17] = "diecisiete", [18] = "dieciocho", [19] = "diecinueve", [20] = "veinte", [30] = "treinta",
            [40] = "cuarenta", [50] = "cincuenta", [60] = "sesenta", [70] = "setenta", [80] = "ochenta",
            [90] = "noventa", [100] = "cien", [1000] = "mil", [1000000] = "un millon"
        };

        // Índices genéricos: palabra normalizada -> señas, y letra -> seña del abecedario
        private readonly IndiceDeBusqueda<Sena> _senas = new();
        private readonly IndiceDeBusqueda<Sena> _letras = new();

        // Cantidad máxima de palabras de una entrada del banco (ej. "santiago del estero" = 3)
        private readonly int _maxPalabras;

        public Traductor(IEnumerable<Sena> senas)
        {
            ArgumentNullException.ThrowIfNull(senas);

            foreach (Sena sena in senas)
            {
                if (sena.CategoriaId.Equals(CategoriaAbecedario, StringComparison.OrdinalIgnoreCase))
                {
                    _letras.Agregar(Normalizar(sena.Palabra), sena);
                    continue;
                }

                foreach (string clave in ClavesDe(sena.Palabra))
                    _senas.Agregar(clave, sena);
            }

            // Homónimos (NARANJA, PAPA, UNO, MERIENDA...): primero el significado más básico (el de menor nivel).
            // A igual nivel, el que está primero en el banco. El usuario puede cambiarlo desde la traducción.
            _senas.Ordenar((a, b) => a.Dificultad != b.Dificultad
                ? a.Dificultad.CompareTo(b.Dificultad)
                : a.Id.CompareTo(b.Id));

            _maxPalabras = _senas.Claves.Select(c => c.Split(' ').Length).DefaultIfEmpty(1).Max();
        }

        // Arma el traductor con todas las señas de la base (puede lanzar DatabaseException)
        public static Traductor CrearDesdeBase() => new(new RepositorioSenas().ObtenerTodos());

        public bool TieneAbecedario => _letras.Cantidad > 0;

        // Traduce un texto. Si está vacío devuelve una lista vacía (story 14: un campo vacío no hace nada).
        public IReadOnlyList<ElementoTraduccion> Traducir(string texto)
        {
            var resultado = new List<ElementoTraduccion>();
            if (string.IsNullOrWhiteSpace(texto))
                return resultado;

            // Las palabras como se escribieron (para mostrarlas) y normalizadas (para buscarlas)
            List<string> originales = Regex.Matches(texto, @"[\p{L}\p{N}]+").Select(m => m.Value).ToList();
            List<string> normalizadas = originales.Select(Normalizar).ToList();

            // "él" con tilde es el pronombre (tiene seña); "el" sin tilde es el artículo
            List<bool> esArticulo = originales.Select(o => Articulos.Contains(o.ToLower(Espanol))).ToList();

            int i = 0;
            while (i < normalizadas.Count)
            {
                int largo = BuscarFraseMasLarga(normalizadas, esArticulo, i, out IReadOnlyList<Sena> opciones);
                if (largo > 0)
                {
                    resultado.Add(ElementoTraduccion.ConSena(string.Join(' ', originales.Skip(i).Take(largo)), opciones));
                    i += largo;
                    continue;
                }

                string original = originales[i];
                string palabra = normalizadas[i];

                if (esArticulo[i])
                    resultado.Add(ElementoTraduccion.Omitida(original));
                else if (int.TryParse(palabra, out int numero) && NumerosEnPalabras.TryGetValue(numero, out string? enPalabras)
                         && _senas.Contiene(enPalabras))
                    resultado.Add(ElementoTraduccion.ConSena(original, _senas.Buscar(enPalabras)));
                else
                    resultado.Add(Deletrear(original));

                i++;
            }

            return resultado;
        }

        // Prueba desde la palabra "inicio" la frase más larga del banco; devuelve cuántas palabras ocupa (0 = ninguna)
        private int BuscarFraseMasLarga(List<string> palabras, List<bool> esArticulo, int inicio, out IReadOnlyList<Sena> opciones)
        {
            for (int largo = Math.Min(_maxPalabras, palabras.Count - inicio); largo >= 1; largo--)
            {
                // El artículo suelto no se busca en el banco ("el" encontraría al pronombre "Él").
                // Dentro de una frase sí cuenta: "la escuela", "la pampa", "un millón".
                if (largo == 1 && esArticulo[inicio])
                    break;

                string clave = string.Join(' ', palabras.Skip(inicio).Take(largo));

                opciones = _senas.Buscar(clave);
                if (opciones.Count > 0)
                    return largo;
            }

            opciones = Array.Empty<Sena>();
            return 0;
        }

        // Story 15: deletrea una palabra letra por letra. Las cifras usan la seña de su número.
        public ElementoTraduccion Deletrear(string palabra)
        {
            string texto = Normalizar(palabra).Replace(" ", string.Empty);
            var letras = new List<Sena>();
            var sinSena = new List<string>();

            for (int i = 0; i < texto.Length; i++)
            {
                string letra = texto[i].ToString();

                // CH es una sola letra del abecedario (LL y RR no existen: quedan como dos letras sueltas)
                if (texto[i] == 'c' && i + 1 < texto.Length && texto[i + 1] == 'h' && _letras.Contiene("ch"))
                {
                    letra = "ch";
                    i++;
                }

                Sena? sena = _letras.Buscar(letra).FirstOrDefault();
                if (sena is null && char.IsDigit(texto[i]))
                    sena = _senas.Buscar(NumerosEnPalabras[texto[i] - '0']).FirstOrDefault();

                if (sena is null)
                    sinSena.Add(letra);
                else
                    letras.Add(sena);
            }

            return ElementoTraduccion.Deletreada(palabra, letras, sinSena);
        }

        // Minúsculas, sin tildes ni signos y con un solo espacio entre palabras: "¿Cómo ESTÁS?" -> "como estas".
        // La Ñ se conserva porque es una letra propia (y "año" no es lo mismo que "ano").
        public static string Normalizar(string texto)
        {
            if (string.IsNullOrWhiteSpace(texto))
                return string.Empty;

            string descompuesto = texto.Trim().ToLower(Espanol).Normalize(NormalizationForm.FormD);
            var sb = new StringBuilder(descompuesto.Length);
            foreach (char c in descompuesto)
            {
                if (CharUnicodeInfo.GetUnicodeCategory(c) == UnicodeCategory.NonSpacingMark)
                {
                    if (c == '̃' && sb.Length > 0 && sb[^1] == 'n') // la "virgulilla" de la ñ
                        sb.Append(c);
                    continue;
                }
                sb.Append(char.IsLetterOrDigit(c) ? c : ' ');
            }

            string limpio = sb.ToString().Normalize(NormalizationForm.FormC);
            return Regex.Replace(limpio, @"\s+", " ").Trim();
        }

        // Las formas en que se puede escribir la palabra de una seña del banco
        public static IEnumerable<string> ClavesDe(string palabra)
        {
            // "Mañana (parte del día)" -> "Mañana": la aclaración no se escribe
            string texto = Regex.Replace(palabra, @"\(.*?\)", " ").Trim();

            // "DESPUES 1" / "DESPUES 2" (variantes de la misma palabra) y "PAISES PLURAL" -> la palabra sola
            texto = Regex.Replace(texto, @"\s+([12]|PLURAL)$", string.Empty, RegexOptions.IgnoreCase);

            // Masculino y femenino: "Abogado/a", "ABUELO A", "DIRECTOR RA", "JUEZ A"
            Match genero = Regex.Match(texto, @"^(?<base>.+?)(\s*/\s*|\s+)r?a$", RegexOptions.IgnoreCase);
            if (genero.Success)
            {
                string masculino = genero.Groups["base"].Value;
                string femenino = masculino.EndsWith("o", StringComparison.OrdinalIgnoreCase)
                    ? masculino[..^1] + "a"
                    : masculino + "a";
                return new[] { Normalizar(masculino), Normalizar(femenino) };
            }

            // Alternativas: "Tomar / Beber", "Él / Ella", "Plaza / Parque"
            return texto.Split('/').Select(Normalizar).Where(c => c.Length > 0).Distinct().ToList();
        }
    }
}
