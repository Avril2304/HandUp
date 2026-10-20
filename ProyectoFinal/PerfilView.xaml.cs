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
        public event EventHandler? PerfilEliminado;
        public event EventHandler<string>? PracticarCategoriaSolicitada; // con el Id de la categoría
        public event EventHandler? NivelacionSolicitada; // rendir la prueba de nivelación pendiente

        // Estado de una regla de PoliticaPassword para el checklist: "pendiente" (sin escribir), "ok" o "error"
        private record EstadoRegla(string Descripcion, string Estado)
        {
            public override string ToString() => Estado switch
            {
                "ok" => $"{Descripcion}: cumplida",
                "error" => $"{Descripcion}: no se cumple",
                _ => $"{Descripcion}: pendiente"
            };
        }

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
            new("Días seguidos", "", p =>
            {
                int dias = p.RachaDiasVigente(DateTime.Today);
                return (Numero(dias), p.MejorRachaDias > 0 ? $"récord: {Dias(p.MejorRachaDias)}" : "practicando");
            }),
        };

        private static string Dias(int dias) => dias == 1 ? "1 día" : $"{Numero(dias)} días";

        private readonly ServicioProgreso _servicio;
        private readonly Dictionary<Estadistica, (Label Tarjeta, TextBlock Valor, TextBlock Detalle)> _tarjetas = new();

        // El último perfil mostrado (de ahí sale el nombre actual al editarlo)
        private Perfil? _perfilMostrado;

        // Botón "Practicar" y fila de cada categoría (para volver a poner el foco ahí al regresar de la práctica)
        private readonly Dictionary<string, Control> _botonesPracticar = new();
        private readonly Dictionary<string, Control> _filasCategoria = new();

        // Cuánto se bajó en la pantalla (el menú lo guarda al ir a practicar y lo devuelve al volver)
        public double PosicionScroll => scroll.VerticalOffset;

        // Al volver de practicar una categoría: misma posición y el foco en esa categoría
        public void RestaurarPosicion(double desplazamiento, string categoriaId)
        {
            // Se espera a que la vista esté dibujada y a que el menú ponga el foco inicial (prioridad Input)
            Dispatcher.BeginInvoke(System.Windows.Threading.DispatcherPriority.Background, () =>
            {
                Control? destino = _botonesPracticar.GetValueOrDefault(categoriaId) ?? _filasCategoria.GetValueOrDefault(categoriaId);
                destino?.Focus();
                scroll.ScrollToVerticalOffset(desplazamiento);
            });
        }

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

            MostrarNivelacion(perfil);
            MostrarCertificado();

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

            MostrarCategorias(perfil.Id);
        }

        // ===== Estadísticas por categoría =====

        private const int MaximoParaReforzar = 3;

        private void MostrarCategorias(int perfilId)
        {
            lstReforzar.Children.Clear();
            lstCategorias.Children.Clear();
            _botonesPracticar.Clear();
            _filasCategoria.Clear();

            IReadOnlyList<EstadisticaCategoria> categorias;
            try
            {
                categorias = new RepositorioPerfiles().ObtenerEstadisticasPorCategoria(perfilId);
            }
            catch (DatabaseException ex)
            {
                MostrarMensajeCategorias($"No se pudieron leer tus estadísticas por categoría. {ex.Message}");
                return;
            }

            var practicadas = categorias.Where(c => c.Practicada).ToList();
            if (practicadas.Count == 0)
            {
                MostrarMensajeCategorias("Cuando practiques una categoría en Aprender, acá vas a ver cómo te va en cada tema.");
                return;
            }

            lblCategoriasMensaje.Visibility = Visibility.Collapsed;

            // Para practicar más: las de menor precisión (debajo de 80 %), con un botón para practicarlas
            var reforzar = practicadas.Where(c => c.PuedeMejorar)
                                      .OrderBy(c => c.Precision).ThenByDescending(c => c.Respondidas)
                                      .Take(MaximoParaReforzar).ToList();
            foreach (EstadisticaCategoria categoria in reforzar)
                lstReforzar.Children.Add(CrearFilaReforzar(categoria));
            if (reforzar.Count == 0)
            {
                var felicitacion = new TextBlock { Text = "✓ Vas muy bien en todas las categorías que practicaste.", FontSize = 13, TextWrapping = TextWrapping.Wrap };
                felicitacion.SetResourceReference(TextBlock.ForegroundProperty, "BrushSuccess");
                lstReforzar.Children.Add(felicitacion);
            }
            pnlReforzar.Visibility = Visibility.Visible;

            foreach (EstadisticaCategoria categoria in practicadas)
                lstCategorias.Children.Add(CrearFilaCategoria(categoria));
            pnlCategoriasPracticadas.Visibility = Visibility.Visible;

            // Las que nunca practicó no se muestran como 0 %: se nombran aparte
            var sinPracticar = categorias.Where(c => !c.Practicada).Select(c => c.Nombre).ToList();
            lblSinPracticar.Text = sinPracticar.Count == 0
                ? "Ya practicaste todas las categorías."
                : $"Sin practicar todavía ({sinPracticar.Count}): {string.Join(", ", sinPracticar)}.";
            lblSinPracticar.Visibility = Visibility.Visible;
        }

        private void MostrarMensajeCategorias(string mensaje)
        {
            lblCategoriasMensaje.Text = mensaje;
            lblCategoriasMensaje.Visibility = Visibility.Visible;
            pnlReforzar.Visibility = Visibility.Collapsed;
            pnlCategoriasPracticadas.Visibility = Visibility.Collapsed;
            lblSinPracticar.Visibility = Visibility.Collapsed;
        }

        private static string Porcentaje(EstadisticaCategoria c) => $"{Math.Round(c.Precision * 100)} %";

        // "🍎 Alimentos y Bebidas · 40 % (para reforzar) · 3 aciertos de 8" + botón Practicar
        private Grid CrearFilaReforzar(EstadisticaCategoria categoria)
        {
            var fila = new Grid { Margin = new Thickness(0, 4, 0, 4) };
            fila.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            fila.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            var texto = new TextBlock { FontSize = 13.5, TextWrapping = TextWrapping.Wrap, VerticalAlignment = VerticalAlignment.Center };
            texto.Inlines.Add(new System.Windows.Documents.Run($"{categoria.Emoji} {categoria.Nombre}") { FontWeight = FontWeights.SemiBold });
            texto.Inlines.Add(new System.Windows.Documents.Run($"  ·  {Porcentaje(categoria)} ({categoria.Valoracion})  ·  {categoria.Aciertos} aciertos de {categoria.Respondidas}"));
            texto.SetResourceReference(TextBlock.ForegroundProperty, "BrushInk");

            var boton = new Button
            {
                Style = (Style)FindResource("SecondaryButton"),
                Height = 36,
                Margin = new Thickness(12, 0, 0, 0),
                Content = new TextBlock { Text = "Practicar  →", Margin = new Thickness(14, 0, 14, 0) }
            };
            AutomationProperties.SetName(boton, $"Practicar {categoria.Nombre}, precisión {Porcentaje(categoria)}");
            boton.Click += (s, e) => PracticarCategoriaSolicitada?.Invoke(this, categoria.CategoriaId);
            Grid.SetColumn(boton, 1);
            _botonesPracticar[categoria.CategoriaId] = boton;

            fila.Children.Add(texto);
            fila.Children.Add(boton);
            return fila;
        }

        // Fila de una categoría practicada: nombre, aciertos, fallos, precisión (número + palabra + barra) y dominadas
        private Label CrearFilaCategoria(EstadisticaCategoria c)
        {
            var grid = new Grid();
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1.4, GridUnitType.Star) });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1.3, GridUnitType.Star) });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

            var nombre = new TextBlock { Text = $"{c.Emoji} {c.Nombre}", FontSize = 13.5, FontWeight = FontWeights.SemiBold, TextTrimming = TextTrimming.CharacterEllipsis, VerticalAlignment = VerticalAlignment.Center };
            nombre.SetResourceReference(TextBlock.ForegroundProperty, "BrushInk");

            var respuestas = Celda($"✓ {c.Aciertos}  ✗ {c.Fallos}", 1);

            // Precisión: número y palabra, con una barra chica debajo (la barra sola no alcanza)
            var precision = new StackPanel { VerticalAlignment = VerticalAlignment.Center };
            var lblPrecision = new TextBlock { Text = $"{Porcentaje(c)} · {c.Valoracion}", FontSize = 13 };
            lblPrecision.SetResourceReference(TextBlock.ForegroundProperty, c.ParaReforzar ? "BrushError" : "BrushInk");
            var barra = new Grid { Height = 5, Margin = new Thickness(0, 4, 16, 0) };
            barra.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(Math.Max(c.Precision, 0.001), GridUnitType.Star) });
            barra.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(Math.Max(1 - c.Precision, 0.001), GridUnitType.Star) });
            var lleno = new Border { CornerRadius = new CornerRadius(2.5) };
            lleno.SetResourceReference(Border.BackgroundProperty, c.ParaReforzar ? "BrushError" : "BrushAccent");
            var vacio = new Border { CornerRadius = new CornerRadius(2.5) };
            vacio.SetResourceReference(Border.BackgroundProperty, "BrushAccentSoft");
            Grid.SetColumn(vacio, 1);
            barra.Children.Add(lleno);
            barra.Children.Add(vacio);
            precision.Children.Add(lblPrecision);
            precision.Children.Add(barra);
            Grid.SetColumn(precision, 2);

            var dominadas = Celda($"{c.Dominadas} de {c.TotalSenas} dominadas", 3);

            grid.Children.Add(nombre);
            grid.Children.Add(respuestas);
            grid.Children.Add(precision);
            grid.Children.Add(dominadas);

            var fila = new Label
            {
                Style = (Style)FindResource("TarjetaEstadistica"),
                Content = grid,
                Padding = new Thickness(14, 8, 14, 8),
                Margin = new Thickness(6, 3, 6, 3)
            };
            // El lector de pantalla lee la fila completa
            AutomationProperties.SetName(fila,
                $"{c.Nombre}: {c.Aciertos} aciertos, {c.Fallos} fallos, precisión {Porcentaje(c)}, {c.Valoracion}. {c.Dominadas} de {c.TotalSenas} señas dominadas.");
            _filasCategoria[c.CategoriaId] = fila;
            return fila;
        }

        private static TextBlock Celda(string texto, int columna)
        {
            var celda = new TextBlock { Text = texto, FontSize = 13, VerticalAlignment = VerticalAlignment.Center };
            celda.SetResourceReference(TextBlock.ForegroundProperty, "BrushMuted");
            Grid.SetColumn(celda, columna);
            return celda;
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
            CerrarPanelPassword();
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
            catch (HandUpException ex)
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

        // --- Cambiar contraseña ---

        private void BtnCambiarPassword_Click(object sender, RoutedEventArgs e)
        {
            pnlEditarNombre.Visibility = Visibility.Collapsed;
            lblCuentaOk.Visibility = Visibility.Collapsed;
            lblErrorPassword.Visibility = Visibility.Collapsed;
            txtPasswordActual.Clear();
            txtPasswordNueva.Clear();
            txtPasswordConfirmar.Clear();
            ActualizarChecklist();
            pnlPassword.Visibility = Visibility.Visible;
            Dispatcher.BeginInvoke(System.Windows.Threading.DispatcherPriority.Input, () => txtPasswordActual.Focus());
        }

        // Evento: cada vez que cambia la contraseña nueva se reevalúan las reglas (y si coincide con la confirmación)
        private void TxtPasswordNueva_PasswordChanged(object sender, RoutedEventArgs e)
        {
            ActualizarChecklist();
            ActualizarCoincidencia();
        }

        private void TxtPasswordConfirmar_PasswordChanged(object sender, RoutedEventArgs e) => ActualizarCoincidencia();

        // Sin escribir nada las reglas quedan neutras; apenas hay texto, cada una se marca ✓ (verde) o ✗ (rojo)
        private void ActualizarChecklist()
        {
            string password = txtPasswordNueva.Password;
            lstReglas.ItemsSource = PoliticaPassword.Reglas
                .Select(r => new EstadoRegla(r.Descripcion,
                    password.Length == 0 ? "pendiente" : r.SeCumple(password) ? "ok" : "error"))
                .ToList();
        }

        private void ActualizarCoincidencia()
        {
            if (txtPasswordConfirmar.Password.Length == 0)
            {
                lblEstadoConfirmar.Visibility = Visibility.Collapsed;
                return;
            }

            bool coinciden = txtPasswordConfirmar.Password == txtPasswordNueva.Password;
            MostrarEstadoCampo(lblEstadoConfirmar, coinciden, coinciden ? "✓ Coinciden" : "✗ Todavía no coincide con la contraseña nueva");
        }

        // La contraseña actual se verifica al salir del campo: si está mal, se avisa antes de llegar a Guardar
        private void TxtPasswordActual_LostFocus(object sender, RoutedEventArgs e) => VerificarPasswordActual();

        // Mientras la corrige, se borra el aviso anterior
        private void TxtPasswordActual_PasswordChanged(object sender, RoutedEventArgs e) =>
            lblEstadoActual.Visibility = Visibility.Collapsed;

        // null si el campo está vacío (no se avisa nada todavía)
        private bool? VerificarPasswordActual()
        {
            if (txtPasswordActual.Password.Length == 0)
            {
                lblEstadoActual.Visibility = Visibility.Collapsed;
                return null;
            }

            bool correcta = _servicio.VerificarPassword(txtPasswordActual.Password);
            MostrarEstadoCampo(lblEstadoActual, correcta, correcta ? "✓ Contraseña correcta" : "✗ La contraseña actual no es correcta");
            return correcta;
        }

        // Aviso debajo de un campo, con símbolo y color (verde si está bien, rojo si no)
        private static void MostrarEstadoCampo(TextBlock aviso, bool bien, string texto)
        {
            aviso.Text = texto;
            aviso.SetResourceReference(TextBlock.ForegroundProperty, bien ? "BrushSuccess" : "BrushError");
            aviso.FontWeight = FontWeights.SemiBold;
            aviso.Visibility = Visibility.Visible;
        }

        // Enter pasa al campo siguiente; en el último, guarda. En la actual, si está mal no avanza.
        private void CampoPassword_KeyDown(object sender, System.Windows.Input.KeyEventArgs e)
        {
            if (e.Key != System.Windows.Input.Key.Return)
                return;

            if (sender == txtPasswordActual)
            {
                if (VerificarPasswordActual() == true)
                    txtPasswordNueva.Focus();
                else
                    txtPasswordActual.SelectAll();
            }
            else if (sender == txtPasswordNueva)
                txtPasswordConfirmar.Focus();
            else
                GuardarPassword();
            e.Handled = true;
        }

        private void BtnGuardarPassword_Click(object sender, RoutedEventArgs e) => GuardarPassword();

        private void BtnCancelarPassword_Click(object sender, RoutedEventArgs e)
        {
            CerrarPanelPassword();
            btnCambiarPassword.Focus();
        }

        private void GuardarPassword()
        {
            try
            {
                _servicio.CambiarPassword(txtPasswordActual.Password, txtPasswordNueva.Password, txtPasswordConfirmar.Password);
                CerrarPanelPassword();
                lblCuentaOk.Text = "✓ Contraseña actualizada. La próxima vez iniciá sesión con la nueva.";
                lblCuentaOk.Visibility = Visibility.Visible;
                btnCambiarPassword.Focus();
            }
            catch (HandUpException ex)
            {
                lblErrorPassword.Text = $"✗ {ex.Message}";
                lblErrorPassword.Visibility = Visibility.Visible;
            }
        }

        private void CerrarPanelPassword()
        {
            // Las contraseñas no quedan en memoria del control
            txtPasswordActual.Clear();
            txtPasswordNueva.Clear();
            txtPasswordConfirmar.Clear();
            lblErrorPassword.Visibility = Visibility.Collapsed;
            lblEstadoActual.Visibility = Visibility.Collapsed;
            lblEstadoConfirmar.Visibility = Visibility.Collapsed;
            pnlPassword.Visibility = Visibility.Collapsed;
        }

        // --- Reiniciar progreso y eliminar perfil (piden confirmación) ---

        // Si eligió un nivel más alto al registrarse y todavía no lo validó, se puede rendir acá o dejarlo de lado
        private void MostrarNivelacion(Perfil perfil)
        {
            if (perfil.NivelacionPendiente is not int objetivo)
            {
                pnlNivelacion.Visibility = Visibility.Collapsed;
                return;
            }

            string elegido = Niveles.NombreDe(objetivo);
            string prueba = Niveles.NombreDe(objetivo - 1);
            lblNivelacion.Text = $"Nivelación pendiente: {elegido}";
            lblNivelacionDetalle.Text = $"Al registrarte elegiste {elegido}. Aprobá la prueba final de {prueba} " +
                                        $"con {PruebaFinal.PorcentajeParaAprobar}% o más para empezar ahí. " +
                                        $"Mientras tanto seguís en {perfil.Nivel}" +
                                        (objetivo - 1 > perfil.NivelDesbloqueado
                                            ? $" y podés avanzar de a uno con la prueba final de {perfil.Nivel}, desde Aprender."
                                            : ".");
            AutomationProperties.SetName(btnRendirNivelacion, $"Rendir la prueba de nivelación para {elegido}");
            pnlNivelacion.Visibility = Visibility.Visible;
        }

        private void BtnRendirNivelacion_Click(object sender, RoutedEventArgs e) =>
            NivelacionSolicitada?.Invoke(this, EventArgs.Empty);

        // Si ya aprobó el examen final, su certificado (para verlo y descargarlo de nuevo cuando quiera)
        private void MostrarCertificado()
        {
            CertificadoHandUp? certificado = null;
            try
            {
                certificado = _servicio.ObtenerCertificado();
            }
            catch (DatabaseException ex)
            {
                // Sin base no se muestra; el resto de Mi perfil sigue funcionando
                RegistroErrores.Registrar(ex, "Leer el certificado en Mi perfil");
            }

            if (certificado is null)
            {
                pnlCertificado.Visibility = Visibility.Collapsed;
                return;
            }

            lblCertificado.Text = $"Aprobaste el examen final con {certificado.Porcentaje}% el " +
                                  $"{certificado.Fecha.ToString("d 'de' MMMM 'de' yyyy", new System.Globalization.CultureInfo("es-AR"))}. " +
                                  $"Certificado N.º {certificado.Codigo}.";
            AutomationProperties.SetName(btnCertificado, "Ver y descargar el certificado de HandUp");
            pnlCertificado.Visibility = Visibility.Visible;
        }

        private void BtnCertificado_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (_servicio.ObtenerCertificado() is CertificadoHandUp certificado)
                    FelicitacionesWindow.Mostrar(Window.GetWindow(this), certificado);
            }
            catch (DatabaseException ex)
            {
                MessageBox.Show(Window.GetWindow(this), $"No se pudo abrir el certificado.\n\nDetalle: {ex.Message}",
                    "HandUp", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }

        private void BtnDescartarNivelacion_Click(object sender, RoutedEventArgs e)
        {
            bool descartada = DialogoConfirmacion.Mostrar(Window.GetWindow(this),
                "Empezar como Principiante",
                "Ya no vas a poder rendir la prueba de nivelación: vas a avanzar de a un nivel, aprobando la prueba final de cada uno.",
                "Empezar como Principiante", palabraClave: null, pedirPassword: false,
                accion: _ => _servicio.DescartarNivelacion());

            if (descartada)
            {
                lblCuentaOk.Text = "✓ Listo: empezás como Principiante.";
                lblCuentaOk.Visibility = Visibility.Visible;
                datoProgresoNivel.Focus();
            }
            else
            {
                btnDescartarNivelacion.Focus();
            }
        }

        private void BtnReiniciar_Click(object sender, RoutedEventArgs e)
        {
            bool reiniciado = DialogoConfirmacion.Mostrar(Window.GetWindow(this),
                "Reiniciar tu progreso",
                "Se van a poner en cero tu nivel, tus señas dominadas, tus puntos, tus aciertos, tus fallos y tus rachas. " +
                "Tu usuario, tu contraseña y tus preferencias no cambian.\n\nEsto no se puede deshacer.",
                "Reiniciar progreso", palabraClave: "REINICIAR", pedirPassword: false,
                accion: _ => _servicio.ReiniciarProgreso());

            if (reiniciado)
            {
                lblCuentaOk.Text = "✓ Tu progreso se reinició. Podés empezar de nuevo en Aprender.";
                lblCuentaOk.Visibility = Visibility.Visible;
            }
            btnReiniciar.Focus();
        }

        private void BtnEliminarPerfil_Click(object sender, RoutedEventArgs e)
        {
            string nombre = _perfilMostrado?.Usuario ?? "tu perfil";
            bool eliminado = DialogoConfirmacion.Mostrar(Window.GetWindow(this),
                "Eliminar tu perfil",
                $"Se va a borrar el perfil «{nombre}» con todo su progreso y sus preferencias, y vas a volver al inicio de sesión.\n\n" +
                "Esto no se puede deshacer.",
                "Eliminar perfil", palabraClave: "ELIMINAR", pedirPassword: true,
                accion: password => _servicio.EliminarPerfil(password));

            if (eliminado)
                PerfilEliminado?.Invoke(this, EventArgs.Empty);
            else
                btnEliminarPerfil.Focus();
        }

        // ===== IVistaDeSeccion =====

        public void EnfocarContenido() => datoIdentidad.Focus();

        // Esc: si se está editando el nombre o la contraseña, cancela; si no, el menú vuelve al inicio
        public bool VolverUnPaso()
        {
            if (pnlEditarNombre.Visibility == Visibility.Visible)
            {
                CancelarEdicionNombre();
                return true;
            }

            if (pnlPassword.Visibility == Visibility.Visible)
            {
                CerrarPanelPassword();
                btnCambiarPassword.Focus();
                return true;
            }

            return false;
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
