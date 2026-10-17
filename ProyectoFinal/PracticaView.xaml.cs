using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Threading;

namespace ProyectoFinal
{
    // Práctica de una categoría (stories: responder por opción múltiple con atajos, responder tipeando,
    // y botón Confirmar deshabilitado hasta elegir). Cada respuesta suma al progreso del perfil.
    public partial class PracticaView : UserControl
    {
        // Terminó la práctica y se quiere elegir otra categoría
        public event EventHandler? OtraCategoriaSolicitada;

        private const int MaximoPreguntas = 10;
        private const int PuntosOpcionMultiple = 10;
        private const int PuntosTipeo = 15;

        // Para tipeo solo se usan palabras simples: con "Tomar / Beber" o "Mañana (parte del día)"
        // no se puede esperar que el usuario escriba exactamente lo mismo
        private static readonly Regex PalabraSimple = new(@"^[\p{L} ]+$");

        private readonly IReadOnlyList<Sena> _senasCategoria;
        private readonly PreferenciasUsuario _preferencias;
        private readonly ServicioProgreso _servicio;
        private readonly GeneradorPreguntas _generador = new(new RepositorioSenas());
        private readonly Random _random = new();

        private List<Sena> _ronda = new();
        private int _indice;
        private int _aciertos;
        private Pregunta? _pregunta;
        private int? _opcionElegida;
        private readonly List<Button> _botonesOpcion = new();

        public PracticaView(IReadOnlyList<Sena> senasCategoria, PreferenciasUsuario preferencias, ServicioProgreso servicio)
        {
            InitializeComponent();
            _senasCategoria = senasCategoria;
            _preferencias = preferencias;
            _servicio = servicio;
            visor.TeclaPausar = _preferencias.TeclaPausar;
            EmpezarRonda();
        }

        // ===== Ronda =====

        private void EmpezarRonda()
        {
            pnlResultado.Visibility = Visibility.Collapsed;

            if (_senasCategoria.Count < 2)
            {
                pnlPregunta.Visibility = Visibility.Collapsed;
                lblMensaje.Text = "Esta categoría tiene muy pocas señas para practicar.";
                lblMensaje.Visibility = Visibility.Visible;
                return;
            }

            // Hasta 10 señas de la categoría, en orden al azar
            _ronda = _senasCategoria.OrderBy(_ => _random.Next()).Take(MaximoPreguntas).ToList();
            _indice = 0;
            _aciertos = 0;
            pnlPregunta.Visibility = Visibility.Visible;
            MostrarPregunta();
        }

        private void MostrarPregunta()
        {
            Sena sena = _ronda[_indice];
            visor.Mostrar(sena);

            lblAvance.Text = $"PREGUNTA {_indice + 1} DE {_ronda.Count}";
            double avance = (double)_indice / _ronda.Count;
            colAvanceLleno.Width = new GridLength(avance, GridUnitType.Star);
            colAvanceVacio.Width = new GridLength(1 - avance, GridUnitType.Star);

            pnlFeedback.Visibility = Visibility.Collapsed;
            _opcionElegida = null;

            // Una de cada tres preguntas es de tipeo (si la palabra se puede escribir sin ambigüedad)
            bool esTipeo = _indice % 3 == 2 && PalabraSimple.IsMatch(sena.Palabra.Trim());
            try
            {
                _pregunta = esTipeo ? _generador.CrearTipeo(sena) : _generador.CrearOpcionMultiple(sena);
            }
            catch (DatabaseException ex)
            {
                MostrarFeedback($"No se pudo armar la pregunta. {ex.Message}", correcto: false);
                return;
            }

            if (_pregunta is PreguntaOpcionMultiple multiple)
                MostrarOpciones(multiple);
            else
                MostrarTipeo();

            ActualizarConfirmar();
        }

