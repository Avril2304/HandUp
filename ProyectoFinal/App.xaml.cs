using System.Windows;

namespace ProyectoFinal
{
    public partial class App : Application
    {
        // LoginWindow tiene un constructor con parámetro opcional (no uno "sin parámetros" puro),
        // así que la activamos a mano en vez de usar StartupUri.
        private void Application_Startup(object sender, StartupEventArgs e)
        {
            try
            {
                // Crea handup.db y sus tablas si no existen (y migra el viejo perfiles.json)
                Database.Inicializar();
            }
            catch (DatabaseException ex)
            {
                MessageBox.Show($"{ex.Message}\n\nDetalle: {ex.InnerException?.Message}",
                    "HandUp", MessageBoxButton.OK, MessageBoxImage.Error);
                Shutdown();
                return;
            }

            try
            {
                // Carga (o actualiza, si el JSON cambió) las categorías y señas del banco multimedia
                ImportadorSenas.Sincronizar();
            }
            catch (BancoSenasException ex)
            {
                // Sin señas la app igual puede loguear y mostrar el perfil, así que avisamos y seguimos
                MessageBox.Show($"{ex.Message}\n\nLos módulos de señas no van a tener contenido.",
                    "HandUp", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
            catch (DatabaseException ex)
            {
                MessageBox.Show($"{ex.Message}\n\nDetalle: {ex.InnerException?.Message}",
                    "HandUp", MessageBoxButton.OK, MessageBoxImage.Error);
                Shutdown();
                return;
            }

            var login = new LoginWindow();
            login.Show();
        }
    }
}
