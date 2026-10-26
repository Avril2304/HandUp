using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Data.Sqlite;

namespace ProyectoFinal
{
    // Carga el banco multimedia LSA (signs_database.json, junto al ejecutable) en las tablas Categorias y Senas.
    // Se llama al arrancar la app. Guarda una huella (SHA-256) del JSON importado: si el archivo no cambió,
    // no hace nada; si cambió (por ejemplo, se corrigieron descripciones), vuelve a cargar todo.
    public static class ImportadorSenas
    {
        private const string NombreArchivo = "signs_database.json";
        private const string ClaveHuella = "HuellaBancoSenas";
        private const string PrefijoCodigo = "sign_";

        public static string RutaArchivo => Path.Combine(AppDomain.CurrentDomain.BaseDirectory, NombreArchivo);

        // Lanza BancoSenasException si el JSON falta (y la base está vacía), no se puede leer o no tiene señas válidas.
        // Si ya hay señas cargadas y el JSON falta, se sigue usando lo que hay en la base.
        public static void Sincronizar()
        {
            bool haySenas = Database.Ejecutar(conexion => ContarSenas(conexion) > 0);

            if (!File.Exists(RutaArchivo))
            {
                if (haySenas)
                    return;
                throw new BancoSenasException($"No se encontró el archivo {NombreArchivo} junto al ejecutable.");
            }

            byte[] contenido;
            try
            {
                contenido = File.ReadAllBytes(RutaArchivo);
            }
            catch (Exception ex) when (ex is IOException || ex is UnauthorizedAccessException)
            {
                throw new BancoSenasException($"No se pudo leer el archivo {NombreArchivo}.", ex);
            }

            // Si la huella coincide con la de la última importación, el JSON no cambió
            string huella = Convert.ToHexString(SHA256.HashData(contenido));
            string? huellaGuardada = Database.Ejecutar(conexion => LeerMetadato(conexion, ClaveHuella));
            if (haySenas && huella == huellaGuardada)
                return;

            BancoJson banco = Deserializar(contenido);
            List<CategoriaJson> categorias = FiltrarCategorias(banco.Categorias);
            List<(int Id, SenaJson Sena)> senas = FiltrarSenas(banco.Senas, categorias);

            if (senas.Count == 0)
                throw new BancoSenasException($"El archivo {NombreArchivo} no tiene ninguna seña válida.");

            Database.Ejecutar(conexion => Guardar(conexion, categorias, senas, huella));
        }

        private static BancoJson Deserializar(byte[] contenido)
        {
            try
            {
                // Sacamos el BOM (si lo tiene) para que System.Text.Json no lo tome como un carácter inválido
                string texto = Encoding.UTF8.GetString(contenido).TrimStart('﻿');
                return JsonSerializer.Deserialize<BancoJson>(texto)
                    ?? throw new BancoSenasException($"El archivo {NombreArchivo} está vacío.");
            }
            catch (JsonException ex)
            {
                throw new BancoSenasException($"El archivo {NombreArchivo} está dañado o no tiene el formato esperado.", ex);
            }
        }

        // Se quedan solo las categorías con id y nombre (sin repetidas). El id se guarda en minúsculas.
        private static List<CategoriaJson> FiltrarCategorias(List<CategoriaJson>? categoriasJson)
        {
            var categorias = new List<CategoriaJson>();
            var idsUsados = new HashSet<string>();

            foreach (CategoriaJson categoria in categoriasJson ?? new List<CategoriaJson>())
            {
                if (string.IsNullOrWhiteSpace(categoria.Id) || string.IsNullOrWhiteSpace(categoria.Nombre))
                    continue;

                categoria.Id = categoria.Id.Trim().ToLowerInvariant();
                if (idsUsados.Add(categoria.Id))
                    categorias.Add(categoria);
            }
            return categorias;
        }

        // Descarta las señas incompletas o inconsistentes en vez de frenar toda la importación
        private static List<(int Id, SenaJson Sena)> FiltrarSenas(List<SenaJson>? senasJson, List<CategoriaJson> categorias)
        {
            var idsCategorias = new HashSet<string>(categorias.Select(c => c.Id!));
            var senas = new List<(int Id, SenaJson Sena)>();
            var idsUsados = new HashSet<int>();
            int descartadas = 0;

            foreach (SenaJson sena in senasJson ?? new List<SenaJson>())
            {
                sena.Categoria = sena.Categoria?.Trim().ToLowerInvariant();

                bool valida = TryObtenerId(sena.Codigo, out int id)
                    && !string.IsNullOrWhiteSpace(sena.Palabra)
                    && sena.Categoria is not null && idsCategorias.Contains(sena.Categoria)
                    && sena.Dificultad is >= 1 and <= 4
                    && !string.IsNullOrWhiteSpace(sena.Media?.ImagenEstatica)
                    && idsUsados.Add(id);

                if (valida)
                    senas.Add((id, sena));
                else
                    descartadas++;
            }

            if (descartadas > 0)
                Debug.WriteLine($"ImportadorSenas: se descartaron {descartadas} señas inválidas o repetidas.");

            return senas;
        }

