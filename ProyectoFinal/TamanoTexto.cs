using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Media;

namespace ProyectoFinal
{
    // Tamaño del texto (accesibilidad). Agranda toda la ventana con un LayoutTransform (como un zoom):
    // así crecen las letras, los íconos, los botones y los espacios, no solo los textos.
    // Se guarda en PreferenciasUsuario.TamanoFuente y avisa con un evento para que las ventanas abiertas se actualicen.
    public static class TamanoTexto
    {
        public static IReadOnlyList<(string Nombre, double Escala)> Opciones { get; } = new[]
        {
            ("Normal", 1.0),
            ("Grande", 1.15),
            ("Muy grande", 1.3)
        };

        public static double Escala { get; private set; } = 1.0;

        public static event EventHandler? Cambio;

        // Un valor guardado que no es una de las opciones (ej. editado a mano) se lleva a la más cercana
        public static double Normalizar(double valor) =>
            double.IsNaN(valor) || valor <= 0
                ? 1.0
                : Opciones.OrderBy(o => Math.Abs(o.Escala - valor)).First().Escala;

        public static void Establecer(double escala)
        {
            double nueva = Normalizar(escala);
            if (nueva == Escala)
                return;

            Escala = nueva;
            Cambio?.Invoke(null, EventArgs.Empty);
        }

        // Aplica el tamaño actual al contenido de una ventana
        public static void AplicarA(Window ventana)
        {
            if (ventana.Content is FrameworkElement raiz)
                raiz.LayoutTransform = Escala == 1.0 ? Transform.Identity : new ScaleTransform(Escala, Escala);
        }

        // El tamaño base agrandado en la misma proporción que el texto es el mínimo de la ventana (así nada queda cortado),
        // sin pasarse de la pantalla. En una ventana que se puede agrandar, si el usuario ya la hizo más grande se respeta.
        // Si está maximizada no hace falta ajustar el tamaño: el contenido se acomoda solo.
        public static void AjustarTamanoVentana(Window ventana, double anchoBase, double altoBase)
        {
            Rect pantalla = SystemParameters.WorkArea;
            double anchoMinimo = Math.Min(anchoBase * Escala, pantalla.Width);
            double altoMinimo = Math.Min(altoBase * Escala, pantalla.Height);
            ventana.MinWidth = anchoMinimo;
            ventana.MinHeight = altoMinimo;

            if (ventana.WindowState != WindowState.Normal)
                return;

            bool redimensionable = ventana.ResizeMode is ResizeMode.CanResize or ResizeMode.CanResizeWithGrip;
            ventana.Width = redimensionable ? Math.Min(Math.Max(ventana.Width, anchoMinimo), pantalla.Width) : anchoMinimo;
            ventana.Height = redimensionable ? Math.Min(Math.Max(ventana.Height, altoMinimo), pantalla.Height) : altoMinimo;

            // Que no quede cortada fuera de la pantalla (antes de mostrarse todavía no tiene posición: la centra Windows)
            if (!double.IsNaN(ventana.Left) && !double.IsNaN(ventana.Top))
            {
                ventana.Left = Math.Max(pantalla.Left, Math.Min(ventana.Left, pantalla.Right - ventana.Width));
                ventana.Top = Math.Max(pantalla.Top, Math.Min(ventana.Top, pantalla.Bottom - ventana.Height));
            }
        }
    }
}
