using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Media;

namespace ProyectoFinal
{
    public partial class PerfilView : UserControl, IVistaDeSeccion
    {
        // Igual que las demás vistas: no navega por su cuenta, avisa y el menú principal decide
        public event EventHandler? VolverSolicitado;
        public event EventHandler<Seccion>? NavegacionSolicitada;
        public event EventHandler? CerrarSesionSolicitado;

        // Una estadística: cómo se llama, su ícono y cómo sacar del perfil el valor y una aclaración corta
        private record Estadistica(string Nombre, string Glifo, Func<Perfil, (string Valor, string Detalle)> Calcular);

        private static readonly CultureInfo Cultura = new("es-AR");
        private static readonly int TotalSenas = Niveles.Todos.Sum(n => n.Cantidad);

        // Primera fila: cuánto avanzó
        private static readonly Estadistica[] Avance =
        {
            new("Dominadas", "", p => (Numero(p.SenasDominadas), $"de {Numero(TotalSenas)} señas")),
            new("Puntos", "", p => (Numero(p.Puntos), "acumulados")),
            new("Racha actual", "", p => (Numero(p.Racha), "aciertos seguidos")),
            // La mejor racha nunca puede ser menor que la actual (Database ya lo corrige al arrancar; esto es por las dudas)
            new("Mejor racha", "", p => (Numero(Math.Max(p.MejorRacha, p.Racha)), "tu récord")),
        };

        // Segunda fila: cómo le va respondiendo
        private static readonly Estadistica[] Respuestas =
        {
            new("Aciertos", "", p => (Numero(p.AciertosTotales), "respuestas correctas")),
            new("Fallos", "", p => (Numero(p.FallosTotales), "respuestas incorrectas")),
            new("Precisión", "", p =>
            {
                int respondidas = p.AciertosTotales + p.FallosTotales;
                return respondidas == 0
                    ? ("Sin datos", "todavía no respondiste")
                    : ($"{Math.Round(p.Precision * 100)} %", $"de {Numero(respondidas)} respuestas");
            }),
        };

        private readonly ServicioProgreso _servicio;
        private readonly Dictionary<Estadistica, (Label Tarjeta, TextBlock Valor, TextBlock Detalle)> _tarjetas = new();

        // El último perfil mostrado (de ahí sale el nombre actual al editarlo)
        private Perfil? _perfilMostrado;

        public PerfilView(Perfil perfil, ServicioProgreso servicio)
        {
            InitializeComponent();
            _servicio = servicio;

            foreach (var estadistica in Avance)
                gridAvance.Children.Add(CrearTarjeta(estadistica));
            foreach (var estadistica in Respuestas)
                gridRespuestas.Children.Add(CrearTarjeta(estadistica));

            Mostrar(CargarGuardado(perfil));

            // Mientras la vista está abierta, cada cambio de progreso se refleja sin reabrirla
            _servicio.ProgresoActualizado += Servicio_ProgresoActualizado;
            Unloaded += (s, e) => _servicio.ProgresoActualizado -= Servicio_ProgresoActualizado;
        }

        // ===== Datos =====

        // Lee el perfil desde la base: así se comprueba que el progreso guardado se recupera bien.
        // Si la base falla, se muestran los datos de esta sesión y se avisa (la app no se cae).
        private Perfil CargarGuardado(Perfil enMemoria)
        {
            try
            {
                return new RepositorioPerfiles().BuscarPorId(enMemoria.Id);
            }
            catch (DatabaseException ex)
            {
                MostrarAviso($"No se pudieron leer tus datos guardados, así que ves los de esta sesión. Detalle: {ex.Message}");
            }
            catch (UserNotFoundException)
            {
                MostrarAviso("No se encontró tu perfil en la base de datos, así que ves los datos de esta sesión.");
            }
            return enMemoria;
        }

        private void Servicio_ProgresoActualizado(object? sender, ProgresoEventArgs e)
        {
            // Nos aseguramos de actualizar en el hilo de la UI
            Dispatcher.Invoke(() => Mostrar(e.Perfil));
        }

