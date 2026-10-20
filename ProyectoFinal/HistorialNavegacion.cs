using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;

namespace ProyectoFinal
{
    // Historial genérico de navegación: una pila con los pasos a los que se puede volver (el último, arriba).
    // Al avanzar se guarda el paso que se deja; Esc (o el botón Volver) saca el último y vuelve a él.
    // Es genérico porque no le importa qué es un paso: en Aprender, T guarda el texto del botón Volver y cómo restaurar la pantalla.
    public class HistorialNavegacion<T>
    {
        private readonly Stack<T> _pasos = new();

        public int Cantidad => _pasos.Count;

        public bool PuedeVolver => _pasos.Count > 0;

        // Se va a otro paso: el que se deja queda guardado para poder volver
        public void Avanzar(T pasoAnterior) => _pasos.Push(pasoAnterior);

        // Saca el último paso guardado. Devuelve false si no hay a dónde volver (ya se está en el primero).
        public bool TryVolver([MaybeNullWhen(false)] out T paso) => _pasos.TryPop(out paso);

        // Mira el último paso guardado sin sacarlo (ej. para el texto del botón Volver)
        public bool TryVerAnterior([MaybeNullWhen(false)] out T paso) => _pasos.TryPeek(out paso);

        // Se olvida todo: se está en el primer paso
        public void Reiniciar() => _pasos.Clear();
    }
}
