using System;
using System.Windows;
using System.Windows.Automation;
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
        private Perfil? _perfilRegistrado;

        // Estado de una regla para el checklist (Descripcion + si la contraseña actual la cumple)
        private record EstadoRegla(string Descripcion, bool Cumplida)
        {
            public override string ToString() => $"{Descripcion}: {(Cumplida ? "cumplida" : "pendiente")}";
        }

        public RegisterWindow()
        {
            InitializeComponent();
            chkAltoContraste.IsChecked = TemaVisual.AltoContrasteActivo;
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
                _perfilRegistrado = nuevoPerfil;
                MostrarExito($"¡Listo, {nuevoPerfil.Nombre}! Tu cuenta se creó correctamente.");

                if (nuevoPerfil.NivelacionPendiente is int objetivo)
                    OfrecerNivelacion(objetivo);
                else
                    IniciarRegresoAutomatico();
            }
            catch (HandUpException ex)
            {
                MostrarError(ex.Message);
            }
        }

        private void BloquearFormulario()
        {
            btnRegistrar.IsEnabled = false;
            txtNombre.IsEnabled = false;
            txtUsuario.IsEnabled = false;
            txtPassword.IsEnabled = false;
            cmbNivel.IsEnabled = false;
        }

        // Eligió un nivel mayor a Principiante: para empezar ahí tiene que aprobar la prueba final del nivel anterior.
        // No hay cuenta regresiva: decide entre rendirla ahora o empezar como Principiante.
        // Si cierra sin elegir, la nivelación queda pendiente (se rinde desde Aprender o Mi perfil).
        private void OfrecerNivelacion(int objetivo)
        {
            BloquearFormulario();
            btnRegistrar.Visibility = Visibility.Collapsed; // la cuenta ya está creada: las opciones ocupan su lugar
            string elegido = Niveles.NombreDe(objetivo);
            string prueba = Niveles.NombreDe(objetivo - 1);

            lblNivelacion.Text = $"Elegiste {elegido}. Para empezar ahí, aprobá la prueba de nivelación: la prueba final de " +
                                 $"{prueba} ({PruebaFinal.CantidadPreguntas} señas, {PruebaFinal.PorcentajeParaAprobar}% o más). " +
                                 $"Si no, empezás como Principiante.";
            AutomationProperties.SetName(btnRendirAhora, $"Rendir ahora la prueba de nivelación para {elegido}");
            pnlNivelacion.Visibility = Visibility.Visible;
            btnRendirAhora.Focus();
        }

        // Entra directo al menú (acaba de elegir su contraseña) y abre la prueba de nivelación
        private void BtnRendirAhora_Click(object sender, RoutedEventArgs e)
        {
            if (_perfilRegistrado is null)
                return;

            try
            {
                LoginWindow.AbrirMenu(_perfilRegistrado, rendirNivelacion: true);
                Close();
            }
            catch (DatabaseException ex)
            {
                MostrarError(ex.Message);
            }
        }

        private void BtnEmpezarPrincipiante_Click(object sender, RoutedEventArgs e)
        {
            if (_perfilRegistrado is null)
                return;

            try
            {
                new ServicioProgreso(_perfilRegistrado, _repositorio).DescartarNivelacion();
                IrALogin();
            }
            catch (HandUpException ex)
            {
                MostrarError(ex.Message);
            }
        }

        // Tras un registro exitoso, cuenta regresiva y vuelve sola al login (con opción de saltarla)
        private void IniciarRegresoAutomatico()
        {
            BloquearFormulario();

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

        private void ChkAltoContraste_Click(object sender, RoutedEventArgs e)
        {
            TemaVisual.Aplicar(chkAltoContraste.IsChecked == true);
        }

        private void MostrarExito(string mensaje)
        {
            pnlFeedback.Visibility = Visibility.Visible;
            pnlFeedback.SetResourceReference(Border.BackgroundProperty, "BrushSuccessBg");
            pnlFeedback.SetResourceReference(Border.BorderBrushProperty, "BrushSuccess");
            lblFeedbackMensaje.SetResourceReference(TextBlock.ForegroundProperty, "BrushSuccess");
            lblFeedbackMensaje.Text = mensaje;
            lblCountdown.SetResourceReference(TextBlock.ForegroundProperty, "BrushSuccess");
        }

        private void MostrarError(string mensaje)
        {
            pnlFeedback.Visibility = Visibility.Visible;
            pnlFeedback.SetResourceReference(Border.BackgroundProperty, "BrushErrorBg");
            pnlFeedback.SetResourceReference(Border.BorderBrushProperty, "BrushError");
            lblFeedbackMensaje.SetResourceReference(TextBlock.ForegroundProperty, "BrushError");
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