        private void Mostrar(Perfil perfil)
        {
            _perfilMostrado = perfil;
            ProgresoNivel progreso = Niveles.ProgresoDe(perfil);
            string desde = perfil.FechaRegistro.ToString("d 'de' MMMM 'de' yyyy", Cultura);

            // Identidad
            lblInicial.Text = perfil.Inicial;
            lblNombre.Text = perfil.Nombre;
            lblDetalleCuenta.Text = $"Usuario: {perfil.Usuario} · Miembro desde el {desde}";
            lblNivel.Text = progreso.Nivel;
            AutomationProperties.SetName(datoIdentidad,
                $"{perfil.Nombre}. Usuario {perfil.Usuario}. Miembro desde el {desde}. Nivel {progreso.Nivel}.");

            // Avance dentro del nivel
            lblTituloProgreso.Text = $"Progreso en {progreso.Nivel}";
            lblCuentaProgreso.Text = $"{progreso.Dominadas} de {progreso.Total} señas dominadas";
            colProgresoLleno.Width = new GridLength(progreso.Proporcion, GridUnitType.Star);
            colProgresoVacio.Width = new GridLength(1 - progreso.Proporcion, GridUnitType.Star);
            lblSiguienteNivel.Text = progreso.Siguiente is null
                ? "Estás en el último nivel."
                : $"Siguiente nivel: {progreso.Siguiente}";
            AutomationProperties.SetName(datoProgresoNivel,
                $"{lblTituloProgreso.Text}: {lblCuentaProgreso.Text}, {Math.Round(progreso.Proporcion * 100)} por ciento. {lblSiguienteNivel.Text}");

            // Estadísticas, o la invitación a empezar si todavía no hay nada que mostrar
            pnlEstadisticas.Visibility = perfil.SinActividad ? Visibility.Collapsed : Visibility.Visible;
            pnlSinActividad.Visibility = perfil.SinActividad ? Visibility.Visible : Visibility.Collapsed;

            foreach (var (estadistica, (tarjeta, valor, detalle)) in _tarjetas)
            {
                var (textoValor, textoDetalle) = estadistica.Calcular(perfil);
                valor.Text = textoValor;
                detalle.Text = textoDetalle;
                AutomationProperties.SetName(tarjeta, $"{estadistica.Nombre}: {textoValor}, {textoDetalle}");
            }
        }

        // ===== Armado de la UI =====

        // Tarjeta: ícono + nombre arriba, el valor grande y una aclaración abajo
        private Label CrearTarjeta(Estadistica estadistica)
        {
            var icono = new TextBlock
            {
                Text = estadistica.Glifo,
                FontFamily = (FontFamily)FindResource("FontIconos"),
                FontSize = 15,
                Margin = new Thickness(0, 1, 6, 0)
            };
            icono.SetResourceReference(TextBlock.ForegroundProperty, "BrushAccent");

            var nombre = new TextBlock { Text = estadistica.Nombre, FontSize = 12.5, FontWeight = FontWeights.SemiBold, TextWrapping = TextWrapping.Wrap };
            nombre.SetResourceReference(TextBlock.ForegroundProperty, "BrushMuted");
            Grid.SetColumn(nombre, 1);

            var encabezado = new Grid();
            encabezado.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            encabezado.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            encabezado.Children.Add(icono);
            encabezado.Children.Add(nombre);

            var valor = new TextBlock
            {
                FontFamily = (FontFamily)FindResource("FontHeading"),
                FontSize = 26,
                FontWeight = FontWeights.Bold,
                Margin = new Thickness(0, 2, 0, 0)
            };
            valor.SetResourceReference(TextBlock.ForegroundProperty, "BrushInk");

            var detalle = new TextBlock { FontSize = 12, TextWrapping = TextWrapping.Wrap };
            detalle.SetResourceReference(TextBlock.ForegroundProperty, "BrushMuted");

            var tarjeta = new Label
            {
                Style = (Style)FindResource("TarjetaEstadistica"),
                Content = new StackPanel { Children = { encabezado, valor, detalle } }
            };

            _tarjetas[estadistica] = (tarjeta, valor, detalle);
            return tarjeta;
        }

