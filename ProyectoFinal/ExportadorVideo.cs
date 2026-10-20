using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using Windows.Media.Editing;
using Windows.Media.MediaProperties;
using Windows.Media.Transcoding;
using Windows.Storage;
using RectVideo = Windows.Foundation.Rect;

namespace ProyectoFinal
{
    // Avance de la exportación: porcentaje (0 a 100) y qué se está haciendo
    public class ProgresoExportacionEventArgs : EventArgs
    {
        public double Porcentaje { get; }
        public string Etapa { get; }

        public ProgresoExportacionEventArgs(double porcentaje, string etapa)
        {
            Porcentaje = porcentaje;
            Etapa = etapa;
        }
    }

    // Exporta una traducción como video MP4 (1280x720): la seña a la izquierda y, a la derecha, el mismo panel
    // que la pantalla completa del Traductor (palabra actual, frase completa y avance).
    // Usa el editor de video que trae Windows (Windows.Media.Editing), así que no hace falta instalar nada:
    //  - Pista principal: el fondo de cada paso (PanelVideoTraduccion guardado como PNG), lo que dure ese paso.
    //  - Capa superpuesta: la seña de cada paso (video, o imagen fija durante "segundosPorImagen").
    public class ExportadorVideo
    {
        // Ojo: mientras Windows genera el video, este evento llega desde otro hilo
        public event EventHandler<ProgresoExportacionEventArgs>? ProgresoCambiado;

        // Dibujar los fondos es la primera parte del trabajo (0 a 30%); generar el video, el resto
        private const double PorcentajePreparacion = 30;

        public async Task ExportarAsync(string texto, IReadOnlyList<ElementoTraduccion> elementos, string rutaSalida,
                                        double segundosPorImagen, CancellationToken cancelacion)
        {
            // Se copia la secuencia al empezar: si después cambia la traducción en pantalla, el video no cambia
            var pasos = new List<(int Elemento, int Letra, Sena Sena)>();
            for (int i = 0; i < elementos.Count; i++)
            {
                IReadOnlyList<Sena> senas = elementos[i].Pasos;
                for (int j = 0; j < senas.Count; j++)
                    pasos.Add((i, j, senas[j]));
            }

            if (pasos.Count == 0)
                throw new ExportacionException("No hay señas para exportar.");

            string carpetaTemporal = Path.Combine(Path.GetTempPath(), "HandUp_video_" + Guid.NewGuid().ToString("N"));
            bool salidaCreada = false;

            try
            {
                Directory.CreateDirectory(carpetaTemporal);
                List<string> fondos = await DibujarFondosAsync(texto, elementos, pasos, carpetaTemporal, cancelacion);

                var composicion = new MediaComposition();
                var capaSenas = new MediaOverlayLayer();
                TimeSpan inicio = TimeSpan.Zero;

                for (int i = 0; i < pasos.Count; i++)
                {
                    cancelacion.ThrowIfCancellationRequested();

                    // Si falta el archivo de una seña, ObtenerRutaMedio lanza AssetNotFoundException.
                    // GetFullPath deja las barras como "\": las rutas del banco usan "/" y el editor de Windows no las acepta.
                    string rutaMedio = Path.GetFullPath(pasos[i].Sena.ObtenerRutaMedio());
                    bool esVideo = Path.GetExtension(rutaMedio).Equals(".mp4", StringComparison.OrdinalIgnoreCase);
                    StorageFile archivoMedio = await StorageFile.GetFileFromPathAsync(rutaMedio);

                    MediaClip sena;
                    TimeSpan duracion;
                    (double ancho, double alto) tamano;
                    if (esVideo)
                    {
                        sena = await MediaClip.CreateFromFileAsync(archivoMedio);
                        duracion = sena.OriginalDuration;
                        VideoEncodingProperties propiedades = sena.GetVideoEncodingProperties();
                        tamano = (propiedades.Width, propiedades.Height);
                    }
                    else
                    {
                        duracion = TimeSpan.FromSeconds(segundosPorImagen);
                        sena = await MediaClip.CreateFromImageFileAsync(archivoMedio, duracion);
                        tamano = TamanoImagen(rutaMedio);
                    }

                    StorageFile fondo = await StorageFile.GetFileFromPathAsync(fondos[i]);
                    composicion.Clips.Add(await MediaClip.CreateFromImageFileAsync(fondo, duracion));
                    capaSenas.Overlays.Add(new MediaOverlay(sena) { Position = Encajar(tamano), Delay = inicio });
                    inicio += duracion;
                }
                composicion.OverlayLayers.Add(capaSenas);

                // MP4 en 720p, sin audio (las señas no tienen sonido)
                MediaEncodingProfile perfil = MediaEncodingProfile.CreateMp4(VideoEncodingQuality.HD720p);
                perfil.Audio = null;

                try
                {
                    File.Create(rutaSalida).Dispose(); // StorageFile solo abre archivos que ya existen
                    salidaCreada = true;
                }
                catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
                {
                    throw new ExportacionException(
                        "No se pudo guardar el video en esa ubicación. Probá con otra carpeta o cerrá el archivo si está abierto.", ex);
                }
                StorageFile salida = await StorageFile.GetFileFromPathAsync(rutaSalida);

                var operacion = composicion.RenderToFileAsync(salida, MediaTrimmingPreference.Precise, perfil);
                operacion.Progress = (op, avance) =>
                    Avisar(PorcentajePreparacion + avance * (100 - PorcentajePreparacion) / 100, "Generando el video");

                TranscodeFailureReason resultado;
                using (cancelacion.Register(() => operacion.Cancel()))
                    resultado = await operacion;

                if (resultado != TranscodeFailureReason.None)
                    throw new ExportacionException($"Windows no pudo generar el video ({resultado}).");

                Avisar(100, "Listo");
            }
            catch (OperationCanceledException)
            {
                if (salidaCreada)
                    BorrarSinFallar(rutaSalida);
                throw;
            }
            catch (AssetNotFoundException ex)
            {
                throw new ExportacionException($"Falta el archivo de una seña, así que no se pudo armar el video. {ex.Message}", ex);
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                if (salidaCreada)
                    BorrarSinFallar(rutaSalida);
                throw new ExportacionException("No se pudo leer o escribir uno de los archivos del video.", ex);
            }
            catch (COMException ex)
            {
                if (salidaCreada)
                    BorrarSinFallar(rutaSalida);
                throw new ExportacionException("El editor de video de Windows no pudo generar el archivo.", ex);
            }
            finally
            {
                try
                {
                    Directory.Delete(carpetaTemporal, recursive: true);
                }
                catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
                {
                    // No es grave: es una carpeta temporal
                    RegistroErrores.Registrar(ex, "Borrar la carpeta temporal del video exportado");
                }
            }
        }

