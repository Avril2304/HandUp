using System;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Threading;

namespace ProyectoFinal
{
    public partial class App : Application
    {
        // Evita que un error que se repite (ej. en un timer) abra un aviso encima de otro
        private bool _mostrandoError;

        // LoginWindow tiene un constructor con parámetro opcional (no uno "sin parámetros" puro),
        // así que la activamos a mano en vez de usar StartupUri.
        private void Application_Startup(object sender, StartupEventArgs e)
        {
            // Controlador global: los errores que ninguna ventana atrapó se registran en errores.log
            DispatcherUnhandledException += App_DispatcherUnhandledException;
            AppDomain.CurrentDomain.UnhandledException += Dominio_UnhandledException;
            TaskScheduler.UnobservedTaskException += Tareas_UnobservedTaskException;

            try
            {
                // Crea handup.db y sus tablas si no existen (y migra el viejo perfiles.json)
                Database.Inicializar();
            }
            catch (DatabaseException ex)
            {
                RegistroErrores.Registrar(ex, "Abrir la base de datos al iniciar");
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
                RegistroErrores.Registrar(ex, "Cargar el banco de señas al iniciar");
                MessageBox.Show($"{ex.Message}\n\nLos módulos de señas no van a tener contenido.",
                    "HandUp", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
            catch (DatabaseException ex)
            {
                RegistroErrores.Registrar(ex, "Cargar el banco de señas al iniciar");
                MessageBox.Show($"{ex.Message}\n\nDetalle: {ex.InnerException?.Message}",
                    "HandUp", MessageBoxButton.OK, MessageBoxImage.Error);
                Shutdown();
                return;
            }

            var login = new LoginWindow();
            login.Show();
        }

        // Error inesperado en la interfaz: se registra, se avisa con un mensaje entendible y la app sigue abierta
        private void App_DispatcherUnhandledException(object sender, DispatcherUnhandledExceptionEventArgs e)
        {
            RegistroErrores.Registrar(e.Exception, "Error no controlado en la interfaz");
            e.Handled = true;

            if (_mostrandoError)
                return;

            _mostrandoError = true;
            MessageBox.Show("Algo salió mal y no se pudo completar lo que estabas haciendo. Lo que ya estaba guardado no se pierde.\n\n" +
                            $"El detalle quedó anotado en {RegistroErrores.NombreArchivo}, en la carpeta de HandUp.",
                "HandUp", MessageBoxButton.OK, MessageBoxImage.Warning);
            _mostrandoError = false;
        }

        // Error en otro hilo: no se puede evitar que la app se cierre, pero queda registrado
        private static void Dominio_UnhandledException(object sender, UnhandledExceptionEventArgs e)
        {
            if (e.ExceptionObject is Exception ex)
                RegistroErrores.Registrar(ex, "Error no controlado en otro hilo (la app se cierra)");
        }

        // Tarea en segundo plano cuyo error nadie esperó (ej. al exportar un video): se registra y se da por visto
        private static void Tareas_UnobservedTaskException(object? sender, UnobservedTaskExceptionEventArgs e)
        {
            RegistroErrores.Registrar(e.Exception, "Error en una tarea en segundo plano");
            e.SetObserved();
        }
    }
}
