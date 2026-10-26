using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Threading;

namespace ProyectoFinal
{
    // Módulo Traductor (stories 14 y 15).
    // Se escribe una palabra o frase y se muestran sus señas una tras otra:
    //  - Las señas con video pasan a la siguiente cuando el video termina (evento VideoTerminado del visor).
    //  - Las letras del deletreo (imágenes) pasan con un DispatcherTimer, a intervalo fijo.
    // La secuencia se puede pausar, repetir o detener.
    public partial class TraductorView : UserControl, IVistaDeSeccion
    {
        public event EventHandler? VolverSolicitado;

        private enum Estado { Detenida, Reproduciendo, Pausada, Terminada }

        // Respaldo por si un video no avisa que terminó (ej. si no se pudo abrir y se mostró la imagen)
        private const double SegundosMaximosPorVideo = 15;

        // Segundos por letra según la velocidad elegida (Lenta, Normal, Rápida)
        private static readonly double[] SegundosPorLetra = { 2.0, 1.2, 0.7 };

        private readonly PreferenciasUsuario _preferencias;
        private readonly Traductor? _traductor;
        private readonly Dictionary<string, Categoria> _categorias = new(StringComparer.OrdinalIgnoreCase);
        private readonly DispatcherTimer _timer = new();

        private IReadOnlyList<ElementoTraduccion> _elementos = Array.Empty<ElementoTraduccion>();

        // La secuencia completa, paso por paso: a qué elemento pertenece, qué número de letra es y qué seña se muestra
        private readonly List<(int Elemento, int Indice, Sena Sena)> _pasos = new();
        private int _pasoActual = -1;
        private Estado _estado = Estado.Detenida;

        private readonly List<Button> _chips = new();
        private readonly Dictionary<int, Button> _botonesCambiar = new();

        // Pantalla completa abierta (null si no). Mientras está abierta, las señas se muestran en su visor.
        private TraductorPantallaCompletaWindow? _pantallaCompleta;

        // El visor donde se muestra la secuencia: el grande si hay pantalla completa, si no el de esta vista
        private VisorSena VisorActivo => _pantallaCompleta?.Visor ?? visor;

        // Exportar como video: el texto que se tradujo, la cancelación (null si no se está exportando) y el último archivo
        private string _textoTraducido = string.Empty;
        private CancellationTokenSource? _cancelacionExportar;
        private string? _ultimoVideo;
        private bool Exportando => _cancelacionExportar is not null;

        public TraductorView(PreferenciasUsuario preferencias)
        {
            InitializeComponent();
            _preferencias = preferencias;

            visor.TeclaPausar = preferencias.TeclaPausar;
            visor.MostrarControles = false; // Pausar / Repetir de esta vista manejan toda la secuencia
            visor.VideoTerminado += Visor_VideoTerminado;
            visor.PantallaCompletaSolicitada += (s, e) => AbrirPantallaCompleta();
            _timer.Tick += Timer_Tick;

            // Al salir de la sección se corta todo (si no, el timer seguiría corriendo)
            Unloaded += (s, e) =>
            {
                _timer.Stop();
                _cancelacionExportar?.Cancel();
                _pantallaCompleta?.Close();
                visor.Detener();
            };

            lblAtajos.Text = $"Enter: interpretar · {TextoTecla.Nombre(preferencias.TeclaPausar)}: pausar · R: repetir · " +
                             "C: cambiar significado · F: pantalla completa · E: exportar video · Esc: detener";

            try
            {
                _traductor = Traductor.CrearDesdeBase();
                foreach (Categoria categoria in new RepositorioCategorias().ObtenerTodos())
                    _categorias[categoria.Id] = categoria;

                if (!_traductor.TieneAbecedario)
                    MostrarAviso("No se encontró el abecedario: las palabras que no estén en el diccionario no se van a poder deletrear.");
            }
            catch (DatabaseException ex)
            {
                MostrarAviso($"No se pudo cargar el banco de señas, así que el intérprete no está disponible. {ex.Message}");
                txtTexto.IsEnabled = false;
            }

            ActualizarControles();
        }

        // ===== Traducir =====

