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
            "Id, Nombre, Usuario, PasswordHash, PasswordSalt, Nivel, Racha, MejorRacha, SenasDominadas, Puntos, AciertosTotales, FallosTotales, FechaRegistro";

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
                        Nombre = $nombre, PasswordHash = $hash, PasswordSalt = $sal, Nivel = $nivel,
                        Racha = $racha, MejorRacha = $mejor, SenasDominadas = $senas, Puntos = $puntos,
                        AciertosTotales = $aciertos, FallosTotales = $fallos
                    WHERE Id = $id;";
                comando.Parameters.AddWithValue("$nombre", perfil.Nombre);
                comando.Parameters.AddWithValue("$hash", perfil.PasswordHash);
                comando.Parameters.AddWithValue("$sal", perfil.PasswordSalt);
                comando.Parameters.AddWithValue("$nivel", perfil.Nivel);
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
                INSERT INTO Perfiles (Nombre, Usuario, PasswordHash, PasswordSalt, Nivel, Racha, MejorRacha, SenasDominadas, Puntos, AciertosTotales, FallosTotales, FechaRegistro)
                VALUES ($nombre, $usuario, $hash, $sal, $nivel, $racha, $mejor, $senas, $puntos, $aciertos, $fallos, $fecha);
                SELECT last_insert_rowid();";
            comando.Parameters.AddWithValue("$nombre", perfil.Nombre);
            comando.Parameters.AddWithValue("$usuario", perfil.Usuario);
            comando.Parameters.AddWithValue("$hash", perfil.PasswordHash);
            comando.Parameters.AddWithValue("$sal", perfil.PasswordSalt);
            comando.Parameters.AddWithValue("$nivel", perfil.Nivel);
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
                Nivel = lector.GetString(5),
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
