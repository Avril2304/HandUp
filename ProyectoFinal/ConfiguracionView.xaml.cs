using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Input;

namespace ProyectoFinal
{
    public partial class ConfiguracionView : UserControl
    {
        // Igual que ProximamenteView: la vista no navega, avisa y el menú principal decide
        public event EventHandler? VolverSolicitado;

        // Una acción reasignable: cómo se llama, y cómo leer y cambiar su tecla en las preferencias
        private record AccionAtajo(string Nombre, Func<PreferenciasUsuario, Key> Obtener, Action<PreferenciasUsuario, Key> Asignar);

        private static readonly AccionAtajo[] Acciones =
        {
            new("Responder opción 1", p => p.TeclaOpcion1, (p, k) => p.TeclaOpcion1 = k),
            new("Responder opción 2", p => p.TeclaOpcion2, (p, k) => p.TeclaOpcion2 = k),
            new("Responder opción 3", p => p.TeclaOpcion3, (p, k) => p.TeclaOpcion3 = k),
            new("Responder opción 4", p => p.TeclaOpcion4, (p, k) => p.TeclaOpcion4 = k),
            new("Confirmar", p => p.TeclaConfirmar, (p, k) => p.TeclaConfirmar = k),
            new("Pausar", p => p.TeclaPausar, (p, k) => p.TeclaPausar = k),
        };

        // Teclas que no se pueden asignar: Esc cancela, Tab mueve el foco y los modificadores solos no son atajos
        private static readonly HashSet<Key> TeclasReservadas = new()
        {
            Key.Escape, Key.Tab, Key.LeftCtrl, Key.RightCtrl, Key.LeftShift, Key.RightShift,
            Key.LeftAlt, Key.RightAlt, Key.LWin, Key.RWin, Key.Apps, Key.CapsLock, Key.NumLock,
            Key.System, Key.ImeProcessed, Key.DeadCharProcessed, Key.None
        };

        private readonly PreferenciasUsuario _preferencias;
        private readonly Dictionary<AccionAtajo, (TextBlock Tecla, Button Cambiar)> _filas = new();

        // Acción que está esperando su nueva tecla (null si no se está cambiando ninguna)
        private AccionAtajo? _esperandoTecla;

        // El menú principal lo consulta para no usar Esc ni Ctrl+número mientras se elige una tecla
        public bool CapturandoTecla => _esperandoTecla is not null;

        public ConfiguracionView(PreferenciasUsuario preferencias)
        {
            InitializeComponent();
            _preferencias = preferencias;
            chkAltoContraste.IsChecked = _preferencias.AltoContraste;

            foreach (var accion in Acciones)
                pnlAtajos.Children.Add(CrearFila(accion));
            ActualizarFilas();
        }

        // ===== Alto contraste =====

        private void ChkAltoContraste_Click(object sender, RoutedEventArgs e)
        {
            _preferencias.AltoContraste = chkAltoContraste.IsChecked == true;
            TemaVisual.Aplicar(_preferencias.AltoContraste);

            string? error = Guardar();
            lblError.Text = error ?? string.Empty;
            lblError.Visibility = error is null ? Visibility.Collapsed : Visibility.Visible;
        }

        // ===== Atajos de teclado =====

        // Fila: nombre de la acción · tecla actual · botón "Cambiar"
        private Grid CrearFila(AccionAtajo accion)
        {
            var fila = new Grid { Margin = new Thickness(0, 10, 0, 0) };
            fila.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            fila.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            fila.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            var nombre = new TextBlock { Text = accion.Nombre, FontSize = 14.5, VerticalAlignment = VerticalAlignment.Center };

            var tecla = new TextBlock { FontSize = 14, FontWeight = FontWeights.Bold, HorizontalAlignment = HorizontalAlignment.Center };
            tecla.SetResourceReference(TextBlock.ForegroundProperty, "BrushAccentDark");
            var recuadro = new Border
            {
                Child = tecla,
                MinWidth = 76,
                Padding = new Thickness(10, 5, 10, 5),
                Margin = new Thickness(12, 0, 12, 0),
                CornerRadius = new CornerRadius(8),
                BorderThickness = new Thickness(1.5),
                VerticalAlignment = VerticalAlignment.Center
            };
            recuadro.SetResourceReference(Border.BorderBrushProperty, "BrushBorderAccent");
            recuadro.SetResourceReference(Border.BackgroundProperty, "BrushAccentSoft");
            Grid.SetColumn(recuadro, 1);

            var cambiar = new Button
            {
                Content = "Cambiar",
                Width = 110,
                Height = 36,
                Style = (Style)FindResource("SecondaryButton")
            };
            cambiar.Click += (s, e) => EmpezarCaptura(accion);
            Grid.SetColumn(cambiar, 2);

            fila.Children.Add(nombre);
            fila.Children.Add(recuadro);
            fila.Children.Add(cambiar);

            _filas[accion] = (tecla, cambiar);
            return fila;
        }