        private void TxtTexto_TextChanged(object sender, TextChangedEventArgs e)
        {
            lblPlaceholder.Visibility = txtTexto.Text.Length == 0 ? Visibility.Visible : Visibility.Collapsed;
            ActualizarControles();
        }

        private void BtnTraducir_Click(object sender, RoutedEventArgs e) => Traducir();

        private void Traducir()
        {
            // Story 14: con el campo vacío no pasa nada (y mientras se exporta un video, la traducción no cambia)
            if (_traductor is null || string.IsNullOrWhiteSpace(txtTexto.Text) || Exportando)
                return;

            Detener();
            OcultarAviso();
            pnlExportado.Visibility = Visibility.Collapsed;
            _textoTraducido = txtTexto.Text.Trim();
            _elementos = _traductor.Traducir(txtTexto.Text);
            ArmarPasos();
            ArmarChips();
            ActualizarAvisoHomonimos();

            if (_pasos.Count == 0)
            {
                MostrarAviso("No hay palabras ni letras para interpretar en lo que escribiste.");
                MostrarEstadoVacio(true);
                ActualizarControles();
                return;
            }

            // Caracteres que no tienen seña (ej. "@"): se avisan y se saltean
            var sinSena = _elementos.SelectMany(el => el.SinSena).Distinct().ToList();
            if (sinSena.Count > 0)
                MostrarAviso($"Estos caracteres no tienen seña y se saltean: {string.Join(" ", sinSena)}");

            MostrarEstadoVacio(false);
            Reproducir(0);
        }

        // Pasa los elementos (palabras) a la lista de pasos que se muestran uno por uno
        private void ArmarPasos()
        {
            _pasos.Clear();
            for (int i = 0; i < _elementos.Count; i++)
            {
                IReadOnlyList<Sena> pasos = _elementos[i].Pasos;
                for (int j = 0; j < pasos.Count; j++)
                    _pasos.Add((i, j, pasos[j]));
            }
        }

        // ===== Secuencia =====

        private void Reproducir(int desdePaso)
        {
            if (desdePaso < 0 || desdePaso >= _pasos.Count)
                return;

            _estado = Estado.Reproduciendo;
            MostrarPaso(desdePaso);
        }

        private void MostrarPaso(int indice)
        {
            _timer.Stop();
            _pasoActual = indice;
            var (elementoIndice, letraIndice, sena) = _pasos[indice];
            ElementoTraduccion elemento = _elementos[elementoIndice];

            // Si falta el archivo, el visor muestra su aviso (AssetNotFoundException) y la secuencia sigue con el timer
            VisorActivo.Mostrar(sena);

            // Si la secuencia está en pausa (ej. al abrir o cerrar la pantalla completa), el video arranca pausado
            if (_estado == Estado.Pausada && VisorActivo.MuestraVideo)
                VisorActivo.AlternarPausa();

            lblAhora.Text = TextoSena.Palabra(sena);
            lblDetalleAhora.Text = elemento.Tipo == TipoElemento.Deletreo
                ? $"Deletreando «{elemento.Texto}» · letra {letraIndice + 1} de {elemento.Letras.Count}"
                : $"«{elemento.Texto}»";

            lblAvance.Text = $"SEÑA {indice + 1} DE {_pasos.Count}";
            double avance = (double)(indice + 1) / _pasos.Count;
            colAvanceLleno.Width = new GridLength(avance, GridUnitType.Star);
            colAvanceVacio.Width = new GridLength(1 - avance, GridUnitType.Star);
            MarcarChipActual(elementoIndice);

            _pantallaCompleta?.MostrarPaso(lblAhora.Text, lblDetalleAhora.Text, lblAvance.Text, avance,
                                           _elementos, elementoIndice, letraIndice);

            // Las letras (imágenes) pasan con el timer; los videos, cuando terminan (el timer queda de respaldo)
            _timer.Interval = TimeSpan.FromSeconds(VisorActivo.MuestraVideo ? SegundosMaximosPorVideo : SegundosLetraActual());
            if (_estado == Estado.Reproduciendo)
                _timer.Start();

            ActualizarControles();
        }

        private void Timer_Tick(object? sender, EventArgs e) => Avanzar();

