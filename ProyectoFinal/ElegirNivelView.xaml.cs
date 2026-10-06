using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;

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

        // Nombres y textos de cada nivel. Las categorías y la cantidad de señas salen de la base.
        private static readonly (string Nombre, string Descripcion)[] TextosNiveles =
        {
            ("Principiante", "Para empezar desde cero."),
            ("Básico", "Señas de todos los días."),
            ("Intermedio", "Ampliá tu vocabulario."),
            ("Avanzado", "Para comunicarte con fluidez.")
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
            var repo = new RepositorioCategorias();
            var niveles = new List<NivelAprendizaje>();

            for (int numero = 1; numero <= TextosNiveles.Length; numero++)
            {
                var (nombre, descripcion) = TextosNiveles[numero - 1];
                IReadOnlyList<Categoria> categorias = repo.ObtenerPorDificultad(numero);
                long cantidad = categorias.Sum(c => repo.ContarSenas(c.Id));
                string textoCategorias = string.Join(" · ", categorias.Select(c => c.Nombre));

                niveles.Add(new NivelAprendizaje(numero, nombre, descripcion, textoCategorias, cantidad,
                    EsTuNivel: nombre.Equals(nivelDelPerfil, StringComparison.OrdinalIgnoreCase)));
            }

            return niveles;
        }

        // Foco inicial: la tarjeta del nivel del perfil (o la primera)
        public void EnfocarNivel()
        {
            int indice = Math.Max(0, _niveles.ToList().FindIndex(n => n.EsTuNivel));
            Dispatcher.BeginInvoke(DispatcherPriority.Input, () =>
            {
                if (lstNiveles.ItemContainerGenerator.ContainerFromIndex(indice) is ContentPresenter contenedor
                    && BuscarBoton(contenedor) is Button boton)
                    boton.Focus();
            });
        }

        // Primer botón dentro de un elemento (la tarjeta está dentro del contenedor que genera el ItemsControl)
        private static Button? BuscarBoton(DependencyObject padre)
        {
            for (int i = 0; i < VisualTreeHelper.GetChildrenCount(padre); i++)
            {
                var hijo = VisualTreeHelper.GetChild(padre, i);
                if (hijo is Button boton)
                    return boton;
                if (BuscarBoton(hijo) is Button encontrado)
                    return encontrado;
            }
            return null;
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
