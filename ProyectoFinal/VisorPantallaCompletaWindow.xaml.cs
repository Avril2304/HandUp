using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace ProyectoFinal
{
    // Muestra una seña a pantalla completa reutilizando el mismo VisorSena (con sus controles)
    public partial class VisorPantallaCompletaWindow : Window
    {
        private readonly Key _teclaPausar;

        // Velocidad con la que quedó el video al cerrar (el visor de origen la conserva)
        public bool Lento => visor.Lento;

        public VisorPantallaCompletaWindow(Sena sena, bool lento, Key teclaPausar, bool ocultarPalabra = false)
        {
            InitializeComponent();
            TamanoTexto.AplicarA(this);
            _teclaPausar = teclaPausar;
            visor.OcultarPalabra = ocultarPalabra;

            visor.ModoPantallaCompleta();
            visor.Lento = lento;
            visor.Mostrar(sena);
        }

        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            // El foco arranca en los controles del visor, así el teclado funciona enseguida
            visor.EnfocarControles();
        }

        // Esc sale; la tecla de Pausar del usuario pausa o reanuda el video
        private void Window_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Escape)
            {
                Close();
                e.Handled = true;
            }
            else if (e.Key == _teclaPausar && Keyboard.Modifiers == ModifierKeys.None)
            {
                // Con Espacio sobre un botón, el botón hace su acción (como en cualquier botón)
                if (e.Key == Key.Space && Keyboard.FocusedElement is Button)
                    return;

                visor.AlternarPausa();
                e.Handled = true;
            }
        }
    }
}
