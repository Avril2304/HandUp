using System;
using System.Windows.Input;
using Microsoft.Data.Sqlite;

namespace ProyectoFinal
{
    // Repositorio para gestionar la configuración de accesibilidad del usuario.
    // NO implementa IRepositorio<T> porque su PK es PerfilId y no tiene sentido ObtenerTodos().
    public class RepositorioPreferencias
    {
        // Devuelve las preferencias del perfil. Si no existen en la BD, devuelve los valores por defecto.
        public PreferenciasUsuario ObtenerPorPerfil(int perfilId)
        {
            return Database.Ejecutar(conexion =>
            {
                using var comando = conexion.CreateCommand();
                comando.CommandText = @"
                    SELECT AltoContraste, TamanoFuente, TeclaOpcion1, TeclaOpcion2,
                           TeclaOpcion3, TeclaOpcion4, TeclaConfirmar, TeclaPausar, BarraColapsada
                    FROM Preferencias
                    WHERE PerfilId = $id;";
                comando.Parameters.AddWithValue("$id", perfilId);

                using var lector = comando.ExecuteReader();
                if (lector.Read())
                {
                    return new PreferenciasUsuario
                    {
                        PerfilId = perfilId,
                        AltoContraste = lector.GetInt32(0) == 1,
                        TamanoFuente = lector.GetDouble(1),
                        TeclaOpcion1 = ParsearTecla(lector.GetString(2), Key.D1),
                        TeclaOpcion2 = ParsearTecla(lector.GetString(3), Key.D2),
                        TeclaOpcion3 = ParsearTecla(lector.GetString(4), Key.D3),
                        TeclaOpcion4 = ParsearTecla(lector.GetString(5), Key.D4),
                        TeclaConfirmar = ParsearTecla(lector.GetString(6), Key.Return),
                        TeclaPausar = ParsearTecla(lector.GetString(7), Key.Space),
                        BarraColapsada = lector.GetInt32(8) == 1
                    };
                }

                // Si no hay fila en la BD, devolvemos una instancia nueva (que ya tiene los defaults)
                return new PreferenciasUsuario { PerfilId = perfilId };
            });
        }

        // Guarda las preferencias mediante UPSERT (Insert o Update si ya existe).
        public void Guardar(PreferenciasUsuario prefs)
        {
            if (prefs.TieneConflictos())
                throw new InvalidInputException("No se pueden guardar los atajos: hay teclas duplicadas.");

            Database.Ejecutar(conexion =>
            {
                using var comando = conexion.CreateCommand();
                comando.CommandText = @"
                    INSERT INTO Preferencias (PerfilId, AltoContraste, TamanoFuente, TeclaOpcion1,
                                              TeclaOpcion2, TeclaOpcion3, TeclaOpcion4, TeclaConfirmar, TeclaPausar, BarraColapsada)
                    VALUES ($id, $contraste, $fuente, $t1, $t2, $t3, $t4, $tconf, $tpausa, $barra)
                    ON CONFLICT(PerfilId) DO UPDATE SET
                        BarraColapsada = excluded.BarraColapsada,
                        AltoContraste = excluded.AltoContraste,
                        TamanoFuente = excluded.TamanoFuente,
                        TeclaOpcion1 = excluded.TeclaOpcion1,
                        TeclaOpcion2 = excluded.TeclaOpcion2,
                        TeclaOpcion3 = excluded.TeclaOpcion3,
                        TeclaOpcion4 = excluded.TeclaOpcion4,
                        TeclaConfirmar = excluded.TeclaConfirmar,
                        TeclaPausar = excluded.TeclaPausar;";

                comando.Parameters.AddWithValue("$id", prefs.PerfilId);
                comando.Parameters.AddWithValue("$contraste", prefs.AltoContraste ? 1 : 0);
                comando.Parameters.AddWithValue("$fuente", prefs.TamanoFuente);
                comando.Parameters.AddWithValue("$t1", prefs.TeclaOpcion1.ToString());
                comando.Parameters.AddWithValue("$t2", prefs.TeclaOpcion2.ToString());
                comando.Parameters.AddWithValue("$t3", prefs.TeclaOpcion3.ToString());
                comando.Parameters.AddWithValue("$t4", prefs.TeclaOpcion4.ToString());
                comando.Parameters.AddWithValue("$tconf", prefs.TeclaConfirmar.ToString());
                comando.Parameters.AddWithValue("$tpausa", prefs.TeclaPausar.ToString());
                comando.Parameters.AddWithValue("$barra", prefs.BarraColapsada ? 1 : 0);

                comando.ExecuteNonQuery();
            });
        }

        private static Key ParsearTecla(string valorStr, Key valorDefault)
        {
            return Enum.TryParse<Key>(valorStr, out Key k) ? k : valorDefault;
        }
    }
}
