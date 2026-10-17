using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using Microsoft.Data.Sqlite;

namespace ProyectoFinal
{
    // Punto único de acceso a la base SQLite (handup.db, junto al ejecutable).
    // Se encarga de crear las tablas, migrar el viejo perfiles.json y traducir los errores de SQLite
    // a DatabaseException para que la app nunca crashee por un problema con el archivo.
    public static class Database
    {
        private const string NombreArchivo = "handup.db";
        private const string NombreJsonViejo = "perfiles.json";

        // Códigos de error de SQLite que indican que el archivo no es una base válida
        private const int SqliteCorrupt = 11;
        private const int SqliteNotADb = 26;

        public static string RutaArchivo => Path.Combine(AppDomain.CurrentDomain.BaseDirectory, NombreArchivo);

        private static string CadenaConexion => new SqliteConnectionStringBuilder
        {
            DataSource = RutaArchivo,
            Mode = SqliteOpenMode.ReadWriteCreate
        }.ToString();

        // Se llama una sola vez al arrancar la app (App.Application_Startup)
        public static void Inicializar()
        {
            try
            {
                CrearTablas();
            }
            catch (SqliteException ex) when (ex.SqliteErrorCode == SqliteCorrupt || ex.SqliteErrorCode == SqliteNotADb)
            {
                // El archivo está corrupto: lo apartamos (sin borrarlo) y arrancamos con una base nueva
                ApartarArchivoCorrupto();
                try
                {
                    CrearTablas();
                }
                catch (SqliteException ex2)
                {
                    throw new DatabaseException("No se pudo crear una base de datos nueva.", ex2);
                }
            }
            catch (SqliteException ex)
            {
                throw new DatabaseException("No se pudo abrir la base de datos de HandUp.", ex);
            }

            Ejecutar(conexion =>
            {
                if (ContarPerfiles(conexion) == 0)
                    CargarPerfilesIniciales(conexion);
            });
        }

        // Método genérico: abre una conexión, ejecuta la acción y devuelve su resultado.
        // Cualquier SqliteException se convierte en DatabaseException (excepción propia).
        public static TResultado Ejecutar<TResultado>(Func<SqliteConnection, TResultado> accion)
        {
            try
            {
                using var conexion = new SqliteConnection(CadenaConexion);
                conexion.Open();
                return accion(conexion);
            }
            catch (SqliteException ex)
            {
                throw new DatabaseException("Ocurrió un error al acceder a la base de datos.", ex);
            }
        }

        // Versión para acciones que no devuelven nada
        public static void Ejecutar(Action<SqliteConnection> accion)
        {
            Ejecutar<bool>(conexion =>
            {
                accion(conexion);
                return true;
            });
        }

