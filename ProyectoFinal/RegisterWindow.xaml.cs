using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;

namespace ProyectoFinal
{
    public partial class RegisterWindow : Window
    {
        private readonly RepositorioPerfiles _repositorio = new();
        private DispatcherTimer? _timerRedireccion;
        private int _segundosRestantes;
        private string _usuarioRegistrado = string.Empty;

        // Estado de una regla para el checklist (Descripcion + si la contraseña actual la cumple)
        private record EstadoRegla(string Descripcion, bool Cumplida);

        public RegisterWindow()
        {
            InitializeComponent();
            ActualizarChecklist();
        }

        // Evento: cada vez que cambia la contraseña, se reevalúan las reglas y se refresca el checklist
        private void TxtPassword_PasswordChanged(object sender, RoutedEventArgs e) => ActualizarChecklist();

        private void ActualizarChecklist()
        {
            string password = txtPassword.Password;
            lstReglas.ItemsSource = PoliticaPassword.Reglas
                .Select(r => new EstadoRegla(r.Descripcion, r.SeCumple(password)))
                .ToList();
        }

        private void BtnCrearPerfil_Click(object sender, RoutedEventArgs e)
        {
            string nivel = (cmbNivel.SelectedItem as ComboBoxItem)?.Content?.ToString() ?? "Principiante";

            try
            {
                Perfil nuevoPerfil = _repositorio.Registrar(txtNombre.Text, txtUsuario.Text, txtPassword.Password, nivel);

                _usuarioRegistrado = nuevoPerfil.Usuario;
                MostrarExito($"¡Listo, {nuevoPerfil.Nombre}! Tu cuenta se creó correctamente.");
                IniciarRegresoAutomatico();
            }
            catch (Exception ex) when (ex is InvalidInputException || ex is DatabaseException)
            {
                MostrarError(ex.Message);
            }
        }

        // Tras un registro exitoso, cuenta regresiva y vuelve sola al login (con opción de saltarla)
        private void IniciarRegresoAutomatico()
        {
            btnRegistrar.IsEnabled = false;
            txtNombre.IsEnabled = false;
            txtUsuario.IsEnabled = false;
            txtPassword.IsEnabled = false;
            cmbNivel.IsEnabled = false;

            _segundosRestantes = 3;
            lblCountdown.Visibility = Visibility.Visible;
            lblCountdown.Text = $"Volviendo al inicio de sesión en {_segundosRestantes}s...";

            _timerRedireccion = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
            _timerRedireccion.Tick += (s, e) =>
            {
                _segundosRestantes--;
                if (_segundosRestantes <= 0)
                {
                    _timerRedireccion!.Stop();
                    IrALogin();
                }
                else
                {
                    lblCountdown.Text = $"Volviendo al inicio de sesión en {_segundosRestantes}s...";
                }
            };
            _timerRedireccion.Start();
        }

        private void IrALogin()
        {
            var login = new LoginWindow(_usuarioRegistrado);
            login.Show();
            Close();
        }

        private void BtnVolverLogin_Click(object sender, RoutedEventArgs e)
        {
            _timerRedireccion?.Stop();

            // Si todavía no se registró nada, "volver al login" es simplemente cancelar
            if (string.IsNullOrEmpty(_usuarioRegistrado))
            {
                var login = new LoginWindow();
                login.Show();
                Close();
                return;
            }

            IrALogin();
        }

        private void MostrarExito(string mensaje)
        {
            pnlFeedback.Visibility = Visibility.Visible;
            pnlFeedback.Background = (Brush)FindResource("BrushSuccessBg");
            pnlFeedback.BorderBrush = (Brush)FindResource("BrushSuccess");
            lblFeedbackMensaje.Foreground = (Brush)FindResource("BrushSuccess");
            lblFeedbackMensaje.Text = mensaje;
            lblCountdown.Foreground = (Brush)FindResource("BrushSuccess");
        }

        private void MostrarError(string mensaje)
        {
            pnlFeedback.Visibility = Visibility.Visible;
            pnlFeedback.Background = (Brush)FindResource("BrushErrorBg");
            pnlFeedback.BorderBrush = (Brush)FindResource("BrushError");
            lblFeedbackMensaje.Foreground = (Brush)FindResource("BrushError");
            lblFeedbackMensaje.Text = mensaje;
            lblCountdown.Visibility = Visibility.Collapsed;
        }

        // Enter dispara el registro, salvo que ya estemos en la cuenta regresiva
        private void Window_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter && btnRegistrar.IsEnabled)
            {
                BtnCrearPerfil_Click(sender, e);
                e.Handled = true;
            }
        }
    }
}
