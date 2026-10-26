using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Input;

namespace ProyectoFinal
{
    // Pantalla completa del Traductor. No maneja la secuencia: TraductorView le dice qué mostrar en cada paso
    // y esta ventana le avisa, con eventos, lo que el usuario pide (pausar, repetir, más lento, cambiar significado).
    public partial class TraductorPantallaCompletaWindow : Window
    {
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
            TamanoTexto.AplicarA(this);
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
            FraseEnSenas.Armar(pnlFrase, elementos, elementoActual, letraActual);
        }

        public void MostrarDetalle(string detalle) => lblDetalle.Text = detalle;

        // Estado de los botones (lo decide TraductorView)
        public void ActualizarBotones(bool enPausa, bool pausaHabilitada, bool lento)
        {
            btnPausar.IsEnabled = pausaHabilitada;
            lblPausar.Text = enPausa ? "Reanudar" : "Pausar";
            icoPausar.Text = enPausa ? "" : ""; // Play / Pause
            AutomationProperties.SetName(btnPausar, enPausa ? "Reanudar la interpretación" : "Pausar la interpretación");
            btnPausar.ToolTip = $"{lblPausar.Text} ({TextoTecla.Nombre(_teclaPausar)})";

            lblLento.Text = lento ? "Normal" : "Más lento";
            AutomationProperties.SetName(btnLento, lento ? "Volver a la velocidad normal" : "Mostrar las señas más lento");
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
