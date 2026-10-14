using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Threading;

namespace ProyectoFinal
{
    // Sección Aprender. Maneja los pasos: 1) elegir nivel, 2) elegir categoría, 3) recorrer las señas,
    // y al final la práctica de la categoría.
    public partial class AprenderView : UserControl, IVistaDeSeccion
    {
        // Pide volver al inicio del menú (desde el primer paso)
        public event EventHandler? VolverSolicitado;

        private readonly Perfil _perfil;
        private readonly PreferenciasUsuario _preferencias;
        private readonly ServicioProgreso _servicio;
        private ElegirNivelView? _vistaNiveles;
        private ElegirCategoriaView? _vistaCategorias;
        private RecorrerSenasView? _vistaRecorrido;
        private PracticaView? _vistaPractica;
        private IReadOnlyList<Sena> _senasCategoria = Array.Empty<Sena>();
        private int _nivelDeVistaCategorias;

        private NivelAprendizaje? _nivelElegido;
        private CategoriaAprendizaje? _categoriaElegida;

        public AprenderView(Perfil perfil, PreferenciasUsuario preferencias, ServicioProgreso servicio)
        {
            InitializeComponent();
            _perfil = perfil;
            _preferencias = preferencias;
            _servicio = servicio;
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
            _vistaPractica = null;
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

        // Paso 3: se cargan solo las señas de la categoría elegida y se recorren de a una
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

            _senasCategoria = senas;
            _vistaPractica = null;
            _vistaRecorrido = new RecorrerSenasView(senas, _preferencias);
            _vistaRecorrido.PracticarSolicitado += (s, e) => MostrarPractica();
            contenidoPaso.Content = _vistaRecorrido;
        }

        // Práctica (stories de opción múltiple, tipeo y Confirmar deshabilitado) con las señas de la categoría
        private void MostrarPractica()
        {
            if (_categoriaElegida is null)
                return;

            lblPaso.Text = $"Práctica · {_categoriaElegida.Nombre}";
            _vistaPractica = new PracticaView(_senasCategoria, _preferencias, _servicio);
            _vistaPractica.OtraCategoriaSolicitada += (s, e) => VolverUnPaso();
            contenidoPaso.Content = _vistaPractica;
            EnfocarContenido();
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
            else if (_vistaPractica is not null)
                _vistaPractica.EnfocarInicio();
            else
                _vistaRecorrido?.EnfocarInicio();
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