        private void Visor_VideoTerminado(object? sender, EventArgs e)
        {
            // Solo cuenta el visor donde se está mostrando la secuencia
            if (_estado == Estado.Reproduciendo && sender == VisorActivo)
                Avanzar();
        }

        private void Avanzar()
        {
            if (_pasoActual < _pasos.Count - 1)
            {
                MostrarPaso(_pasoActual + 1);
                return;
            }

            // Terminó: la última seña queda en pantalla (si es un video, sigue en loop)
            _timer.Stop();
            _estado = Estado.Terminada;
            lblDetalleAhora.Text = "Listo. Apretá R para verla de nuevo, o hacé clic en una palabra.";
            _pantallaCompleta?.MostrarDetalle("Listo. Apretá R para verla de nuevo.");
            ActualizarControles();
        }

        // Story 15: pausar el deletreo (o la seña) en curso, y reanudarlo
        private void AlternarPausa()
        {
            if (_estado == Estado.Reproduciendo)
            {
                _estado = Estado.Pausada;
                _timer.Stop();
                if (VisorActivo.MuestraVideo && !VisorActivo.EstaPausado)
                    VisorActivo.AlternarPausa();
            }
            else if (_estado == Estado.Pausada)
            {
                _estado = Estado.Reproduciendo;
                if (VisorActivo.MuestraVideo && VisorActivo.EstaPausado)
                    VisorActivo.AlternarPausa();
                _timer.Start();
            }
            else if (_estado == Estado.Terminada && VisorActivo.MuestraVideo)
            {
                // Ya terminó: solo se pausa o reanuda el video que quedó en pantalla
                VisorActivo.AlternarPausa();
            }

            ActualizarControles();
        }

        // "Más lento" de la pantalla completa: pasa la velocidad a Lenta (o vuelve a Normal)
        private void AlternarLento() => cmbVelocidad.SelectedIndex = VisorActivo.Lento ? 1 : 0;

        // ===== Pantalla completa =====

        // Abre la pantalla completa y sigue la secuencia ahí, desde la seña en la que estaba
        private void AbrirPantallaCompleta()
        {
            if (_pantallaCompleta is not null || _pasos.Count == 0)
                return;

            var ventana = new TraductorPantallaCompletaWindow(_preferencias.TeclaPausar) { Owner = Window.GetWindow(this) };
            ventana.Visor.Lento = visor.Lento;
            ventana.Visor.VideoTerminado += Visor_VideoTerminado;
            ventana.PausaSolicitada += (s, e) => AlternarPausa();
            ventana.RepetirSolicitado += (s, e) => Repetir();
            ventana.LentoSolicitado += (s, e) => AlternarLento();
            ventana.CambiarSignificadoSolicitado += (s, e) => CambiarSignificadoConTeclado();
            ventana.Closed += (s, e) => CerrarPantallaCompleta();

            _pantallaCompleta = ventana;
            visor.Detener(); // la seña pasa al visor grande: el de acá no tiene que seguir reproduciendo
            ventana.Show();
            MostrarPasoEnVisorActivo();
        }

        private void CerrarPantallaCompleta()
        {
            if (_pantallaCompleta is null)
                return;

            visor.Lento = _pantallaCompleta.Visor.Lento;
            _pantallaCompleta.Visor.VideoTerminado -= Visor_VideoTerminado;
            _pantallaCompleta = null;

            // La secuencia vuelve al visor de esta vista, en la misma seña
            if (IsLoaded)
            {
                MostrarPasoEnVisorActivo();
                visor.EnfocarControles();
            }
        }

        // Vuelve a mostrar la seña actual en el visor activo (al entrar o salir de pantalla completa)
        private void MostrarPasoEnVisorActivo()
        {
            if (_pasoActual >= 0)
            {
                Estado estado = _estado;
                MostrarPaso(_pasoActual);
                if (estado == Estado.Terminada)
                    Avanzar(); // ya estaba en la última: queda terminada, sin volver a arrancar el timer
            }
            else
            {
                Reproducir(0); // estaba detenida: arranca desde el principio
            }
        }

