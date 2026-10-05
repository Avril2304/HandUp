using System;
using System.Linq;
using System.Windows.Input;

namespace ProyectoFinal
{
    // Modelo unificado de preferencias del usuario (Accesibilidad y Atajos)
    public class PreferenciasUsuario
    {
        public int PerfilId { get; set; }

        // Accesibilidad visual
        public bool AltoContraste { get; set; } = false;
        public double TamanoFuente { get; set; } = 1.0;  // Multiplicador: 1.0 = normal, 1.5 = grande

        // Atajos de teclado (almacenados como string del enum Key, parseados a Key al cargar)
        public Key TeclaOpcion1 { get; set; } = Key.D1;
        public Key TeclaOpcion2 { get; set; } = Key.D2;
        public Key TeclaOpcion3 { get; set; } = Key.D3;
        public Key TeclaOpcion4 { get; set; } = Key.D4;
        public Key TeclaConfirmar { get; set; } = Key.Return;
        public Key TeclaPausar { get; set; } = Key.Space;

        // Devuelve la tecla asignada a una opción (1-4), para uso genérico en los juegos
        public Key ObtenerTeclaOpcion(int numero) => numero switch
        {
            1 => TeclaOpcion1,
            2 => TeclaOpcion2,
            3 => TeclaOpcion3,
            4 => TeclaOpcion4,
            _ => throw new ArgumentOutOfRangeException(nameof(numero))
        };

        // Verifica que no haya dos acciones con la misma tecla asignada
        public bool TieneConflictos()
        {
            var teclas = new[] { TeclaOpcion1, TeclaOpcion2, TeclaOpcion3, TeclaOpcion4,
                                 TeclaConfirmar, TeclaPausar };
            return teclas.Distinct().Count() != teclas.Length;
        }
    }
}