        private void ActualizarFilas()
        {
            foreach (var (accion, (tecla, cambiar)) in _filas)
            {
                bool esperando = accion == _esperandoTecla;
                string nombreTecla = TextoTecla.Nombre(accion.Obtener(_preferencias));

                tecla.Text = esperando ? "…" : nombreTecla;
                cambiar.Content = esperando ? "Cancelar" : "Cambiar";
                AutomationProperties.SetName(cambiar, esperando
                    ? $"Cancelar el cambio de {accion.Nombre}"
                    : $"Cambiar la tecla de {accion.Nombre}. Ahora es {nombreTecla}");
            }
        }

        private void EmpezarCaptura(AccionAtajo accion)
        {
            // Volver a tocar el botón de la acción que se está cambiando la cancela
            if (_esperandoTecla == accion)
            {
                TerminarCaptura();
                MostrarEstado(null, esError: false);
                return;
            }

            _esperandoTecla = accion;
            ActualizarFilas();
            MostrarEstado($"Presioná la nueva tecla para «{accion.Nombre}». Esc para cancelar.", esError: false);
        }

        private void TerminarCaptura()
        {
            _esperandoTecla = null;
            ActualizarFilas();
        }

        // Mientras se espera una tecla, cualquier tecla que se presione es la respuesta
        private void Vista_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            if (_esperandoTecla is not AccionAtajo accion)
                return;

            e.Handled = true; // que la tecla no active botones ni mueva el foco
            Key tecla = e.Key == Key.System ? e.SystemKey : e.Key;

            if (tecla == Key.Escape)
            {
                TerminarCaptura();
                MostrarEstado("Cambio cancelado.", esError: false);
                return;
            }

            if (TeclasReservadas.Contains(tecla))
            {
                MostrarEstado($"La tecla {TextoTecla.Nombre(tecla)} no se puede usar. Probá con otra (Esc para cancelar).", esError: true);
                return;
            }

            AccionAtajo? enUso = Acciones.FirstOrDefault(a => a != accion && a.Obtener(_preferencias) == tecla);
            if (enUso is not null)
            {
                MostrarEstado($"{TextoTecla.Nombre(tecla)} ya la usa «{enUso.Nombre}». Elegí otra tecla o cambiá primero esa acción.", esError: true);
                return;
            }

            Key anterior = accion.Obtener(_preferencias);
            accion.Asignar(_preferencias, tecla);
            string? error = Guardar();
            if (error is not null)
                accion.Asignar(_preferencias, anterior); // si no se guardó, no queda a medias

            Button botonFila = _filas[accion].Cambiar;
            TerminarCaptura();
            botonFila.Focus();
            MostrarEstado(error ?? $"Listo: «{accion.Nombre}» ahora se hace con {TextoTecla.Nombre(tecla)}.", esError: error is not null);
        }

        private void BtnRestaurar_Click(object sender, RoutedEventArgs e)
        {
            TerminarCaptura();

            var originales = new PreferenciasUsuario();
            var anteriores = Acciones.Select(a => a.Obtener(_preferencias)).ToArray();
            foreach (var accion in Acciones)
                accion.Asignar(_preferencias, accion.Obtener(originales));

            string? error = Guardar();
            if (error is not null)
            {
                for (int i = 0; i < Acciones.Length; i++)
                    Acciones[i].Asignar(_preferencias, anteriores[i]);
            }

            ActualizarFilas();
            MostrarEstado(error ?? "Se restauraron las teclas originales.", esError: error is not null);
        }

        private void MostrarEstado(string? mensaje, bool esError)
        {
            lblEstadoAtajos.Text = mensaje ?? string.Empty;
            lblEstadoAtajos.Visibility = mensaje is null ? Visibility.Collapsed : Visibility.Visible;
            lblEstadoAtajos.SetResourceReference(TextBlock.ForegroundProperty, esError ? "BrushError" : "BrushAccentDark");
        }

        // ===== Guardado =====

        // Devuelve null si se guardó, o el mensaje de error. Si falla, lo ya aplicado vale solo para esta sesión.
        private string? Guardar()
        {
            try
            {
                new RepositorioPreferencias().Guardar(_preferencias);
                return null;
            }
            catch (Exception ex) when (ex is DatabaseException || ex is InvalidInputException)
            {
                return $"No se pudo guardar el cambio: {ex.Message}";
            }
        }

        private void BtnVolver_Click(object sender, RoutedEventArgs e)
        {
            TerminarCaptura();
            VolverSolicitado?.Invoke(this, EventArgs.Empty);
        }
    }
}
