using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace ProyectoFinal
{
    // Diálogo con el listado completo de perfiles (se abre desde "Ver más" en el login).
    // Se usa con ShowDialog(): si devuelve true, el perfil elegido queda en PerfilElegido.
    public partial class SeleccionarPerfilWindow : Window
    {
        private readonly IReadOnlyList<Perfil> _perfiles;

        public Perfil? PerfilElegido { get; private set; }

        public SeleccionarPerfilWindow(IReadOnlyList<Perfil> perfiles)
        {
            InitializeComponent();
            _perfiles = perfiles;

            lblSubtitulo.Text = perfiles.Count == 1
                ? "Hay 1 perfil guardado en esta computadora."
                : $"Hay {perfiles.Count} perfiles guardados en esta computadora.";

            AplicarFiltro();
            Loaded += (s, e) => txtBuscar.Focus();
        }

        private void TxtBuscar_TextChanged(object sender, TextChangedEventArgs e) => AplicarFiltro();

        // Filtra por nombre o usuario sin distinguir mayúsculas ni tildes ("lucia" encuentra a "Lucía")
        private void AplicarFiltro()
        {
            string texto = txtBuscar.Text.Trim();

            List<Perfil> filtrados = _perfiles
                .Where(p => Contiene(p.Nombre, texto) || Contiene(p.Usuario, texto))
                .ToList();

            lstTodos.ItemsSource = filtrados;

            // Si queda un solo resultado lo preseleccionamos, así alcanza con apretar Enter
            if (filtrados.Count == 1)
                lstTodos.SelectedIndex = 0;

            lblSinResultados.Text = $"No hay perfiles que coincidan con \"{texto}\".";
            lblSinResultados.Visibility = filtrados.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
        }

        private static bool Contiene(string texto, string buscado)
        {
            return CultureInfo.InvariantCulture.CompareInfo.IndexOf(texto, buscado,
                CompareOptions.IgnoreCase | CompareOptions.IgnoreNonSpace) >= 0;
        }

        private void LstTodos_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            btnSeleccionar.IsEnabled = lstTodos.SelectedItem is Perfil;
        }

        private void LstTodos_MouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            // Solo confirmamos si el doble clic fue sobre un perfil (no sobre el espacio vacío)
            if (e.OriginalSource is FrameworkElement elemento && elemento.DataContext is Perfil)
                Confirmar();
        }

        private void BtnSeleccionar_Click(object sender, RoutedEventArgs e) => Confirmar();

        private void Confirmar()
        {
            if (lstTodos.SelectedItem is not Perfil perfil)
                return;

            PerfilElegido = perfil;
            DialogResult = true;   // cierra el diálogo y ShowDialog() devuelve true
        }

        // Flecha abajo desde el buscador pasa a la lista, para recorrerla sin usar el mouse
        private void TxtBuscar_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Down && lstTodos.Items.Count > 0)
            {
                if (lstTodos.SelectedIndex < 0)
                    lstTodos.SelectedIndex = 0;

                lstTodos.UpdateLayout();
                if (lstTodos.ItemContainerGenerator.ContainerFromIndex(lstTodos.SelectedIndex) is ListBoxItem item)
                    item.Focus();

                e.Handled = true;
            }
        }

        // Enter confirma desde cualquier lugar (Esc cancela gracias a IsCancel en el botón Cancelar)
        private void Window_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter && btnSeleccionar.IsEnabled)
            {
                Confirmar();
                e.Handled = true;
            }
        }
    }
}
