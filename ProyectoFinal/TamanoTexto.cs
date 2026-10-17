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

        // Agranda (o achica) la ventana en la misma proporción que el texto, sin pasarse de la pantalla.
        // Si está maximizada no hace falta: el contenido se acomoda solo.
        public static void AjustarTamanoVentana(Window ventana, double anchoBase, double altoBase)
        {
            if (ventana.WindowState != WindowState.Normal)
                return;

            Rect pantalla = SystemParameters.WorkArea;
            ventana.Width = Math.Min(anchoBase * Escala, pantalla.Width);
            ventana.Height = Math.Min(altoBase * Escala, pantalla.Height);

            // Que no quede cortada fuera de la pantalla (antes de mostrarse todavía no tiene posición: la centra Windows)
            if (!double.IsNaN(ventana.Left) && !double.IsNaN(ventana.Top))
            {
                ventana.Left = Math.Max(pantalla.Left, Math.Min(ventana.Left, pantalla.Right - ventana.Width));
                ventana.Top = Math.Max(pantalla.Top, Math.Min(ventana.Top, pantalla.Bottom - ventana.Height));
            }
        }
    }
}