        private void MostrarOpciones(PreguntaOpcionMultiple pregunta)
        {
            lblEnunciado.Text = "¿Qué seña es esta?";
            pnlTipeo.Visibility = Visibility.Collapsed;
            pnlOpciones.Visibility = Visibility.Visible;
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
                    Content = CrearContenidoOpcion(tecla, texto, estado: null)
                };
                AutomationProperties.SetName(boton, $"{texto}. Tecla {tecla}");
                boton.Click += (s, e) => ElegirOpcion(indice);
                _botonesOpcion.Add(boton);
                pnlOpciones.Children.Add(boton);
            }

            Dispatcher.BeginInvoke(DispatcherPriority.Input, () => _botonesOpcion[0].Focus());
        }

        // Contenido de una opción: [tecla] texto ............ estado (✓ Correcta / ✗ Tu respuesta)
        private static Grid CrearContenidoOpcion(string tecla, string texto, string? estado)
        {
            var grid = new Grid();
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            var lblTecla = new TextBlock { Text = tecla, FontSize = 12, FontWeight = FontWeights.Bold };
            lblTecla.SetResourceReference(TextBlock.ForegroundProperty, "BrushAccentDark");
            var badge = new Border
            {
                Child = lblTecla,
                MinWidth = 28,
                Padding = new Thickness(6, 2, 6, 2),
                CornerRadius = new CornerRadius(5),
                BorderThickness = new Thickness(1.5),
                VerticalAlignment = VerticalAlignment.Center
            };
            lblTecla.HorizontalAlignment = HorizontalAlignment.Center;
            badge.SetResourceReference(Border.BorderBrushProperty, "BrushBorderAccent");

            var lblTexto = new TextBlock
            {
                Text = texto,
                FontWeight = FontWeights.SemiBold,
                TextWrapping = TextWrapping.Wrap,
                Margin = new Thickness(12, 0, 8, 0),
                VerticalAlignment = VerticalAlignment.Center
            };
            Grid.SetColumn(lblTexto, 1);

            grid.Children.Add(badge);
            grid.Children.Add(lblTexto);

            if (estado is not null)
            {
                var lblEstado = new TextBlock { Text = estado, FontSize = 13, FontWeight = FontWeights.Bold, VerticalAlignment = VerticalAlignment.Center };
                lblEstado.SetResourceReference(TextBlock.ForegroundProperty, estado.StartsWith('✓') ? "BrushSuccess" : "BrushError");
                Grid.SetColumn(lblEstado, 2);
                grid.Children.Add(lblEstado);
            }
            return grid;
        }

        private void MostrarTipeo()
        {
            lblEnunciado.Text = "Escribí la palabra de esta seña";
            pnlOpciones.Visibility = Visibility.Collapsed;
            pnlTipeo.Visibility = Visibility.Visible;
            txtRespuesta.IsReadOnly = false;
            txtRespuesta.Text = string.Empty;
            Dispatcher.BeginInvoke(DispatcherPriority.Input, () => txtRespuesta.Focus());
        }

        // ===== Responder =====

        // Elegir (sin confirmar). Tocar de nuevo la opción elegida la deselecciona.
        private void ElegirOpcion(int indice)
        {
            if (_pregunta is not PreguntaOpcionMultiple || _pregunta.FueRespondida)
                return;

            _opcionElegida = _opcionElegida == indice ? null : indice;
            for (int i = 0; i < _botonesOpcion.Count; i++)
                _botonesOpcion[i].Tag = i == _opcionElegida ? "elegida" : null;
            ActualizarConfirmar();
        }

        private void TxtRespuesta_TextChanged(object sender, TextChangedEventArgs e) => ActualizarConfirmar();

        // Story: Confirmar arranca deshabilitado y se habilita al elegir una opción o escribir algo.
        // Si se borra la elección o el texto, vuelve a deshabilitarse. Después de responder, pasa a "Siguiente".
        private void ActualizarConfirmar()
        {
            if (_pregunta?.FueRespondida == true)
            {
                bool esUltima = _indice == _ronda.Count - 1;
                lblConfirmar.Text = esUltima ? "Ver resultado" : "Siguiente";
                btnConfirmar.IsEnabled = true;
                AutomationProperties.SetName(btnConfirmar, lblConfirmar.Text);
                return;
            }

            lblConfirmar.Text = $"Confirmar ({TextoTecla.Nombre(_preferencias.TeclaConfirmar)})";
            AutomationProperties.SetName(btnConfirmar, "Confirmar respuesta");
            btnConfirmar.IsEnabled = _pregunta switch
            {
                PreguntaOpcionMultiple => _opcionElegida is not null,
                PreguntaTipeo => !string.IsNullOrWhiteSpace(txtRespuesta.Text),
                _ => false
            };
        }

        private void BtnConfirmar_Click(object sender, RoutedEventArgs e) => ConfirmarOAvanzar();

        private void ConfirmarOAvanzar()
        {
            if (_pregunta is null || !btnConfirmar.IsEnabled)
                return;

            if (_pregunta.FueRespondida)
                Avanzar();
            else
                Confirmar();
        }

        private void Confirmar()
        {
            if (_pregunta is null || _pregunta.FueRespondida)
                return;

            string respuesta = _pregunta is PreguntaOpcionMultiple ? _opcionElegida.ToString()! : txtRespuesta.Text;
            bool correcto = _pregunta.EvaluarRespuesta(respuesta);
            string palabraCorrecta = TextoSena.Palabra(_pregunta.Sena);

            if (_pregunta is PreguntaOpcionMultiple multiple)
                MarcarOpciones(multiple);
            else
                txtRespuesta.IsReadOnly = true;

            string? avisoGuardado = RegistrarProgreso(correcto);
            if (correcto)
                _aciertos++;

            MostrarFeedback(correcto ? "✓ ¡Correcto!" : $"✗ Incorrecto. Era «{palabraCorrecta}».", correcto, avisoGuardado);
            ActualizarConfirmar();
            btnConfirmar.Focus();
        }

        // Después de confirmar: la correcta en verde con "✓ Correcta"; la elegida, si estaba mal, en rojo con "✗ Tu respuesta"
        private void MarcarOpciones(PreguntaOpcionMultiple pregunta)
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
                else if (i == _opcionElegida)
                {
                    _botonesOpcion[i].Tag = "incorrecta";
                    estado = "✗ Tu respuesta";
                }
                else
                {
                    _botonesOpcion[i].Tag = null;
                }

                _botonesOpcion[i].Content = CrearContenidoOpcion(tecla, texto, estado);
                _botonesOpcion[i].IsHitTestVisible = false; // ya no se puede cambiar
            }
        }

        // Suma al progreso del perfil (puntos y racha). Si falla la base, la práctica sigue igual.
        private string? RegistrarProgreso(bool correcto)
        {
            try
            {
                if (correcto)
                    _servicio.RegistrarAcierto(_pregunta!.Sena.Id, _pregunta is PreguntaTipeo ? PuntosTipeo : PuntosOpcionMultiple);
                else
                    _servicio.RegistrarFallo(_pregunta!.Sena.Id);
                return null;
            }
            catch (DatabaseException)
            {
                return "No se pudo guardar el progreso de esta respuesta.";
            }
        }

        private void MostrarFeedback(string mensaje, bool correcto, string? aviso = null)
        {
            lblFeedback.Text = aviso is null ? mensaje : $"{mensaje}\n{aviso}";
            pnlFeedback.SetResourceReference(Border.BackgroundProperty, correcto ? "BrushSuccessBg" : "BrushErrorBg");
            pnlFeedback.SetResourceReference(Border.BorderBrushProperty, correcto ? "BrushSuccess" : "BrushError");
            lblFeedback.SetResourceReference(TextBlock.ForegroundProperty, correcto ? "BrushSuccess" : "BrushError");
            pnlFeedback.Visibility = Visibility.Visible;
        }

        private void Avanzar()
        {
            if (_indice < _ronda.Count - 1)
            {
                _indice++;
                MostrarPregunta();
            }
            else
            {
                MostrarResultado();
            }
        }

        // ===== Resultado final =====

        private void MostrarResultado()
        {
            visor.Detener();
            pnlPregunta.Visibility = Visibility.Collapsed;
            pnlResultado.Visibility = Visibility.Visible;

            double porcentaje = (double)_aciertos / _ronda.Count;
            lblResultado.Text = $"Acertaste {_aciertos} de {_ronda.Count}";
            (lblResultadoIcono.Text, lblResultadoDetalle.Text) = porcentaje switch
            {
                >= 0.8 => ("", "¡Excelente! Ya conocés muy bien estas señas."),
                >= 0.5 => ("", "¡Bien! Practicá un poco más para afianzarlas."),
                _ => ("", "Repasá las señas de la categoría y probá de nuevo.")
            };

            Dispatcher.BeginInvoke(DispatcherPriority.Input, () => btnPracticarDeNuevo.Focus());
        }

        private void BtnPracticarDeNuevo_Click(object sender, RoutedEventArgs e) => EmpezarRonda();

        private void BtnOtraCategoria_Click(object sender, RoutedEventArgs e)
        {
            visor.Detener();
            OtraCategoriaSolicitada?.Invoke(this, EventArgs.Empty);
        }

        // Foco inicial al entrar a la práctica
        public void EnfocarInicio()
        {
            if (_pregunta is PreguntaOpcionMultiple && _botonesOpcion.Count > 0)
                Dispatcher.BeginInvoke(DispatcherPriority.Input, () => _botonesOpcion[0].Focus());
            else if (_pregunta is PreguntaTipeo)
                Dispatcher.BeginInvoke(DispatcherPriority.Input, () => txtRespuesta.Focus());
        }

        // ===== Teclado =====
        // Opciones: las teclas que el usuario eligió en Configuración (1-4 por defecto).
        // Confirmar / Siguiente: su tecla de Confirmar (Enter por defecto). Pausar: su tecla de Pausar.
        private void Vista_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            if (pnlPregunta.Visibility != Visibility.Visible || _pregunta is null || Keyboard.Modifiers != ModifierKeys.None)
                return;

            bool escribiendo = txtRespuesta.IsKeyboardFocused && !txtRespuesta.IsReadOnly;

            // Mientras se escribe, solo Enter (o la tecla de Confirmar si no es una letra o número) confirma;
            // el resto de las teclas son para el campo de texto
            if (escribiendo)
            {
                if (e.Key == Key.Return || (e.Key == _preferencias.TeclaConfirmar && !EsTeclaDeTexto(e.Key)))
                {
                    ConfirmarOAvanzar();
                    e.Handled = true;
                }
                return;
            }

            // Con el foco en un botón del visor (pausar, repetir, pantalla completa...), Enter y Espacio son para ese botón
            if (visor.IsKeyboardFocusWithin && e.Key is Key.Return or Key.Space)
                return;

            if (e.Key == _preferencias.TeclaConfirmar)
            {
                ConfirmarOAvanzar();
                e.Handled = true;
                return;
            }

            // Con el foco sobre una opción, Espacio la elige (como en cualquier botón) en vez de pausar
            bool espacioSobreOpcion = e.Key == Key.Space && Keyboard.FocusedElement is Button b && _botonesOpcion.Contains(b);
            if (e.Key == _preferencias.TeclaPausar && !espacioSobreOpcion)
            {
                visor.AlternarPausa();
                e.Handled = true;
                return;
            }

            if (_pregunta is PreguntaOpcionMultiple && !_pregunta.FueRespondida)
            {
                for (int numero = 1; numero <= _botonesOpcion.Count; numero++)
                {
                    if (e.Key == _preferencias.ObtenerTeclaOpcion(numero))
                    {
                        ElegirOpcion(numero - 1);
                        _botonesOpcion[numero - 1].Focus();
                        e.Handled = true;
                        return;
                    }
                }
            }
        }

        private static bool EsTeclaDeTexto(Key tecla) =>
            tecla is >= Key.A and <= Key.Z or >= Key.D0 and <= Key.D9 or >= Key.NumPad0 and <= Key.NumPad9
                or Key.Space or Key.OemComma or Key.OemPeriod or Key.OemMinus or Key.OemPlus;
    }
}
