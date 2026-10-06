using System;
using System.IO;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Media.Imaging;

namespace ProyectoFinal
{
    // Visor reutilizable de una seña (story "ver la imagen o el video de cada seña").
    // La seña decide qué archivo mostrar (ObtenerRutaMedio, polimórfico); el visor solo sabe mostrar
    // una imagen o reproducir un video según el archivo que le toca.
    public partial class VisorSena : UserControl
    {
        private const double VelocidadLenta = 0.5;

        private Sena? _sena;
        private bool _esVideo;
        private bool _pausado;
        private bool _lento;

        public VisorSena()
        {
            InitializeComponent();
        }

        // Muestra una seña nueva (reemplaza la anterior)
        public void Mostrar(Sena sena)
        {
            Detener();
            _sena = sena;
            _pausado = false;

            // Cuando esté B1 (AssetNotFoundException, de Lolo), atraparla también en este catch
            string ruta;
            try
            {
                ruta = sena.ObtenerRutaMedio();
            }
            catch (Exception ex) when (ex is IOException || ex is ArgumentException)
            {
                MostrarError($"No se pudo encontrar el archivo de la seña «{sena.Palabra}».");
                return;
            }

            if (!File.Exists(ruta))
            {
                MostrarError($"Falta el archivo de la seña «{sena.Palabra}». Probá con la siguiente.");
                return;
            }

            _esVideo = Path.GetExtension(ruta).Equals(".mp4", StringComparison.OrdinalIgnoreCase);
            pnlError.Visibility = Visibility.Collapsed;

            if (_esVideo)
                MostrarVideo(ruta);
            else
                MostrarImagen(ruta);
        }

        private void MostrarImagen(string ruta)
        {
            var imagen = new BitmapImage();
            imagen.BeginInit();
            imagen.UriSource = new Uri(ruta, UriKind.Absolute);
            imagen.CacheOption = BitmapCacheOption.OnLoad; // lee el archivo y lo suelta enseguida
            imagen.EndInit();

            imgSena.Source = imagen;
            imgSena.Visibility = Visibility.Visible;
            AutomationProperties.SetName(imgSena, $"Imagen de la seña {_sena?.Palabra}");
            pnlControles.Visibility = Visibility.Collapsed;
        }

        private void MostrarVideo(string ruta)
        {
            videoSena.Source = new Uri(ruta, UriKind.Absolute);
            videoSena.SpeedRatio = _lento ? VelocidadLenta : 1.0;
            videoSena.Visibility = Visibility.Visible;
            AutomationProperties.SetName(videoSena, $"Video de la seña {_sena?.Palabra}");
            videoSena.Play();

            pnlControles.Visibility = Visibility.Visible;
            ActualizarControles();
        }

        private void MostrarError(string mensaje)
        {
            imgSena.Visibility = Visibility.Collapsed;
            videoSena.Visibility = Visibility.Collapsed;
            pnlControles.Visibility = Visibility.Collapsed;
            lblError.Text = mensaje;
            pnlError.Visibility = Visibility.Visible;
            _esVideo = false;
        }

        // Deja de reproducir y libera el archivo (al cambiar de seña o salir de la pantalla)
        public void Detener()
        {
            videoSena.Stop();
            videoSena.Source = null;
            videoSena.Visibility = Visibility.Collapsed;
            imgSena.Source = null;
            imgSena.Visibility = Visibility.Collapsed;
        }

        // ===== Controles (también se llaman desde el teclado, ej. la tecla de Pausar del usuario) =====

        public void AlternarPausa()
        {
            if (!_esVideo)
                return;

            _pausado = !_pausado;
            if (_pausado)
                videoSena.Pause();
            else
                videoSena.Play();
            ActualizarControles();
        }

        public void Repetir()
        {
            if (!_esVideo)
                return;

            videoSena.Position = TimeSpan.Zero;
            videoSena.Play();
            _pausado = false;
            ActualizarControles();
        }

        public void AlternarVelocidad()
        {
            _lento = !_lento;
            if (_esVideo)
                videoSena.SpeedRatio = _lento ? VelocidadLenta : 1.0;
            ActualizarControles();
        }

        private void ActualizarControles()
        {
            lblPausa.Text = _pausado ? "Reproducir" : "Pausar";
            icoPausa.Text = _pausado ? "" : ""; // Play / Pause
            AutomationProperties.SetName(btnPausa, _pausado ? "Reproducir el video" : "Pausar el video");

            lblVelocidad.Text = _lento ? "Normal" : "Más lento";
            AutomationProperties.SetName(btnVelocidad, _lento ? "Volver a la velocidad normal" : "Reproducir más lento");
        }

        // ===== Eventos =====

        private void BtnPausa_Click(object sender, RoutedEventArgs e) => AlternarPausa();
        private void BtnRepetir_Click(object sender, RoutedEventArgs e) => Repetir();
        private void BtnVelocidad_Click(object sender, RoutedEventArgs e) => AlternarVelocidad();

        // Loop: al terminar, vuelve a empezar
        private void VideoSena_MediaEnded(object sender, RoutedEventArgs e)
        {
            videoSena.Position = TimeSpan.Zero;
            videoSena.Play();
        }

        // Si el video no se puede abrir, se intenta con la imagen fija de la seña
        private void VideoSena_MediaFailed(object? sender, ExceptionRoutedEventArgs e)
        {
            string? imagen = _sena?.RutaImagenAbsoluta;
            if (imagen is not null && File.Exists(imagen))
            {
                videoSena.Visibility = Visibility.Collapsed;
                _esVideo = false;
                MostrarImagen(imagen);
            }
            else
            {
                MostrarError($"No se pudo reproducir la seña «{_sena?.Palabra}».");
            }
        }

        private void ImgSena_ImageFailed(object? sender, ExceptionRoutedEventArgs e)
        {
            MostrarError($"No se pudo abrir la imagen de la seña «{_sena?.Palabra}».");
        }

        private void Visor_Unloaded(object sender, RoutedEventArgs e) => Detener();
    }
}
