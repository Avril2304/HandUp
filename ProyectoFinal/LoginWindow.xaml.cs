using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace ProyectoFinal
{
    public partial class LoginWindow : Window
    {
        // Cuántos chips de perfil entran en la fila del login; el resto se ve con "Ver más"
        private const int PerfilesVisibles = 3;

        private readonly RepositorioPerfiles _repositorio = new();
        private IReadOnlyList<Perfil> _todosLosPerfiles = new List<Perfil>();

        // usuarioRecienRegistrado != null cuando venimos de crear una cuenta nueva:
        // precarga el usuario y muestra un mensaje de bienvenida.
        public LoginWindow(string? usuarioRecienRegistrado = null)
        {
            InitializeComponent();
            chkAltoContraste.IsChecked = TemaVisual.AltoContrasteActivo;

            try
            {
                _todosLosPerfiles = _repositorio.ObtenerTodos();
            }
            catch (DatabaseException ex)
            {
                lblError.Text = ex.Message;
            }

            // El perfil recién creado se muestra primero y seleccionado, aunque haya muchos perfiles
            Perfil? recienRegistrado = _todosLosPerfiles.FirstOrDefault(p =>
                p.Usuario.Equals(usuarioRecienRegistrado, StringComparison.OrdinalIgnoreCase));
            MostrarPerfiles(recienRegistrado);

            if (!string.IsNullOrWhiteSpace(usuarioRecienRegistrado))
            {
                txtUsuario.Text = usuarioRecienRegistrado;
                lblInfo.Text = "¡Cuenta creada! Ingresá tu contraseña para continuar.";
                lblInfo.Visibility = Visibility.Visible;
                Loaded += (s, e) => txtPassword.Focus();
            }
        }

        // Muestra hasta 3 chips. Si se indica un perfil destacado, va primero y queda seleccionado.
        private void MostrarPerfiles(Perfil? destacado)
        {
            List<Perfil> visibles = _todosLosPerfiles
                .Where(p => destacado is null || p.Id != destacado.Id)
                .Take(destacado is null ? PerfilesVisibles : PerfilesVisibles - 1)
                .ToList();

            if (destacado is not null)
                visibles.Insert(0, destacado);

            lstPerfiles.ItemsSource = visibles;
            lstPerfiles.SelectedItem = destacado;

            // "Ver más" solo tiene sentido si hay perfiles que no entraron en la fila
            int ocultos = _todosLosPerfiles.Count - visibles.Count;
            btnVerMas.Visibility = ocultos > 0 ? Visibility.Visible : Visibility.Collapsed;
            lblCantidadOcultos.Text = ocultos == 1 ? "+1 perfil" : $"+{ocultos} perfiles";
        }

        // Al elegir un perfil de la lista, autocompleto el campo Usuario
        private void LstPerfiles_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (lstPerfiles.SelectedItem is Perfil perfilSeleccionado)
            {
                txtUsuario.Text = perfilSeleccionado.Usuario;
                lblError.Text = string.Empty;   // un error del intento anterior ya no aplica al nuevo perfil
            }
        }

        // Abre el listado completo; si se elige un perfil, pasa a ser el primer chip y seguimos con la contraseña
        private void BtnVerMas_Click(object sender, RoutedEventArgs e)
        {
            var selector = new SeleccionarPerfilWindow(_todosLosPerfiles) { Owner = this };

            if (selector.ShowDialog() == true && selector.PerfilElegido is not null)
            {
                MostrarPerfiles(selector.PerfilElegido);
                txtPassword.Clear();
                txtPassword.Focus();
            }
        }

        private void BtnIniciarSesion_Click(object sender, RoutedEventArgs e)
        {
            lblError.Text = string.Empty;

            try
            {
                // Verifico usuario y contraseña (hasheada) contra la base de datos
                Perfil perfil = _repositorio.IniciarSesion(txtUsuario.Text, txtPassword.Password);
                AbrirMenu(perfil);
                Close();
            }
            catch (Exception ex) when (ex is UserNotFoundException || ex is InvalidInputException || ex is DatabaseException)
            {
                lblInfo.Visibility = Visibility.Collapsed;
                lblError.Text = ex.Message;
            }
        }

        // Carga las preferencias del perfil, aplica su tema y abre el menú principal.
        // También lo usa el registro ("Rendir ahora" entra directo, sin volver a pedir la contraseña).
        public static void AbrirMenu(Perfil perfil, bool rendirNivelacion = false)
        {
            PreferenciasUsuario prefs = new RepositorioPreferencias().ObtenerPorPerfil(perfil.Id);

            // Si prendió el alto contraste en el login, se respeta y queda guardado en su perfil
            if (TemaVisual.AltoContrasteActivo && !prefs.AltoContraste)
            {
                prefs.AltoContraste = true;
                GuardarPreferenciasSinInterrumpir(prefs);
            }

            // Aplicar tema visual según preferencias
            TemaVisual.Aplicar(prefs.AltoContraste);

            var menuPrincipal = new MainMenuWindow(perfil, prefs, rendirNivelacion);
            menuPrincipal.Show();
        }

        // Un error al guardar preferencias no tiene que impedir entrar: el tema igual se aplica en esta sesión
        private static void GuardarPreferenciasSinInterrumpir(PreferenciasUsuario prefs)
        {
            try
            {
                new RepositorioPreferencias().Guardar(prefs);
            }
            catch (Exception ex) when (ex is DatabaseException || ex is InvalidInputException)
            {
            }
        }

        private void ChkAltoContraste_Click(object sender, RoutedEventArgs e)
        {
            TemaVisual.Aplicar(chkAltoContraste.IsChecked == true);
        }

        private void BtnCrearPerfil_Click(object sender, RoutedEventArgs e)
        {
            var registro = new RegisterWindow();
            registro.Show();
            Close();
        }

        // Enter en cualquier campo dispara el login, igual que si se apretara el botón
        private void Window_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter)
            {
                BtnIniciarSesion_Click(sender, e);
                e.Handled = true;
            }
        }
    }
}
