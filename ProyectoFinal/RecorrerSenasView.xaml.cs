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
    // Paso 3 de Aprender: muestra las señas de una categoría de a una, con anterior / siguiente.
    public partial class RecorrerSenasView : UserControl
    {
        // Se terminó de recorrer la categoría y se quiere practicar con sus señas
        public event EventHandler? PracticarSolicitado;

        private record PasoInstruccion(int Numero, string Texto);

        private readonly IReadOnlyList<Sena> _senas;
        private readonly PreferenciasUsuario _preferencias;
        private int _indice;

        public RecorrerSenasView(IReadOnlyList<Sena> senas, PreferenciasUsuario preferencias)
        {
            InitializeComponent();
            _senas = senas;
            _preferencias = preferencias;
            visor.TeclaPausar = _preferencias.TeclaPausar;

            if (_senas.Count == 0)
            {
                lblMensaje.Text = "Esta categoría todavía no tiene señas.";
                lblMensaje.Visibility = Visibility.Visible;
                return;
            }

            MostrarSena(0);
        }

        private void MostrarSena(int indice)
        {
            _indice = Math.Clamp(indice, 0, _senas.Count - 1);
            Sena sena = _senas[_indice];
            bool esUltima = _indice == _senas.Count - 1;

            visor.Mostrar(sena);

            string palabra = TextoSena.Palabra(sena);
            lblPalabra.Text = palabra;
            lblAvance.Text = $"SEÑA {_indice + 1} DE {_senas.Count}";
            double avance = (double)(_indice + 1) / _senas.Count;
            colAvanceLleno.Width = new GridLength(avance, GridUnitType.Star);
            colAvanceVacio.Width = new GridLength(1 - avance, GridUnitType.Star);

            lblDescripcion.Text = string.IsNullOrWhiteSpace(sena.Descripcion)
                ? "Todavía no hay una descripción para esta seña."
                : sena.Descripcion;

            // Los pasos se muestran solo si la seña los tiene (56 señas no tienen)
            var pasos = sena.ListaInstrucciones.Select((texto, i) => new PasoInstruccion(i + 1, texto.Trim())).ToList();
            lstPasos.ItemsSource = pasos;
            pnlPasos.Visibility = pasos.Count > 0 ? Visibility.Visible : Visibility.Collapsed;

            btnAnterior.IsEnabled = _indice > 0;
            lblSiguiente.Text = esUltima ? "Practicar" : "Siguiente";
            AutomationProperties.SetName(btnSiguiente, esUltima ? "Practicar con las señas de esta categoría" : "Seña siguiente");
            btnSiguiente.ToolTip = esUltima ? "Practicar con las señas de esta categoría" : "Seña siguiente (flecha derecha)";
            AutomationProperties.SetName(lblPalabra, $"{palabra}, seña {_indice + 1} de {_senas.Count}");
        }

        // Foco inicial: el botón Siguiente (Enter avanza)
        public void EnfocarInicio()
        {
            Dispatcher.BeginInvoke(DispatcherPriority.Input, () => btnSiguiente.Focus());
        }

        private void Siguiente()
        {
            if (_senas.Count == 0)
                return;

            if (_indice < _senas.Count - 1)
            {
                MostrarSena(_indice + 1);
            }
            else
            {
                visor.Detener();
                PracticarSolicitado?.Invoke(this, EventArgs.Empty);
            }
        }

        private void Anterior()
        {
            if (_indice > 0)
                MostrarSena(_indice - 1);

            // Si el foco estaba en "Anterior" y se deshabilitó (primera seña), pasa a "Siguiente"
            if (!btnAnterior.IsEnabled && btnAnterior.IsKeyboardFocused)
                btnSiguiente.Focus();
        }

        private void BtnAnterior_Click(object sender, RoutedEventArgs e) => Anterior();
        private void BtnSiguiente_Click(object sender, RoutedEventArgs e) => Siguiente();

        // Flechas: anterior / siguiente. La tecla de Pausar del usuario (Configuración) pausa el video.
        private void Vista_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            if (_senas.Count == 0 || Keyboard.Modifiers != ModifierKeys.None)
                return;

            if (e.Key == Key.Right)
            {
                if (_indice < _senas.Count - 1)
                    MostrarSena(_indice + 1);
                e.Handled = true;
            }
            else if (e.Key == Key.Left)
            {
                Anterior();
                e.Handled = true;
            }
            // Con el foco en un botón del visor, Espacio es para ese botón (ej. abrir la pantalla completa)
            else if (e.Key == _preferencias.TeclaPausar && !(e.Key == Key.Space && visor.IsKeyboardFocusWithin))
            {
                visor.AlternarPausa();
                e.Handled = true;
            }
        }
    }
}
