using System;
using System.Windows;
using System.Windows.Input;

namespace ProyectoFinal
{
    public partial class MainMenuWindow : Window
    {
        private readonly Perfil _perfil;
        private readonly ServicioProgreso _servicio;

        public MainMenuWindow(Perfil perfil)
        {
            InitializeComponent();
            _perfil = perfil;
            _servicio = new ServicioProgreso(_perfil, new RepositorioPerfiles());
            
            _servicio.ProgresoActualizado += Servicio_ProgresoActualizado;
            Closing += MainMenuWindow_Closing;

            ActualizarUI();
        }

        private void ActualizarUI()
        {
            lblSaludo.Text = $"¡Hola, {PrimerNombre(_perfil.Nombre)}!";
            lblNivel.Text = $"Nivel {_perfil.Nivel}";
            lblRacha.Text = _perfil.Racha.ToString();
            borderRacha.ToolTip = $"Mejor racha: {_perfil.MejorRacha}";
            
            int metaNivel = ObtenerMetaNivel(_perfil.Nivel);
            lblSenas.Text = $"{_perfil.SenasDominadas} / {metaNivel}";

            if (XpTrack.ActualWidth > 0)
            {
                double proporcion = Math.Min(1.0, (double)_perfil.SenasDominadas / metaNivel);
                XpFill.Width = XpTrack.ActualWidth * proporcion;
            }
        }

        private void Servicio_ProgresoActualizado(object? sender, ProgresoEventArgs e)
        {
            // Nos aseguramos de actualizar en el hilo de la UI
            Dispatcher.Invoke(ActualizarUI);
        }

        private void MainMenuWindow_Closing(object? sender, System.ComponentModel.CancelEventArgs e)
        {
            _servicio.GuardarProgreso("Cierre de aplicación");
        }

        private int ObtenerMetaNivel(string nivel)
        {
            int n1 = 117;
            int n2 = 185;
            int n3 = 154;
            int n4 = 107;

            return nivel switch
            {
                "Principiante" => n1,
                "Básico" => n1 + n2,
                "Intermedio" => n1 + n2 + n3,
                _ => n1 + n2 + n3 + n4 // Avanzado
            };
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
            ActualizarUI();
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
