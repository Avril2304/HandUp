using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Automation.Peers;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media.Imaging;
using System.Windows.Threading;

namespace ProyectoFinal
{
    // Guía de uso: índice de secciones a la izquierda y la sección elegida a la derecha.
    // Se abre con el botón "i" del inicio, con "Guía" en la barra lateral o con F1.
    public partial class GuiaView : UserControl, IVistaDeSeccion
    {
        public event EventHandler? VolverSolicitado;

        private readonly List<SeccionGuia> _secciones = new();
        private readonly List<Button> _botonesIndice = new();
        private int _actual;
        private TextBlock? _lblTituloSeccion;

        // bienvenida: la guía se abrió sola porque es la primera vez que entra el perfil
        public GuiaView(bool bienvenida = false)
        {
            InitializeComponent();

            if (bienvenida)
            {
                lblBienvenida.Text = "¡Te damos la bienvenida a HandUp! Esta guía se abre sola la primera vez. " +
                                     "Después la encontrás en el botón \"i\", al lado de la racha, o con F1.";
                pnlBienvenida.Visibility = Visibility.Visible;
            }

            try
            {
                GuiaUso guia = GuiaUso.Cargar();
                lblTituloGuia.Text = guia.Titulo;
                lblSubtitulo.Text = guia.Subtitulo;
                _secciones.AddRange(guia.Secciones);
            }
            catch (GuiaException ex)
            {
                pnlGuia.Visibility = Visibility.Collapsed;
                pnlNavegacion.Visibility = Visibility.Collapsed;
                lblError.Text = ex.Message;
                lblError.Visibility = Visibility.Visible;
                return;
            }

            CrearIndice();
            MostrarSeccion(0, anunciar: false);
        }

        // ===== Índice =====

        private void CrearIndice()
        {
            for (int i = 0; i < _secciones.Count; i++)
            {
                int indice = i;
                SeccionGuia seccion = _secciones[i];

                var icono = new TextBlock { Text = seccion.Icono, FontSize = 14, VerticalAlignment = VerticalAlignment.Center, Width = 24 };
                icono.SetResourceReference(TextBlock.FontFamilyProperty, "FontIconos");
                icono.SetResourceReference(TextBlock.ForegroundProperty, "BrushAccent");
                var titulo = new TextBlock { Text = seccion.Titulo, FontSize = 13.5, FontWeight = FontWeights.SemiBold,
                                             TextTrimming = TextTrimming.CharacterEllipsis, VerticalAlignment = VerticalAlignment.Center };
                var contenido = new DockPanel();
                DockPanel.SetDock(icono, Dock.Left);
                contenido.Children.Add(icono);
                contenido.Children.Add(titulo);

                var boton = new Button
                {
                    Style = (Style)FindResource("OpcionRespuesta"),
                    MinHeight = 34,
                    Padding = new Thickness(0),
                    Margin = new Thickness(0, 0, 0, 5),
                    Content = contenido
                };
                AutomationProperties.SetName(boton, $"{seccion.Titulo}. Sección {i + 1} de {_secciones.Count}");
                boton.Click += (s, e) => MostrarSeccion(indice, anunciar: true);

                _botonesIndice.Add(boton);
                pnlIndice.Children.Add(boton);
            }
        }

        // ===== Sección elegida =====

        private void MostrarSeccion(int indice, bool anunciar)
        {
            if (_secciones.Count == 0)
                return;

            _actual = Math.Clamp(indice, 0, _secciones.Count - 1);
            SeccionGuia seccion = _secciones[_actual];

            for (int i = 0; i < _botonesIndice.Count; i++)
            {
                bool elegida = i == _actual;
                _botonesIndice[i].Tag = elegida ? "elegida" : null;
                AutomationProperties.SetItemStatus(_botonesIndice[i], elegida ? "elegida" : string.Empty);
            }

            pnlContenido.Children.Clear();
            ArmarEncabezado(seccion);
            ArmarPasos(seccion);
            ArmarConsejo(seccion);
            ArmarAtajos(seccion);
            ArmarCapturas(seccion);
            svContenido.ScrollToTop();

            lblPosicion.Text = $"Sección {_actual + 1} de {_secciones.Count}";
            btnAnterior.IsEnabled = _actual > 0;
            btnSiguiente.IsEnabled = _actual < _secciones.Count - 1;

            // El lector de pantalla anuncia el título de la sección nueva
            if (anunciar && _lblTituloSeccion is not null)
            {
                AutomationPeer? peer = UIElementAutomationPeer.FromElement(_lblTituloSeccion)
                                       ?? UIElementAutomationPeer.CreatePeerForElement(_lblTituloSeccion);
                peer?.RaiseAutomationEvent(AutomationEvents.LiveRegionChanged);
            }
        }

