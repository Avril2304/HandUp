using System;
using System.Windows;
using System.Windows.Media;

namespace ProyectoFinal
{
    // Barra del tiempo que queda en el Desafío. No usa controles: se dibuja sola en OnRender,
    // que es el "evento Paint" de WPF (en Win32 era WM_PAINT con BeginPaint/EndPaint).
    // Las propiedades están marcadas con AffectsRender: al cambiar Proporcion o un color, WPF invalida
    // el dibujo y vuelve a llamar a OnRender (como InvalidateRect en Win32). También se redibuja al cambiar de tamaño.
    public class BarraTiempo : FrameworkElement
    {
        // Parte que queda del tiempo: 1 = lleno, 0 = vacío
        public static readonly DependencyProperty ProporcionProperty = DependencyProperty.Register(
            nameof(Proporcion), typeof(double), typeof(BarraTiempo),
            new FrameworkPropertyMetadata(1.0, FrameworkPropertyMetadataOptions.AffectsRender, null, LimitarProporcion));

        public static readonly DependencyProperty RellenoProperty = DependencyProperty.Register(
            nameof(Relleno), typeof(Brush), typeof(BarraTiempo),
            new FrameworkPropertyMetadata(Brushes.Teal, FrameworkPropertyMetadataOptions.AffectsRender));

        public static readonly DependencyProperty FondoProperty = DependencyProperty.Register(
            nameof(Fondo), typeof(Brush), typeof(BarraTiempo),
            new FrameworkPropertyMetadata(Brushes.LightGray, FrameworkPropertyMetadataOptions.AffectsRender));

        public double Proporcion
        {
            get => (double)GetValue(ProporcionProperty);
            set => SetValue(ProporcionProperty, value);
        }

        public Brush Relleno
        {
            get => (Brush)GetValue(RellenoProperty);
            set => SetValue(RellenoProperty, value);
        }

        public Brush Fondo
        {
            get => (Brush)GetValue(FondoProperty);
            set => SetValue(FondoProperty, value);
        }

        private static object LimitarProporcion(DependencyObject d, object valor) => Math.Clamp((double)valor, 0, 1);

        // Se llama cada vez que hay que redibujar: primero el fondo entero y encima la parte que queda
        protected override void OnRender(DrawingContext dc)
        {
            double ancho = ActualWidth;
            double alto = ActualHeight;
            double radio = alto / 2;

            dc.DrawRoundedRectangle(Fondo, null, new Rect(0, 0, ancho, alto), radio, radio);

            double lleno = ancho * Proporcion;
            if (lleno > 0)
                dc.DrawRoundedRectangle(Relleno, null, new Rect(0, 0, lleno, alto), radio, radio);
        }
    }
}
