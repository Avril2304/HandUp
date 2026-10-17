using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Automation.Peers;
using System.Windows.Controls;
using System.Windows.Threading;

namespace ProyectoFinal
{
    // Sección Desafío. Primero se configura la partida (nivel y categoría) y después se juega (PartidaDesafioView).
    // Los niveles por encima del desbloqueado se ven con candado y no se pueden elegir, igual que en Aprender.
    public partial class DesafioView : UserControl, IVistaDeSeccion
    {
        // Pide volver al inicio del menú (desde la configuración)
        public event EventHandler? VolverSolicitado;

        private readonly Perfil _perfil;
        private readonly PreferenciasUsuario _preferencias;
        private readonly ServicioProgreso _servicio;

        private readonly List<Button> _botonesNivel = new();
        private readonly List<(Button Boton, Categoria Categoria)> _botonesCategoria = new();
        private int _nivelElegido;
        private Categoria? _categoriaElegida;
        private PartidaDesafioView? _partida;

        public DesafioView(Perfil perfil, PreferenciasUsuario preferencias, ServicioProgreso servicio)
        {
            InitializeComponent();
            _perfil = perfil;
            _preferencias = preferencias;
            _servicio = servicio;

            lblReglaTiempo.Text = $"{PartidaDesafio.SegundosIniciales} segundos";
            lblReglaVidas.Text = $"{PartidaDesafio.VidasIniciales} vidas";
            lblReglaPuntos.Text = $"{PartidaDesafio.PuntosPorAcierto} puntos por acierto " +
                                  $"(+{PartidaDesafio.BonusRacha} con racha de {PartidaDesafio.RachaParaBonus} o más)";

            CrearBotonesNivel();
            ElegirNivel(_perfil.NivelDesbloqueado);
            MostrarConfiguracion();
        }

        // ===== Configuración =====

        private void CrearBotonesNivel()
        {
            for (int numero = 1; numero <= Niveles.Nombres.Count; numero++)
            {
                int nivel = numero;
                bool bloqueado = nivel > _perfil.NivelDesbloqueado;
                string nombre = Niveles.NombreDe(nivel);

                var contenido = new StackPanel { Orientation = Orientation.Horizontal };
                if (bloqueado)
                {
                    var candado = new TextBlock { Text = "", FontSize = 13, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 0, 8, 0) };
                    candado.SetResourceReference(TextBlock.FontFamilyProperty, "FontIconos");
                    contenido.Children.Add(candado);
                }
                contenido.Children.Add(new TextBlock { Text = nombre, FontWeight = FontWeights.SemiBold, VerticalAlignment = VerticalAlignment.Center });

                var boton = new Button
                {
                    Style = (Style)FindResource("OpcionRespuesta"),
                    Margin = new Thickness(0, 0, 8, 0),
                    Content = contenido
                };
                if (bloqueado)
                    boton.SetResourceReference(Control.ForegroundProperty, "BrushMuted");

                AutomationProperties.SetName(boton, bloqueado ? $"{nombre}, bloqueado. {MensajeBloqueado(nivel)}" : nombre);
                boton.Click += (s, e) => ElegirNivel(nivel);
                _botonesNivel.Add(boton);
                pnlNiveles.Children.Add(boton);
            }
        }

        private static string MensajeBloqueado(int nivel) =>
            $"Aprobá la prueba final de {Niveles.NombreDe(nivel - 1)} con {PruebaFinal.PorcentajeParaAprobar}% o más " +
            $"para desbloquear {Niveles.NombreDe(nivel)}.";

        private void ElegirNivel(int nivel)
        {
            if (nivel > _perfil.NivelDesbloqueado)
            {
                lblBloqueado.Text = MensajeBloqueado(nivel);
                avisoBloqueado.Visibility = Visibility.Visible;
                AutomationPeer? peer = UIElementAutomationPeer.FromElement(lblBloqueado)
                                       ?? UIElementAutomationPeer.CreatePeerForElement(lblBloqueado);
                peer?.RaiseAutomationEvent(AutomationEvents.LiveRegionChanged);
                return;
            }

            avisoBloqueado.Visibility = Visibility.Collapsed;
            if (_nivelElegido == nivel)
                return;

            _nivelElegido = nivel;
            for (int i = 0; i < _botonesNivel.Count; i++)
                _botonesNivel[i].Tag = i + 1 == nivel ? "elegida" : null;

            CargarCategorias(nivel);
        }

        private void CargarCategorias(int nivel)
        {
            _categoriaElegida = null;
            _botonesCategoria.Clear();
            pnlCategorias.Children.Clear();
            lblMensajeCategorias.Visibility = Visibility.Collapsed;
            lblTituloCategorias.Text = $"2. Elegí la categoría de {Niveles.NombreDe(nivel)}";

            try
            {
                var repo = new RepositorioCategorias();
                foreach (Categoria categoria in repo.ObtenerPorDificultad(nivel))
                    AgregarBotonCategoria(categoria, repo.ContarSenas(categoria.Id));
            }
            catch (DatabaseException ex)
            {
                MostrarMensajeCategorias($"No se pudieron cargar las categorías. {ex.Message}", esError: true);
            }

            if (_botonesCategoria.Count == 0 && lblMensajeCategorias.Visibility != Visibility.Visible)
                MostrarMensajeCategorias("Este nivel todavía no tiene categorías.", esError: false);

            ActualizarEmpezar();
        }