        private void ArmarEncabezado(SeccionGuia seccion)
        {
            var icono = new TextBlock { Text = seccion.Icono, FontSize = 22, VerticalAlignment = VerticalAlignment.Center };
            icono.SetResourceReference(TextBlock.FontFamilyProperty, "FontIconos");
            icono.SetResourceReference(TextBlock.ForegroundProperty, "BrushAccent");

            _lblTituloSeccion = new TextBlock { Text = seccion.Titulo, FontSize = 22, FontWeight = FontWeights.Bold,
                                                Margin = new Thickness(12, 0, 0, 0), TextWrapping = TextWrapping.Wrap,
                                                VerticalAlignment = VerticalAlignment.Center };
            _lblTituloSeccion.SetResourceReference(TextBlock.FontFamilyProperty, "FontHeading");
            _lblTituloSeccion.SetResourceReference(TextBlock.ForegroundProperty, "BrushInk");
            AutomationProperties.SetHeadingLevel(_lblTituloSeccion, AutomationHeadingLevel.Level2);
            AutomationProperties.SetLiveSetting(_lblTituloSeccion, AutomationLiveSetting.Polite);

            var fila = new StackPanel { Orientation = Orientation.Horizontal };
            fila.Children.Add(icono);
            fila.Children.Add(_lblTituloSeccion);
            pnlContenido.Children.Add(fila);

            if (!string.IsNullOrWhiteSpace(seccion.Resumen))
                pnlContenido.Children.Add(Texto(seccion.Resumen, 15, "BrushInk", new Thickness(0, 14, 0, 0)));
        }

        // Pasos numerados: el número va en un círculo y también se lee ("1. Elegí un nivel…")
        private void ArmarPasos(SeccionGuia seccion)
        {
            if (seccion.Pasos.Count == 0)
                return;

            pnlContenido.Children.Add(Subtitulo("CÓMO SE USA", ""));
            for (int i = 0; i < seccion.Pasos.Count; i++)
            {
                var numero = new Border { Width = 24, Height = 24, CornerRadius = new CornerRadius(12), Margin = new Thickness(0, 1, 12, 0),
                                          VerticalAlignment = VerticalAlignment.Top };
                numero.SetResourceReference(Border.BackgroundProperty, "BrushAccentSoft");
                var lblNumero = new TextBlock { Text = (i + 1).ToString(), FontSize = 12, FontWeight = FontWeights.Bold,
                                                HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center };
                lblNumero.SetResourceReference(TextBlock.ForegroundProperty, "BrushAccentDark");
                numero.Child = lblNumero;

                var fila = new DockPanel { Margin = new Thickness(0, 0, 0, 10) };
                DockPanel.SetDock(numero, Dock.Left);
                fila.Children.Add(numero);
                fila.Children.Add(Texto(seccion.Pasos[i], 14, "BrushInk", new Thickness(0)));
                AutomationProperties.SetName(fila, $"{i + 1}. {seccion.Pasos[i]}");
                pnlContenido.Children.Add(fila);
            }
        }