        // Story 15: cancelar el deletreo (o la traducción) en curso
        private void Detener()
        {
            _timer.Stop();
            VisorActivo.Detener();

            if (_pasos.Count > 0 && _estado != Estado.Detenida)
            {
                lblAhora.Text = "Interpretación detenida";
                lblDetalleAhora.Text = "Apretá R para verla desde el principio, o hacé clic en una palabra.";
            }

            _estado = Estado.Detenida;
            _pasoActual = -1;
            MarcarChipActual(-1);
            ActualizarControles();
        }

        private void Repetir()
        {
            if (_pasos.Count > 0)
                Reproducir(0);
        }

        private double SegundosLetraActual() =>
            SegundosPorLetra[Math.Clamp(cmbVelocidad.SelectedIndex, 0, SegundosPorLetra.Length - 1)];

        // Velocidad: Lenta también pasa los videos a media velocidad
        private void CmbVelocidad_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (!IsLoaded)
                return; // todavía se está armando la pantalla

            bool lento = cmbVelocidad.SelectedIndex == 0;
            visor.Lento = lento;
            VisorActivo.Lento = lento;
            ActualizarControles();

            // Si se está mostrando una letra, el cambio se nota enseguida
            if (_estado == Estado.Reproduciendo && !VisorActivo.MuestraVideo)
            {
                _timer.Stop();
                _timer.Interval = TimeSpan.FromSeconds(SegundosLetraActual());
                _timer.Start();
            }
        }

        // ===== Lista de palabras (chips) =====

        private void ArmarChips()
        {
            pnlElementos.Children.Clear();
            _chips.Clear();
            _botonesCambiar.Clear();
            lblSinTraduccion.Visibility = _elementos.Count == 0 ? Visibility.Visible : Visibility.Collapsed;

            for (int i = 0; i < _elementos.Count; i++)
            {
                int indice = i;
                ElementoTraduccion elemento = _elementos[i];
                bool tienePasos = elemento.Pasos.Count > 0;

                var chip = new Button
                {
                    Style = (Style)FindResource("ChipElemento"),
                    Content = CrearContenidoChip(elemento),
                    Tag = tienePasos ? null : "omitida",
                    Focusable = tienePasos,
                    ToolTip = tienePasos ? "Ver desde esta palabra" : null
                };
                AutomationProperties.SetName(chip, DescripcionAccesible(elemento));
                if (tienePasos)
                    chip.Click += (s, e) => ReproducirDesdeElemento(indice);

                var contenedor = new StackPanel { Margin = new Thickness(0, 0, 8, 8) };
                contenedor.Children.Add(chip);

                // Homónimos (NARANJA, PAPA, UNO, MERIENDA, MAÑANA...): se puede elegir el otro significado
                if (elemento.EsHomonimo)
                {
                    var cambiar = new Button
                    {
                        Style = (Style)FindResource("LinkButton"),
                        Content = "⇄ Cambiar significado",
                        HorizontalAlignment = HorizontalAlignment.Left,
                        Margin = new Thickness(4, 2, 0, 0)
                    };
                    AutomationProperties.SetName(cambiar, $"Cambiar el significado de {elemento.Texto}");
                    cambiar.Click += (s, e) => CambiarSignificado(indice);
                    contenedor.Children.Add(cambiar);
                    _botonesCambiar[indice] = cambiar;
                }

                pnlElementos.Children.Add(contenedor);
                _chips.Add(chip);
            }
        }

        private StackPanel CrearContenidoChip(ElementoTraduccion elemento)
        {
            var panel = new StackPanel();
            panel.Children.Add(new TextBlock { Text = elemento.Texto, FontSize = 14.5, FontWeight = FontWeights.Bold });

            var detalle = new TextBlock { Text = DetalleChip(elemento), FontSize = 12, TextWrapping = TextWrapping.Wrap, MaxWidth = 300 };
            detalle.SetResourceReference(TextBlock.ForegroundProperty, "BrushMuted");
            panel.Children.Add(detalle);
            return panel;
        }

