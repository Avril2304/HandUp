using System;
using System.Collections.Generic;
using System.Globalization;
using Microsoft.Data.Sqlite;

namespace ProyectoFinal
{
    // Acceso a la tabla Perfiles de SQLite. Implementa el contrato genérico IRepositorio<Perfil>
    // y suma las operaciones propias de los perfiles (buscar por usuario, registrar con validación).
    public class RepositorioPerfiles : IRepositorio<Perfil>
    {
        private const string ColumnasPerfil =
            "Id, Nombre, Usuario, PasswordHash, PasswordSalt, Nivel, Racha, MejorRacha, SenasDominadas, Puntos, AciertosTotales, FallosTotales, FechaRegistro, NivelDesbloqueado";

        public IReadOnlyList<Perfil> ObtenerTodos()
        {
            return Database.Ejecutar(conexion =>
            {
                using var comando = conexion.CreateCommand();
                comando.CommandText = $"SELECT {ColumnasPerfil} FROM Perfiles ORDER BY Id;";

                var perfiles = new List<Perfil>();
                using SqliteDataReader lector = comando.ExecuteReader();
                while (lector.Read())
                    perfiles.Add(LeerPerfil(lector));

                return perfiles;
            });
        }

        public Perfil BuscarPorId(int id)
        {
            Perfil? perfil = BuscarUno("Id = $valor", id);
            return perfil ?? throw new UserNotFoundException(id);
        }

        public Perfil BuscarPorUsuario(string usuario)
        {
            string usuarioLimpio = (usuario ?? string.Empty).Trim();

            // La columna Usuario es COLLATE NOCASE, así que la búsqueda no distingue mayúsculas
            Perfil? perfil = BuscarUno("Usuario = $valor", usuarioLimpio);
            return perfil ?? throw new UserNotFoundException(usuarioLimpio);
        }

        public Perfil Agregar(Perfil perfil)
        {
            return Database.Ejecutar(conexion =>
            {
                perfil.Id = Insertar(conexion, null, perfil);
                return perfil;
            });
        }

        // Guarda los cambios de un perfil existente (lo va a usar el guardado automático del progreso)
        public void Actualizar(Perfil perfil)
        {
            int filasAfectadas = Database.Ejecutar(conexion =>
            {
                using var comando = conexion.CreateCommand();
                comando.CommandText = @"
                    UPDATE Perfiles SET
                        Nombre = $nombre, PasswordHash = $hash, PasswordSalt = $sal, Nivel = $nivel, NivelDesbloqueado = $desbloqueado,
                        Racha = $racha, MejorRacha = $mejor, SenasDominadas = $senas, Puntos = $puntos,
                        AciertosTotales = $aciertos, FallosTotales = $fallos
                    WHERE Id = $id;";
                comando.Parameters.AddWithValue("$nombre", perfil.Nombre);
                comando.Parameters.AddWithValue("$hash", perfil.PasswordHash);
                comando.Parameters.AddWithValue("$sal", perfil.PasswordSalt);
                comando.Parameters.AddWithValue("$nivel", perfil.Nivel);
                comando.Parameters.AddWithValue("$desbloqueado", perfil.NivelDesbloqueado);
                comando.Parameters.AddWithValue("$racha", perfil.Racha);
                comando.Parameters.AddWithValue("$mejor", perfil.MejorRacha);
                comando.Parameters.AddWithValue("$senas", perfil.SenasDominadas);
                comando.Parameters.AddWithValue("$puntos", perfil.Puntos);
                comando.Parameters.AddWithValue("$aciertos", perfil.AciertosTotales);
                comando.Parameters.AddWithValue("$fallos", perfil.FallosTotales);
                comando.Parameters.AddWithValue("$id", perfil.Id);
                return comando.ExecuteNonQuery();
            });

            if (filasAfectadas == 0)
                throw new UserNotFoundException(perfil.Id);
        }

