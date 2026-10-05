using System;
using System.Collections.Generic;
using Microsoft.Data.Sqlite;

namespace ProyectoFinal
{
    // Repositorio de solo lectura para las señas (las carga ImportadorSenas)
    // Instancia clases concretas (SenaEstatica o SenaAnimada) según la categoría
    public class RepositorioSenas : IRepositorio<Sena>
    {
        private const string ColumnasSena = "Id, Codigo, Palabra, CategoriaId, Dificultad, Descripcion, Instrucciones, RutaImagen, RutaVideo";

        private static readonly HashSet<string> CategoriasEstaticas = new(StringComparer.OrdinalIgnoreCase)
        { 
            "abecedario", "numeros" 
        };

        public IReadOnlyList<Sena> ObtenerTodos()
        {
            return Database.Ejecutar(conexion =>
            {
                using var comando = conexion.CreateCommand();
                comando.CommandText = $"SELECT {ColumnasSena} FROM Senas ORDER BY Palabra;";

                var lista = new List<Sena>();
                using var lector = comando.ExecuteReader();
                while (lector.Read())
                {
                    lista.Add(LeerSena(lector));
                }
                return lista.AsReadOnly();
            });
        }

        public Sena BuscarPorId(int id)
        {
            return Database.Ejecutar(conexion =>
            {
                using var comando = conexion.CreateCommand();
                comando.CommandText = $"SELECT {ColumnasSena} FROM Senas WHERE Id = $id;";
                comando.Parameters.AddWithValue("$id", id);

                using var lector = comando.ExecuteReader();
                if (lector.Read())
                    return LeerSena(lector);

                throw new SenaNotFoundException(id);
            });
        }

        public Sena? BuscarPorPalabra(string palabra)
        {
            return Database.Ejecutar(conexion =>
            {
                using var comando = conexion.CreateCommand();
                // COLLLATE NOCASE para buscar sin importar mayúsculas/minúsculas
                comando.CommandText = $"SELECT {ColumnasSena} FROM Senas WHERE Palabra = $p COLLATE NOCASE;";
                comando.Parameters.AddWithValue("$p", palabra);

                using var lector = comando.ExecuteReader();
                if (lector.Read())
                    return LeerSena(lector); // Si hay homónimos, devuelve el primero

                return null;
            });
        }

        public IReadOnlyList<Sena> BuscarPorCategoria(string categoriaId)
        {
            return Database.Ejecutar(conexion =>
            {
                using var comando = conexion.CreateCommand();
                comando.CommandText = $"SELECT {ColumnasSena} FROM Senas WHERE CategoriaId = $cat ORDER BY Palabra;";
                comando.Parameters.AddWithValue("$cat", categoriaId);

                var lista = new List<Sena>();
                using var lector = comando.ExecuteReader();
                while (lector.Read())
                {
                    lista.Add(LeerSena(lector));
                }
                return lista.AsReadOnly();
            });
        }

        public IReadOnlyList<Sena> BuscarPorDificultad(int nivel)
        {
            return Database.Ejecutar(conexion =>
            {
                using var comando = conexion.CreateCommand();
                comando.CommandText = $"SELECT {ColumnasSena} FROM Senas WHERE Dificultad = $nivel ORDER BY Palabra;";
                comando.Parameters.AddWithValue("$nivel", nivel);

                var lista = new List<Sena>();
                using var lector = comando.ExecuteReader();
                while (lector.Read())
                {
                    lista.Add(LeerSena(lector));
                }
                return lista.AsReadOnly();
            });
        }

        public IReadOnlyList<Sena> BuscarPorCategoriaYDificultad(string categoriaId, int dificultad)
        {
             return Database.Ejecutar(conexion =>
            {
                using var comando = conexion.CreateCommand();
                comando.CommandText = $"SELECT {ColumnasSena} FROM Senas WHERE CategoriaId = $cat AND Dificultad = $dif ORDER BY Palabra;";
                comando.Parameters.AddWithValue("$cat", categoriaId);
                comando.Parameters.AddWithValue("$dif", dificultad);

                var lista = new List<Sena>();
                using var lector = comando.ExecuteReader();
                while (lector.Read())
                {
                    lista.Add(LeerSena(lector));
                }
                return lista.AsReadOnly();
            });
        }

        // Búsqueda para el diccionario visual (coincidencia parcial)
        public IReadOnlyList<Sena> Buscar(string textoParcial)
        {
            return Database.Ejecutar(conexion =>
            {
                using var comando = conexion.CreateCommand();
                comando.CommandText = $"SELECT {ColumnasSena} FROM Senas WHERE Palabra LIKE '%' || $texto || '%' COLLATE NOCASE ORDER BY Palabra;";
                comando.Parameters.AddWithValue("$texto", textoParcial);

                var lista = new List<Sena>();
                using var lector = comando.ExecuteReader();
                while (lector.Read())
                {
                    lista.Add(LeerSena(lector));
                }
                return lista.AsReadOnly();
            });
        }

        // Usado para generar opciones falsas (distractores) en los juegos
        public IReadOnlyList<Sena> ObtenerAleatorias(int cantidad, string? categoriaId, int? dificultad, HashSet<int>? excluirIds)
        {
             return Database.Ejecutar(conexion =>
            {
                using var comando = conexion.CreateCommand();
                
                string query = $"SELECT {ColumnasSena} FROM Senas WHERE 1=1";
                
                if (!string.IsNullOrEmpty(categoriaId))
                {
                    query += " AND CategoriaId = $cat";
                    comando.Parameters.AddWithValue("$cat", categoriaId);
                }

                if (dificultad.HasValue)
                {
                    query += " AND Dificultad = $dif";
                    comando.Parameters.AddWithValue("$dif", dificultad.Value);
                }

                if (excluirIds != null && excluirIds.Count > 0)
                {
                    string excluidos = string.Join(",", excluirIds);
                    query += $" AND Id NOT IN ({excluidos})";
                }

                query += " ORDER BY RANDOM() LIMIT $cant;";
                comando.Parameters.AddWithValue("$cant", cantidad);
                comando.CommandText = query;

                var lista = new List<Sena>();
                using var lector = comando.ExecuteReader();
                while (lector.Read())
                {
                    lista.Add(LeerSena(lector));
                }
                return lista.AsReadOnly();
            });
        }

        public void Agregar(Sena item) => 
            throw new NotSupportedException("Las señas son de solo lectura y se cargan mediante el ImportadorSenas.");

        public void Actualizar(Sena item) => 
            throw new NotSupportedException("Las señas son de solo lectura y se actualizan mediante el ImportadorSenas.");

        private static Sena LeerSena(SqliteDataReader lector)
        {
            string categoriaId = lector.GetString(3);

            // Decisión polimórfica basada en la categoría
            Sena sena = CategoriasEstaticas.Contains(categoriaId)
                ? new SenaEstatica()
                : new SenaAnimada();

            sena.Id = lector.GetInt32(0);
            sena.Codigo = lector.GetString(1);
            sena.Palabra = lector.GetString(2);
            sena.CategoriaId = categoriaId;
            sena.Dificultad = lector.GetInt32(4);
            sena.Descripcion = lector.GetString(5);
            sena.Instrucciones = lector.IsDBNull(6) ? null : lector.GetString(6);
            sena.RutaImagen = lector.GetString(7);
            sena.RutaVideo = lector.IsDBNull(8) ? null : lector.GetString(8);

            return sena;
        }
    }
}
