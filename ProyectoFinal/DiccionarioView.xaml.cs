using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace ProyectoFinal
{
    // Módulo Diccionario Visual (Stories 11, 12 y 13)
    // Permite buscar señas en tiempo real (sin distinguir mayúsculas ni tildes),
    // filtrar de forma combinada por categoría y tipo (video/imagen),
    // y consultar la ficha completa con el visor multimedia y la descripción del movimiento.
    public partial class DiccionarioView : UserControl, IVistaDeSeccion
    {
        public event EventHandler? VolverSolicitado;

        private readonly RepositorioSenas _repoSenas = new();
        private readonly RepositorioCategorias _repoCategorias = new();

        private readonly IReadOnlyList<Sena> _todasLasSenas;
        private readonly Dictionary<string, Categoria> _mapaCategorias = new(StringComparer.OrdinalIgnoreCase);

        // Modelo de presentación para cada elemento del ListBox
        public class SenaItem
        {
            public Sena Sena { get; }
            public string PalabraFormateada { get; }
            public string SubtituloCategoria { get; }
            public string BadgeTipo { get; }

            public SenaItem(Sena sena, Categoria? categoria)
            {
                Sena = sena;
                PalabraFormateada = TextoSena.Palabra(sena);
                string emoji = categoria?.Emoji ?? "✋";
                string nombreCat = categoria?.Nombre ?? sena.CategoriaId;
                SubtituloCategoria = $"{emoji} {nombreCat} · Nivel {sena.Dificultad}";
                BadgeTipo = sena is SenaAnimada ? "🎬 Video" : "📷 Imagen";
                _nombreAccesible = $"{PalabraFormateada}. {nombreCat}, nivel {sena.Dificultad}, {(sena is SenaAnimada ? "video" : "imagen")}";
            }

            private readonly string _nombreAccesible;

            // Accesibilidad: el lector de pantalla anuncia cada ítem de la lista con este texto
            // (sin esto lee "ProyectoFinal.DiccionarioView+SenaItem")
            public override string ToString() => _nombreAccesible;
        }

        public class PasoInstruccion
        {
            public string Numero { get; set; } = string.Empty;
            public string Texto { get; set; } = string.Empty;

            public override string ToString() => $"Paso {Numero}: {Texto}";
        }

        public DiccionarioView()
        {
            InitializeComponent();

            // 1. Cargar categorías de la base para el filtro
            try
            {
                var categorias = _repoCategorias.ObtenerTodos();
                foreach (var cat in categorias)
                {
                    _mapaCategorias[cat.Id] = cat;
                }

                cmbCategoria.Items.Add(new ComboBoxItem { Content = "Todas las categorías", Tag = null, IsSelected = true });
                foreach (var cat in categorias)
                {
                    string emoji = string.IsNullOrWhiteSpace(cat.Emoji) ? "📁" : cat.Emoji;
                    cmbCategoria.Items.Add(new ComboBoxItem { Content = $"{emoji} {cat.Nombre}", Tag = cat.Id });
                }
            }
            catch (DatabaseException)
            {
                cmbCategoria.Items.Add(new ComboBoxItem { Content = "Todas las categorías", Tag = null, IsSelected = true });
            }

            // 2. Cargar todas las señas en memoria para filtrado instantáneo en tiempo real (Story 11)
            try
            {
                _todasLasSenas = _repoSenas.ObtenerTodos();
            }
            catch (DatabaseException)
            {
                _todasLasSenas = Array.Empty<Sena>();
            }

            // Liberar el reproductor multimedia cuando se cierre la pantalla
            Unloaded += (s, e) => visor.Detener();

            // Filtrar inicialmente para mostrar todo el catálogo
            Filtrar();
        }

        // ===== Búsqueda y Filtros Combinables (Story 11 y Story 12) =====

        private void TxtBuscar_TextChanged(object sender, TextChangedEventArgs e)
        {
            lblPlaceholderBuscar.Visibility = string.IsNullOrEmpty(txtBuscar.Text)
                ? Visibility.Visible
                : Visibility.Collapsed;

            Filtrar();
        }

        private void Filtro_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            // Solo filtramos si la inicialización ya terminó
            if (_todasLasSenas is not null)
                Filtrar();
        }

        private void Filtrar()
        {
            if (_todasLasSenas is null) return;

            string textoBusqueda = Sena.NormalizarTexto(txtBuscar.Text);

            // Filtro de categoría
            string? categoriaFiltro = null;
            if (cmbCategoria.SelectedItem is ComboBoxItem itemCat && itemCat.Tag is string catId)
            {
                categoriaFiltro = catId;
            }

            // Filtro de tipo (0: todos, 1: animadas, 2: estáticas)
            int tipoFiltro = cmbTipo.SelectedIndex;

            // Aplicamos los 3 filtros combinados
            var resultados = _todasLasSenas.Where(s =>
            {
                // Filtro 1: Texto (Story 11 - Normalizado sin tildes ni mayúsculas)
                if (!string.IsNullOrEmpty(textoBusqueda))
                {
                    if (!s.PalabraNormalizada.Contains(textoBusqueda, StringComparison.OrdinalIgnoreCase))
                        return false;
                }

                // Filtro 2: Categoría (Story 12)
                if (categoriaFiltro is not null)
                {
                    if (!s.CategoriaId.Equals(categoriaFiltro, StringComparison.OrdinalIgnoreCase))
                        return false;
                }

                // Filtro 3: Tipo de seña (Story 12)
                if (tipoFiltro == 1 && s is not SenaAnimada) // Solo animadas (videos)
                    return false;
                if (tipoFiltro == 2 && s is not SenaEstatica) // Solo estáticas (imágenes)
                    return false;

                return true;
            }).ToList();

            // Transformar a items visuales
            var items = resultados.Select(s =>
            {
                _mapaCategorias.TryGetValue(s.CategoriaId, out var cat);
                return new SenaItem(s, cat);
            }).ToList();

            lstSenas.ItemsSource = items;

            // Actualizar contador y estado vacío
            int cantidad = items.Count;
            lblContador.Text = cantidad == 1
                ? "1 seña encontrada"
                : $"{cantidad} señas encontradas";

            if (cantidad == 0)
            {
                lstSenas.Visibility = Visibility.Collapsed;
                pnlSinResultados.Visibility = Visibility.Visible;
                MostrarDetalle(null);
            }
            else
            {
                lstSenas.Visibility = Visibility.Visible;
                pnlSinResultados.Visibility = Visibility.Collapsed;

                if (lstSenas.SelectedItem is not SenaItem actual || !items.Any(i => i.Sena.Palabra == actual.Sena.Palabra))
                {
                    lstSenas.SelectedIndex = 0;
                }
            }
        }

        // ===== Detalle de la seña (Story 13) =====

        private void LstSenas_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (lstSenas.SelectedItem is SenaItem item)
            {
                MostrarDetalle(item.Sena);
            }
        }

        private void MostrarDetalle(Sena? sena)
        {
            if (sena is null)
            {
                visor.Detener();
                pnlDetalleSena.Visibility = Visibility.Collapsed;
                pnlSeleccionarSena.Visibility = Visibility.Visible;
                return;
            }

            pnlSeleccionarSena.Visibility = Visibility.Collapsed;
            pnlDetalleSena.Visibility = Visibility.Visible;

            // 1. Mostrar medio en el visor (video o imagen)
            visor.Mostrar(sena);

            // 2. Título de la seña
            lblPalabraDetalle.Text = TextoSena.Palabra(sena);

            // 3. Badges informativos
            if (_mapaCategorias.TryGetValue(sena.CategoriaId, out var cat))
                lblBadgeCategoria.Text = $"{cat.Emoji} {cat.Nombre}";
            else
                lblBadgeCategoria.Text = sena.CategoriaId;

            lblBadgeNivel.Text = $"Nivel {sena.Dificultad}";
            lblBadgeTipo.Text = sena is SenaAnimada ? "🎬 Animada (Video)" : "📷 Estática (Imagen)";

            // 4. Sistematización LSA (configuración, ubicación, orientación...) arriba de nuestra descripción
            var parametros = sena.ParametrosLsa;
            lstParametros.ItemsSource = parametros;
            pnlSistematizacion.Visibility = parametros.Count > 0 ? Visibility.Visible : Visibility.Collapsed;

            // 5. Descripción del movimiento (Story 13)
            lblDescripcion.Text = string.IsNullOrWhiteSpace(sena.Descripcion)
                ? "Sin descripción disponible para esta seña."
                : sena.Descripcion;

            // 6. Paso a paso de instrucciones (Story 13: solo se muestra si existen)
            var instrucciones = sena.ListaInstrucciones;
            if (instrucciones.Count > 0)
            {
                pnlInstrucciones.Visibility = Visibility.Visible;
                lstPasos.ItemsSource = instrucciones.Select((paso, i) => new PasoInstruccion
                {
                    Numero = $"{i + 1}.",
                    Texto = paso
                }).ToList();
            }
            else
            {
                // Si no tiene pasos, se oculta completamente este bloque
                pnlInstrucciones.Visibility = Visibility.Collapsed;
                lstPasos.ItemsSource = null;
            }
        }

        // ===== Navegación y Accesibilidad (IVistaDeSeccion) =====

        public void EnfocarContenido()
        {
            txtBuscar.Focus();
        }

        public bool VolverUnPaso()
        {
            // Si el buscador tiene texto, limpiar el buscador
            if (!string.IsNullOrEmpty(txtBuscar.Text))
            {
                txtBuscar.Text = string.Empty;
                return true;
            }

            // Si hay una seña seleccionada en el detalle, deseleccionarla
            if (lstSenas.SelectedItem is not null)
            {
                lstSenas.SelectedItem = null;
                MostrarDetalle(null);
                txtBuscar.Focus();
                return true;
            }

            // Si ya no hay nada que deshacer, vuelve al menú principal
            return false;
        }

        private void BtnVolver_Click(object sender, RoutedEventArgs e)
        {
            VolverSolicitado?.Invoke(this, EventArgs.Empty);
        }

        private void Vista_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            // Flecha abajo desde el buscador enfoca la lista de señas
            if (e.Key == Key.Down && txtBuscar.IsKeyboardFocused && lstSenas.Items.Count > 0)
            {
                lstSenas.Focus();
                if (lstSenas.SelectedIndex < 0)
                    lstSenas.SelectedIndex = 0;
                e.Handled = true;
            }
        }
    }
}
