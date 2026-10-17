using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Automation.Peers;
using System.Windows.Controls;
using System.Windows.Input;

namespace ProyectoFinal
{
    // Datos de una tarjeta de nivel (lo que muestra la grilla).
    // Bloqueado: está por encima del nivel desbloqueado del perfil; para abrirlo hay que aprobar la prueba final del anterior.
    public record NivelAprendizaje(int Numero, string Nombre, string Descripcion, string Categorias,
                                   long CantidadSenas, bool EsTuNivel, bool Bloqueado)
    {
        public Visibility VisibilidadTuNivel => EsTuNivel ? Visibility.Visible : Visibility.Collapsed;
        public Visibility VisibilidadBloqueado => Bloqueado ? Visibility.Visible : Visibility.Collapsed;
        public Visibility VisibilidadDisponible => Bloqueado ? Visibility.Collapsed : Visibility.Visible;

        public string NivelAnterior => Niveles.NombreDe(Numero - 1);

        public string MensajeBloqueado =>
            $"Aprobá la prueba final de {NivelAnterior} con 80% o más para desbloquear {Nombre}.";

        public string NombreAccesible => Bloqueado
            ? $"{Nombre}, bloqueado. {MensajeBloqueado} Atajo: tecla {Numero}"
            : $"{Nombre}, {CantidadSenas} señas{(EsTuNivel ? ", tu nivel" : "")}. Atajo: tecla {Numero}";
    }

    public partial class ElegirNivelView : UserControl
    {
        // Avisa qué nivel se eligió (1-4); AprenderView decide a qué paso ir. Nunca se dispara con un nivel bloqueado.
        public event EventHandler<NivelAprendizaje>? NivelElegido;

        // Se quiere rendir la prueba final de este nivel (el nivel actual del perfil)
        public event EventHandler<int>? PruebaFinalSolicitada;

        private readonly int _nivelDesbloqueado;

        // Frase corta de cada nivel (texto de pantalla). Nombre, señas y categorías salen de la base.
        private static readonly Dictionary<int, string> Descripciones = new()
        {
            [1] = "Para empezar desde cero.",
            [2] = "Señas de todos los días.",
            [3] = "Ampliá tu vocabulario.",
            [4] = "Para comunicarte con fluidez."
        };

        private IReadOnlyList<NivelAprendizaje> _niveles = Array.Empty<NivelAprendizaje>();

        // mejorPorcentaje: el mejor resultado en la prueba final del nivel actual (null si nunca la rindió)
        public ElegirNivelView(int nivelDesbloqueado, int? mejorPorcentaje)
        {
            InitializeComponent();
            _nivelDesbloqueado = nivelDesbloqueado;
            MostrarPruebaFinal(mejorPorcentaje);

            try
            {
                _niveles = CargarNiveles(nivelDesbloqueado);
                lstNiveles.ItemsSource = _niveles;
            }
            catch (DatabaseException ex)
            {
                lstNiveles.Visibility = Visibility.Collapsed;
                lblError.Text = $"No se pudieron cargar los niveles. {ex.Message}";
                lblError.Visibility = Visibility.Visible;
            }
        }

        // En el último nivel no hay prueba final: no queda nada por desbloquear
        private bool HayPruebaFinal => _nivelDesbloqueado < Niveles.Nombres.Count;

        private void MostrarPruebaFinal(int? mejorPorcentaje)
        {
            if (!HayPruebaFinal)
                return;

            string nivel = Niveles.NombreDe(_nivelDesbloqueado);
            string siguiente = Niveles.NombreDe(_nivelDesbloqueado + 1);
            string mejor = mejorPorcentaje is null ? string.Empty : $" Tu mejor resultado: {mejorPorcentaje}%.";

            lblPruebaFinal.Text = $"Prueba final de {nivel}";
            lblPruebaDetalle.Text = $"{PruebaFinal.CantidadPreguntas} señas del nivel. " +
                                    $"Con {PruebaFinal.PorcentajeParaAprobar}% o más desbloqueás {siguiente}.{mejor}";
            lblBotonPrueba.Text = "Rendir prueba (P)";
            AutomationProperties.SetName(btnPruebaFinal,
                $"Rendir la prueba final de {nivel}. {lblPruebaDetalle.Text} Atajo: tecla P");
            pnlPruebaFinal.Visibility = Visibility.Visible;
        }

