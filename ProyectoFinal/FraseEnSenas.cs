using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Media;

namespace ProyectoFinal
{
    // Dibuja la frase traducida sobre fondo oscuro: cada palabra con su tipo, la actual resaltada y,
    // si se está deletreando, la letra actual marcada. La usan la pantalla completa del Traductor
    // y el video exportado, así se ven igual.
    public static class FraseEnSenas
    {
        private static readonly Brush TextoClaro = Congelado(0xF1, 0xEF, 0xE8);
        private static readonly Brush TextoApagado = Congelado(0xB4, 0xB2, 0xA9);
        private static readonly Brush BordeNormal = Congelado(0x5F, 0x5E, 0x5A);
        private static readonly Brush BordeActual = Congelado(0x5D, 0xCA, 0xA5);
        private static readonly Brush FondoActual = Congelado(0x08, 0x50, 0x41);

        // escala: 1 en pantalla; el video exportado usa la misma proporción con otro tamaño de letra
        public static void Armar(Panel destino, IReadOnlyList<ElementoTraduccion> elementos,
                                 int elementoActual, int letraActual, double escala = 1)
        {
            destino.Children.Clear();
            for (int i = 0; i < elementos.Count; i++)
            {
                ElementoTraduccion elemento = elementos[i];
                bool actual = i == elementoActual;

                var titulo = new TextBlock { Text = elemento.Texto, FontSize = 17 * escala, FontWeight = FontWeights.Bold, Foreground = TextoClaro };
                var detalle = new TextBlock
                {
                    FontSize = 13 * escala,
                    Foreground = TextoApagado,
                    TextWrapping = TextWrapping.Wrap,
                    Margin = new Thickness(0, 2 * escala, 0, 0)
                };

                switch (elemento.Tipo)
                {
                    case TipoElemento.Sena:
                        detalle.Text = elemento.EsHomonimo ? $"Seña ({elemento.OpcionElegida + 1} de {elemento.Opciones.Count})" : "Seña";
                        break;

                    case TipoElemento.Deletreo:
                        // Las letras separadas por guiones, con la que se está mostrando resaltada
                        for (int j = 0; j < elemento.Letras.Count; j++)
                        {
                            if (j > 0)
                                detalle.Inlines.Add(new Run("-"));
                            var letra = new Run(TextoSena.Palabra(elemento.Letras[j]));
                            if (actual && j == letraActual)
                            {
                                letra.Foreground = BordeActual;
                                letra.FontWeight = FontWeights.Bold;
                                letra.TextDecorations = TextDecorations.Underline;
                            }
                            detalle.Inlines.Add(letra);
                        }
                        break;

                    default:
                        detalle.Text = "No se seña";
                        break;
                }

                var contenido = new StackPanel();
                contenido.Children.Add(titulo);
                contenido.Children.Add(detalle);

                destino.Children.Add(new Border
                {
                    Child = contenido,
                    Background = actual ? FondoActual : Brushes.Transparent,
                    BorderBrush = actual ? BordeActual : BordeNormal,
                    BorderThickness = new Thickness((actual ? 2 : 1.5) * escala),
                    CornerRadius = new CornerRadius(10 * escala),
                    Padding = new Thickness(12 * escala, 7 * escala, 12 * escala, 7 * escala),
                    Margin = new Thickness(0, 0, 8 * escala, 8 * escala),
                    Opacity = elemento.Tipo == TipoElemento.Omitida ? 0.5 : 1
                });
            }
        }

        // Los pinceles congelados se pueden compartir y usar fuera de la ventana (ej. al dibujar el video)
        private static Brush Congelado(byte r, byte g, byte b)
        {
            var pincel = new SolidColorBrush(Color.FromRgb(r, g, b));
            pincel.Freeze();
            return pincel;
        }
    }
}