        private void MostrarAviso(string mensaje)
        {
            lblAviso.Text = mensaje;
            bordeAviso.Visibility = Visibility.Visible;
        }

        private static string Numero(int valor) => valor.ToString("N0", Cultura);

        // ===== Cuenta =====

        private void BtnEditarNombre_Click(object sender, RoutedEventArgs e)
        {
            lblCuentaOk.Visibility = Visibility.Collapsed;
            lblErrorNombre.Visibility = Visibility.Collapsed;
            txtNombre.Text = _perfilMostrado?.Nombre ?? string.Empty;
            pnlEditarNombre.Visibility = Visibility.Visible;
            Dispatcher.BeginInvoke(System.Windows.Threading.DispatcherPriority.Input, () =>
            {
                txtNombre.Focus();
                txtNombre.SelectAll();
            });
        }

        private void BtnGuardarNombre_Click(object sender, RoutedEventArgs e) => GuardarNombre();
        private void BtnCancelarNombre_Click(object sender, RoutedEventArgs e) => CancelarEdicionNombre();

        private void TxtNombre_KeyDown(object sender, System.Windows.Input.KeyEventArgs e)
        {
            if (e.Key == System.Windows.Input.Key.Return)
            {
                GuardarNombre();
                e.Handled = true;
            }
        }

        // Si queda vacío, ServicioProgreso lanza InvalidInputException y se muestra el mensaje sin cerrar la edición.
        // Al guardar se dispara ProgresoActualizado: esta vista y el saludo del menú se actualizan solos.
        private void GuardarNombre()
        {
            try
            {
                _servicio.CambiarNombre(txtNombre.Text);
                pnlEditarNombre.Visibility = Visibility.Collapsed;
                lblCuentaOk.Text = "✓ Nombre actualizado.";
                lblCuentaOk.Visibility = Visibility.Visible;
                btnEditarNombre.Focus();
            }
            catch (InvalidInputException ex)
            {
                MostrarErrorNombre(ex.Message);
            }
            catch (Exception ex) when (ex is DatabaseException or UserNotFoundException)
            {
                MostrarErrorNombre($"No se pudo guardar el nombre. {ex.Message}");
            }
        }

        private void MostrarErrorNombre(string mensaje)
        {
            lblErrorNombre.Text = $"✗ {mensaje}";
            lblErrorNombre.Visibility = Visibility.Visible;
            txtNombre.Focus();
        }

        private void CancelarEdicionNombre()
        {
            pnlEditarNombre.Visibility = Visibility.Collapsed;
            lblErrorNombre.Visibility = Visibility.Collapsed;
            btnEditarNombre.Focus();
        }

        // El progreso ya se guarda solo; el menú principal se encarga de volver al login
        private void BtnCerrarSesion_Click(object sender, RoutedEventArgs e)
        {
            CerrarSesionSolicitado?.Invoke(this, EventArgs.Empty);
        }

        // ===== IVistaDeSeccion =====

        public void EnfocarContenido() => datoIdentidad.Focus();

        // Esc: si se está editando el nombre, cancela la edición; si no, el menú vuelve al inicio
        public bool VolverUnPaso()
        {
            if (pnlEditarNombre.Visibility != Visibility.Visible)
                return false;

            CancelarEdicionNombre();
            return true;
        }

        // ===== Navegación =====

        private void BtnEmpezar_Click(object sender, RoutedEventArgs e)
        {
            NavegacionSolicitada?.Invoke(this, Seccion.Aprender);
        }

        private void BtnVolver_Click(object sender, RoutedEventArgs e)
        {
            VolverSolicitado?.Invoke(this, EventArgs.Empty);
        }
    }
}
