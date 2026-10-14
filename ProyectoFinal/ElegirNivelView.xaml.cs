using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace ProyectoFinal
{
    // Datos de una tarjeta de nivel (lo que muestra la grilla)
    public record NivelAprendizaje(int Numero, string Nombre, string Descripcion, string Categorias,
                                   long CantidadSenas, bool EsTuNivel)
    {
        public Visibility VisibilidadTuNivel => EsTuNivel ? Visibility.Visible : Visibility.Collapsed;

        public string NombreAccesible =>
            $"{Nombre}, {CantidadSenas} señas{(EsTuNivel ? ", tu nivel" : "")}. Atajo: tecla {Numero}";
    }

    public partial class ElegirNivelView : UserControl
    {
        // Avisa qué nivel se eligió (1-4); AprenderView decide a qué paso ir
        public event EventHandler<NivelAprendizaje>? NivelElegido;

        // Frase corta de cada nivel (texto de pantalla). Nombre, señas y categorías salen de la base.
        private static readonly Dictionary<int, string> Descripciones = new()
        {
            [1] = "Para empezar desde cero.",
            [2] = "Señas de todos los días.",
            [3] = "Ampliá tu vocabulario.",
            [4] = "Para comunicarte con fluidez."
        };

        private IReadOnlyList<NivelAprendizaje> _niveles = Array.Empty<NivelAprendizaje>();

        public ElegirNivelView(string nivelDelPerfil)
        {
            InitializeComponent();

            try
            {
                _niveles = CargarNiveles(nivelDelPerfil);
                lstNiveles.ItemsSource = _niveles;
            }
            catch (DatabaseException ex)
            {
                lstNiveles.Visibility = Visibility.Collapsed;
                lblError.Text = $"No se pudieron cargar los niveles. {ex.Message}";
                lblError.Visibility = Visibility.Visible;
            }
        }

        private static IReadOnlyList<NivelAprendizaje> CargarNiveles(string nivelDelPerfil)
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
                    EsTuNivel: nivel.Nombre.Equals(nivelDelPerfil, StringComparison.OrdinalIgnoreCase)))
                .ToList();
        }

        // Foco: el nivel indicado; si no se indica, el del perfil (o el primero)
        public void EnfocarNivel(int? numero = null)
        {
            int indice = Math.Max(0, _niveles.ToList().FindIndex(n => numero is null ? n.EsTuNivel : n.Numero == numero));
            AyudaFoco.EnfocarTarjeta(lstNiveles, indice);
        }

        private void Nivel_Click(object sender, RoutedEventArgs e)
        {
            if (sender is FrameworkElement { DataContext: NivelAprendizaje nivel })
                NivelElegido?.Invoke(this, nivel);
        }

        // Teclas 1-4 (fila de números o teclado numérico) eligen el nivel directamente
        private void Vista_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            if (Keyboard.Modifiers != ModifierKeys.None)
                return;

            int numero = e.Key switch
            {
                >= Key.D1 and <= Key.D4 => e.Key - Key.D0,
                >= Key.NumPad1 and <= Key.NumPad4 => e.Key - Key.NumPad0,
                _ => 0
            };

            NivelAprendizaje? nivel = _niveles.FirstOrDefault(n => n.Numero == numero);
            if (nivel is not null)
            {
                NivelElegido?.Invoke(this, nivel);
                e.Handled = true;
            }
        }
    }
}
