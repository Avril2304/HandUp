using System;
using System.Windows;
using System.Windows.Input;

namespace ProyectoFinal
{
    public partial class MainMenuWindow : Window
    {
        private readonly Perfil _perfil;
        private const int SenasParaSiguienteNivel = 100;

        public MainMenuWindow(Perfil perfil)
        {
            InitializeComponent();
            _perfil = perfil;

            lblSaludo.Text = $"¡Hola, {PrimerNombre(_perfil.Nombre)}!";
            lblNivel.Text = $"Nivel {_perfil.Nivel}";
            lblRacha.Text = _perfil.Racha.ToString();
            lblSenas.Text = $"{_perfil.SenasDominadas} / {SenasParaSiguienteNivel}";
        }

        private static string PrimerNombre(string nombreCompleto)
        {
            if (string.IsNullOrWhiteSpace(nombreCompleto))
                return "!";

            return nombreCompleto.Split(' ', StringSplitOptions.RemoveEmptyEntries)[0];
        }

        // La barra se dimensiona una vez que la ventana ya midió su ancho real
        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            double proporcion = Math.Min(1.0, (double)_perfil.SenasDominadas / SenasParaSiguienteNivel);
            XpFill.Width = XpTrack.ActualWidth * proporcion;
        }

        private void TileAprender_Click(object sender, MouseButtonEventArgs e) => MostrarModuloPendiente("Aprender");
        private void TileDesafio_Click(object sender, MouseButtonEventArgs e) => MostrarModuloPendiente("Desafío");
        private void TileTraductor_Click(object sender, MouseButtonEventArgs e) => MostrarModuloPendiente("Traductor");
        private void TileDiccionario_Click(object sender, MouseButtonEventArgs e) => MostrarModuloPendiente("Diccionario");

        private void MostrarModuloPendiente(string modulo)
        {
            MessageBox.Show($"El módulo '{modulo}' todavía no está implementado en esta demo.",
                "HandUp", MessageBoxButton.OK, MessageBoxImage.Information);
        }
    }
}
