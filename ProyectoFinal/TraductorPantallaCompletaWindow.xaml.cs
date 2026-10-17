using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;

namespace ProyectoFinal
{
    // Pantalla completa del Traductor. No maneja la secuencia: TraductorView le dice qué mostrar en cada paso
    // y esta ventana le avisa, con eventos, lo que el usuario pide (pausar, repetir, más lento, cambiar significado).
    public partial class TraductorPantallaCompletaWindow : Window
    {
        private static readonly Brush TextoClaro = new SolidColorBrush(Color.FromRgb(0xF1, 0xEF, 0xE8));
        private static readonly Brush TextoApagado = new SolidColorBrush(Color.FromRgb(0xB4, 0xB2, 0xA9));
        private static readonly Brush BordeNormal = new SolidColorBrush(Color.FromRgb(0x5F, 0x5E, 0x5A));
        private static readonly Brush BordeActual = new SolidColorBrush(Color.FromRgb(0x5D, 0xCA, 0xA5));
        private static readonly Brush FondoActual = new SolidColorBrush(Color.FromRgb(0x08, 0x50, 0x41));

        public event EventHandler? PausaSolicitada;
        public event EventHandler? RepetirSolicitado;
        public event EventHandler? LentoSolicitado;
        public event EventHandler? CambiarSignificadoSolicitado;

        private readonly Key _teclaPausar;

        // El visor grande: TraductorView le muestra las señas y escucha su VideoTerminado
        public VisorSena Visor => visor;

        public TraductorPantallaCompletaWindow(Key teclaPausar)
        {
            InitializeComponent();
            _teclaPausar = teclaPausar;

            visor.ModoPantallaCompleta();
            visor.MostrarControles = false; // los controles de abajo manejan toda la secuencia

            lblAtajos.Text = $"{TextoTecla.Nombre(teclaPausar)}: pausar · R: repetir · C: cambiar significado · Esc: salir";
        }

        private void Window_Loaded(object sender, RoutedEventArgs e) => btnPausar.Focus();

        // Lo llama TraductorView en cada paso de la secuencia
        public void MostrarPaso(string ahora, string detalle, string avance, double proporcion,
                                IReadOnlyList<ElementoTraduccion> elementos, int elementoActual, int letraActual)
        {
            lblAhora.Text = ahora;
            lblDetalle.Text = detalle;
            lblAvance.Text = avance;
            colAvanceLleno.Width = new GridLength(proporcion, GridUnitType.Star);
            colAvanceVacio.Width = new GridLength(1 - proporcion, GridUnitType.Star);
            ArmarFrase(elementos, elementoActual, letraActual);
        }

        public void MostrarDetalle(string detalle) => lblDetalle.Text = detalle;

        // Estado de los botones (lo decide TraductorView)
        public void ActualizarBotones(bool enPausa, bool pausaHabilitada, bool lento)
        {
            btnPausar.IsEnabled = pausaHabilitada;
            lblPausar.Text = enPausa ? "Reanudar" : "Pausar";
            icoPausar.Text = enPausa ? "" : ""; // Play / Pause
            AutomationProperties.SetName(btnPausar, enPausa ? "Reanudar la traducción" : "Pausar la traducción");
            btnPausar.ToolTip = $"{lblPausar.Text} ({TextoTecla.Nombre(_teclaPausar)})";

            lblLento.Text = lento ? "Normal" : "Más lento";
            AutomationProperties.SetName(btnLento, lento ? "Volver a la velocidad normal" : "Mostrar las señas más lento");
        }

        // La frase completa: cada palabra con su tipo; la actual resaltada (y, si se deletrea, la letra actual)
        private void ArmarFrase(IReadOnlyList<ElementoTraduccion> elementos, int elementoActual, int letraActual)
        {
            pnlFrase.Children.Clear();
            for (int i = 0; i < elementos.Count; i++)
            {
                ElementoTraduccion elemento = elementos[i];
                bool actual = i == elementoActual;

                var titulo = new TextBlock { Text = elemento.Texto, FontSize = 17, FontWeight = FontWeights.Bold, Foreground = TextoClaro };
                var detalle = new TextBlock { FontSize = 13, Foreground = TextoApagado, TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 2, 0, 0) };

                switch (elemento.Tipo)
                {
                    case TipoElemento.Sena:
                        detalle.Text = elemento.EsHomonimo ? $"Seña ({elemento.OpcionElegida + 1} de {elemento.Opciones.Count})" : "Seña";
                        break;

                    case TipoElemento.Deletreo:
                        // Las letras separadas por guiones, con la que se está mostrando resaltada
                        for (int j = 0; j < elemento.Letras.Count; j++)
                        {
                            if (j > 0)
                                detalle.Inlines.Add(new Run("-"));
                            var letra = new Run(TextoSena.Palabra(elemento.Letras[j]));
                            if (actual && j == letraActual)
                            {
                                letra.Foreground = BordeActual;
                                letra.FontWeight = FontWeights.Bold;
                                letra.TextDecorations = TextDecorations.Underline;
                            }
                            detalle.Inlines.Add(letra);
                        }
                        break;

                    default:
                        detalle.Text = "No se seña";
                        break;
                }

                var contenido = new StackPanel();
                contenido.Children.Add(titulo);
                contenido.Children.Add(detalle);

                pnlFrase.Children.Add(new Border
                {
                    Child = contenido,
                    Background = actual ? FondoActual : Brushes.Transparent,
                    BorderBrush = actual ? BordeActual : BordeNormal,
                    BorderThickness = new Thickness(actual ? 2 : 1.5),
                    CornerRadius = new CornerRadius(10),
                    Padding = new Thickness(12, 7, 12, 7),
                    Margin = new Thickness(0, 0, 8, 8),
                    Opacity = elemento.Tipo == TipoElemento.Omitida ? 0.5 : 1
                });
            }
        }

        private void BtnPausar_Click(object sender, RoutedEventArgs e) => PausaSolicitada?.Invoke(this, EventArgs.Empty);
        private void BtnRepetir_Click(object sender, RoutedEventArgs e) => RepetirSolicitado?.Invoke(this, EventArgs.Empty);
        private void BtnLento_Click(object sender, RoutedEventArgs e) => LentoSolicitado?.Invoke(this, EventArgs.Empty);

        // Esc o F salen; la tecla de Pausar del usuario pausa; R repite; C cambia el significado de un homónimo
        private void Window_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            if (Keyboard.Modifiers != ModifierKeys.None)
                return;

            // Con Espacio sobre un botón, el botón hace su acción (como en cualquier botón)
            bool espacioSobreBoton = e.Key == Key.Space && Keyboard.FocusedElement is Button;

            if (e.Key is Key.Escape or Key.F)
                Close();
            else if (e.Key == _teclaPausar && !espacioSobreBoton)
                PausaSolicitada?.Invoke(this, EventArgs.Empty);
            else if (e.Key == Key.R)
                RepetirSolicitado?.Invoke(this, EventArgs.Empty);
            else if (e.Key == Key.C)
                CambiarSignificadoSolicitado?.Invoke(this, EventArgs.Empty);
            else
                return;

            e.Handled = true;
        }
    }
}
