using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace ProyectoFinal
{
    // Datos de una tarjeta de categoría (lo que muestra la grilla)
    public record CategoriaAprendizaje(int Atajo, Categoria Categoria, long Cantidad)
    {
        public string Nombre => Categoria.Nombre;
        public string Emoji => Categoria.Emoji ?? string.Empty;
        public string Descripcion => Categoria.Descripcion ?? string.Empty;
        public string TextoSenas => Cantidad == 1 ? " seña  →" : " señas  →";

        public string NombreAccesible => $"{Nombre}, {Cantidad} {(Cantidad == 1 ? "seña" : "señas")}. Atajo: tecla {Atajo}";
    }

    public partial class ElegirCategoriaView : UserControl
    {
        // Avisa qué categoría se eligió; AprenderView decide a qué paso ir
        public event EventHandler<CategoriaAprendizaje>? CategoriaElegida;

        private IReadOnlyList<CategoriaAprendizaje> _categorias = Array.Empty<CategoriaAprendizaje>();

        public ElegirCategoriaView(int nivel)
        {
            InitializeComponent();

            try
            {
                var repo = new RepositorioCategorias();
                _categorias = repo.ObtenerPorDificultad(nivel)
                    .Select((categoria, i) => new CategoriaAprendizaje(i + 1, categoria, repo.ContarSenas(categoria.Id)))
                    .ToList();
            }
            catch (DatabaseException ex)
            {
                MostrarMensaje($"No se pudieron cargar las categorías. {ex.Message}", esError: true);
                return;
            }

            if (_categorias.Count == 0)
                MostrarMensaje("Este nivel todavía no tiene categorías.", esError: false);
            else
                lstCategorias.ItemsSource = _categorias;
        }

        private void MostrarMensaje(string texto, bool esError)
        {
            lstCategorias.Visibility = Visibility.Collapsed;
            lblMensaje.Text = texto;
            lblMensaje.SetResourceReference(TextBlock.ForegroundProperty, esError ? "BrushError" : "BrushMuted");
            lblMensaje.Visibility = Visibility.Visible;
        }

        // Para ir directo a una categoría (ej. desde "Practicar" en Mi perfil); null si no es de este nivel
        public CategoriaAprendizaje? BuscarCategoria(string categoriaId) =>
            _categorias.FirstOrDefault(c => c.Categoria.Id.Equals(categoriaId, StringComparison.OrdinalIgnoreCase));

        // Foco inicial: la categoría indicada (por defecto, la primera)
        public void EnfocarCategoria(string? categoriaId = null)
        {
            int indice = Math.Max(0, _categorias.ToList().FindIndex(c => c.Categoria.Id == categoriaId));
            AyudaFoco.EnfocarTarjeta(lstCategorias, indice);
        }

        private void Categoria_Click(object sender, RoutedEventArgs e)
        {
            if (sender is FrameworkElement { DataContext: CategoriaAprendizaje categoria })
                CategoriaElegida?.Invoke(this, categoria);
        }

        // Teclas 1-9 (fila de números o teclado numérico) eligen la categoría directamente
        private void Vista_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            if (Keyboard.Modifiers != ModifierKeys.None)
                return;

            int numero = e.Key switch
            {
                >= Key.D1 and <= Key.D9 => e.Key - Key.D0,
                >= Key.NumPad1 and <= Key.NumPad9 => e.Key - Key.NumPad0,
                _ => 0
            };

            CategoriaAprendizaje? categoria = _categorias.FirstOrDefault(c => c.Atajo == numero);
            if (categoria is not null)
            {
                CategoriaElegida?.Invoke(this, categoria);
                e.Handled = true;
            }
        }
    }
}
