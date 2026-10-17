using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Automation;
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

            // Tamaño del texto del perfil (accesibilidad). Si se cambia en Configuración, se aplica al instante.
            _anchoBase = Width;
            _altoBase = Height;
            TamanoTexto.Establecer(_preferencias.TamanoFuente);
            AplicarTamanoTexto();
            TamanoTexto.Cambio += TamanoTexto_Cambio;
            Closed += (s, e) => TamanoTexto.Cambio -= TamanoTexto_Cambio;

            // Barra lateral: cada ítem muestra su nombre y atajo al pasar el mouse (útil con la barra achicada)
            foreach (Button item in ItemsBarra())
                item.ToolTip = TextoDeItem(item);
            AplicarBarra(_preferencias.BarraColapsada);
        }

        // ===== Barra lateral achicable =====

        private const double AnchoBarra = 220;
        private const double AnchoBarraColapsada = 72;

        private IEnumerable<Button> ItemsBarra() => _itemsNav.Values.Append(navSalir);

        // "Aprender (Ctrl+1)": el nombre y, si tiene, el atajo que se ve a la derecha del ítem
        private static string TextoDeItem(Button item)
        {
            var textos = ((Grid)item.Content).Children.OfType<TextBlock>().Where(t => Grid.GetColumn(t) >= 1).Select(t => t.Text).ToList();
            return textos.Count > 1 ? $"{textos[0]} ({textos[1]})" : textos.FirstOrDefault() ?? string.Empty;
        }

        private void BtnColapsarBarra_Click(object sender, RoutedEventArgs e) => AlternarBarra();

        private void AlternarBarra()
        {
            AplicarBarra(!_preferencias.BarraColapsada); // el foco queda donde estaba (con Ctrl+B no se pierde lo que se escribía)

            // Se recuerda en el perfil; si la base falla, vale solo para esta sesión
            try
            {
                new RepositorioPreferencias().Guardar(_preferencias);
            }
            catch (Exception ex) when (ex is DatabaseException or InvalidInputException)
            {
            }
        }

        // Achicada: solo los íconos (los nombres quedan en el tooltip y en el lector de pantalla)
        private void AplicarBarra(bool colapsada)
        {
            _preferencias.BarraColapsada = colapsada;
            barraLateral.Width = colapsada ? AnchoBarraColapsada : AnchoBarra;
            barraLateral.Padding = colapsada ? new Thickness(10, 20, 10, 20) : new Thickness(14, 20, 14, 20);
            pnlMarca.Visibility = colapsada ? Visibility.Collapsed : Visibility.Visible;
            pnlUsuarioLateral.Visibility = colapsada ? Visibility.Hidden : Visibility.Visible; // Hidden: los ítems no se mueven de altura

            // El botón queda centrado cuando no está la marca al lado
            Grid.SetColumn(btnColapsarBarra, colapsada ? 0 : 1);
            Grid.SetColumnSpan(btnColapsarBarra, colapsada ? 2 : 1);
            btnColapsarBarra.HorizontalAlignment = colapsada ? HorizontalAlignment.Center : HorizontalAlignment.Right;
            icoColapsarBarra.Text = colapsada ? "" : ""; // flecha a la derecha / a la izquierda
            string accion = colapsada ? "Mostrar la barra lateral (Ctrl+B)" : "Achicar la barra lateral (Ctrl+B)";
            btnColapsarBarra.ToolTip = accion;
            AutomationProperties.SetName(btnColapsarBarra, accion);

            foreach (Button item in ItemsBarra())
            {
                var contenido = (Grid)item.Content;
                contenido.ColumnDefinitions[0].Width = colapsada ? GridLength.Auto : new GridLength(28);
                foreach (TextBlock texto in contenido.Children.OfType<TextBlock>().Where(t => Grid.GetColumn(t) >= 1))
                    texto.Visibility = colapsada ? Visibility.Collapsed : Visibility.Visible;
                item.HorizontalContentAlignment = colapsada ? HorizontalAlignment.Center : HorizontalAlignment.Stretch;
            }
        }

        // Tamaño de la ventana con el texto normal (la del XAML): con texto más grande, crece en la misma proporción
        private readonly double _anchoBase;
        private readonly double _altoBase;

        private void TamanoTexto_Cambio(object? sender, EventArgs e) => AplicarTamanoTexto();

        private void AplicarTamanoTexto()
        {
            TamanoTexto.AplicarA(this);
            TamanoTexto.AjustarTamanoVentana(this, _anchoBase, _altoBase);
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

            UserControl vista = CrearVista(destino);
            contenidoSeccion.Content = vista;

            panelInicio.Visibility = Visibility.Collapsed;
            contenidoSeccion.Visibility = Visibility.Visible;
            barraLateral.Visibility = Visibility.Visible;

            MarcarItemActivo(destino);
            _seccionActual = destino;

            // Las secciones con pasos ponen el foco en su contenido (así el teclado funciona al entrar);
            // las demás lo dejan en el ítem activo de la barra, para seguir navegando con Tab
            if (vista is IVistaDeSeccion conPasos)
                Dispatcher.BeginInvoke(DispatcherPriority.Input, conPasos.EnfocarContenido);
            else
                EnfocarDespuesDeMostrar(_itemsNav[destino]);
        }

        // Las secciones que todavía no están hechas muestran "Próximamente".
        // Cuando un módulo esté listo, se agrega acá su vista.
        private UserControl CrearVista(Seccion destino)
        {
            if (destino == Seccion.Aprender)
            {
                var aprender = new AprenderView(_perfil, _preferencias, _servicio);
                aprender.VolverSolicitado += (s, e) => MostrarInicio();
                return aprender;
            }

            if (destino == Seccion.Configuracion)
            {
                var configuracion = new ConfiguracionView(_preferencias);
                configuracion.VolverSolicitado += (s, e) => MostrarInicio();
                return configuracion;
            }

            if (destino == Seccion.MiPerfil)
            {
                var miPerfil = new PerfilView(_perfil, _servicio);
                miPerfil.VolverSolicitado += (s, e) => MostrarInicio();
                miPerfil.NavegacionSolicitada += (s, seccion) => Navegar(seccion);
                miPerfil.CerrarSesionSolicitado += (s, e) => CerrarSesion();
                miPerfil.PerfilEliminado += (s, e) => CerrarSesion(); // ya no hay nada que guardar: ServicioProgreso lo sabe
                miPerfil.PracticarCategoriaSolicitada += (s, categoriaId) =>
                {
                    // Al volver de la práctica, Mi perfil queda donde estaba (mismo scroll, foco en esa categoría)
                    double posicion = miPerfil.PosicionScroll;
                    Navegar(Seccion.Aprender);
                    if (contenidoSeccion.Content is AprenderView aprender)
                    {
                        aprender.VolverAPerfilSolicitado += (s2, e2) =>
                        {
                            Navegar(Seccion.MiPerfil);
                            if (contenidoSeccion.Content is PerfilView perfilNuevo)
                                perfilNuevo.RestaurarPosicion(posicion, categoriaId);
                        };
                        aprender.PracticarCategoria(categoriaId);
                    }
                };
                return miPerfil;
            }

            if (destino == Seccion.Diccionario)
            {
                var diccionario = new DiccionarioView();
                diccionario.VolverSolicitado += (s, e) => MostrarInicio();
                return diccionario;
            }

            if (destino == Seccion.Traductor)
            {
                var traductor = new TraductorView(_preferencias);
                traductor.VolverSolicitado += (s, e) => MostrarInicio();
                return traductor;
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

        // Vuelve al login sin cerrar la app (Closing guarda el progreso, como siempre).
        // El tema vuelve al normal: si no, el próximo que inicie sesión heredaría el alto contraste de este perfil.
        private void CerrarSesion()
        {
            TemaVisual.Aplicar(false);
            new LoginWindow().Show(); // se abre antes de cerrar el menú, así la app no se queda sin ventanas
            Close();
            TamanoTexto.Establecer(1.0); // lo mismo con el tamaño del texto: el próximo perfil usa el suyo
        }

        private void Salir()
        {
            var respuesta = MessageBox.Show("¿Querés salir de HandUp? Tu progreso ya está guardado.",
                "Salir de HandUp", MessageBoxButton.YesNo, MessageBoxImage.Question, MessageBoxResult.No);

            if (respuesta == MessageBoxResult.Yes)
                Close(); // Closing guarda el progreso y, al ser la última ventana, se cierra la app
        }

        // ===== Teclado =====
        // Inicio:  1-6 entra a cada sección, Esc sale.
        // Sección: Ctrl+1..6 cambia de sección, Esc vuelve un paso (o al inicio si ya está en el primero).
        // (Dentro de una sección los números quedan libres para los módulos, ej. responder 1-4 en Aprender.)
        private void Window_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            // En Configuración, mientras se elige una tecla nueva, cualquier tecla (incluso Esc) es para esa pantalla
            if (contenidoSeccion.Content is ConfiguracionView { CapturandoTecla: true })
                return;

            // Ctrl+B: achicar o mostrar la barra lateral (solo se ve dentro de una sección)
            if (e.Key == Key.B && Keyboard.Modifiers == ModifierKeys.Control && barraLateral.Visibility == Visibility.Visible)
            {
                AlternarBarra();
                e.Handled = true;
                return;
            }

            if (e.Key == Key.Escape)
            {
                if (_seccionActual == Seccion.Inicio)
                    Salir();
                else if (contenidoSeccion.Content is not IVistaDeSeccion conPasos || !conPasos.VolverUnPaso())
                    MostrarInicio(); // la sección no tenía un paso anterior al cual volver
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