        private void AgregarBotonCategoria(Categoria categoria, long cantidad)
        {
            var titulo = new TextBlock { Text = $"{categoria.Emoji} {categoria.Nombre}", FontWeight = FontWeights.SemiBold, TextWrapping = TextWrapping.Wrap };
            var detalle = new TextBlock { Text = cantidad == 1 ? "1 seña" : $"{cantidad} señas", FontSize = 12.5, Margin = new Thickness(0, 2, 0, 0) };
            detalle.SetResourceReference(TextBlock.ForegroundProperty, "BrushMuted");

            var boton = new Button
            {
                Style = (Style)FindResource("OpcionRespuesta"),
                Width = 230,
                Margin = new Thickness(0, 0, 8, 8),
                Content = new StackPanel { Children = { titulo, detalle } }
            };
            AutomationProperties.SetName(boton, $"{categoria.Nombre}, {detalle.Text}");
            boton.Click += (s, e) => ElegirCategoria(categoria);

            _botonesCategoria.Add((boton, categoria));
            pnlCategorias.Children.Add(boton);
        }

        // Elegir una categoría; tocar de nuevo la elegida la deselecciona (y Empezar vuelve a deshabilitarse)
        private void ElegirCategoria(Categoria categoria)
        {
            _categoriaElegida = _categoriaElegida?.Id == categoria.Id ? null : categoria;
            foreach (var (boton, cat) in _botonesCategoria)
            {
                bool elegida = cat.Id == _categoriaElegida?.Id;
                boton.Tag = elegida ? "elegida" : null;
                AutomationProperties.SetItemStatus(boton, elegida ? "elegida" : string.Empty);
            }
            ActualizarEmpezar();
        }

        // Story: "Empezar partida" arranca deshabilitado, se habilita al elegir una categoría
        // y vuelve a deshabilitarse si se deselecciona
        private void ActualizarEmpezar()
        {
            btnEmpezar.IsEnabled = _categoriaElegida is not null;
            lblAyudaEmpezar.Text = _categoriaElegida is null
                ? "Elegí una categoría para empezar."
                : $"{_categoriaElegida.Emoji} {_categoriaElegida.Nombre} · {Niveles.NombreDe(_nivelElegido)}";
            AutomationProperties.SetName(btnEmpezar, _categoriaElegida is null
                ? "Empezar partida. Primero elegí una categoría"
                : $"Empezar partida de {_categoriaElegida.Nombre}");
        }

        private void MostrarMensajeCategorias(string texto, bool esError)
        {
            lblMensajeCategorias.Text = texto;
            lblMensajeCategorias.SetResourceReference(TextBlock.ForegroundProperty, esError ? "BrushError" : "BrushMuted");
            lblMensajeCategorias.Visibility = Visibility.Visible;
        }

        private void MostrarConfiguracion()
        {
            _partida?.Detener();
            _partida = null;
            contenidoPartida.Content = null;
            contenidoPartida.Visibility = Visibility.Collapsed;
            pnlConfig.Visibility = Visibility.Visible;

            lblPaso.Text = "Contrarreloj con vidas · elegí el nivel y la categoría";
            ActualizarBotonVolver("Volver al inicio");
        }

        // ===== Partida =====

        private void BtnEmpezar_Click(object sender, RoutedEventArgs e)
        {
            if (_categoriaElegida is null)
                return;

            IReadOnlyList<Sena> senas;
            IReadOnlyDictionary<int, (int Aciertos, int Fallos)> historial;
            try
            {
                senas = new RepositorioSenas().BuscarPorCategoria(_categoriaElegida.Id);
                historial = new RepositorioPerfiles().ObtenerHistorialSenas(_perfil.Id);
            }
            catch (DatabaseException ex)
            {
                MostrarMensajeCategorias($"No se pudo armar la partida. {ex.Message}", esError: true);
                return;
            }

            _partida = new PartidaDesafioView(senas, historial, _perfil, _preferencias, _servicio);
            _partida.CambiarCategoriaSolicitada += (s, ev) =>
            {
                MostrarConfiguracion();
                EnfocarContenido();
            };

            pnlConfig.Visibility = Visibility.Collapsed;
            contenidoPartida.Content = _partida;
            contenidoPartida.Visibility = Visibility.Visible;
            lblPaso.Text = $"{_categoriaElegida.Emoji} {_categoriaElegida.Nombre} · {Niveles.NombreDe(_nivelElegido)}";
            ActualizarBotonVolver("Terminar y volver");
            _partida.EnfocarInicio();
        }

        private void ActualizarBotonVolver(string texto)
        {
            lblVolver.Text = texto;
            btnVolver.ToolTip = $"{texto} (Esc)";
            AutomationProperties.SetName(btnVolver, texto);
        }

        // ===== IVistaDeSeccion =====

        public void EnfocarContenido()
        {
            if (_partida is not null)
            {
                _partida.EnfocarInicio();
                return;
            }

            Button? destino = _botonesCategoria.FirstOrDefault(b => b.Categoria.Id == _categoriaElegida?.Id).Boton
                              ?? _botonesNivel.ElementAtOrDefault(_nivelElegido - 1);
            if (destino is not null)
                Dispatcher.BeginInvoke(DispatcherPriority.Input, () => destino.Focus());
        }

        // Esc en la partida la abandona (no se guarda un resultado, pero cada respuesta ya contó para el progreso)
        public bool VolverUnPaso()
        {
            if (_partida is null)
                return false;

            MostrarConfiguracion();
            EnfocarContenido();
            return true;
        }

        private void BtnVolver_Click(object sender, RoutedEventArgs e)
        {
            if (!VolverUnPaso())
                VolverSolicitado?.Invoke(this, EventArgs.Empty);
        }
    }
}