        private void ArmarConsejo(SeccionGuia seccion)
        {
            if (string.IsNullOrWhiteSpace(seccion.Consejo))
                return;

            var icono = new TextBlock { Text = "", FontSize = 15, Margin = new Thickness(0, 1, 10, 0), VerticalAlignment = VerticalAlignment.Top };
            icono.SetResourceReference(TextBlock.FontFamilyProperty, "FontIconos");
            icono.SetResourceReference(TextBlock.ForegroundProperty, "BrushAccentDark");

            var fila = new DockPanel();
            DockPanel.SetDock(icono, Dock.Left);
            fila.Children.Add(icono);
            fila.Children.Add(Texto(seccion.Consejo, 13.5, "BrushInk", new Thickness(0)));

            var caja = new Border { CornerRadius = new CornerRadius(12), Padding = new Thickness(14, 10, 14, 10),
                                    BorderThickness = new Thickness(1.5), Margin = new Thickness(0, 10, 0, 0), Child = fila };
            caja.SetResourceReference(Border.BackgroundProperty, "BrushAccentSoft");
            caja.SetResourceReference(Border.BorderBrushProperty, "BrushBorderAccent");
            pnlContenido.Children.Add(caja);
        }

        // Atajos: la tecla en un recuadro (como los de los tiles) y qué hace
        private void ArmarAtajos(SeccionGuia seccion)
        {
            if (seccion.Atajos.Count == 0)
                return;

            pnlContenido.Children.Add(Subtitulo("ATAJOS DE TECLADO", ""));
            var grilla = new Grid();
            grilla.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            grilla.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

            for (int i = 0; i < seccion.Atajos.Count; i++)
            {
                AtajoGuia atajo = seccion.Atajos[i];
                grilla.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

                var tecla = new Border { BorderThickness = new Thickness(1.5), CornerRadius = new CornerRadius(6),
                                         Padding = new Thickness(8, 2, 8, 2), Margin = new Thickness(0, 0, 14, 8),
                                         HorizontalAlignment = HorizontalAlignment.Left, VerticalAlignment = VerticalAlignment.Top };
                tecla.SetResourceReference(Border.BorderBrushProperty, "BrushBorderAccent");
                var lblTecla = new TextBlock { Text = atajo.Teclas, FontSize = 12.5, FontWeight = FontWeights.Bold };
                lblTecla.SetResourceReference(TextBlock.ForegroundProperty, "BrushAccentDark");
                tecla.Child = lblTecla;
                AutomationProperties.SetName(tecla, $"{atajo.Teclas}: {atajo.Accion}");

                TextBlock accion = Texto(atajo.Accion, 13.5, "BrushInk", new Thickness(0, 2, 0, 8));
                Grid.SetRow(tecla, i);
                Grid.SetRow(accion, i);
                Grid.SetColumn(accion, 1);
                grilla.Children.Add(tecla);
                grilla.Children.Add(accion);
            }
            pnlContenido.Children.Add(grilla);
        }

        // Capturas de la app: si falta una imagen, simplemente no se muestra
        private void ArmarCapturas(SeccionGuia seccion)
        {
            bool primera = true;
            foreach (CapturaGuia captura in seccion.Capturas)
            {
                if (!File.Exists(captura.RutaAbsoluta))
                    continue;

                BitmapImage imagen;
                try
                {
                    imagen = new BitmapImage();
                    imagen.BeginInit();
                    imagen.CacheOption = BitmapCacheOption.OnLoad; // no deja el archivo bloqueado
                    imagen.UriSource = new Uri(captura.RutaAbsoluta, UriKind.Absolute);
                    imagen.EndInit();
                }
                catch (Exception ex) when (ex is IOException || ex is NotSupportedException || ex is UriFormatException)
                {
                    continue;
                }

                if (primera)
                {
                    pnlContenido.Children.Add(Subtitulo("ASÍ SE VE", ""));
                    primera = false;
                }

                var marco = new Border { BorderThickness = new Thickness(1.5), CornerRadius = new CornerRadius(10),
                                         ClipToBounds = true, HorizontalAlignment = HorizontalAlignment.Left,
                                         MaxWidth = 620, Child = new Image { Source = imagen, Stretch = System.Windows.Media.Stretch.Uniform } };
                marco.SetResourceReference(Border.BorderBrushProperty, "BrushBorder");
                AutomationProperties.SetName(marco, $"Captura: {captura.Pie}");
                pnlContenido.Children.Add(marco);
                pnlContenido.Children.Add(Texto(captura.Pie, 12.5, "BrushMuted", new Thickness(2, 6, 0, 16)));
            }
        }

