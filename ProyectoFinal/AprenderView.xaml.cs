using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Threading;

namespace ProyectoFinal
{
    // Sección Aprender. Maneja los pasos: 1) elegir nivel, 2) elegir categoría, 3) recorrer las señas.
    public partial class AprenderView : UserControl, IVistaDeSeccion
    {
        // Pide volver al inicio del menú (desde el primer paso)
        public event EventHandler? VolverSolicitado;

        private readonly Perfil _perfil;
        private ElegirNivelView? _vistaNiveles;
        private ElegirCategoriaView? _vistaCategorias;
        private int _nivelDeVistaCategorias;

        private NivelAprendizaje? _nivelElegido;
        private CategoriaAprendizaje? _categoriaElegida;

        public AprenderView(Perfil perfil)
        {
            InitializeComponent();
            _perfil = perfil;
            MostrarNiveles();
        }

        // ===== Pasos =====

        // Paso 1 (story "elegir un nivel de dificultad")
        private void MostrarNiveles()
        {
            _nivelElegido = null;
            _categoriaElegida = null;
            _vistaNiveles ??= CrearVistaNiveles();
            contenidoPaso.Content = _vistaNiveles;

            lblPaso.Text = "Paso 1 de 3 · Elegí tu nivel (teclas 1 a 4)";
            ActualizarBotonVolver("Volver al inicio");
        }

        private ElegirNivelView CrearVistaNiveles()
        {
            var vista = new ElegirNivelView(_perfil.Nivel);
            vista.NivelElegido += (s, nivel) =>
            {
                MostrarCategorias(nivel);
                EnfocarContenido();
            };
            return vista;
        }

        // Paso 2 (story "elegir una categoría temática")
        private void MostrarCategorias(NivelAprendizaje nivel)
        {
            // Si se vuelve a entrar al mismo nivel, se reusa la vista (y el foco vuelve a la categoría de antes)
            if (_vistaCategorias is null || _nivelDeVistaCategorias != nivel.Numero)
            {
                _vistaCategorias = CrearVistaCategorias(nivel);
                _nivelDeVistaCategorias = nivel.Numero;
            }

            _nivelElegido = nivel;
            _categoriaElegida = null;
            contenidoPaso.Content = _vistaCategorias;

            lblPaso.Text = $"Paso 2 de 3 · {nivel.Nombre}: elegí una categoría";
            ActualizarBotonVolver("Volver a los niveles");
        }

        private ElegirCategoriaView CrearVistaCategorias(NivelAprendizaje nivel)
        {
            var vista = new ElegirCategoriaView(nivel.Numero);
            vista.CategoriaElegida += (s, categoria) =>
            {
                MostrarSenas(categoria);
                EnfocarContenido();
            };
            return vista;
        }

        // Paso 3: se cargan solo las señas de la categoría elegida.
        // Por ahora muestra un resumen; el visor y el recorrido llegan en F3 y F4.
        private void MostrarSenas(CategoriaAprendizaje categoria)
        {
            _categoriaElegida = categoria;
            lblPaso.Text = $"Paso 3 de 3 · {categoria.Nombre}";
            ActualizarBotonVolver("Volver a las categorías");

            IReadOnlyList<Sena> senas;
            try
            {
                senas = new RepositorioSenas().BuscarPorCategoria(categoria.Categoria.Id);
            }
            catch (DatabaseException ex)
            {
                contenidoPaso.Content = new TextBlock
                {
                    Text = $"No se pudieron cargar las señas. {ex.Message}",
                    FontSize = 14,
                    TextWrapping = TextWrapping.Wrap
                };
                return;
            }

            // Provisorio hasta F4: el visor con la primera seña y la lista de las que se cargaron
            var visor = new VisorSena { Width = 360, Height = 410, VerticalAlignment = VerticalAlignment.Top };
            var detalle = new TextBlock
            {
                FontSize = 14,
                TextWrapping = TextWrapping.Wrap,
                Margin = new Thickness(24, 0, 0, 0),
                Text = $"Se cargaron {senas.Count} señas de {categoria.Nombre}:\n\n"
                     + string.Join(" · ", senas.Select(s => s.Palabra))
                     + "\n\nEl recorrido seña por seña está en construcción."
            };
            DockPanel.SetDock(visor, Dock.Left);
            contenidoPaso.Content = new DockPanel { Children = { visor, detalle } };
            if (senas.Count > 0)
                visor.Mostrar(senas[0]);
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
            if (_nivelElegido is null)
                _vistaNiveles?.EnfocarNivel();
            else if (_categoriaElegida is null)
                _vistaCategorias?.EnfocarCategoria();
            else
                Dispatcher.BeginInvoke(DispatcherPriority.Input, () => btnVolver.Focus());
        }

        public bool VolverUnPaso()
        {
            if (_categoriaElegida is not null && _nivelElegido is not null)
            {
                string idAnterior = _categoriaElegida.Categoria.Id;
                MostrarCategorias(_nivelElegido);
                _vistaCategorias?.EnfocarCategoria(idAnterior);
                return true;
            }

            if (_nivelElegido is not null)
            {
                int numeroAnterior = _nivelElegido.Numero;
                MostrarNiveles();
                _vistaNiveles?.EnfocarNivel(numeroAnterior);
                return true;
            }

            return false; // ya está en el primer paso: que el menú vuelva al inicio
        }

        private void BtnVolver_Click(object sender, RoutedEventArgs e)
        {
            if (!VolverUnPaso())
                VolverSolicitado?.Invoke(this, EventArgs.Empty);
        }
    }
}
