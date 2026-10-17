using System;
using System.Globalization;
using System.IO;
using System.Text;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Microsoft.Win32;

namespace ProyectoFinal
{
    // Dibuja el certificado (CertificadoVisual) y lo guarda como PDF o como imagen PNG, sin programas externos.
    // El PDF es una hoja A4 horizontal con el certificado como imagen (lo arma EscribirPdf, a mano).
    public static class CertificadoExportador
    {
        // A4 horizontal en puntos de PDF (1/72 de pulgada)
        private const double AnchoA4 = 842;
        private const double AltoA4 = 595;

        // El certificado a 3 veces su tamaño de pantalla (unos 290 ppp en A4): se imprime nítido
        private const double EscalaArchivo = 3;

        // Dibuja el certificado en una imagen (escala 1 = 1123 x 794 píxeles)
        public static BitmapSource Renderizar(CertificadoHandUp certificado, double escala)
        {
            var visual = new CertificadoVisual(certificado);
            var tamano = new Size(visual.Width, visual.Height);
            visual.Measure(tamano);
            visual.Arrange(new Rect(tamano));
            visual.UpdateLayout();

            var imagen = new RenderTargetBitmap((int)(tamano.Width * escala), (int)(tamano.Height * escala),
                                                96 * escala, 96 * escala, PixelFormats.Pbgra32);
            imagen.Render(visual);
            imagen.Freeze();
            return imagen;
        }

        // Pregunta dónde guardar (PDF o PNG) y lo guarda. Devuelve la ruta, o null si se canceló.
        // Lanza ExportacionException si no se pudo escribir el archivo.
        public static string? GuardarConDialogo(Window? duenio, CertificadoHandUp certificado)
        {
            var dialogo = new SaveFileDialog
            {
                Title = "Guardar el certificado de HandUp",
                FileName = $"Certificado HandUp - {NombreDeArchivo(certificado.Nombre)}",
                Filter = "Documento PDF (*.pdf)|*.pdf|Imagen PNG (*.png)|*.png",
                DefaultExt = ".pdf",
                InitialDirectory = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments)
            };
            if (dialogo.ShowDialog(duenio) != true)
                return null;

            Guardar(certificado, dialogo.FileName);
            return dialogo.FileName;
        }

        // Guarda según la extensión: .png como imagen, cualquier otra como PDF
        public static void Guardar(CertificadoHandUp certificado, string ruta)
        {
            BitmapSource imagen = Renderizar(certificado, EscalaArchivo);
            try
            {
                if (Path.GetExtension(ruta).Equals(".png", StringComparison.OrdinalIgnoreCase))
                {
                    var png = new PngBitmapEncoder();
                    png.Frames.Add(BitmapFrame.Create(imagen));
                    using FileStream archivo = File.Create(ruta);
                    png.Save(archivo);
                }
                else
                {
                    EscribirPdf(ruta, imagen, $"Certificado de HandUp - {certificado.Nombre}");
                }
            }
            catch (Exception ex) when (ex is IOException || ex is UnauthorizedAccessException)
            {
                throw new ExportacionException($"No se pudo guardar el certificado en «{ruta}». Probá en otra carpeta.", ex);
            }
        }

        // "Ana Gómez" -> "Ana Gómez"; saca los caracteres que no puede llevar un nombre de archivo
        private static string NombreDeArchivo(string nombre)
        {
            foreach (char invalido in Path.GetInvalidFileNameChars())
                nombre = nombre.Replace(invalido, ' ');
            return nombre.Trim();
        }

        // ===== PDF =====
        // Un PDF mínimo de una página: la imagen va como JPEG (filtro DCTDecode) y ocupa toda la hoja.
        // Objetos: 1 catálogo, 2 páginas, 3 página, 4 imagen, 5 contenido (dibuja la imagen), 6 datos del documento.
        private static void EscribirPdf(string ruta, BitmapSource imagen, string titulo)
        {
            byte[] jpeg = CodificarJpeg(imagen);
            string contenido = string.Format(CultureInfo.InvariantCulture, "q {0} 0 0 {1} 0 0 cm /Im0 Do Q", AnchoA4, AltoA4);

            using var salida = new MemoryStream();
            var posiciones = new long[7];

            void Escribir(string texto) => salida.Write(Encoding.ASCII.GetBytes(texto));
            void Objeto(int numero, string cuerpo)
            {
                posiciones[numero] = salida.Position;
                Escribir($"{numero} 0 obj\n{cuerpo}\nendobj\n");
            }

            Escribir("%PDF-1.4\n");
            Objeto(1, "<< /Type /Catalog /Pages 2 0 R >>");
            Objeto(2, "<< /Type /Pages /Kids [3 0 R] /Count 1 >>");
            Objeto(3, string.Format(CultureInfo.InvariantCulture,
                "<< /Type /Page /Parent 2 0 R /MediaBox [0 0 {0} {1}] /Resources << /XObject << /Im0 4 0 R >> >> /Contents 5 0 R >>",
                AnchoA4, AltoA4));

            posiciones[4] = salida.Position;
            Escribir($"4 0 obj\n<< /Type /XObject /Subtype /Image /Width {imagen.PixelWidth} /Height {imagen.PixelHeight} " +
                     $"/ColorSpace /DeviceRGB /BitsPerComponent 8 /Filter /DCTDecode /Length {jpeg.Length} >>\nstream\n");
            salida.Write(jpeg);
            Escribir("\nendstream\nendobj\n");

            Objeto(5, $"<< /Length {contenido.Length} >>\nstream\n{contenido}\nendstream");
            Objeto(6, $"<< /Title ({TextoPdf(titulo)}) /Creator (HandUp) /CreationDate (D:{DateTime.Now:yyyyMMddHHmmss}) >>");

            long xref = salida.Position;
            var tabla = new StringBuilder("xref\n0 7\n0000000000 65535 f \n");
            for (int i = 1; i <= 6; i++)
                tabla.Append(posiciones[i].ToString("D10", CultureInfo.InvariantCulture)).Append(" 00000 n \n");
            Escribir(tabla.ToString());
            Escribir($"trailer\n<< /Size 7 /Root 1 0 R /Info 6 0 R >>\nstartxref\n{xref}\n%%EOF\n");

            File.WriteAllBytes(ruta, salida.ToArray());
        }

        private static byte[] CodificarJpeg(BitmapSource imagen)
        {
            // JPEG no tiene transparencia: se pasa a RGB de 24 bits
            var rgb = new FormatConvertedBitmap(imagen, PixelFormats.Bgr24, null, 0);
            var encoder = new JpegBitmapEncoder { QualityLevel = 92 };
            encoder.Frames.Add(BitmapFrame.Create(rgb));
            using var memoria = new MemoryStream();
            encoder.Save(memoria);
            return memoria.ToArray();
        }

        // Los textos de un PDF van entre paréntesis y en ASCII: se escapan \ ( ) y se sacan las tildes
        private static string TextoPdf(string texto)
        {
            string sinTildes = new string(texto.Normalize(NormalizationForm.FormD)
                .Where(c => CharUnicodeInfo.GetUnicodeCategory(c) != UnicodeCategory.NonSpacingMark && c < 128).ToArray());
            return sinTildes.Replace("\\", "\\\\").Replace("(", "\\(").Replace(")", "\\)");
        }
    }
}
