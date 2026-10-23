using System;
using System.Globalization;
using Microsoft.Data.Sqlite;

namespace ProyectoFinal
{
    // Perfil para que quien recibe HandUp pueda recorrer todo el programa sin tener que aprobar cada nivel:
    // tiene desbloqueado el último nivel, aprobadas las pruebas finales de los anteriores y dominadas sus señas.
    // El examen final queda sin rendir, así se puede probar y ver el certificado.
    // Se crea una sola vez por base (queda marcado en Metadatos): si lo eliminan o reinician, no vuelve a aparecer.
    public static class PerfilDePrueba
    {
        public const string Usuario = "prueba";
        public const string Password = "1234";
        private const string ClaveMetadato = "PerfilDePruebaCreado";

        // Se llama al arrancar, después de importar las señas (las necesita para marcarlas como dominadas)
        public static void CrearSiFalta()
        {
            Database.Ejecutar(conexion =>
            {
                using var transaccion = conexion.BeginTransaction();

                if (Contar(conexion, transaccion, "SELECT COUNT(*) FROM Metadatos WHERE Clave = $valor;", ClaveMetadato) > 0)
                    return;

                // Sin señas todavía (no se pudo importar el banco): se intenta de nuevo la próxima vez
                int ultimoNivel = Niveles.Nombres.Count;
                int dominadas = (int)Contar(conexion, transaccion, "SELECT COUNT(*) FROM Senas WHERE Dificultad < $valor;", ultimoNivel);
                if (dominadas == 0)
                    return;

                // Si alguien ya se registró con ese usuario, no lo tocamos
                if (Contar(conexion, transaccion, "SELECT COUNT(*) FROM Perfiles WHERE Usuario = $valor;", Usuario) == 0)
                {
                    (string hash, string sal) = PasswordHasher.Hashear(Password);
                    var perfil = new Perfil
                    {
                        Nombre = "Perfil de prueba",
                        Usuario = Usuario,
                        PasswordHash = hash,
                        PasswordSalt = sal,
                        NivelDesbloqueado = ultimoNivel,
                        SenasDominadas = dominadas,
                        Puntos = dominadas * 10,
                        AciertosTotales = dominadas,
                        FallosTotales = dominadas / 10,
                        MejorRacha = 25,
                        FechaRegistro = DateTime.Now.AddDays(-30)
                    };
                    int id = RepositorioPerfiles.Insertar(conexion, transaccion, perfil);

                    using var progreso = conexion.CreateCommand();
                    progreso.Transaction = transaccion;
                    progreso.CommandText = @"
                        INSERT OR IGNORE INTO ProgresoSenas (PerfilId, SenaId, Dominada, Aciertos, Fallos)
                        SELECT $id, Id, 1, 1, 0 FROM Senas WHERE Dificultad < $ultimo;";
                    progreso.Parameters.AddWithValue("$id", id);
                    progreso.Parameters.AddWithValue("$ultimo", ultimoNivel);
                    progreso.ExecuteNonQuery();

                    for (int nivel = 1; nivel < ultimoNivel; nivel++)
                    {
                        using var prueba = conexion.CreateCommand();
                        prueba.Transaction = transaccion;
                        prueba.CommandText = "INSERT OR IGNORE INTO PruebasFinales (PerfilId, Nivel, MejorPorcentaje, FechaAprobada) VALUES ($id, $nivel, 100, $fecha);";
                        prueba.Parameters.AddWithValue("$id", id);
                        prueba.Parameters.AddWithValue("$nivel", nivel);
                        prueba.Parameters.AddWithValue("$fecha", DateTime.Now.ToString("o", CultureInfo.InvariantCulture));
                        prueba.ExecuteNonQuery();
                    }
                }

                using (var marcar = conexion.CreateCommand())
                {
                    marcar.Transaction = transaccion;
                    marcar.CommandText = "INSERT INTO Metadatos (Clave, Valor) VALUES ($clave, $valor);";
                    marcar.Parameters.AddWithValue("$clave", ClaveMetadato);
                    marcar.Parameters.AddWithValue("$valor", DateTime.Now.ToString("o", CultureInfo.InvariantCulture));
                    marcar.ExecuteNonQuery();
                }

                transaccion.Commit();
            });
        }

        private static long Contar(SqliteConnection conexion, SqliteTransaction transaccion, string consulta, object valor)
        {
            using var comando = conexion.CreateCommand();
            comando.Transaction = transaccion;
            comando.CommandText = consulta;
            comando.Parameters.AddWithValue("$valor", valor);
            return Convert.ToInt64(comando.ExecuteScalar());
        }
    }
}
