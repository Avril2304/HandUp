using System.Globalization;

namespace ProyectoFinal
{
    // Cómo se muestra la palabra de una seña en pantalla.
    // En el banco las mayúsculas son inconsistentes ("APELLIDO", "Buenas noches", "MIL"): acá se emprolijan.
    public static class TextoSena
    {
        private static readonly TextInfo Espanol = new CultureInfo("es-AR").TextInfo;

        public static string Palabra(Sena sena) => Palabra(sena.Palabra);

        // Para textos sueltos, como las opciones de una pregunta
        public static string Palabra(string texto)
        {
            string palabra = texto.Trim();
            if (palabra.Length == 0)
                return palabra;

            // Letras del abecedario ("A", "CH", "Ñ"): siempre en mayúscula
            if (palabra.Length <= 2)
                return Espanol.ToUpper(palabra);

            // El resto, con mayúscula en la primera letra: "BUENAS NOCHES" -> "Buenas noches",
            // "¿CÓMO ESTÁS?" -> "¿Cómo estás?" (se saltean los signos del principio)
            string minusculas = Espanol.ToLower(palabra);
            int primeraLetra = minusculas.TakeWhile(c => !char.IsLetterOrDigit(c)).Count();
            if (primeraLetra >= minusculas.Length)
                return minusculas;
            return minusculas[..primeraLetra] + Espanol.ToUpper(minusculas[primeraLetra]) + minusculas[(primeraLetra + 1)..];
        }
    }
}