        private string DetalleChip(ElementoTraduccion elemento)
        {
            switch (elemento.Tipo)
            {
                case TipoElemento.Sena:
                    Sena sena = elemento.SenaElegida!;
                    string tipo = sena is SenaAnimada ? "🎬 Seña" : "📷 Seña";
                    return elemento.EsHomonimo
                        ? $"{tipo}: {TextoSena.Palabra(sena)} · {NombreCategoria(sena)} ({elemento.OpcionElegida + 1} de {elemento.Opciones.Count})"
                        : tipo;

                case TipoElemento.Deletreo:
                    string letras = elemento.Letras.Count > 0
                        ? "🔤 Deletreo: " + string.Join("-", elemento.Letras.Select(l => TextoSena.Palabra(l)))
                        : "Sin seña";
                    return elemento.SinSena.Count > 0 ? $"{letras} (se saltea: {string.Join("", elemento.SinSena)})" : letras;

                default:
                    return "Artículo: en LSA no se seña";
            }
        }

        private string DescripcionAccesible(ElementoTraduccion elemento) => elemento.Tipo switch
        {
            TipoElemento.Sena => $"{elemento.Texto}, seña{(elemento.EsHomonimo ? $" de {NombreCategoria(elemento.SenaElegida!)}" : "")}. Enter para verla",
            TipoElemento.Deletreo => $"{elemento.Texto}, se deletrea. Enter para verla",
            _ => $"{elemento.Texto}, artículo, no se seña"
        };

        private string NombreCategoria(Sena sena) =>
            _categorias.TryGetValue(sena.CategoriaId, out Categoria? categoria) ? categoria.Nombre : sena.CategoriaId;

        private void MarcarChipActual(int elementoActual)
        {
            for (int i = 0; i < _chips.Count; i++)
            {
                bool omitida = _elementos[i].Pasos.Count == 0;
                _chips[i].Tag = omitida ? "omitida" : i == elementoActual ? "actual" : null;
            }
        }

        private void ReproducirDesdeElemento(int elemento)
        {
            int paso = _pasos.FindIndex(p => p.Elemento == elemento);
            if (paso >= 0)
            {
                MostrarEstadoVacio(false);
                Reproducir(paso);
            }
        }

        private void CambiarSignificado(int elemento)
        {
            if (Exportando)
                return; // el video que se está generando usa la traducción tal como estaba

            _elementos[elemento].CambiarOpcion();
            ArmarPasos();
            ArmarChips();
            ActualizarAvisoHomonimos();
            ReproducirDesdeElemento(elemento);

            // Al rearmar la lista el botón es otro: el foco vuelve al nuevo, para poder seguir cambiando
            // (en pantalla completa no: el foco tiene que quedarse en esa ventana)
            if (_pantallaCompleta is null && _botonesCambiar.TryGetValue(elemento, out Button? boton))
                Dispatcher.BeginInvoke(DispatcherPriority.Input, () => boton.Focus());
        }

        // Avisa qué palabras tienen más de un significado, cuál se está mostrando y cómo cambiarlo
        private void ActualizarAvisoHomonimos()
        {
            var homonimos = _elementos.Where(el => el.EsHomonimo).ToList();
            if (homonimos.Count == 0)
            {
                pnlHomonimos.Visibility = Visibility.Collapsed;
                return;
            }

            string Significado(ElementoTraduccion el) => $"{TextoSena.Palabra(el.SenaElegida!)} · {NombreCategoria(el.SenaElegida!)}";

            lblHomonimos.Text = homonimos.Count == 1
                ? $"«{homonimos[0].Texto}» tiene más de un significado. Ahora se muestra {Significado(homonimos[0])}. " +
                  "Para ver el otro, usá «⇄ Cambiar significado» debajo de la palabra o apretá C."
                : "Algunas palabras tienen más de un significado: " +
                  string.Join(", ", homonimos.Select(el => $"«{el.Texto}» ({Significado(el)})")) +
                  ". Usá «⇄ Cambiar significado» debajo de cada una, o apretá C mientras se muestra.";
            pnlHomonimos.Visibility = Visibility.Visible;
        }