        // Registra el resultado de una seña en la tabla ProgresoSenas y devuelve la cantidad total de señas dominadas
        public int RegistrarResultadoSena(int perfilId, int senaId, bool correcto)
        {
            return Database.Ejecutar(conexion =>
            {
                using var comando = conexion.CreateCommand();
                if (correcto)
                {
                    comando.CommandText = @"
                        INSERT INTO ProgresoSenas (PerfilId, SenaId, Dominada, Aciertos, Fallos)
                        VALUES ($perfilId, $senaId, 1, 1, 0)
                        ON CONFLICT(PerfilId, SenaId) DO UPDATE SET
                            Aciertos = Aciertos + 1,
                            Dominada = 1;

                        SELECT COUNT(*) FROM ProgresoSenas WHERE PerfilId = $perfilId AND Dominada = 1;";
                }
                else
                {
                    comando.CommandText = @"
                        INSERT INTO ProgresoSenas (PerfilId, SenaId, Dominada, Aciertos, Fallos)
                        VALUES ($perfilId, $senaId, 0, 0, 1)
                        ON CONFLICT(PerfilId, SenaId) DO UPDATE SET
                            Fallos = Fallos + 1;

                        SELECT COUNT(*) FROM ProgresoSenas WHERE PerfilId = $perfilId AND Dominada = 1;";
                }
                comando.Parameters.AddWithValue("$perfilId", perfilId);
                comando.Parameters.AddWithValue("$senaId", senaId);

                return Convert.ToInt32(comando.ExecuteScalar());
            });
        }

        // Cuántas señas de un nivel (1 a 4) tiene dominadas el perfil. Es lo que muestra la barra de progreso del nivel.
        public int ContarDominadasDelNivel(int perfilId, int nivel)
        {
            return Database.Ejecutar(conexion =>
            {
                using var comando = conexion.CreateCommand();
                comando.CommandText = @"
                    SELECT COUNT(*) FROM ProgresoSenas p
                    JOIN Senas s ON s.Id = p.SenaId
                    WHERE p.PerfilId = $id AND p.Dominada = 1 AND s.Dificultad = $nivel;";
                comando.Parameters.AddWithValue("$id", perfilId);
                comando.Parameters.AddWithValue("$nivel", nivel);
                return Convert.ToInt32(comando.ExecuteScalar());
            });
        }

        // Mejor porcentaje que sacó el perfil en la prueba final de un nivel; null si nunca la rindió
        public int? ObtenerMejorPorcentaje(int perfilId, int nivel)
        {
            return Database.Ejecutar(conexion =>
            {
                using var comando = conexion.CreateCommand();
                comando.CommandText = "SELECT MejorPorcentaje FROM PruebasFinales WHERE PerfilId = $id AND Nivel = $nivel;";
                comando.Parameters.AddWithValue("$id", perfilId);
                comando.Parameters.AddWithValue("$nivel", nivel);
                object? valor = comando.ExecuteScalar();
                return valor is null ? (int?)null : Convert.ToInt32(valor);
            });
        }

        // Guarda el resultado de una prueba final sin pisar uno mejor que ya estuviera
        public void GuardarResultadoPrueba(int perfilId, int nivel, int porcentaje)
        {
            Database.Ejecutar(conexion =>
            {
                using var comando = conexion.CreateCommand();
                comando.CommandText = @"
                    INSERT INTO PruebasFinales (PerfilId, Nivel, MejorPorcentaje) VALUES ($id, $nivel, $porcentaje)
                    ON CONFLICT(PerfilId, Nivel) DO UPDATE SET
                        MejorPorcentaje = MAX(MejorPorcentaje, excluded.MejorPorcentaje);";
                comando.Parameters.AddWithValue("$id", perfilId);
                comando.Parameters.AddWithValue("$nivel", nivel);
                comando.Parameters.AddWithValue("$porcentaje", Math.Clamp(porcentaje, 0, 100));
                comando.ExecuteNonQuery();
            });
        }

        // Borra el progreso por seña y los resultados de las pruebas finales de un perfil (al reiniciar el progreso desde Mi perfil)
        public void BorrarProgresoSenas(int perfilId)
        {
            Database.Ejecutar(conexion =>
            {
                using var comando = conexion.CreateCommand();
                comando.CommandText = @"
                    DELETE FROM ProgresoSenas WHERE PerfilId = $id;
                    DELETE FROM PruebasFinales WHERE PerfilId = $id;";
                comando.Parameters.AddWithValue("$id", perfilId);
                comando.ExecuteNonQuery();
            });
        }

