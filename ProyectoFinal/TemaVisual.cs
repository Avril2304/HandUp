using System;
using System.Windows;

namespace ProyectoFinal
{
    // Utilitario que la UI llama al cargar las preferencias para aplicar el tema visual.
    // Los colores viven en Temas/TemaNormal.xaml y Temas/TemaAltoContraste.xaml (mismas claves en los dos).
    // Las vistas usan {DynamicResource Brush...}, así que el cambio se ve en vivo en las ventanas abiertas.
    public static class TemaVisual
    {
        private static readonly Uri UriNormal = new("pack://application:,,,/ProyectoFinal;component/Temas/TemaNormal.xaml");
        private static readonly Uri UriAltoContraste = new("pack://application:,,,/ProyectoFinal;component/Temas/TemaAltoContraste.xaml");

        // Devuelve un ResourceDictionary con los brushes de alto contraste
        public static ResourceDictionary ObtenerDiccionarioAltoContraste() => new() { Source = UriAltoContraste };

        // Devuelve el diccionario normal (para revertir el alto contraste)
        public static ResourceDictionary ObtenerDiccionarioNormal() => new() { Source = UriNormal };

        // Tema que se está viendo ahora (sirve para el interruptor del login, antes de que haya un usuario)
        public static bool AltoContrasteActivo { get; private set; }

        // Activa o desactiva el alto contraste en toda la app.
        // Reemplaza el diccionario de tema actual en lugar de agregar otro encima (si se apilaran, crecerían sin fin).
        public static void Aplicar(bool altoContraste)
        {
            AltoContrasteActivo = altoContraste;
            var nuevo = altoContraste ? ObtenerDiccionarioAltoContraste() : ObtenerDiccionarioNormal();
            var diccionarios = Application.Current.Resources.MergedDictionaries;

            for (int i = 0; i < diccionarios.Count; i++)
            {
                if (diccionarios[i].Contains("BrushBackground"))
                {
                    diccionarios[i] = nuevo;
                    return;
                }
            }

            diccionarios.Add(nuevo);
        }
    }
}
