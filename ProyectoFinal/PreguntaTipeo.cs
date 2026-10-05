namespace ProyectoFinal
{
    // Pregunta donde el usuario debe escribir la palabra exacta
    public class PreguntaTipeo : Pregunta
    {
        public PreguntaTipeo(Sena sena) : base(sena) { }

        // respuesta es el texto tipeado por el usuario
        protected override bool Verificar(string respuesta)
        {
            // Comparación normalizada: sin tildes, sin mayúsculas, sin espacios extra
            string esperada = Sena.NormalizarTexto(Sena.Palabra);
            string recibida = Sena.NormalizarTexto(respuesta);
            return esperada == recibida;
        }
    }
}