        // Elimina el perfil con su progreso y sus preferencias. Todo en una transacción: se borra todo o nada.
        public void Eliminar(int perfilId)
        {
            int filasAfectadas = Database.Ejecutar(conexion =>
            {
                using var transaccion = conexion.BeginTransaction();
                foreach (string tabla in new[] { "ProgresoSenas", "PruebasFinales", "Preferencias" })
                {
                    using var borrarDatos = conexion.CreateCommand();
                    borrarDatos.Transaction = transaccion;
                    borrarDatos.CommandText = $"DELETE FROM {tabla} WHERE PerfilId = $id;";
                    borrarDatos.Parameters.AddWithValue("$id", perfilId);
                    borrarDatos.ExecuteNonQuery();
                }

                using var borrarPerfil = conexion.CreateCommand();
                borrarPerfil.Transaction = transaccion;
                borrarPerfil.CommandText = "DELETE FROM Perfiles WHERE Id = $id;";
                borrarPerfil.Parameters.AddWithValue("$id", perfilId);
                int filas = borrarPerfil.ExecuteNonQuery();

                transaccion.Commit();
                return filas;
            });

            if (filasAfectadas == 0)
                throw new UserNotFoundException(perfilId);
        }

        // Aciertos, fallos y señas dominadas del perfil en cada categoría (también las que no practicó, con ceros)
        public IReadOnlyList<EstadisticaCategoria> ObtenerEstadisticasPorCategoria(int perfilId)
        {
            return Database.Ejecutar(conexion =>
            {
                using var comando = conexion.CreateCommand();
                comando.CommandText = @"
                    SELECT c.Id, c.Nombre, c.Emoji, COUNT(s.Id),
                           COALESCE(SUM(p.Aciertos), 0), COALESCE(SUM(p.Fallos), 0), COALESCE(SUM(p.Dominada), 0)
                    FROM Categorias c
                    JOIN Senas s ON s.CategoriaId = c.Id
                    LEFT JOIN ProgresoSenas p ON p.SenaId = s.Id AND p.PerfilId = $id
                    GROUP BY c.Id
                    ORDER BY c.Orden;";
                comando.Parameters.AddWithValue("$id", perfilId);

                var lista = new List<EstadisticaCategoria>();
                using SqliteDataReader lector = comando.ExecuteReader();
                while (lector.Read())
                {
                    lista.Add(new EstadisticaCategoria(
                        lector.GetString(0), lector.GetString(1), lector.IsDBNull(2) ? null : lector.GetString(2),
                        lector.GetInt32(3), lector.GetInt32(4), lector.GetInt32(5), lector.GetInt32(6)));
                }
                return lista;
            });
        }

        // Valida y registra un perfil nuevo; lanza InvalidInputException si los datos no son válidos
        public Perfil Registrar(string nombre, string usuario, string password, string nivelInicial)
        {
            if (string.IsNullOrWhiteSpace(nombre))
                throw new InvalidInputException("El nombre es obligatorio.");

            if (string.IsNullOrWhiteSpace(usuario) || usuario.Trim().Length < 3)
                throw new InvalidInputException("El usuario debe tener al menos 3 caracteres.");

            PoliticaPassword.Validar(password);

            string usuarioLimpio = usuario.Trim();

            if (ExisteUsuario(usuarioLimpio))
                throw new InvalidInputException($"El usuario '{usuarioLimpio}' ya está registrado.");

            (string hash, string sal) = PasswordHasher.Hashear(password);

            var nuevoPerfil = new Perfil
            {
                Nombre = nombre.Trim(),
                Usuario = usuarioLimpio,
                PasswordHash = hash,
                PasswordSalt = sal,
                Nivel = string.IsNullOrWhiteSpace(nivelInicial) ? "Principiante" : nivelInicial,
                Racha = 0,
                SenasDominadas = 0,
                Puntos = 0,
                FechaRegistro = DateTime.Now
            };

            return Agregar(nuevoPerfil);
        }

        // Devuelve el perfil si usuario y contraseña coinciden; si no, lanza una excepción propia
        public Perfil IniciarSesion(string usuario, string password)
        {
            Perfil perfil = BuscarPorUsuario(usuario);

            if (!PasswordHasher.Verificar(password, perfil.PasswordHash, perfil.PasswordSalt))
                throw new InvalidInputException("Contraseña incorrecta.");

            return perfil;
        }