        private static void CrearTablas()
        {
            using var conexion = new SqliteConnection(CadenaConexion);
            conexion.Open();

            using var comando = conexion.CreateCommand();
            // COLLATE NOCASE: "Avril" y "avril" se consideran el mismo usuario
            comando.CommandText = @"
                CREATE TABLE IF NOT EXISTS Perfiles (
                    Id             INTEGER PRIMARY KEY AUTOINCREMENT,
                    Nombre         TEXT    NOT NULL,
                    Usuario        TEXT    NOT NULL UNIQUE COLLATE NOCASE,
                    PasswordHash   TEXT    NOT NULL,
                    PasswordSalt   TEXT    NOT NULL,
                    Nivel          TEXT    NOT NULL DEFAULT 'Principiante',
                    Racha          INTEGER NOT NULL DEFAULT 0,
                    MejorRacha     INTEGER NOT NULL DEFAULT 0,
                    SenasDominadas INTEGER NOT NULL DEFAULT 0,
                    Puntos         INTEGER NOT NULL DEFAULT 0,
                    AciertosTotales INTEGER NOT NULL DEFAULT 0,
                    FallosTotales  INTEGER NOT NULL DEFAULT 0,
                    FechaRegistro  TEXT    NOT NULL,
                    NivelDesbloqueado INTEGER NOT NULL DEFAULT 1 CHECK (NivelDesbloqueado BETWEEN 1 AND 4),
                    NivelacionPendiente INTEGER CHECK (NivelacionPendiente BETWEEN 2 AND 4)
                );

                -- Mejor resultado de cada perfil en la prueba final de cada nivel (con 80% o más se desbloquea el siguiente)
                CREATE TABLE IF NOT EXISTS PruebasFinales (
                    PerfilId        INTEGER NOT NULL REFERENCES Perfiles(Id),
                    Nivel           INTEGER NOT NULL CHECK (Nivel BETWEEN 1 AND 4),
                    MejorPorcentaje INTEGER NOT NULL CHECK (MejorPorcentaje BETWEEN 0 AND 100),
                    PRIMARY KEY (PerfilId, Nivel)
                );

                CREATE TABLE IF NOT EXISTS ProgresoSenas (
                    PerfilId   INTEGER NOT NULL REFERENCES Perfiles(Id),
                    SenaId     INTEGER NOT NULL REFERENCES Senas(Id),
                    Dominada   INTEGER NOT NULL DEFAULT 0,
                    Aciertos   INTEGER NOT NULL DEFAULT 0,
                    Fallos     INTEGER NOT NULL DEFAULT 0,
                    PRIMARY KEY (PerfilId, SenaId)
                );

                -- Banco multimedia LSA: se llena desde signs_database.json (ver ImportadorSenas)
                CREATE TABLE IF NOT EXISTS Categorias (
                    Id          TEXT    PRIMARY KEY,
                    Nombre      TEXT    NOT NULL,
                    Emoji       TEXT,
                    Descripcion TEXT,
                    Color       TEXT,
                    Orden       INTEGER NOT NULL DEFAULT 0
                );

                -- Id = número del código del JSON (sign_036 -> 36), así se mantiene estable entre importaciones
                CREATE TABLE IF NOT EXISTS Senas (
                    Id            INTEGER PRIMARY KEY,
                    Codigo        TEXT    NOT NULL UNIQUE,
                    Palabra       TEXT    NOT NULL,
                    CategoriaId   TEXT    NOT NULL REFERENCES Categorias(Id),
                    Dificultad    INTEGER NOT NULL CHECK (Dificultad BETWEEN 1 AND 4),
                    Descripcion   TEXT    NOT NULL,
                    Instrucciones TEXT,
                    RutaImagen    TEXT    NOT NULL,
                    RutaVideo     TEXT
                );
                CREATE INDEX IF NOT EXISTS IX_Senas_CategoriaId ON Senas(CategoriaId);

                -- Pares clave/valor de uso interno (por ejemplo, la huella del último JSON importado)
                CREATE TABLE IF NOT EXISTS Metadatos (
                    Clave TEXT PRIMARY KEY,
                    Valor TEXT NOT NULL
                );

                CREATE TABLE IF NOT EXISTS Preferencias (
                    PerfilId            INTEGER PRIMARY KEY REFERENCES Perfiles(Id),
                    AltoContraste       INTEGER NOT NULL DEFAULT 0,
                    TamanoFuente        REAL    NOT NULL DEFAULT 1.0,
                    TeclaOpcion1        TEXT    NOT NULL DEFAULT 'D1',
                    TeclaOpcion2        TEXT    NOT NULL DEFAULT 'D2',
                    TeclaOpcion3        TEXT    NOT NULL DEFAULT 'D3',
                    TeclaOpcion4        TEXT    NOT NULL DEFAULT 'D4',
                    TeclaConfirmar      TEXT    NOT NULL DEFAULT 'Return',
                    TeclaPausar         TEXT    NOT NULL DEFAULT 'Space',
                    BarraColapsada      INTEGER NOT NULL DEFAULT 0
                );";
            comando.ExecuteNonQuery();

            MigrarEsquemaProgreso(conexion);
            MigrarNivelDesbloqueado(conexion);
            CorregirMejorRacha(conexion);
        }

        private const string ClaveMigracionNivel = "MigracionNivelDesbloqueado";

