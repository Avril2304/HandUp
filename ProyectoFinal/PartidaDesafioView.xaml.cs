using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Automation.Peers;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Threading;

namespace ProyectoFinal
{
    // Pantalla de una partida del Desafío (stories: contrarreloj con vidas, racha y repetición espaciada).
    // Las reglas están en PartidaDesafio; acá van los timers, las opciones y el marcador.
    // Se responde con un solo toque (sin Confirmar) para que sea ágil: tecla de la opción o clic.
    public partial class PartidaDesafioView : UserControl
    {
        // Terminó (o no se pudo armar) y se quiere volver a elegir nivel y categoría
        public event EventHandler? CambiarCategoriaSolicitada;

        private static readonly TimeSpan PausaTrasAcierto = TimeSpan.FromMilliseconds(900);
        private static readonly TimeSpan PausaTrasFallo = TimeSpan.FromMilliseconds(1800); // más larga: para ver cuál era

        private readonly IReadOnlyList<Sena> _senas;
        private IReadOnlyDictionary<int, (int Aciertos, int Fallos)> _historial;
        private readonly Perfil _perfil;
        private readonly PreferenciasUsuario _preferencias;
        private readonly ServicioProgreso _servicio;
        private readonly GeneradorPreguntas _generador = new(new RepositorioSenas());
        private readonly Random _random = new();

        // Timers de la materia: el reloj de la partida (cada 1 s) y la pausa para ver el resultado de cada respuesta
        private readonly DispatcherTimer _reloj = new() { Interval = TimeSpan.FromSeconds(1) };
        private readonly DispatcherTimer _pausa = new();

        private PartidaDesafio _partida = null!;
        private PreguntaOpcionMultiple? _pregunta;
        private readonly List<Button> _botonesOpcion = new();
        private int _mejorRachaAntes;
        private bool _avisoGuardado;

        public PartidaDesafioView(IReadOnlyList<Sena> senas, IReadOnlyDictionary<int, (int Aciertos, int Fallos)> historial,
                                  Perfil perfil, PreferenciasUsuario preferencias, ServicioProgreso servicio)
        {
            InitializeComponent();
            _senas = senas;
            _historial = historial;
            _perfil = perfil;
            _preferencias = preferencias;
            _servicio = servicio;
            visor.TeclaPausar = _preferencias.TeclaPausar;

            _reloj.Tick += Reloj_Tick;
            _pausa.Tick += (s, e) =>
            {
                _pausa.Stop();
                if (_partida.Terminada)
                    Terminar();
                else
                    SiguientePregunta();
            };

            // Si se sale de la sección a mitad de la partida, los timers no pueden quedar andando
            Unloaded += (s, e) => Detener();

            Empezar();
        }

        // ===== Partida =====

        private void Empezar()
        {
            _partida = new PartidaDesafio(_senas, _historial, _random);
            _mejorRachaAntes = _perfil.MejorRacha;
            _avisoGuardado = false;

            pnlResultado.Visibility = Visibility.Collapsed;
            pnlMensaje.Visibility = Visibility.Collapsed;
            pnlJuego.Visibility = Visibility.Visible;
            lblAvisoTiempo.Visibility = Visibility.Collapsed;
            ActualizarMarcador();

            if (!SiguientePregunta())
                return;
            _reloj.Start();
        }

        // Devuelve false si no se pudo armar la pregunta (ahí ya se muestra el mensaje)
        private bool SiguientePregunta()
        {
            Sena sena;
            try
            {
                sena = _partida.SiguienteSena();
                _pregunta = _generador.CrearOpcionMultiple(sena);
            }
            catch (HandUpException ex)
            {
                MostrarMensaje(ex.Message);
                return false;
            }

            visor.Mostrar(sena);
            pnlFeedback.Visibility = Visibility.Collapsed;
            MostrarOpciones(_pregunta);
            return true;
        }

        private void MostrarOpciones(PreguntaOpcionMultiple pregunta)
        {
            pnlOpciones.Children.Clear();
            _botonesOpcion.Clear();

            for (int i = 0; i < pregunta.Opciones.Count; i++)
            {
                int indice = i;
                string texto = TextoSena.Palabra(pregunta.Opciones[i]);
                string tecla = TextoTecla.Nombre(_preferencias.ObtenerTeclaOpcion(i + 1));

                var boton = new Button
                {
                    Style = (Style)FindResource("OpcionRespuesta"),
                    Margin = new Thickness(0, 0, 0, 8),
                    Content = PracticaView.CrearContenidoOpcion(tecla, texto, estado: null)
                };
                AutomationProperties.SetName(boton, $"{texto}. Tecla {tecla}");
                boton.Click += (s, e) => Responder(indice);
                _botonesOpcion.Add(boton);
                pnlOpciones.Children.Add(boton);
            }

            Dispatcher.BeginInvoke(DispatcherPriority.Input, () => _botonesOpcion.FirstOrDefault()?.Focus());
        }

