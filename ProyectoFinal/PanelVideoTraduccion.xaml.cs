using System.Collections.Generic;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace ProyectoFinal
{
    // Fondo de un cuadro del video exportado. No se muestra en pantalla: se dibuja y se guarda como PNG.
    public partial class PanelVideoTraduccion : UserControl
    {
        public const int Ancho = 1280;
        public const int Alto = 720;

        // Dónde va la seña dentro del cuadro (tiene que coincidir con el recuadro negro del XAML)
        public static readonly Rect ZonaSena = new(32, 90, 820, 540);

        // La frase se dibuja un poco más grande que en pantalla para que se lea bien en el video
        private const double EscalaFrase = 1.15;

        public PanelVideoTraduccion()
        {
            InitializeComponent();
        }

        public void Mostrar(string texto, string ahora, string detalle, string avance, double proporcion,
                            IReadOnlyList<ElementoTraduccion> elementos, int elementoActual, int letraActual)
        {
            lblTexto.Text = $"«{texto}»";
            lblAhora.Text = ahora;
            lblDetalle.Text = detalle;
            lblAvance.Text = avance;
            colAvanceLleno.Width = new GridLength(proporcion, GridUnitType.Star);
            colAvanceVacio.Width = new GridLength(1 - proporcion, GridUnitType.Star);
            FraseEnSenas.Armar(pnlFrase, elementos, elementoActual, letraActual, EscalaFrase);
        }

        // Dibuja el panel (sin necesidad de mostrarlo en una ventana) y lo guarda como PNG
        public void GuardarPng(string ruta)
        {
            Measure(new Size(Ancho, Alto));
            Arrange(new Rect(0, 0, Ancho, Alto));
            UpdateLayout();

            var imagen = new RenderTargetBitmap(Ancho, Alto, 96, 96, PixelFormats.Pbgra32);
            imagen.Render(this);

            var png = new PngBitmapEncoder();
            png.Frames.Add(BitmapFrame.Create(imagen));
            using FileStream archivo = File.Create(ruta);
            png.Save(archivo);
        }
    }
}
