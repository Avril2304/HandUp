using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Threading;

namespace ProyectoFinal
{
    // Menú principal. Tiene dos estados dentro de la misma ventana:
    //  - Inicio: tiles grandes, sin barra lateral.
    //  - Sección: barra lateral a la izquierda y la vista de la sección a la derecha.
    public partial class MainMenuWindow : Window
    {
        private readonly Perfil _perfil;
        private readonly ServicioProgreso _servicio;
        private readonly PreferenciasUsuario _preferencias;

        // Ítem de la barra lateral que corresponde a cada sección (para marcar el activo)
        private readonly Dictionary<Seccion, Button> _itemsNav;

        private Seccion _seccionActual = Seccion.Inicio;

        // Tile desde el que se entró a una sección: al volver, el foco regresa ahí
        private Button? _tileOrigen;

        public MainMenuWindow(Perfil perfil, PreferenciasUsuario preferencias)
        {
            InitializeComponent();
            _perfil = perfil;
            _preferencias = preferencias;
            _servicio = new ServicioProgreso(_perfil, new RepositorioPerfiles());

            _servicio.ProgresoActualizado += Servicio_ProgresoActualizado;
            Closing += MainMenuWindow_Closing;

            _itemsNav = new Dictionary<Seccion, Button>
            {
                [Seccion.Inicio] = navInicio,
                [Seccion.Aprender] = navAprender,
                [Seccion.Desafio] = navDesafio,
                [Seccion.Traductor] = navTraductor,
                [Seccion.Diccionario] = navDiccionario,
                [Seccion.MiPerfil] = navPerfil,
                [Seccion.Configuracion] = navConfiguracion
            };

            lblCantidadSenas.Text = TextoCantidadSenas();
            ActualizarUI();
        }

        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            // Arranca con el foco en la opción principal: Enter entra directo a Aprender
            tileAprender.Focus();
        }

        // ===== Datos del perfil =====

        private void ActualizarUI()
        {
            string nombre = PrimerNombre(_perfil.Nombre);
            lblSaludo.Text = $"¡Hola, {nombre}!";
            lblNivel.Text = _perfil.Nivel;
            lblRacha.Text = $"Racha {_perfil.Racha}";
            borderRacha.ToolTip = $"Aciertos seguidos. Mejor racha: {_perfil.MejorRacha}";
            lblMejorRacha.Text = _perfil.MejorRacha > 0
                ? $"Mejor racha: {_perfil.MejorRacha}"
                : "Contrarreloj con vidas";

            lblUsuarioLateral.Text = nombre;
            lblRachaLateral.Text = _perfil.Racha.ToString();

            // Progreso dentro del nivel actual (no del total de la app)
            ProgresoNivel progreso = Niveles.ProgresoDe(_perfil);
            lblProgresoNivel.Text = $"{_perfil.Nivel}: {progreso.Dominadas} / {progreso.Total} señas";
            colProgresoLleno.Width = new GridLength(progreso.Proporcion, GridUnitType.Star);
            colProgresoVacio.Width = new GridLength(1 - progreso.Proporcion, GridUnitType.Star);
        }

        // Si la base no responde, el tile muestra un texto genérico en vez de fallar
        private static string TextoCantidadSenas()
        {
            try
            {
                int cantidad = Database.Ejecutar(conexion =>
                {
                    using var comando = conexion.CreateCommand();
                    comando.CommandText = "SELECT COUNT(*) FROM Senas";
                    return Convert.ToInt32(comando.ExecuteScalar());
                });
                return cantidad > 0 ? $"{cantidad} señas para consultar" : "Buscá cualquier seña";
            }
            catch (DatabaseException)
            {
                return "Buscá cualquier seña";
            }
        }

        private void Servicio_ProgresoActualizado(object? sender, ProgresoEventArgs e)
        {
            // Nos aseguramos de actualizar en el hilo de la UI
            Dispatcher.Invoke(ActualizarUI);
        }

        private void MainMenuWindow_Closing(object? sender, System.ComponentModel.CancelEventArgs e)
        {
            try
            {
                _servicio.GuardarProgreso("Cierre de aplicación");
            }
            catch (DatabaseException ex)
            {
                MessageBox.Show($"No se pudo guardar tu progreso.\n\nDetalle: {ex.Message}",
                    "HandUp", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }

        private static string PrimerNombre(string nombreCompleto)
        {
            if (string.IsNullOrWhiteSpace(nombreCompleto))
                return "!";

            return nombreCompleto.Split(' ', StringSplitOptions.RemoveEmptyEntries)[0];
        }

        // ===== Navegación =====

        // Un solo manejador para todos los tiles e ítems de la barra: la sección viene en CommandParameter
        private void Nav_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button boton && boton.CommandParameter is Seccion destino)
                Navegar(destino);
        }