        private void Responder(int indice)
        {
            if (_pregunta is null || _pregunta.FueRespondida || _partida.Terminada)
                return;

            bool correcto = _pregunta.EvaluarRespuesta(indice.ToString());
            MarcarOpciones(_pregunta, indice);
            Sena sena = _pregunta.Sena;

            string mensaje;
            if (correcto)
            {
                int puntos = _partida.RegistrarAcierto(sena);
                Guardar(() => _servicio.RegistrarAcierto(sena.Id, puntos));
                mensaje = _partida.Racha >= PartidaDesafio.RachaParaBonus + 1
                    ? $"✓ ¡Correcto! +{puntos} (racha de {_partida.Racha})"
                    : $"✓ ¡Correcto! +{puntos}";
            }
            else
            {
                _partida.RegistrarFallo(sena);
                Guardar(() => _servicio.RegistrarFallo(sena.Id));
                string vidas = _partida.Vidas == 1 ? "Te queda 1 vida" : $"Te quedan {_partida.Vidas} vidas";
                mensaje = $"✗ Era «{TextoSena.Palabra(sena)}». {vidas}.";
            }

            MostrarFeedback(mensaje, correcto);
            ActualizarMarcador();

            _pausa.Interval = correcto ? PausaTrasAcierto : PausaTrasFallo;
            _pausa.Start();
        }

        // Guarda en el progreso del perfil (puntos, racha, ProgresoSenas). Si falla la base, la partida sigue igual.
        private void Guardar(Action guardar)
        {
            try
            {
                guardar();
            }
            catch (HandUpException)
            {
                _avisoGuardado = true;
            }
        }

        private void MarcarOpciones(PreguntaOpcionMultiple pregunta, int elegida)
        {
            for (int i = 0; i < _botonesOpcion.Count; i++)
            {
                string texto = TextoSena.Palabra(pregunta.Opciones[i]);
                string tecla = TextoTecla.Nombre(_preferencias.ObtenerTeclaOpcion(i + 1));
                string? estado = null;

                if (i == pregunta.IndiceCorrecta)
                {
                    _botonesOpcion[i].Tag = "correcta";
                    estado = "✓ Correcta";
                }
                else if (i == elegida)
                {
                    _botonesOpcion[i].Tag = "incorrecta";
                    estado = "✗ Tu respuesta";
                }

                _botonesOpcion[i].Content = PracticaView.CrearContenidoOpcion(tecla, texto, estado);
                _botonesOpcion[i].IsHitTestVisible = false;
            }
        }

        private void MostrarFeedback(string mensaje, bool correcto)
        {
            lblFeedback.Text = mensaje;
            pnlFeedback.SetResourceReference(Border.BackgroundProperty, correcto ? "BrushSuccessBg" : "BrushErrorBg");
            pnlFeedback.SetResourceReference(Border.BorderBrushProperty, correcto ? "BrushSuccess" : "BrushError");
            lblFeedback.SetResourceReference(TextBlock.ForegroundProperty, correcto ? "BrushSuccess" : "BrushError");
            pnlFeedback.Visibility = Visibility.Visible;
        }

        // ===== Reloj y marcador =====

        private void Reloj_Tick(object? sender, EventArgs e)
        {
            _partida.PasarUnSegundo();
            ActualizarMarcador();

            if (_partida.SegundosRestantes is 30 or 10)
                AvisarTiempo($"¡Quedan {_partida.SegundosRestantes} segundos!");

            if (_partida.SinTiempo)
                Terminar();
        }

        private void ActualizarMarcador()
        {
            int s = _partida.SegundosRestantes;
            lblTiempo.Text = $"{s / 60}:{s % 60:00}";
            AutomationProperties.SetName(lblTiempo, $"Quedan {s} segundos");
            bool apurado = s <= 10;
            lblTiempo.SetResourceReference(TextBlock.ForegroundProperty, apurado ? "BrushError" : "BrushInk");
            icoTiempo.SetResourceReference(TextBlock.ForegroundProperty, apurado ? "BrushError" : "BrushAccentDark");
            barraTiempo.SetResourceReference(Border.BackgroundProperty, apurado ? "BrushError" : "BrushAccent");
            double proporcion = (double)s / PartidaDesafio.SegundosIniciales;
            colTiempoLleno.Width = new GridLength(proporcion, GridUnitType.Star);
            colTiempoVacio.Width = new GridLength(1 - proporcion, GridUnitType.Star);

            // Corazones llenos por las vidas que quedan y vacíos por las perdidas, más el número en texto
            lblCorazones.Text = new string('', _partida.Vidas) + new string('', PartidaDesafio.VidasIniciales - _partida.Vidas);
            lblVidas.Text = _partida.Vidas == 1 ? "1 vida" : $"{_partida.Vidas} vidas";
            lblRacha.Text = $"Racha {_partida.Racha}";
            lblPuntaje.Text = $"{_partida.Puntaje} puntos";
        }