        // "sign_036" -> 36
        private static bool TryObtenerId(string? codigo, out int id)
        {
            id = 0;
            return codigo is not null
                && codigo.StartsWith(PrefijoCodigo, StringComparison.OrdinalIgnoreCase)
                && int.TryParse(codigo.AsSpan(PrefijoCodigo.Length), out id)
                && id > 0;
        }

        // Reemplaza el contenido de las tablas dentro de una transacción: se carga todo o nada.
        // ProgresoSenas apunta a Senas: las claves foráneas se controlan recién al confirmar, cuando las señas
        // ya se volvieron a cargar con los mismos Id. Así el progreso de los perfiles se conserva.
        private static void Guardar(SqliteConnection conexion, List<CategoriaJson> categorias,
            List<(int Id, SenaJson Sena)> senas, string huella)
        {
            using SqliteTransaction transaccion = conexion.BeginTransaction();

            using (var borrar = conexion.CreateCommand())
            {
                borrar.Transaction = transaccion;
                borrar.CommandText = "PRAGMA defer_foreign_keys = ON; DELETE FROM Senas; DELETE FROM Categorias;";
                borrar.ExecuteNonQuery();
            }

            using (var insertarCategoria = conexion.CreateCommand())
            {
                insertarCategoria.Transaction = transaccion;
                insertarCategoria.CommandText = @"
                    INSERT INTO Categorias (Id, Nombre, Emoji, Descripcion, Color, Orden)
                    VALUES ($id, $nombre, $emoji, $descripcion, $color, $orden);";

                for (int i = 0; i < categorias.Count; i++)
                {
                    CategoriaJson categoria = categorias[i];
                    insertarCategoria.Parameters.Clear();
                    insertarCategoria.Parameters.AddWithValue("$id", categoria.Id);
                    insertarCategoria.Parameters.AddWithValue("$nombre", categoria.Nombre!.Trim());
                    insertarCategoria.Parameters.AddWithValue("$emoji", ValorONull(categoria.Emoji));
                    insertarCategoria.Parameters.AddWithValue("$descripcion", ValorONull(categoria.Descripcion));
                    insertarCategoria.Parameters.AddWithValue("$color", ValorONull(categoria.Color));
                    insertarCategoria.Parameters.AddWithValue("$orden", i);
                    insertarCategoria.ExecuteNonQuery();
                }
            }

            using (var insertarSena = conexion.CreateCommand())
            {
                insertarSena.Transaction = transaccion;
                insertarSena.CommandText = @"
                    INSERT INTO Senas (Id, Codigo, Palabra, CategoriaId, Dificultad, Descripcion, Instrucciones, RutaImagen, RutaVideo, Sistematizacion)
                    VALUES ($id, $codigo, $palabra, $categoria, $dificultad, $descripcion, $instrucciones, $imagen, $video, $sistematizacion);";

                foreach ((int id, SenaJson sena) in senas)
                {
                    // Las instrucciones son una lista de pasos: las guardamos una por línea
                    string? instrucciones = sena.Instrucciones is { Count: > 0 }
                        ? string.Join("\n", sena.Instrucciones.Where(p => !string.IsNullOrWhiteSpace(p)).Select(p => p.Trim()))
                        : null;

                    insertarSena.Parameters.Clear();
                    insertarSena.Parameters.AddWithValue("$id", id);
                    insertarSena.Parameters.AddWithValue("$codigo", sena.Codigo!.Trim());
                    insertarSena.Parameters.AddWithValue("$palabra", sena.Palabra!.Trim());
                    insertarSena.Parameters.AddWithValue("$categoria", sena.Categoria);
                    insertarSena.Parameters.AddWithValue("$dificultad", sena.Dificultad);
                    insertarSena.Parameters.AddWithValue("$descripcion", sena.Descripcion?.Trim() ?? string.Empty);
                    insertarSena.Parameters.AddWithValue("$instrucciones", ValorONull(instrucciones));
                    insertarSena.Parameters.AddWithValue("$imagen", sena.Media!.ImagenEstatica!.Trim());
                    insertarSena.Parameters.AddWithValue("$video", ValorONull(sena.Media.Video));
                    insertarSena.Parameters.AddWithValue("$sistematizacion", ValorONull(sena.Sistematizacion?.ComoLineas()));
                    insertarSena.ExecuteNonQuery();
                }
            }

            using (var guardarHuella = conexion.CreateCommand())
            {
                guardarHuella.Transaction = transaccion;
                guardarHuella.CommandText = @"
                    INSERT INTO Metadatos (Clave, Valor) VALUES ($clave, $valor)
                    ON CONFLICT(Clave) DO UPDATE SET Valor = excluded.Valor;";
                guardarHuella.Parameters.AddWithValue("$clave", ClaveHuella);
                guardarHuella.Parameters.AddWithValue("$valor", huella);
                guardarHuella.ExecuteNonQuery();
            }

            // Si una seña se sacó del JSON, se borra también el progreso que los perfiles tenían en ella
            using (var limpiarProgreso = conexion.CreateCommand())
            {
                limpiarProgreso.Transaction = transaccion;
                limpiarProgreso.CommandText = "DELETE FROM ProgresoSenas WHERE SenaId NOT IN (SELECT Id FROM Senas);";
                limpiarProgreso.ExecuteNonQuery();
            }

            transaccion.Commit();
        }

