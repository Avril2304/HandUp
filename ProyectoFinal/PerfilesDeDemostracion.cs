using System;
using System.Collections.Generic;
using System.Globalization;
using Microsoft.Data.Sqlite;

namespace ProyectoFinal
{
    // Dos perfiles para mostrar HandUp en la presentación, con contraseña "1234":
    //  - "nuevo" (Ana): recién empieza en Principiante, con unas pocas letras practicadas y algunas falladas.
    //  - "avanzado" (Tomás): ya aprobó Principiante, Básico e Intermedio y está en Avanzado, con estadísticas
    //    y rachas de alguien que viene practicando hace un mes y medio.
    // A diferencia de PerfilDePrueba (todo dominado y al 100%), estos tienen aciertos y fallos variados, así
    // Mi perfil, la repetición espaciada y "Practicar" las categorías flojas muestran algo real.
    // Se crean una sola vez por base (queda marcado en Metadatos): si los eliminan o reinician, no vuelven a aparecer.
    public static class PerfilesDeDemostracion
    {
        public const string UsuarioNuevo = "nuevo";
        public const string UsuarioAvanzado = "avanzado";
        public const string Password = "1234";
        private const string ClaveMetadato = "PerfilesDeDemostracionCreados";

        // Se llama al arrancar, después de importar las señas (las necesita para armar el progreso)
        public static void CrearSiFaltan()
        {
            Database.Ejecutar(conexion =>
            {
                using var transaccion = conexion.BeginTransaction();

                if (Contar(conexion, transaccion, "SELECT COUNT(*) FROM Metadatos WHERE Clave = $valor;", ClaveMetadato) > 0)
                    return;

                // Sin señas todavía (no se pudo importar el banco): se intenta de nuevo la próxima vez
                if (Contar(conexion, transaccion, "SELECT COUNT(*) FROM Senas WHERE Dificultad >= $valor;", 1) == 0)
                    return;

                DateTime hoy = DateTime.Today;

                // Si alguien ya se registró con alguno de esos usuarios, no lo tocamos
                if (!ExisteUsuario(conexion, transaccion, UsuarioNuevo))
                    CrearNuevo(conexion, transaccion, hoy);

                if (!ExisteUsuario(conexion, transaccion, UsuarioAvanzado))
                    CrearAvanzado(conexion, transaccion, hoy);

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

        // Ana: Principiante, 10 letras del abecedario (6 dominadas, 4 que le cuestan) y 4 saludos recién empezados
        private static void CrearNuevo(SqliteConnection conexion, SqliteTransaction transaccion, DateTime hoy)
        {
            int id = InsertarPerfil(conexion, transaccion, "Ana", UsuarioNuevo, nivelDesbloqueado: 1,
                racha: 2, mejorRacha: 5, fechaRegistro: hoy.AddDays(-3));

            List<int> letras = IdsDeCategoria(conexion, transaccion, "abecedario", 10);
            for (int i = 0; i < letras.Count; i++)
            {
                bool dominada = i < 6;
                GuardarProgreso(conexion, transaccion, id, letras[i], dominada,
                    aciertos: dominada ? 3 : 1,
                    fallos: dominada ? (i % 3 == 0 ? 1 : 0) : 2);
            }

            foreach (int sena in IdsDeCategoria(conexion, transaccion, "saludos", 4))
                GuardarProgreso(conexion, transaccion, id, sena, dominada: false, aciertos: 1, fallos: 1);

            CompletarTotales(conexion, transaccion, id, rachaDias: 2, mejorRachaDias: 3, ultimoDia: hoy.AddDays(-1));
        }

        // Tomás: aprobó las pruebas finales de los 3 primeros niveles y ya practicó 40 señas de Avanzado.
        // El examen final queda sin rendir, así se puede mostrar en la demo.
        private static void CrearAvanzado(SqliteConnection conexion, SqliteTransaction transaccion, DateTime hoy)
        {
            int ultimoNivel = Niveles.Nombres.Count;
            int id = InsertarPerfil(conexion, transaccion, "Tomás", UsuarioAvanzado, nivelDesbloqueado: ultimoNivel,
                racha: 7, mejorRacha: 31, fechaRegistro: hoy.AddDays(-45));

            // Las señas de los niveles aprobados: todas dominadas, con algunos fallos repartidos (no todo al 100%)
            foreach (int sena in IdsDeNivel(conexion, transaccion, "Dificultad < $valor", ultimoNivel, int.MaxValue))
                GuardarProgreso(conexion, transaccion, id, sena, dominada: true,
                    aciertos: 3 + sena % 3,
                    fallos: sena % 4 == 0 ? 2 : sena % 7 == 0 ? 1 : 0);

            // Avanzado a medio camino: de 40 señas, dos de cada tres ya dominadas
            List<int> avanzado = IdsDeNivel(conexion, transaccion, "Dificultad = $valor", ultimoNivel, 40);
            for (int i = 0; i < avanzado.Count; i++)
            {
                bool dominada = i % 3 != 2;
                GuardarProgreso(conexion, transaccion, id, avanzado[i], dominada,
                    aciertos: dominada ? 3 : 1,
                    fallos: dominada ? i % 2 : 2);
            }

            int[] porcentajes = { 95, 90, 85 };
            int[] diasAtras = { 40, 25, 10 };
            for (int nivel = 1; nivel < ultimoNivel && nivel <= porcentajes.Length; nivel++)
                AprobarPruebaFinal(conexion, transaccion, id, nivel, porcentajes[nivel - 1], hoy.AddDays(-diasAtras[nivel - 1]));

            CompletarTotales(conexion, transaccion, id, rachaDias: 9, mejorRachaDias: 16, ultimoDia: hoy.AddDays(-1));
        }

        private static int InsertarPerfil(SqliteConnection conexion, SqliteTransaction transaccion, string nombre, string usuario,
            int nivelDesbloqueado, int racha, int mejorRacha, DateTime fechaRegistro)
        {
            (string hash, string sal) = PasswordHasher.Hashear(Password);
            var perfil = new Perfil
            {
                Nombre = nombre,
                Usuario = usuario,
                PasswordHash = hash,
                PasswordSalt = sal,
                NivelDesbloqueado = nivelDesbloqueado,
                Racha = racha,
                MejorRacha = mejorRacha,
                FechaRegistro = fechaRegistro
            };
            return RepositorioPerfiles.Insertar(conexion, transaccion, perfil);
        }

        private static void GuardarProgreso(SqliteConnection conexion, SqliteTransaction transaccion, int perfilId, int senaId,
            bool dominada, int aciertos, int fallos)
        {
            using var comando = conexion.CreateCommand();
            comando.Transaction = transaccion;
            comando.CommandText = @"
                INSERT OR IGNORE INTO ProgresoSenas (PerfilId, SenaId, Dominada, Aciertos, Fallos)
                VALUES ($perfil, $sena, $dominada, $aciertos, $fallos);";
            comando.Parameters.AddWithValue("$perfil", perfilId);
            comando.Parameters.AddWithValue("$sena", senaId);
            comando.Parameters.AddWithValue("$dominada", dominada ? 1 : 0);
            comando.Parameters.AddWithValue("$aciertos", aciertos);
            comando.Parameters.AddWithValue("$fallos", fallos);
            comando.ExecuteNonQuery();
        }

        private static void AprobarPruebaFinal(SqliteConnection conexion, SqliteTransaction transaccion, int perfilId, int nivel,
            int porcentaje, DateTime fecha)
        {
            using var comando = conexion.CreateCommand();
            comando.Transaction = transaccion;
            comando.CommandText = "INSERT OR IGNORE INTO PruebasFinales (PerfilId, Nivel, MejorPorcentaje, FechaAprobada) VALUES ($id, $nivel, $porcentaje, $fecha);";
            comando.Parameters.AddWithValue("$id", perfilId);
            comando.Parameters.AddWithValue("$nivel", nivel);
            comando.Parameters.AddWithValue("$porcentaje", porcentaje);
            comando.Parameters.AddWithValue("$fecha", fecha.ToString("o", CultureInfo.InvariantCulture));
            comando.ExecuteNonQuery();
        }

        // Los totales del perfil salen del progreso que se acaba de guardar, así coinciden con Mi perfil
        private static void CompletarTotales(SqliteConnection conexion, SqliteTransaction transaccion, int perfilId,
            int rachaDias, int mejorRachaDias, DateTime ultimoDia)
        {
            using var comando = conexion.CreateCommand();
            comando.Transaction = transaccion;
            comando.CommandText = @"
                UPDATE Perfiles SET
                    SenasDominadas = (SELECT COUNT(*) FROM ProgresoSenas WHERE PerfilId = $id AND Dominada = 1),
                    AciertosTotales = (SELECT COALESCE(SUM(Aciertos), 0) FROM ProgresoSenas WHERE PerfilId = $id),
                    FallosTotales = (SELECT COALESCE(SUM(Fallos), 0) FROM ProgresoSenas WHERE PerfilId = $id),
                    Puntos = 10 * (SELECT COALESCE(SUM(Aciertos), 0) FROM ProgresoSenas WHERE PerfilId = $id),
                    RachaDias = $rachaDias, MejorRachaDias = $mejorDias, UltimoDiaPractica = $ultimoDia
                WHERE Id = $id;";
            comando.Parameters.AddWithValue("$id", perfilId);
            comando.Parameters.AddWithValue("$rachaDias", rachaDias);
            comando.Parameters.AddWithValue("$mejorDias", mejorRachaDias);
            comando.Parameters.AddWithValue("$ultimoDia", ultimoDia.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture));
            comando.ExecuteNonQuery();
        }

        private static List<int> IdsDeCategoria(SqliteConnection conexion, SqliteTransaction transaccion, string categoria, int limite) =>
            Ids(conexion, transaccion, "SELECT Id FROM Senas WHERE CategoriaId = $valor ORDER BY Id LIMIT $limite;", categoria, limite);

        private static List<int> IdsDeNivel(SqliteConnection conexion, SqliteTransaction transaccion, string condicion, int nivel, int limite) =>
            Ids(conexion, transaccion, $"SELECT Id FROM Senas WHERE {condicion} ORDER BY Id LIMIT $limite;", nivel, limite);

        private static List<int> Ids(SqliteConnection conexion, SqliteTransaction transaccion, string consulta, object valor, int limite)
        {
            using var comando = conexion.CreateCommand();
            comando.Transaction = transaccion;
            comando.CommandText = consulta;
            comando.Parameters.AddWithValue("$valor", valor);
            comando.Parameters.AddWithValue("$limite", limite);

            var ids = new List<int>();
            using var lector = comando.ExecuteReader();
            while (lector.Read())
                ids.Add(lector.GetInt32(0));
            return ids;
        }

        private static bool ExisteUsuario(SqliteConnection conexion, SqliteTransaction transaccion, string usuario) =>
            Contar(conexion, transaccion, "SELECT COUNT(*) FROM Perfiles WHERE Usuario = $valor;", usuario) > 0;

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
