using System;
using System.Collections.Generic;
using Microsoft.Data.Sqlite;

namespace ProyectoFinal
{
    // Repositorio de solo lectura para las categorías (las carga ImportadorSenas)
    public class RepositorioCategorias : IRepositorio<Categoria>
    {
        public IReadOnlyList<Categoria> ObtenerTodos()
        {
            return Database.Ejecutar(conexion =>
            {
                using var comando = conexion.CreateCommand();
                comando.CommandText = @"
                    SELECT c.Id, c.Nombre, c.Emoji, c.Descripcion, c.Color, c.Orden, MIN(s.Dificultad) AS Dificultad
                    FROM Categorias c
                    LEFT JOIN Senas s ON s.CategoriaId = c.Id
                    GROUP BY c.Id
                    ORDER BY c.Orden;";

                var lista = new List<Categoria>();
                using var lector = comando.ExecuteReader();
                while (lector.Read())
                {
                    lista.Add(LeerCategoria(lector));
                }
                return lista.AsReadOnly();
            });
        }

        public Categoria BuscarPorId(int id)
        {
            throw new InvalidInputException("Las categorías usan un Id de texto (ej: 'saludos'). Usa BuscarPorClave en su lugar.");
        }

        public Categoria? BuscarPorClave(string id)
        {
            return Database.Ejecutar(conexion =>
            {
                using var comando = conexion.CreateCommand();
                comando.CommandText = @"
                    SELECT c.Id, c.Nombre, c.Emoji, c.Descripcion, c.Color, c.Orden, MIN(s.Dificultad) AS Dificultad
                    FROM Categorias c
                    LEFT JOIN Senas s ON s.CategoriaId = c.Id
                    WHERE c.Id = $id
                    GROUP BY c.Id;";
                comando.Parameters.AddWithValue("$id", id);

                using var lector = comando.ExecuteReader();
                if (lector.Read())
                    return LeerCategoria(lector);

                return null;
            });
        }
        
        public IReadOnlyList<Categoria> ObtenerPorDificultad(int nivel)
        {
            return Database.Ejecutar(conexion =>
            {
                using var comando = conexion.CreateCommand();
                comando.CommandText = @"
                    SELECT c.Id, c.Nombre, c.Emoji, c.Descripcion, c.Color, c.Orden, MIN(s.Dificultad) AS Dificultad
                    FROM Categorias c
                    JOIN Senas s ON s.CategoriaId = c.Id
                    GROUP BY c.Id
                    HAVING MIN(s.Dificultad) = $nivel
                    ORDER BY c.Orden;";
                comando.Parameters.AddWithValue("$nivel", nivel);

                var lista = new List<Categoria>();
                using var lector = comando.ExecuteReader();
                while (lector.Read())
                {
                    lista.Add(LeerCategoria(lector));
                }
                return lista.AsReadOnly();
            });
        }

        public long ContarSenas(string categoriaId)
        {
            return Database.Ejecutar(conexion =>
            {
                using var comando = conexion.CreateCommand();
                comando.CommandText = "SELECT COUNT(*) FROM Senas WHERE CategoriaId = $id;";
                comando.Parameters.AddWithValue("$id", categoriaId);
                return (long)(comando.ExecuteScalar() ?? 0L);
            });
        }

        public void Agregar(Categoria item) => 
            throw new NotSupportedException("Las categorías son de solo lectura y se cargan mediante el ImportadorSenas.");

        public void Actualizar(Categoria item) => 
            throw new NotSupportedException("Las categorías son de solo lectura y se actualizan mediante el ImportadorSenas.");

        private static Categoria LeerCategoria(SqliteDataReader lector)
        {
            return new Categoria
            {
                Id = lector.GetString(0),
                Nombre = lector.GetString(1),
                Emoji = lector.IsDBNull(2) ? null : lector.GetString(2),
                Descripcion = lector.IsDBNull(3) ? null : lector.GetString(3),
                Color = lector.IsDBNull(4) ? null : lector.GetString(4),
                Orden = lector.GetInt32(5),
                Dificultad = lector.IsDBNull(6) ? 0 : lector.GetInt32(6)
            };
        }
    }
}