        // ===== Ayudantes para armar el contenido =====

        private static TextBlock Texto(string texto, double tamano, string brush, Thickness margen)
        {
            var bloque = new TextBlock { Text = texto, FontSize = tamano, TextWrapping = TextWrapping.Wrap, Margin = margen, LineHeight = tamano * 1.45 };
            bloque.SetResourceReference(TextBlock.ForegroundProperty, brush);
            return bloque;
        }

        // Subtítulo en mayúsculas con ícono, igual que las tarjetas de Configuración ("ACCESIBILIDAD")
        private static StackPanel Subtitulo(string texto, string glifo)
        {
            var icono = new TextBlock { Text = glifo, FontSize = 14, VerticalAlignment = VerticalAlignment.Center };
            icono.SetResourceReference(TextBlock.FontFamilyProperty, "FontIconos");
            icono.SetResourceReference(TextBlock.ForegroundProperty, "BrushAccent");
            var lbl = new TextBlock { Text = texto, FontSize = 12, FontWeight = FontWeights.Bold, Margin = new Thickness(8, 0, 0, 0),
                                      VerticalAlignment = VerticalAlignment.Center };
            lbl.SetResourceReference(TextBlock.ForegroundProperty, "BrushMuted");

            var fila = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 22, 0, 12) };
            fila.Children.Add(icono);
            fila.Children.Add(lbl);
            return fila;
        }

        // ===== Botones =====

        private void BtnAnterior_Click(object sender, RoutedEventArgs e) => Mover(-1);
        private void BtnSiguiente_Click(object sender, RoutedEventArgs e) => Mover(+1);

        private void Mover(int delta)
        {
            int nuevo = _actual + delta;
            if (nuevo < 0 || nuevo >= _secciones.Count)
                return;

            MostrarSeccion(nuevo, anunciar: true);
            _botonesIndice[_actual].Focus(); // el foco acompaña a la sección (así ↑ ↓ y Tab siguen desde ahí)
        }

        // Abre el PDF con el programa que tenga la compu para PDF
        private void BtnAbrirPdf_Click(object sender, RoutedEventArgs e)
        {
            if (!File.Exists(GuiaUso.RutaPdf))
            {
                MessageBox.Show(Window.GetWindow(this), "No se encontró el PDF de la guía junto al ejecutable.",
                    "HandUp", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            try
            {
                Process.Start(new ProcessStartInfo(GuiaUso.RutaPdf) { UseShellExecute = true });
            }
            catch (Win32Exception)
            {
                MessageBox.Show(Window.GetWindow(this), "No hay ningún programa para abrir archivos PDF en esta compu.",
                    "HandUp", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }

        private void BtnVolver_Click(object sender, RoutedEventArgs e) => VolverSolicitado?.Invoke(this, EventArgs.Empty);

        // ← → cambian de sección; ↑ ↓ recorren el índice (como en una lista)
        private void Vista_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            if (Keyboard.Modifiers != ModifierKeys.None || _secciones.Count == 0)
                return;

            switch (e.Key)
            {
                case Key.Right:
                    Mover(+1);
                    e.Handled = true;
                    break;
                case Key.Left:
                    Mover(-1);
                    e.Handled = true;
                    break;
                case Key.Down when Keyboard.FocusedElement is Button b && _botonesIndice.Contains(b):
                    Mover(+1);
                    e.Handled = true;
                    break;
                case Key.Up when Keyboard.FocusedElement is Button b && _botonesIndice.Contains(b):
                    Mover(-1);
                    e.Handled = true;
                    break;
            }
        }

        // ===== IVistaDeSeccion =====

        public void EnfocarContenido()
        {
            if (_botonesIndice.Count > 0)
                Dispatcher.BeginInvoke(DispatcherPriority.Input, () => _botonesIndice[_actual].Focus());
        }

        // La guía tiene un solo paso: Esc vuelve al inicio
        public bool VolverUnPaso() => false;
    }
}
