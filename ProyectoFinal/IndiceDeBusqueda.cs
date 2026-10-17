using System;
using System.Collections.Generic;

namespace ProyectoFinal
{
    // Índice genérico de búsqueda: a cada clave (un texto ya normalizado) le corresponden uno o más valores.
    // Puede haber más de un valor por clave, por ejemplo los homónimos ("naranja" fruta y "naranja" color).
    // El Traductor lo usa con señas, pero sirve para cualquier tipo.
    public class IndiceDeBusqueda<TValor>
    {
        private readonly Dictionary<string, List<TValor>> _mapa = new(StringComparer.Ordinal);

        public int Cantidad => _mapa.Count;

        public void Agregar(string clave, TValor valor)
        {
            if (string.IsNullOrWhiteSpace(clave))
                return;

            if (!_mapa.TryGetValue(clave, out List<TValor>? valores))
            {
                valores = new List<TValor>();
                _mapa[clave] = valores;
            }

            // Una misma seña puede generar la misma clave dos veces (ej. "Tomar / Beber" y su forma completa)
            if (!valores.Contains(valor))
                valores.Add(valor);
        }

        public bool Contiene(string clave) => _mapa.ContainsKey(clave);

        // Devuelve los valores de la clave (lista vacía si no existe: nunca null)
        public IReadOnlyList<TValor> Buscar(string clave) =>
            _mapa.TryGetValue(clave, out List<TValor>? valores) ? valores : Array.Empty<TValor>();

        // Ordena los valores de cada clave (el primero queda como el "preferido")
        public void Ordenar(Comparison<TValor> comparacion)
        {
            foreach (List<TValor> valores in _mapa.Values)
                valores.Sort(comparacion);
        }

        public IEnumerable<string> Claves => _mapa.Keys;
    }
}
