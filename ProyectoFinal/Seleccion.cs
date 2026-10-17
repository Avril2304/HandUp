using System;
using System.Collections.Generic;
using System.Linq;

namespace ProyectoFinal
{
    // Utilidades genéricas para elegir elementos al azar
    public static class Seleccion
    {
        // Elige 'cantidad' elementos al azar repartidos lo más parejo posible entre sus grupos:
        // toma de a uno por grupo, por turnos, hasta completar. Si no alcanzan, devuelve todos los que hay.
        // Sirve para cualquier tipo: en la prueba final, señas (T = Sena) agrupadas por categoría (TGrupo = string).
        public static List<T> ElegirRepartido<T, TGrupo>(IEnumerable<T> elementos, Func<T, TGrupo> grupoDe,
                                                         int cantidad, Random random) where TGrupo : notnull
        {
            List<Queue<T>> colas = elementos
                .GroupBy(grupoDe)
                .Select(grupo => new Queue<T>(grupo.OrderBy(_ => random.Next())))
                .OrderBy(_ => random.Next())
                .ToList();

            var elegidos = new List<T>(cantidad);
            while (elegidos.Count < cantidad && colas.Count > 0)
            {
                foreach (Queue<T> cola in colas.ToList())
                {
                    if (elegidos.Count == cantidad)
                        break;

                    elegidos.Add(cola.Dequeue());
                    if (cola.Count == 0)
                        colas.Remove(cola);
                }
            }

            // Mezclamos para que no aparezcan siempre en el mismo orden de categorías
            return elegidos.OrderBy(_ => random.Next()).ToList();
        }
    }
}