        // Tecla C: cambia el significado de la palabra que se está mostrando (o del primer homónimo, si no se muestra ninguno)
        private void CambiarSignificadoConTeclado()
        {
            int actual = _pasoActual >= 0 ? _pasos[_pasoActual].Elemento : -1;
            int elemento = actual >= 0 && _elementos[actual].EsHomonimo
                ? actual
                : _elementos.ToList().FindIndex(el => el.EsHomonimo);

            if (elemento >= 0)
                CambiarSignificado(elemento);
        }

        // ===== Exportar como video =====

        private async void BtnExportar_Click(object sender, RoutedEventArgs e) => await ExportarVideoAsync();

        private void BtnCancelarExportacion_Click(object sender, RoutedEventArgs e) => _cancelacionExportar?.Cancel();

        private async Task ExportarVideoAsync()
        {
            if (Exportando || _pasos.Count == 0)
                return;

            var dialogo = new Microsoft.Win32.SaveFileDialog
            {
                Title = "Guardar la interpretación como video",
                Filter = "Video MP4 (*.mp4)|*.mp4",
                DefaultExt = ".mp4",
                AddExtension = true,
                FileName = NombreSugerido(),
                InitialDirectory = Environment.GetFolderPath(Environment.SpecialFolder.MyVideos)
            };
            if (dialogo.ShowDialog(Window.GetWindow(this)) != true)
                return;

            var exportador = new ExportadorVideo();
            exportador.ProgresoCambiado += Exportador_ProgresoCambiado;
            _cancelacionExportar = new CancellationTokenSource();

            OcultarAviso();
            pnlExportado.Visibility = Visibility.Collapsed;
            lblExportando.Text = "Preparando las señas…";
            barExportando.Value = 0;
            pnlExportando.Visibility = Visibility.Visible;
            ActualizarControles();

            try
            {
                await exportador.ExportarAsync(_textoTraducido, _elementos, dialogo.FileName,
                                               SegundosLetraActual(), _cancelacionExportar.Token);
                _ultimoVideo = dialogo.FileName;
                MostrarResultadoExportacion($"✓ Video guardado: {Path.GetFileName(dialogo.FileName)}", conArchivo: true);
            }
            catch (OperationCanceledException)
            {
                MostrarResultadoExportacion("Se canceló la exportación del video.", conArchivo: false);
            }
            catch (ExportacionException ex)
            {
                MostrarAviso(ex.Message);
            }
            finally
            {
                exportador.ProgresoCambiado -= Exportador_ProgresoCambiado;
                _cancelacionExportar.Dispose();
                _cancelacionExportar = null;
                pnlExportando.Visibility = Visibility.Collapsed;
                ActualizarControles();
            }
        }

        // El avance puede llegar desde otro hilo (mientras Windows genera el video): se pasa al hilo de la pantalla
        private void Exportador_ProgresoCambiado(object? sender, ProgresoExportacionEventArgs e)
        {
            Dispatcher.BeginInvoke(() =>
            {
                barExportando.Value = e.Porcentaje;
                lblExportando.Text = $"{e.Etapa}… {e.Porcentaje:0}%";
            });
        }

        private void MostrarResultadoExportacion(string mensaje, bool conArchivo)
        {
            lblExportado.Text = mensaje;
            btnMostrarVideo.Visibility = conArchivo ? Visibility.Visible : Visibility.Collapsed;
            pnlExportado.Visibility = Visibility.Visible;
        }

        private void BtnMostrarVideo_Click(object sender, RoutedEventArgs e)
        {
            if (_ultimoVideo is null || !File.Exists(_ultimoVideo))
            {
                MostrarAviso("No se encontró el video: puede que lo hayan movido o borrado.");
                return;
            }
            Process.Start("explorer.exe", $"/select,\"{_ultimoVideo}\"");
        }

        // "HandUp - Hola me llamo Ana.mp4" (sin caracteres que Windows no acepta en un nombre de archivo)
        private string NombreSugerido()
        {
            string limpio = new string(_textoTraducido.Where(c => !Path.GetInvalidFileNameChars().Contains(c)).ToArray()).Trim();
            if (limpio.Length > 40)
                limpio = limpio[..40].Trim();
            return limpio.Length == 0 ? "HandUp - Interpretación.mp4" : $"HandUp - {limpio}.mp4";
        }

        // ===== Estado de la pantalla =====

