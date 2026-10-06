using System.Globalization;

namespace ProyectoFinal
{
    // Cómo se muestra la palabra de una seña en pantalla.
    // En el banco las mayúsculas son inconsistentes ("APELLIDO", "Buenas noches", "MIL"): acá se emprolijan.
    public static class TextoSena
    {
        private static readonly TextInfo Espanol = new CultureInfo("es-AR").TextInfo;

        public static string Palabra(Sena sena)
        {
            string palabra = sena.Palabra.Trim();

            // Letras del abecedario ("A", "CH", "Ñ"): siempre en mayúscula
            if (palabra.Length <= 2)
                return Espanol.ToUpper(palabra);

            // El resto, con mayúscula inicial: "BUENAS NOCHES" -> "Buenas noches"
            string minusculas = Espanol.ToLower(palabra);
            return Espanol.ToUpper(minusculas[0]) + minusculas[1..];
        }
    }
}