        // Un PNG por paso con el panel lateral de ese momento. Se cede el control entre dibujo y dibujo
        // para que la pantalla no se congele con frases largas.
        private async Task<List<string>> DibujarFondosAsync(string texto, IReadOnlyList<ElementoTraduccion> elementos,
                                                             List<(int Elemento, int Letra, Sena Sena)> pasos,
                                                             string carpeta, CancellationToken cancelacion)
        {
            var panel = new PanelVideoTraduccion();
            var fondos = new List<string>();

            for (int i = 0; i < pasos.Count; i++)
            {
                cancelacion.ThrowIfCancellationRequested();
                var (elementoIndice, letraIndice, sena) = pasos[i];
                ElementoTraduccion elemento = elementos[elementoIndice];

                string detalle = elemento.Tipo == TipoElemento.Deletreo
                    ? $"Deletreando «{elemento.Texto}» · letra {letraIndice + 1} de {elemento.Letras.Count}"
                    : $"«{elemento.Texto}»";

                panel.Mostrar(texto, TextoSena.Palabra(sena), detalle, $"SEÑA {i + 1} DE {pasos.Count}",
                              (double)(i + 1) / pasos.Count, elementos, elementoIndice, letraIndice);

                string ruta = Path.Combine(carpeta, $"fondo_{i:D4}.png");
                panel.GuardarPng(ruta);
                fondos.Add(ruta);

                Avisar(PorcentajePreparacion * (i + 1) / pasos.Count, "Preparando las señas");
                await Dispatcher.Yield(DispatcherPriority.Background);
            }

            return fondos;
        }

        // Ubica la seña dentro de su zona sin deformarla (centrada, con bordes negros si hace falta)
        private static RectVideo Encajar((double Ancho, double Alto) tamano)
        {
            var zona = PanelVideoTraduccion.ZonaSena;
            if (tamano.Ancho <= 0 || tamano.Alto <= 0)
                return new RectVideo(zona.X, zona.Y, zona.Width, zona.Height);

            double escala = Math.Min(zona.Width / tamano.Ancho, zona.Height / tamano.Alto);
            double ancho = tamano.Ancho * escala, alto = tamano.Alto * escala;
            return new RectVideo(zona.X + (zona.Width - ancho) / 2, zona.Y + (zona.Height - alto) / 2, ancho, alto);
        }

        // Lee solo el encabezado de la imagen para saber su tamaño
        private static (double, double) TamanoImagen(string ruta)
        {
            try
            {
                BitmapFrame imagen = BitmapFrame.Create(new Uri(ruta), BitmapCreateOptions.DelayCreation, BitmapCacheOption.None);
                return (imagen.PixelWidth, imagen.PixelHeight);
            }
            catch (Exception ex) when (ex is IOException or NotSupportedException or FileFormatException)
            {
                return (0, 0); // se usa toda la zona
            }
        }

        private void Avisar(double porcentaje, string etapa) =>
            ProgresoCambiado?.Invoke(this, new ProgresoExportacionEventArgs(Math.Clamp(porcentaje, 0, 100), etapa));

        private static void BorrarSinFallar(string ruta)
        {
            try
            {
                File.Delete(ruta);
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                // Si no se puede borrar el archivo a medio hacer, se deja
                RegistroErrores.Registrar(ex, "Borrar el video a medio exportar");
            }
        }
    }
}
