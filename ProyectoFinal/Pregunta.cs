using System;

namespace ProyectoFinal
{
    // Clase base para evaluar las respuestas del usuario
    public abstract class Pregunta
    {
        public Sena Sena { get; }                    // La seña que se está evaluando
        public bool FueRespondida { get; private set; } = false;
        public bool FueCorrecta { get; private set; } = false;

        protected Pregunta(Sena sena)
        {
            Sena = sena ?? throw new ArgumentNullException(nameof(sena));
        }

        // Método polimórfico: cada subclase evalúa la respuesta a su manera
        // Devuelve true si la respuesta es correcta
        public bool EvaluarRespuesta(string respuesta)
        {
            if (FueRespondida)
                throw new InvalidOperationException("Esta pregunta ya fue respondida.");

            FueRespondida = true;
            FueCorrecta = Verificar(respuesta);
            return FueCorrecta;
        }

        // Template method: la lógica específica la implementa cada subclase
        protected abstract bool Verificar(string respuesta);

        public override string ToString() => $"Pregunta sobre: {Sena.Palabra}";
    }
}