        // Corre una sola vez por base (queda marcada en Metadatos). Las bases anteriores tenían solo el nombre del nivel,
        // y la columna NivelDesbloqueado se agregó con 1: cada perfil conserva el nivel que ya tenía.
        private static void MigrarNivelDesbloqueado(SqliteConnection conexion)
        {
            using var transaccion = conexion.BeginTransaction();

            using (var consultarMarca = conexion.CreateCommand())
            {
                consultarMarca.Transaction = transaccion;
                consultarMarca.CommandText = "SELECT COUNT(*) FROM Metadatos WHERE Clave = $clave;";
                consultarMarca.Parameters.AddWithValue("$clave", ClaveMigracionNivel);
                if (Convert.ToInt64(consultarMarca.ExecuteScalar()) > 0)
                    return;
            }

            var niveles = new List<(long Id, string Nivel)>();
            using (var leer = conexion.CreateCommand())
            {
                leer.Transaction = transaccion;
                leer.CommandText = "SELECT Id, Nivel FROM Perfiles;";
                using SqliteDataReader lector = leer.ExecuteReader();
                while (lector.Read())
                    niveles.Add((lector.GetInt64(0), lector.GetString(1)));
            }

            foreach (var (id, nivel) in niveles)
            {
                using var actualizar = conexion.CreateCommand();
                actualizar.Transaction = transaccion;
                actualizar.CommandText = "UPDATE Perfiles SET NivelDesbloqueado = $numero WHERE Id = $id;";
                actualizar.Parameters.AddWithValue("$numero", Niveles.NumeroDe(nivel));
                actualizar.Parameters.AddWithValue("$id", id);
                actualizar.ExecuteNonQuery();
            }

            using (var marcar = conexion.CreateCommand())
            {
                marcar.Transaction = transaccion;
                marcar.CommandText = "INSERT INTO Metadatos (Clave, Valor) VALUES ($clave, $valor);";
                marcar.Parameters.AddWithValue("$clave", ClaveMigracionNivel);
                marcar.Parameters.AddWithValue("$valor", DateTime.Now.ToString("o", System.Globalization.CultureInfo.InvariantCulture));
                marcar.ExecuteNonQuery();
            }

            transaccion.Commit();
        }

        // La mejor racha nunca puede ser menor que la racha actual. Las bases creadas con versiones anteriores
        // tienen perfiles de ejemplo con racha pero sin mejor racha: se corrigen al arrancar (no hace nada si ya están bien).
        private static void CorregirMejorRacha(SqliteConnection conexion)
        {
            using var comando = conexion.CreateCommand();
            comando.CommandText = "UPDATE Perfiles SET MejorRacha = Racha WHERE MejorRacha < Racha;";
            comando.ExecuteNonQuery();
        }

        private static void MigrarEsquemaProgreso(SqliteConnection conexion)
        {
            string[] columnasAAgregar = {
                "ALTER TABLE Perfiles ADD COLUMN MejorRacha INTEGER NOT NULL DEFAULT 0;",
                "ALTER TABLE Perfiles ADD COLUMN AciertosTotales INTEGER NOT NULL DEFAULT 0;",
                "ALTER TABLE Perfiles ADD COLUMN FallosTotales INTEGER NOT NULL DEFAULT 0;",
                "ALTER TABLE Preferencias ADD COLUMN BarraColapsada INTEGER NOT NULL DEFAULT 0;",
                "ALTER TABLE Perfiles ADD COLUMN NivelDesbloqueado INTEGER NOT NULL DEFAULT 1 CHECK (NivelDesbloqueado BETWEEN 1 AND 4);",
                "ALTER TABLE Perfiles ADD COLUMN NivelacionPendiente INTEGER CHECK (NivelacionPendiente BETWEEN 2 AND 4);"
            };

            foreach (var alter in columnasAAgregar)
            {
                try
                {
                    using var cmd = conexion.CreateCommand();
                    cmd.CommandText = alter;
                    cmd.ExecuteNonQuery();
                }
                catch (SqliteException)
                {
                    // Ignoramos el error: significa que la columna ya existe
                }
            }
        }

        private static void ApartarArchivoCorrupto()
        {
            // Liberamos las conexiones que SQLite deja abiertas en el pool antes de mover el archivo
            SqliteConnection.ClearAllPools();

            string destino = Path.Combine(AppDomain.CurrentDomain.BaseDirectory,
                $"handup.corrupto-{DateTime.Now:yyyyMMdd-HHmmss}.db");

            try
            {
                File.Move(RutaArchivo, destino);
            }
            catch (IOException ex)
            {
                throw new DatabaseException("La base de datos está dañada y no se pudo reemplazar.", ex);
            }
        }

        private static long ContarPerfiles(SqliteConnection conexion)
        {
            using var comando = conexion.CreateCommand();
            comando.CommandText = "SELECT COUNT(*) FROM Perfiles;";
            return (long)(comando.ExecuteScalar() ?? 0L);
        }