        private void Navegar(Seccion destino)
        {
            if (destino == Seccion.Inicio)
            {
                MostrarInicio();
                return;
            }

            if (_seccionActual == Seccion.Inicio)
                _tileOrigen = Keyboard.FocusedElement as Button;

            contenidoSeccion.Content = CrearVista(destino);

            panelInicio.Visibility = Visibility.Collapsed;
            contenidoSeccion.Visibility = Visibility.Visible;
            barraLateral.Visibility = Visibility.Visible;

            MarcarItemActivo(destino);
            _seccionActual = destino;

            // El foco queda en el ítem activo de la barra, así se sigue navegando con Tab
            EnfocarDespuesDeMostrar(_itemsNav[destino]);
        }

        // Las secciones que todavía no están hechas muestran "Próximamente".
        // Cuando un módulo esté listo, se agrega acá su vista.
        private UserControl CrearVista(Seccion destino)
        {
            if (destino == Seccion.Configuracion)
            {
                var configuracion = new ConfiguracionView(_preferencias);
                configuracion.VolverSolicitado += (s, e) => MostrarInicio();
                return configuracion;
            }

            var proximamente = new ProximamenteView(Secciones.Info[destino]);
            proximamente.VolverSolicitado += (s, e) => MostrarInicio();
            return proximamente;
        }

        private void MostrarInicio()
        {
            contenidoSeccion.Content = null;
            contenidoSeccion.Visibility = Visibility.Collapsed;
            barraLateral.Visibility = Visibility.Collapsed;
            panelInicio.Visibility = Visibility.Visible;

            _seccionActual = Seccion.Inicio;
            ActualizarUI();

            EnfocarDespuesDeMostrar(_tileOrigen ?? tileAprender);
        }

        private void MarcarItemActivo(Seccion activa)
        {
            foreach (var (seccion, item) in _itemsNav)
                item.Tag = seccion == activa ? "activo" : null;
        }

        // Un control recién hecho visible todavía no se puede enfocar: se espera a que WPF lo dibuje
        private void EnfocarDespuesDeMostrar(Control control)
        {
            Dispatcher.BeginInvoke(DispatcherPriority.Input, () => control.Focus());
        }

        private void Salir_Click(object sender, RoutedEventArgs e) => Salir();

        private void Salir()
        {
            var respuesta = MessageBox.Show("¿Querés salir de HandUp? Tu progreso ya está guardado.",
                "Salir de HandUp", MessageBoxButton.YesNo, MessageBoxImage.Question, MessageBoxResult.No);

            if (respuesta == MessageBoxResult.Yes)
                Close(); // Closing guarda el progreso y, al ser la última ventana, se cierra la app
        }

        // ===== Teclado =====
        // Inicio:  1-6 entra a cada sección, Esc sale.
        // Sección: Ctrl+1..6 cambia de sección, Esc vuelve al inicio.
        // (Dentro de una sección los números quedan libres para los módulos, ej. responder 1-4 en Aprender.)
        private void Window_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            // En Configuración, mientras se elige una tecla nueva, cualquier tecla (incluso Esc) es para esa pantalla
            if (contenidoSeccion.Content is ConfiguracionView { CapturandoTecla: true })
                return;

            if (e.Key == Key.Escape)
            {
                if (_seccionActual == Seccion.Inicio)
                    Salir();
                else
                    MostrarInicio();
                e.Handled = true;
                return;
            }

            Seccion? destino = SeccionPorTecla(e.Key);
            if (destino is null)
                return;

            bool conCtrl = Keyboard.Modifiers == ModifierKeys.Control;
            bool sinModificadores = Keyboard.Modifiers == ModifierKeys.None;

            if (conCtrl || (sinModificadores && _seccionActual == Seccion.Inicio))
            {
                Navegar(destino.Value);
                e.Handled = true;
            }
        }

        private static Seccion? SeccionPorTecla(Key tecla) => tecla switch
        {
            Key.D1 or Key.NumPad1 => Seccion.Aprender,
            Key.D2 or Key.NumPad2 => Seccion.Desafio,
            Key.D3 or Key.NumPad3 => Seccion.Traductor,
            Key.D4 or Key.NumPad4 => Seccion.Diccionario,
            Key.D5 or Key.NumPad5 => Seccion.MiPerfil,
            Key.D6 or Key.NumPad6 => Seccion.Configuracion,
            _ => null
        };
    }
}
