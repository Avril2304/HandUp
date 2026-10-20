using System;
using System.Diagnostics;
using System.IO;
using System.Text;

namespace ProyectoFinal
{
    // Registro de errores: errores.log, junto al ejecutable (igual que handup.db).
    // Anota los errores que la app maneja sin interrumpir al usuario y los que nadie atrapó,
    // con la fecha, qué se estaba haciendo y el detalle completo (tipo, mensaje, excepción interna y pila de llamadas).
    public static class RegistroErrores
    {
        public const string NombreArchivo = "errores.log";
        private const long TamanoMaximo = 1024 * 1024; // 1 MB: al pasarlo, lo anterior queda en errores.log.old

        // El exportador de video y las tareas en segundo plano pueden registrar desde otro hilo
        private static readonly object Candado = new();

        public static string RutaArchivo => Path.Combine(AppDomain.CurrentDomain.BaseDirectory, NombreArchivo);

        // contexto: qué estaba haciendo la app cuando falló (ej. "Guardar las preferencias al iniciar sesión")
        public static void Registrar(Exception error, string contexto)
        {
            string entrada = new StringBuilder()
                .AppendLine($"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] {contexto}")
                .AppendLine(error.ToString())
                .AppendLine()
                .ToString();

            lock (Candado)
            {
                try
                {
                    var archivo = new FileInfo(RutaArchivo);
                    if (archivo.Exists && archivo.Length > TamanoMaximo)
                        File.Move(RutaArchivo, RutaArchivo + ".old", overwrite: true);

                    File.AppendAllText(RutaArchivo, entrada);
                }
                catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
                {
                    // Si no se puede escribir (carpeta sin permisos, disco lleno) no queda otro lugar donde anotarlo:
                    // registrar nunca puede interrumpir al usuario, así que solo se avisa en la salida de depuración
                    Debug.WriteLine($"RegistroErrores: no se pudo escribir {NombreArchivo} ({ex.Message}). Error original: {error}");
                }
            }
        }
    }
}