        private static long ContarSenas(SqliteConnection conexion)
        {
            using var comando = conexion.CreateCommand();
            comando.CommandText = "SELECT COUNT(*) FROM Senas;";
            return (long)(comando.ExecuteScalar() ?? 0L);
        }

        private static string? LeerMetadato(SqliteConnection conexion, string clave)
        {
            using var comando = conexion.CreateCommand();
            comando.CommandText = "SELECT Valor FROM Metadatos WHERE Clave = $clave;";
            comando.Parameters.AddWithValue("$clave", clave);
            return comando.ExecuteScalar() as string;
        }

        // SQLite necesita DBNull.Value (no null) para guardar un NULL; los textos vacíos también van como NULL
        private static object ValorONull(string? valor) =>
            string.IsNullOrWhiteSpace(valor) ? DBNull.Value : valor.Trim();

        // ----- Formato de signs_database.json (solo los campos que usamos) -----

        private class BancoJson
        {
            [JsonPropertyName("categories")] public List<CategoriaJson>? Categorias { get; set; }
            [JsonPropertyName("signs")] public List<SenaJson>? Senas { get; set; }
        }

        private class CategoriaJson
        {
            [JsonPropertyName("id")] public string? Id { get; set; }
            [JsonPropertyName("name")] public string? Nombre { get; set; }
            [JsonPropertyName("emoji")] public string? Emoji { get; set; }
            [JsonPropertyName("description")] public string? Descripcion { get; set; }
            [JsonPropertyName("color")] public string? Color { get; set; }
        }

        private class SenaJson
        {
            [JsonPropertyName("id")] public string? Codigo { get; set; }
            [JsonPropertyName("word")] public string? Palabra { get; set; }
            [JsonPropertyName("category")] public string? Categoria { get; set; }
            [JsonPropertyName("difficulty")] public int Dificultad { get; set; }
            [JsonPropertyName("description")] public string? Descripcion { get; set; }
            [JsonPropertyName("instructions")] public List<string>? Instrucciones { get; set; }
            [JsonPropertyName("media")] public MediaJson? Media { get; set; }
            [JsonPropertyName("sistematizacion")] public SistematizacionJson? Sistematizacion { get; set; }
        }

        // Parámetros con los que se sistematiza una seña en LSA
        private class SistematizacionJson
        {
            [JsonPropertyName("configuracion")] public string? Configuracion { get; set; }
            [JsonPropertyName("ubicacion")] public string? Ubicacion { get; set; }
            [JsonPropertyName("orientacion")] public string? Orientacion { get; set; }
            [JsonPropertyName("movimiento")] public string? Movimiento { get; set; }
            [JsonPropertyName("direccion")] public string? Direccion { get; set; }
            [JsonPropertyName("rasgo_no_manual")] public string? RasgoNoManual { get; set; }

            // Se guarda en la base como "Parámetro: valor", uno por línea y en este orden (los vacíos no van)
            public string ComoLineas()
            {
                var parametros = new (string Nombre, string? Valor)[]
                {
                    ("Configuración", Configuracion),
                    ("Ubicación", Ubicacion),
                    ("Orientación", Orientacion),
                    ("Movimiento", Movimiento),
                    ("Dirección", Direccion),
                    ("Rasgo no manual", RasgoNoManual)
                };
                return string.Join("\n", parametros
                    .Where(p => !string.IsNullOrWhiteSpace(p.Valor))
                    .Select(p => $"{p.Nombre}: {p.Valor!.Trim()}"));
            }
        }

        private class MediaJson
        {
            [JsonPropertyName("image_static")] public string? ImagenEstatica { get; set; }
            [JsonPropertyName("video")] public string? Video { get; set; }
        }
    }
}