        private bool ExisteUsuario(string usuario)
        {
            return BuscarUno("Usuario = $valor", usuario) is not null;
        }

        private Perfil? BuscarUno(string condicion, object valor)
        {
            return Database.Ejecutar(conexion =>
            {
                using var comando = conexion.CreateCommand();
                comando.CommandText = $"SELECT {ColumnasPerfil} FROM Perfiles WHERE {condicion} LIMIT 1;";
                comando.Parameters.AddWithValue("$valor", valor);

                using SqliteDataReader lector = comando.ExecuteReader();
                return lector.Read() ? LeerPerfil(lector) : null;
            });
        }

        // Inserta un perfil usando una conexión ya abierta y devuelve el Id generado.
        // Es internal porque también la usa Database al migrar el JSON viejo dentro de una transacción.
        internal static int Insertar(SqliteConnection conexion, SqliteTransaction? transaccion, Perfil perfil)
        {
            using var comando = conexion.CreateCommand();
            comando.Transaction = transaccion;
            comando.CommandText = @"
                INSERT INTO Perfiles (Nombre, Usuario, PasswordHash, PasswordSalt, Nivel, NivelDesbloqueado, Racha, MejorRacha, SenasDominadas, Puntos, AciertosTotales, FallosTotales, FechaRegistro)
                VALUES ($nombre, $usuario, $hash, $sal, $nivel, $desbloqueado, $racha, $mejor, $senas, $puntos, $aciertos, $fallos, $fecha);
                SELECT last_insert_rowid();";
            comando.Parameters.AddWithValue("$nombre", perfil.Nombre);
            comando.Parameters.AddWithValue("$usuario", perfil.Usuario);
            comando.Parameters.AddWithValue("$hash", perfil.PasswordHash);
            comando.Parameters.AddWithValue("$sal", perfil.PasswordSalt);
            comando.Parameters.AddWithValue("$nivel", perfil.Nivel);
            comando.Parameters.AddWithValue("$desbloqueado", perfil.NivelDesbloqueado);
            comando.Parameters.AddWithValue("$racha", perfil.Racha);
            comando.Parameters.AddWithValue("$mejor", perfil.MejorRacha);
            comando.Parameters.AddWithValue("$senas", perfil.SenasDominadas);
            comando.Parameters.AddWithValue("$puntos", perfil.Puntos);
            comando.Parameters.AddWithValue("$aciertos", perfil.AciertosTotales);
            comando.Parameters.AddWithValue("$fallos", perfil.FallosTotales);
            // Guardamos la fecha como texto ISO 8601 para que se lea igual en cualquier configuración regional
            comando.Parameters.AddWithValue("$fecha", perfil.FechaRegistro.ToString("o", CultureInfo.InvariantCulture));

            return Convert.ToInt32(comando.ExecuteScalar());
        }

        private static Perfil LeerPerfil(SqliteDataReader lector)
        {
            string fechaTexto = lector.GetString(12);
            bool fechaValida = DateTime.TryParse(fechaTexto, CultureInfo.InvariantCulture,
                DateTimeStyles.RoundtripKind, out DateTime fecha);

            return new Perfil
            {
                Id = lector.GetInt32(0),
                Nombre = lector.GetString(1),
                Usuario = lector.GetString(2),
                PasswordHash = lector.GetString(3),
                PasswordSalt = lector.GetString(4),
                // El nivel sale del número desbloqueado; la columna Nivel (texto) se sigue guardando para leer la base a mano
                NivelDesbloqueado = lector.GetInt32(13),
                Racha = lector.GetInt32(6),
                MejorRacha = lector.GetInt32(7),
                SenasDominadas = lector.GetInt32(8),
                Puntos = lector.GetInt32(9),
                AciertosTotales = lector.GetInt32(10),
                FallosTotales = lector.GetInt32(11),
                // Si alguien editó la fecha a mano y quedó inválida, no rompemos la carga del perfil
                FechaRegistro = fechaValida ? fecha : DateTime.Now
            };
        }
    }
}