        // Si hay un perfiles.json de la versión anterior, migramos esos perfiles.
        // Si no hay (o está corrupto), sembramos perfiles de ejemplo para poder probar el login.
        private static void CargarPerfilesIniciales(SqliteConnection conexion)
        {
            string rutaJson = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, NombreJsonViejo);
            List<PerfilJsonViejo>? perfilesViejos = LeerJsonViejo(rutaJson);
            bool vieneDelJson = perfilesViejos is { Count: > 0 };

            List<PerfilJsonViejo> aCargar = vieneDelJson ? perfilesViejos! : PerfilesDeEjemplo();

            // Todo en una transacción: se cargan todos los perfiles o ninguno
            using var transaccion = conexion.BeginTransaction();
            var usuariosCargados = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (PerfilJsonViejo viejo in aCargar)
            {
                // Salteamos entradas del JSON incompletas o con el usuario repetido
                if (string.IsNullOrWhiteSpace(viejo.Nombre) || string.IsNullOrWhiteSpace(viejo.Usuario)
                    || string.IsNullOrEmpty(viejo.Password) || !usuariosCargados.Add(viejo.Usuario.Trim()))
                    continue;

                (string hash, string sal) = PasswordHasher.Hashear(viejo.Password);
                var perfil = new Perfil
                {
                    Nombre = viejo.Nombre.Trim(),
                    Usuario = viejo.Usuario.Trim(),
                    PasswordHash = hash,
                    PasswordSalt = sal,
                    Nivel = string.IsNullOrWhiteSpace(viejo.Nivel) ? "Principiante" : viejo.Nivel,
                    Racha = viejo.Racha,
                    MejorRacha = Math.Max(viejo.MejorRacha, viejo.Racha),
                    SenasDominadas = viejo.SenasDominadas,
                    Puntos = viejo.Puntos,
                    FechaRegistro = viejo.FechaRegistro
                };
                RepositorioPerfiles.Insertar(conexion, transaccion, perfil);
            }
            transaccion.Commit();

            // Renombramos el JSON para que quede claro que ya no se usa (no lo borramos por las dudas)
            if (vieneDelJson)
            {
                try
                {
                    File.Move(rutaJson, rutaJson + ".migrado", overwrite: true);
                }
                catch (IOException)
                {
                    // No es grave: la migración ya quedó guardada en la base
                }
            }
        }

        private static List<PerfilJsonViejo>? LeerJsonViejo(string ruta)
        {
            if (!File.Exists(ruta))
                return null;

            try
            {
                string json = File.ReadAllText(ruta);
                return string.IsNullOrWhiteSpace(json)
                    ? null
                    : JsonSerializer.Deserialize<List<PerfilJsonViejo>>(json);
            }
            catch (Exception ex) when (ex is JsonException || ex is IOException)
            {
                // JSON corrupto o ilegible: lo ignoramos y usamos los perfiles de ejemplo
                return null;
            }
        }

        private static List<PerfilJsonViejo> PerfilesDeEjemplo() => new()
        {
            new PerfilJsonViejo { Nombre = "Avril Ogas",  Usuario = "avril", Password = "Avril2026!", Nivel = "Intermedio",   Racha = 12, MejorRacha = 18, SenasDominadas = 84, Puntos = 245, FechaRegistro = DateTime.Now.AddDays(-30) },
            new PerfilJsonViejo { Nombre = "Juan Pérez",  Usuario = "juan",  Password = "Juan2026!",  Nivel = "Principiante", Racha = 3,  MejorRacha = 5,  SenasDominadas = 10, Puntos = 40,  FechaRegistro = DateTime.Now.AddDays(-6) },
            new PerfilJsonViejo { Nombre = "Lucía Gómez", Usuario = "lucia", Password = "Lucia2026!", Nivel = "Básico",       Racha = 7,  MejorRacha = 9,  SenasDominadas = 32, Puntos = 120, FechaRegistro = DateTime.Now.AddDays(-14) },
        };

        // Formato del perfiles.json de la versión anterior (con la contraseña en texto plano)
        private class PerfilJsonViejo
        {
            public string Nombre { get; set; } = string.Empty;
            public string Usuario { get; set; } = string.Empty;
            public string Password { get; set; } = string.Empty;
            public string Nivel { get; set; } = "Principiante";
            public int Racha { get; set; }
            public int MejorRacha { get; set; } // el JSON viejo no la tenía: si falta queda en 0 y se toma la racha
            public int SenasDominadas { get; set; }
            public int Puntos { get; set; }
            public DateTime FechaRegistro { get; set; } = DateTime.Now;
        }
    }
}
