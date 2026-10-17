using System;
using System.Windows;
using System.Windows.Automation;

namespace ProyectoFinal
{
    // Diálogo para confirmar algo que no se puede deshacer. El botón de confirmar se habilita recién cuando
    // se completó lo que pide (contraseña y/o palabra). Al confirmar ejecuta la acción: si lanza una
    // excepción propia (ej. contraseña incorrecta), se muestra el mensaje y el diálogo sigue abierto.
    public partial class DialogoConfirmacion : Window
    {
        private readonly string? _palabraClave;
        private readonly bool _pedirPassword;
        private readonly Action<string> _accion;

        private DialogoConfirmacion(string titulo, string mensaje, string textoConfirmar,
                                    string? palabraClave, bool pedirPassword, Action<string> accion)
        {
            InitializeComponent();
            TamanoTexto.AplicarA(this);
            Width *= TamanoTexto.Escala; // el alto se acomoda solo (SizeToContent)
            _palabraClave = palabraClave;
            _pedirPassword = pedirPassword;
            _accion = accion;

            Title = $"HandUp — {titulo}";
            lblTitulo.Text = titulo;
            lblMensaje.Text = mensaje;
            btnConfirmar.Content = textoConfirmar;
            AutomationProperties.SetName(btnConfirmar, textoConfirmar);

            pnlPassword.Visibility = pedirPassword ? Visibility.Visible : Visibility.Collapsed;
            if (palabraClave is not null)
            {
                lblPalabra.Text = $"Escribí {palabraClave} para confirmar";
                AutomationProperties.SetName(txtPalabra, lblPalabra.Text);
                pnlPalabra.Visibility = Visibility.Visible;
            }

            // Si no pide nada para completar, Confirmar arranca habilitado (igual el foco empieza en Cancelar)
            btnConfirmar.IsEnabled = palabraClave is null && !pedirPassword;
        }

        // Devuelve true si se confirmó y la acción terminó bien
        public static bool Mostrar(Window? duenio, string titulo, string mensaje, string textoConfirmar,
                                   string? palabraClave, bool pedirPassword, Action<string> accion)
        {
            var dialogo = new DialogoConfirmacion(titulo, mensaje, textoConfirmar, palabraClave, pedirPassword, accion)
            {
                Owner = duenio
            };
            return dialogo.ShowDialog() == true;
        }

        // El foco arranca en Cancelar: así un Enter apurado no confirma nada
        private void Window_Loaded(object sender, RoutedEventArgs e) => btnCancelar.Focus();

        private void Campo_Cambio(object sender, RoutedEventArgs e)
        {
            lblError.Visibility = Visibility.Collapsed;
            bool palabraOk = _palabraClave is null
                             || txtPalabra.Text.Trim().Equals(_palabraClave, StringComparison.OrdinalIgnoreCase);
            bool passwordOk = !_pedirPassword || txtPassword.Password.Length > 0;
            btnConfirmar.IsEnabled = palabraOk && passwordOk;
        }

        private void BtnConfirmar_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                _accion(txtPassword.Password);
                DialogResult = true;
            }
            catch (Exception ex) when (ex is InvalidInputException or DatabaseException or UserNotFoundException)
            {
                txtPassword.Clear(); // primero se limpia: al cambiar el campo se oculta el error anterior
                lblError.Text = $"✗ {ex.Message}";
                lblError.Visibility = Visibility.Visible;
                if (_pedirPassword)
                    txtPassword.Focus();
            }
        }
    }
}