        private void ActualizarControles()
        {
            btnTraducir.IsEnabled = _traductor is not null && !string.IsNullOrWhiteSpace(txtTexto.Text) && !Exportando;
            btnExportar.IsEnabled = _pasos.Count > 0 && !Exportando;
            btnRepetir.IsEnabled = _pasos.Count > 0;
            btnDetener.IsEnabled = _estado is Estado.Reproduciendo or Estado.Pausada;
            btnPausar.IsEnabled = _estado is Estado.Reproduciendo or Estado.Pausada
                                  || (_estado == Estado.Terminada && VisorActivo.MuestraVideo);
            pnlAvance.Visibility = _pasos.Count > 0 && _estado != Estado.Detenida ? Visibility.Visible : Visibility.Collapsed;

            bool enPausa = _estado == Estado.Pausada || (_estado == Estado.Terminada && VisorActivo.EstaPausado);
            lblPausar.Text = enPausa ? "Reanudar" : "Pausar";
            icoPausar.Text = enPausa ? "" : ""; // Play / Pause
            AutomationProperties.SetName(btnPausar, enPausa ? "Reanudar la interpretación" : "Pausar la interpretación");
            btnPausar.ToolTip = $"{lblPausar.Text} ({TextoTecla.Nombre(_preferencias.TeclaPausar)})";

            _pantallaCompleta?.ActualizarBotones(enPausa, btnPausar.IsEnabled, VisorActivo.Lento);
        }

        private void MostrarEstadoVacio(bool vacio) =>
            pnlVacio.Visibility = vacio ? Visibility.Visible : Visibility.Collapsed;

        private void MostrarAviso(string mensaje)
        {
            lblAviso.Text = mensaje;
            pnlAviso.Visibility = Visibility.Visible;
        }

        private void OcultarAviso() => pnlAviso.Visibility = Visibility.Collapsed;

        // ===== Botones =====

        private void BtnPausar_Click(object sender, RoutedEventArgs e) => AlternarPausa();
        private void BtnRepetir_Click(object sender, RoutedEventArgs e) => Repetir();
        private void BtnDetener_Click(object sender, RoutedEventArgs e) => Detener();

        private void BtnVolver_Click(object sender, RoutedEventArgs e)
        {
            Detener();
            VolverSolicitado?.Invoke(this, EventArgs.Empty);
        }

        // ===== Teclado =====
        // Enter (en el campo de texto): traducir. Tecla de Pausar del usuario: pausar / reanudar. R: repetir.
        // Esc lo maneja el menú con VolverUnPaso: primero detiene la traducción y, si no había nada, vuelve al inicio.
        private void Vista_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            if (Keyboard.Modifiers != ModifierKeys.None)
                return;

            if (txtTexto.IsKeyboardFocused)
            {
                if (e.Key == Key.Return)
                {
                    Traducir();
                    e.Handled = true;
                }
                return; // el resto de las teclas son para escribir
            }

            // Con el foco en un botón, Espacio es para ese botón (como en cualquier botón)
            bool espacioSobreBoton = e.Key == Key.Space && Keyboard.FocusedElement is Button;

            if (e.Key == _preferencias.TeclaPausar && !espacioSobreBoton && btnPausar.IsEnabled)
            {
                AlternarPausa();
                e.Handled = true;
            }
            else if (e.Key == Key.R && btnRepetir.IsEnabled)
            {
                Repetir();
                e.Handled = true;
            }
            else if (e.Key == Key.C && _elementos.Any(el => el.EsHomonimo))
            {
                CambiarSignificadoConTeclado();
                e.Handled = true;
            }
            else if (e.Key == Key.E && btnExportar.IsEnabled)
            {
                _ = ExportarVideoAsync();
                e.Handled = true;
            }
            else if (e.Key == Key.F && _pasos.Count > 0)
            {
                AbrirPantallaCompleta();
                e.Handled = true;
            }
        }

        // ===== IVistaDeSeccion =====

        public void EnfocarContenido() => txtTexto.Focus();

        public bool VolverUnPaso()
        {
            if (_estado is Estado.Reproduciendo or Estado.Pausada)
            {
                Detener();
                return true;
            }
            return false;
        }
    }
}
