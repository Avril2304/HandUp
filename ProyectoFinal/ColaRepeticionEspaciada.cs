using System;
using System.Collections.Generic;
using System.Linq;

namespace ProyectoFinal
{
    // Cola de repetición espaciada (story del Desafío): decide qué elemento sale después.
    //  - Al empezar, los elementos con más peso (más fallos históricos) tienden a salir primero.
    //  - Uno fallado vuelve a aparecer enseguida (después de DistanciaTrasFallo elementos).
    //  - Uno acertado vuelve más tarde: al final de la cola si tiene peso 1, y antes cuanto más peso tiene.
    // Es genérica: en el Desafío T = Sena y el peso sale de los fallos guardados en ProgresoSenas.
    public class ColaRepeticionEspaciada<T> where T : notnull
    {
        public const int DistanciaTrasFallo = 2;

        private readonly List<T> _cola;
        private readonly Func<T, int> _pesoHistorico;
        private readonly Dictionary<T, int> _fallosEnPartida = new();

        // pesoHistorico: 1 = normal; cuanto más alto, más seguido aparece
        public ColaRepeticionEspaciada(IEnumerable<T> elementos, Func<T, int> pesoHistorico, Random random)
        {
            _pesoHistorico = pesoHistorico;

            // Orden inicial al azar pero sesgado por el peso (muestreo ponderado: clave = U^(1/peso), de mayor a menor)
            _cola = elementos
                .Select(e => (Elemento: e, Clave: Math.Pow(random.NextDouble(), 1.0 / Peso(e))))
                .OrderByDescending(x => x.Clave)
                .Select(x => x.Elemento)
                .ToList();
        }

        public int Cantidad => _cola.Count;

        // Peso actual: el histórico más los fallos de esta partida
        public int Peso(T elemento) =>
            Math.Max(1, _pesoHistorico(elemento)) + _fallosEnPartida.GetValueOrDefault(elemento);

        // Saca el próximo elemento. Después hay que avisar si se acertó o se falló, para volver a ponerlo en la cola.
        // Con la cola vacía lanza InvalidOperationException (igual que Queue<T>.Dequeue): quien la usa tiene que
        // validar antes que haya elementos, porque la cola es genérica y no sabe qué significa quedarse sin ellos.
        public T Siguiente()
        {
            if (_cola.Count == 0)
                throw new InvalidOperationException("La cola de repetición espaciada está vacía.");

            T elemento = _cola[0];
            _cola.RemoveAt(0);
            return elemento;
        }

        public void Acerto(T elemento) =>
            Insertar(elemento, Math.Max(DistanciaTrasFallo + 1, _cola.Count / Peso(elemento)));

        public void Fallo(T elemento)
        {
            _fallosEnPartida[elemento] = _fallosEnPartida.GetValueOrDefault(elemento) + 1;
            Insertar(elemento, DistanciaTrasFallo);
        }

        private void Insertar(T elemento, int posicion) =>
            _cola.Insert(Math.Clamp(posicion, 0, _cola.Count), elemento);
    }
}