        private void BtnPruebaFinal_Click(object sender, RoutedEventArgs e) => RendirPruebaFinal();

        private void RendirPruebaFinal()
        {
            if (HayPruebaFinal)
                PruebaFinalSolicitada?.Invoke(this, _nivelDesbloqueado);
        }

        private static IReadOnlyList<NivelAprendizaje> CargarNiveles(int nivelDesbloqueado)
        {
            // Nombres legibles de las categorías ("saludos" -> "Saludos y Cortesía"), en el orden del banco
            var categorias = new RepositorioCategorias().ObtenerTodos();
            var nombres = categorias.ToDictionary(c => c.Id, c => c.Nombre);
            var orden = categorias.Select((c, i) => (c.Id, i)).ToDictionary(x => x.Id, x => x.i);

            return new RepositorioSenas().ObtenerNiveles()
                .Select(nivel => new NivelAprendizaje(
                    nivel.Numero,
                    nivel.Nombre,
                    Descripciones.GetValueOrDefault(nivel.Numero, string.Empty),
                    string.Join(" · ", nivel.Categorias
                        .OrderBy(id => orden.GetValueOrDefault(id, int.MaxValue))
                        .Select(id => nombres.GetValueOrDefault(id, id))),
                    nivel.CantidadSenas,
                    EsTuNivel: nivel.Numero == nivelDesbloqueado,
                    Bloqueado: nivel.Numero > nivelDesbloqueado))
                .ToList();
        }

        // Para ir directo a un nivel (ej. desde "Practicar" en Mi perfil); null si no existe
        public NivelAprendizaje? BuscarNivel(int numero) => _niveles.FirstOrDefault(n => n.Numero == numero);

        // Foco: el nivel indicado; si no se indica, el del perfil (o el primero)
        public void EnfocarNivel(int? numero = null)
        {
            int indice = Math.Max(0, _niveles.ToList().FindIndex(n => numero is null ? n.EsTuNivel : n.Numero == numero));
            AyudaFoco.EnfocarTarjeta(lstNiveles, indice);
        }

        private void Nivel_Click(object sender, RoutedEventArgs e)
        {
            if (sender is FrameworkElement { DataContext: NivelAprendizaje nivel })
                Elegir(nivel);
        }

        // Un nivel disponible avanza al paso siguiente; uno bloqueado muestra el aviso y se queda acá
        private void Elegir(NivelAprendizaje nivel)
        {
            if (nivel.Bloqueado)
            {
                AvisarBloqueado(nivel);
                return;
            }

            filaBloqueado.Visibility = Visibility.Collapsed;
            NivelElegido?.Invoke(this, nivel);
        }

        // Muestra arriba qué hay que aprobar para desbloquear el nivel (justo encima de la prueba final)
        // y se lo anuncia al lector de pantalla
        public void AvisarBloqueado(NivelAprendizaje nivel)
        {
            lblBloqueado.Text = nivel.MensajeBloqueado;
            filaBloqueado.Visibility = Visibility.Visible;
            pnlPruebaFinal.Visibility = Visibility.Visible;

            AutomationPeer? peer = UIElementAutomationPeer.FromElement(lblBloqueado)
                                   ?? UIElementAutomationPeer.CreatePeerForElement(lblBloqueado);
            peer?.RaiseAutomationEvent(AutomationEvents.LiveRegionChanged);
        }

        // Teclas 1-4 (fila de números o teclado numérico) eligen el nivel directamente; P rinde la prueba final
        private void Vista_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            if (Keyboard.Modifiers != ModifierKeys.None)
                return;

            if (e.Key == Key.P && HayPruebaFinal)
            {
                RendirPruebaFinal();
                e.Handled = true;
                return;
            }

            int numero = e.Key switch
            {
                >= Key.D1 and <= Key.D4 => e.Key - Key.D0,
                >= Key.NumPad1 and <= Key.NumPad4 => e.Key - Key.NumPad0,
                _ => 0
            };

            NivelAprendizaje? nivel = _niveles.FirstOrDefault(n => n.Numero == numero);
            if (nivel is not null)
            {
                Elegir(nivel);
                e.Handled = true;
            }
        }
    }
}
