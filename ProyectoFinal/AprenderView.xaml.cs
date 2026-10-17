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

        // Se llegó desde "Practicar" en Mi perfil y se quiere volver ahí (Esc o el botón Volver)
        public event EventHandler? VolverAPerfilSolicitado;
        private bool _vinoDeMiPerfil;

        private readonly Perfil _perfil;
        private readonly PreferenciasUsuario _preferencias;
        private readonly ServicioProgreso _servicio;
        private ElegirNivelView? _vistaNiveles;
        private ElegirCategoriaView? _vistaCategorias;
        private RecorrerSenasView? _vistaRecorrido;
        private PracticaView? _vistaPractica;
        private PracticaView? _vistaPrueba;           // prueba final del nivel (sale desde Elegir nivel)
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
            _vistaPrueba = null;
            _vistaNiveles ??= CrearVistaNiveles();
            contenidoPaso.Content = _vistaNiveles;

            lblPaso.Text = "Paso 1 de 3 · Elegí tu nivel (teclas 1 a 4)";
            ActualizarBotonVolver("Volver al inicio");
        }

        private ElegirNivelView CrearVistaNiveles()
        {
            int? mejorPorcentaje = null;
            try
            {
                mejorPorcentaje = new RepositorioPerfiles().ObtenerMejorPorcentaje(_perfil.Id, _perfil.NivelDesbloqueado);
            }
            catch (DatabaseException)
            {
                // Sin base no mostramos el mejor resultado; la prueba igual se puede rendir
            }

            var vista = new ElegirNivelView(_perfil.NivelDesbloqueado, mejorPorcentaje);
            vista.NivelElegido += (s, nivel) =>
            {
                MostrarCategorias(nivel);
                EnfocarContenido();
            };
            vista.PruebaFinalSolicitada += (s, nivel) => MostrarPruebaFinal(nivel);
            return vista;
        }

        // Prueba final del nivel: reutiliza la pantalla de Práctica en modo prueba.
        // Al salir se vuelve a armar la elección de nivel, porque puede haber cambiado el nivel desbloqueado.
        private void MostrarPruebaFinal(int nivel)
        {
            _vistaPrueba = new PracticaView(nivel, _preferencias, _servicio);
            _vistaPrueba.VolverANivelesSolicitado += (s, e) => SalirDePrueba();
            _vistaPrueba.NivelSolicitado += (s, numero) =>
            {
                SalirDePrueba();
                NivelAprendizaje? desbloqueado = _vistaNiveles?.BuscarNivel(numero);
                if (desbloqueado is not null && !desbloqueado.Bloqueado)
                {
                    MostrarCategorias(desbloqueado);
                    EnfocarContenido();
                }
            };

            contenidoPaso.Content = _vistaPrueba;
            lblPaso.Text = $"Prueba final de {Niveles.NombreDe(nivel)} · aprobás con {PruebaFinal.PorcentajeParaAprobar}% o más";
            ActualizarBotonVolver("Volver a los niveles");
            _vistaPrueba.EnfocarInicio();
        }

        private void SalirDePrueba()
        {
            _vistaNiveles = null; // se rearma con el nivel y el mejor resultado actualizados
            MostrarNiveles();
            _vistaNiveles?.EnfocarNivel();
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
            _vistaPractica.OtraCategoriaSolicitada += (s, e) =>
            {
                _vinoDeMiPerfil = false; // "Elegir otra categoría" va a las categorías, aunque se haya llegado desde Mi perfil
                VolverUnPaso();
            };
            contenidoPaso.Content = _vistaPractica;
            EnfocarContenido();
        }

        // Desde "Practicar" en Mi perfil: va directo a la práctica de esa categoría.
        // Esc (o el botón Volver) vuelve a Mi perfil; "Elegir otra categoría" va a las categorías de su nivel.
        public void PracticarCategoria(string categoriaId)
        {
            Categoria? categoria;
            try
            {
                categoria = new RepositorioCategorias().BuscarPorClave(categoriaId);
            }
            catch (DatabaseException)
            {
                return; // se queda en la elección de nivel
            }

            NivelAprendizaje? nivel = categoria is null ? null : _vistaNiveles?.BuscarNivel(categoria.Dificultad);
            if (nivel is null)
                return;

            // Una categoría de un nivel bloqueado (practicada antes de que existieran los bloqueos) no se abre:
            // se queda en la elección de nivel con el aviso de qué hay que aprobar
            if (nivel.Bloqueado)
            {
                _vistaNiveles!.AvisarBloqueado(nivel);
                _vistaNiveles.EnfocarNivel(nivel.Numero);
                return;
            }

            MostrarCategorias(nivel);
            CategoriaAprendizaje? elegida = _vistaCategorias?.BuscarCategoria(categoriaId);
            if (elegida is null)
                return;

            MostrarSenas(elegida);
            MostrarPractica();
            _vinoDeMiPerfil = true;
            ActualizarBotonVolver("Volver a Mi perfil");
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
            if (_vistaPrueba is not null)
                _vistaPrueba.EnfocarInicio();
            else if (_nivelElegido is null)
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
            if (_vinoDeMiPerfil)
            {
                _vinoDeMiPerfil = false;
                VolverAPerfilSolicitado?.Invoke(this, EventArgs.Empty);
                return true;
            }

            // Salir a mitad de la prueba final la abandona (no se guarda ningún resultado)
            if (_vistaPrueba is not null)
            {
                SalirDePrueba();
                return true;
            }

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
