using System;
using System.IO;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Input;
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
        private bool _mediaListo;
        private bool _rebobinando;
        private bool _enPantallaCompleta;

        // Se terminó una vuelta del video (antes de volver a empezar). El Traductor lo usa para pasar a la seña siguiente.
        public event EventHandler? VideoTerminado;

        // Tecla que pausa el video dentro de la pantalla completa (la vista que usa el visor le pasa la del usuario)
        public Key TeclaPausar { get; set; } = Key.Space;

        // Si alguien escucha este evento, el botón de ampliar lo avisa en vez de abrir la pantalla completa común.
        // El Traductor lo usa para abrir su propia pantalla completa, que sigue la secuencia de señas.
        public event EventHandler? PantallaCompletaSolicitada;

        public bool PermitirPantallaCompleta { get; set; } = true;

        // Botones Pausar / Repetir / Más lento debajo del video (el Traductor usa los suyos, que manejan toda la secuencia)
        public bool MostrarControles { get; set; } = true;

        // En Práctica, la prueba final y el Desafío: el nombre accesible no dice qué seña es (sería decir la respuesta)
        public bool OcultarPalabra { get; set; }

        private string NombreAccesible(string tipo) =>
            OcultarPalabra ? $"{tipo} de la seña a adivinar" : $"{tipo} de la seña {_sena?.Palabra}";

        // Para los mensajes de error: con OcultarPalabra tampoco se nombra la seña
        private string DeLaSena => OcultarPalabra ? "de esta seña" : $"de la seña «{_sena?.Palabra}»";

        // Si lo que se está mostrando es un video (false si es una imagen o un aviso de error)
        public bool MuestraVideo => _esVideo;

        public bool EstaPausado => _pausado;

        // Velocidad lenta activada o no (se conserva al entrar y salir de pantalla completa)
        public bool Lento
        {
            get => _lento;
            set
            {
                if (_lento == value)
                    return;

                _lento = value;
                if (_esVideo)
                {
                    // Cambiar SpeedRatio hace que el video vuelva al principio: guardamos la posición y la restauramos
                    TimeSpan posicion = videoSena.Position;
                    videoSena.SpeedRatio = _lento ? VelocidadLenta : 1.0;
                    videoSena.Position = posicion;
                }
                ActualizarControles();
            }
        }

        public VisorSena()
        {
            InitializeComponent();
        }

        // Lo usa la ventana de pantalla completa: el botón de ampliar pasa a ser el de salir
        public void ModoPantallaCompleta()
        {
            _enPantallaCompleta = true;
            icoAmpliar.Text = ""; // BackToWindow
            btnAmpliar.ToolTip = "Salir de pantalla completa (Esc)";
            AutomationProperties.SetName(btnAmpliar, "Salir de pantalla completa");
        }

        // Muestra una seña nueva (reemplaza la anterior)
        public void Mostrar(Sena sena)
        {
            ArgumentNullException.ThrowIfNull(sena);
            Detener();
            _sena = sena;
            _pausado = false;

            // ObtenerRutaMedio ya verifica que el archivo exista (y si falta el video, devuelve la imagen).
            // Si no hay ningún archivo, lanza AssetNotFoundException.
            string ruta;
            try
            {
                ruta = sena.ObtenerRutaMedio();
            }
            catch (AssetNotFoundException)
            {
                MostrarError($"Falta el archivo {DeLaSena}. Probá con la siguiente.");
                return;
            }
            catch (ArgumentException)
            {
                MostrarError($"La ruta del archivo {DeLaSena} no es válida.");
                return;
            }

            _esVideo = Path.GetExtension(ruta).Equals(".mp4", StringComparison.OrdinalIgnoreCase);
            pnlError.Visibility = Visibility.Collapsed;
            btnAmpliar.Visibility = PermitirPantallaCompleta ? Visibility.Visible : Visibility.Collapsed;

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
            AutomationProperties.SetName(imgSena, NombreAccesible("Imagen"));
            pnlControles.Visibility = Visibility.Collapsed;
        }

        private void MostrarVideo(string ruta)
        {
            _mediaListo = false;
            _rebobinando = false;
            videoSena.Source = new Uri(ruta, UriKind.Absolute);
            videoSena.SpeedRatio = _lento ? VelocidadLenta : 1.0;
            videoSena.Visibility = Visibility.Visible;
            AutomationProperties.SetName(videoSena, NombreAccesible("Video"));
            videoSena.Play();

            pnlControles.Visibility = MostrarControles ? Visibility.Visible : Visibility.Collapsed;
            ActualizarControles();
        }

        private void MostrarError(string mensaje)
        {
            imgSena.Visibility = Visibility.Collapsed;
            videoSena.Visibility = Visibility.Collapsed;
            pnlControles.Visibility = Visibility.Collapsed;
            btnAmpliar.Visibility = _enPantallaCompleta ? Visibility.Visible : Visibility.Collapsed;
            lblError.Text = mensaje;
            pnlError.Visibility = Visibility.Visible;
            _esVideo = false;
        }

        // Deja de reproducir y libera el archivo (al cambiar de seña o salir de la pantalla)
        public void Detener()
        {
            _esVideo = false;
            _mediaListo = false;
            _rebobinando = false;
            try
            {
                videoSena.Stop();
            }
            catch { }

            videoSena.Source = null;
            videoSena.Visibility = Visibility.Collapsed;
            imgSena.Source = null;
            imgSena.Visibility = Visibility.Collapsed;
            if (!_enPantallaCompleta)
                btnAmpliar.Visibility = Visibility.Collapsed;
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

            _rebobinando = false;
            videoSena.Position = TimeSpan.Zero;
            videoSena.Play();
            _pausado = false;
            ActualizarControles();
        }

        public void AlternarVelocidad() => Lento = !_lento;

        // Pone el foco en Pausar si es un video; si es una imagen, en el botón de ampliar / salir
        public void EnfocarControles()
        {
            if (pnlControles.Visibility == Visibility.Visible)
                btnPausa.Focus();
            else
                btnAmpliar.Focus();
        }

        // Abre la misma seña en una ventana a pantalla completa, con los mismos controles.
        // Mientras tanto el video de acá queda en pausa, y al volver sigue donde estaba.
        public void AbrirPantallaCompleta()
        {
            if (_sena is null)
                return;

            bool estabaReproduciendo = _esVideo && !_pausado;
            if (estabaReproduciendo)
                AlternarPausa();

            var ventana = new VisorPantallaCompletaWindow(_sena, _lento, TeclaPausar, OcultarPalabra)
            {
                Owner = Window.GetWindow(this)
            };
            ventana.ShowDialog();

            // Si cambió la velocidad en pantalla completa, la mantenemos
            Lento = ventana.Lento;
            if (estabaReproduciendo && _pausado)
                AlternarPausa();
            btnAmpliar.Focus();
        }

        private void ActualizarControles()
        {
            lblPausa.Text = _pausado ? "Reproducir" : "Pausar";
            icoPausa.Text = _pausado ? "" : ""; // Play / Pause
            AutomationProperties.SetName(btnPausa, _pausado ? "Reproducir el video" : "Pausar el video");

            lblVelocidad.Text = _lento ? "Normal" : "Más lento";
            AutomationProperties.SetName(btnVelocidad, _lento ? "Volver a la velocidad normal" : "Reproducir más lento");

            // El tooltip dice lo mismo que el botón (sirve sobre todo en el modo compacto, cuando solo se ve el ícono)
            btnPausa.ToolTip = AutomationProperties.GetName(btnPausa);
            btnRepetir.ToolTip = AutomationProperties.GetName(btnRepetir);
            btnVelocidad.ToolTip = AutomationProperties.GetName(btnVelocidad);

            // "Reproducir" y "Normal" ocupan distinto que "Pausar" y "Más lento": se vuelve a ver si entran
            AjustarControlesAlAncho(ActualWidth);
        }

        // En un lugar angosto (ej. la ficha del Diccionario o el Desafío) los botones muestran solo el ícono,
        // para no quedar cortados. El nombre accesible y el tooltip siguen diciendo qué hace cada uno.
        private void Visor_SizeChanged(object sender, SizeChangedEventArgs e) => AjustarControlesAlAncho(e.NewSize.Width);

        private void AjustarControlesAlAncho(double disponible)
        {
            if (disponible <= 0)
                return; // todavía no se dibujó

            AplicarModoControles(compacto: false);
            if (AnchoControles() > disponible)
                AplicarModoControles(compacto: true);
        }

        // Lo que ocupan los tres botones con su texto (se mide cada uno, así funciona aunque el panel esté oculto)
        private double AnchoControles()
        {
            double ancho = 0;
            foreach (Button boton in new[] { btnPausa, btnRepetir, btnVelocidad })
            {
                boton.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
                ancho += boton.DesiredSize.Width + boton.Margin.Left + boton.Margin.Right;
            }
            return ancho;
        }

        private void AplicarModoControles(bool compacto)
        {
            Visibility textos = compacto ? Visibility.Collapsed : Visibility.Visible;
            lblPausa.Visibility = textos;
            lblRepetir.Visibility = textos;
            lblVelocidad.Visibility = textos;

            btnPausa.MinWidth = compacto ? 56 : 96;
            btnRepetir.MinWidth = compacto ? 56 : 96;
            btnVelocidad.MinWidth = compacto ? 56 : 112;
        }

        // ===== Eventos =====

        private void BtnPausa_Click(object sender, RoutedEventArgs e) => AlternarPausa();
        private void BtnRepetir_Click(object sender, RoutedEventArgs e) => Repetir();
        private void BtnVelocidad_Click(object sender, RoutedEventArgs e) => AlternarVelocidad();

        private void BtnAmpliar_Click(object sender, RoutedEventArgs e)
        {
            if (_enPantallaCompleta)
                Window.GetWindow(this)?.Close();
            else if (PantallaCompletaSolicitada is not null)
                PantallaCompletaSolicitada.Invoke(this, EventArgs.Empty);
            else
                AbrirPantallaCompleta();
        }

        private void VideoSena_MediaOpened(object sender, RoutedEventArgs e)
        {
            // Por las dudas arrancamos siempre desde el principio
            videoSena.Position = TimeSpan.Zero;
            _mediaListo = true;

            if (_esVideo && !_pausado)
            {
                videoSena.SpeedRatio = _lento ? VelocidadLenta : 1.0;
                videoSena.Play();
            }
        }

        // Loop: al terminar, vuelve a empezar
        private void VideoSena_MediaEnded(object sender, RoutedEventArgs e)
        {
            if (!_mediaListo || _rebobinando || !_esVideo || _pausado || videoSena.Source is null)
                return;

            // Evitamos falsos disparos al inicio de la carga: solo rebobinamos si el video realmente tiene duración y avanzó
            if (videoSena.NaturalDuration.HasTimeSpan && videoSena.Position >= TimeSpan.FromMilliseconds(500))
            {
                _rebobinando = true;
                videoSena.Position = TimeSpan.Zero;
                videoSena.Play();

                Dispatcher.BeginInvoke(System.Windows.Threading.DispatcherPriority.Background, async () =>
                {
                    await System.Threading.Tasks.Task.Delay(350);
                    _rebobinando = false;
                });

                // Se avisa al final: quien escucha puede mostrar otra seña sin pisar el loop de esta
                VideoTerminado?.Invoke(this, EventArgs.Empty);
            }
        }

        // Si el video no se puede abrir, se intenta con la imagen fija de la seña
        private void VideoSena_MediaFailed(object? sender, ExceptionRoutedEventArgs e)
        {
            System.Diagnostics.Debug.WriteLine($"VisorSena MediaFailed para '{_sena?.Palabra}': {e.ErrorException?.Message}");
            string? imagen = _sena?.RutaImagenAbsoluta;
            if (imagen is not null && File.Exists(imagen))
            {
                videoSena.Visibility = Visibility.Collapsed;
                _esVideo = false;
                MostrarImagen(imagen);
            }
            else
            {
                MostrarError($"No se pudo reproducir el video {DeLaSena}.");
            }
        }

        private void ImgSena_ImageFailed(object? sender, ExceptionRoutedEventArgs e)
        {
            MostrarError($"No se pudo abrir la imagen {DeLaSena}.");
        }

        private void Visor_Unloaded(object sender, RoutedEventArgs e) => Detener();
    }
}