        private void AvisarTiempo(string texto)
        {
            lblAvisoTiempo.Text = texto;
            lblAvisoTiempo.Visibility = Visibility.Visible;
            AutomationPeer? peer = UIElementAutomationPeer.FromElement(lblAvisoTiempo)
                                   ?? UIElementAutomationPeer.CreatePeerForElement(lblAvisoTiempo);
            peer?.RaiseAutomationEvent(AutomationEvents.LiveRegionChanged);
        }

        // ===== Fin =====

        private void Terminar()
        {
            Detener();
            pnlJuego.Visibility = Visibility.Collapsed;
            pnlResultado.Visibility = Visibility.Visible;

            (lblResultadoIcono.Text, lblResultadoTitulo.Text) = _partida.SinVidas
                ? ("", "¡Te quedaste sin vidas!")
                : ("", "¡Se acabó el tiempo!");

            lblPuntajeFinal.Text = $"Puntaje final: {_partida.Puntaje} puntos";
            lblResultadoDetalle.Text = $"{_partida.Aciertos} {(_partida.Aciertos == 1 ? "acierto" : "aciertos")} y " +
                                       $"{_partida.Fallos} {(_partida.Fallos == 1 ? "fallo" : "fallos")} · " +
                                       $"mejor racha de la partida: {_partida.MejorRacha}";
            lblRecord.Text = _perfil.MejorRacha > _mejorRachaAntes
                ? $"¡Nueva mejor racha: {_perfil.MejorRacha}!"
                : $"Tu mejor racha: {_perfil.MejorRacha}";

            lblAvisoGuardado.Text = "Algunas respuestas no se pudieron guardar en tu progreso.";
            lblAvisoGuardado.Visibility = _avisoGuardado ? Visibility.Visible : Visibility.Collapsed;

            Dispatcher.BeginInvoke(DispatcherPriority.Input, () => btnJugarDeNuevo.Focus());
        }

        private void MostrarMensaje(string mensaje)
        {
            Detener();
            pnlJuego.Visibility = Visibility.Collapsed;
            lblMensaje.Text = mensaje;
            pnlMensaje.Visibility = Visibility.Visible;
        }

        // Para los timers y el video (al terminar, o al salir de la partida)
        public void Detener()
        {
            _reloj.Stop();
            _pausa.Stop();
            visor.Detener();
        }

        // El historial se vuelve a leer: así las señas falladas en la partida anterior también salen más seguido
        private void BtnJugarDeNuevo_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                _historial = new RepositorioPerfiles().ObtenerHistorialSenas(_perfil.Id);
            }
            catch (DatabaseException ex)
            {
                // Sin base se juega con el historial que ya teníamos
                RegistroErrores.Registrar(ex, "Leer el historial de señas para jugar de nuevo");
            }
            Empezar();
        }

        private void BtnCambiarCategoria_Click(object sender, RoutedEventArgs e) =>
            CambiarCategoriaSolicitada?.Invoke(this, EventArgs.Empty);

        public void EnfocarInicio()
        {
            if (pnlResultado.Visibility == Visibility.Visible)
                Dispatcher.BeginInvoke(DispatcherPriority.Input, () => btnJugarDeNuevo.Focus());
            else if (_botonesOpcion.Count > 0)
                Dispatcher.BeginInvoke(DispatcherPriority.Input, () => _botonesOpcion[0].Focus());
        }

        // ===== Teclado =====
        // Las teclas de opción (1-4 por defecto, o las de Configuración) responden directo. La de Pausar pausa el video.
        private void Vista_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            if (pnlJuego.Visibility != Visibility.Visible || Keyboard.Modifiers != ModifierKeys.None)
                return;

            // Con el foco en un botón del visor, Enter y Espacio son para ese botón
            if (visor.IsKeyboardFocusWithin && e.Key is Key.Return or Key.Space)
                return;

            bool espacioSobreOpcion = e.Key == Key.Space && Keyboard.FocusedElement is Button b && _botonesOpcion.Contains(b);
            if (e.Key == _preferencias.TeclaPausar && !espacioSobreOpcion)
            {
                visor.AlternarPausa();
                e.Handled = true;
                return;
            }

            for (int numero = 1; numero <= _botonesOpcion.Count; numero++)
            {
                if (e.Key == _preferencias.ObtenerTeclaOpcion(numero))
                {
                    Responder(numero - 1);
                    e.Handled = true;
                    return;
                }
            }
        }
    }
}
