using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;

namespace ProyectoFinal
{
    // Banco de frases validado (fase 4 del plan de niveles): lo usa la prueba final para las preguntas de frases.
    // Lee frases.json (junto al ejecutable), arma las frases a partir de sus plantillas y valida cada una contra el banco de señas:
    //  - todas sus señas existen (o se pueden deletrear, si llevan #),
    //  - ninguna es de un nivel mayor al de la frase,
    //  - y al menos una es del nivel de la frase.
    // Las que no cumplen se descartan y quedan anotadas en Avisos, igual que hace ImportadorSenas con las señas.
    public class BancoFrases
    {
        private const string NombreArchivo = "frases.json";
        private const string CategoriaAbecedario = "abecedario";

        // Cada nivel tiene que tener al menos estas frases, para que dos pruebas seguidas no sean iguales
        public const int MinimoPorNivel = 30;

        private static readonly Regex Hueco = new(@"\{(\w+)\}");

        private static BancoFrases? _cargado;

        private readonly Dictionary<int, List<Frase>> _porNivel;

        public IReadOnlyList<Frase> Frases { get; }

        // Frases o plantillas descartadas y niveles con pocas frases (para revisar el JSON)
        public IReadOnlyList<string> Avisos { get; }

        private BancoFrases(List<Frase> frases, List<string> avisos)
        {
            Frases = frases;
            Avisos = avisos;
            _porNivel = frases.GroupBy(f => f.Nivel).ToDictionary(g => g.Key, g => g.ToList());
        }

        public static string RutaArchivo => Path.Combine(AppDomain.CurrentDomain.BaseDirectory, NombreArchivo);

        public IReadOnlyList<Frase> ObtenerPorNivel(int nivel) =>
            _porNivel.TryGetValue(nivel, out List<Frase>? frases) ? frases : Array.Empty<Frase>();

        // Se carga una sola vez por ejecución (las señas no cambian mientras la app está abierta).
        // Lanza BancoFrasesException si el archivo falta o está dañado, y DatabaseException si la base no responde.
        public static BancoFrases Cargar()
        {
            if (_cargado is not null)
                return _cargado;

            if (!File.Exists(RutaArchivo))
                throw new BancoFrasesException($"No se encontró el banco de frases ({NombreArchivo}) junto al ejecutable.");

            string json;
            try
            {
                json = File.ReadAllText(RutaArchivo);
            }
            catch (Exception ex) when (ex is IOException || ex is UnauthorizedAccessException)
            {
                throw new BancoFrasesException($"No se pudo leer el banco de frases ({NombreArchivo}).", ex);
            }

            _cargado = Construir(json, new RepositorioSenas().ObtenerTodos());
            foreach (string aviso in _cargado.Avisos)
                Debug.WriteLine($"BancoFrases: {aviso}");
            return _cargado;
        }

        // Arma y valida el banco a partir del texto del JSON y de las señas disponibles
        public static BancoFrases Construir(string json, IEnumerable<Sena> senas)
        {
            ArchivoFrases archivo;
            try
            {
                archivo = JsonSerializer.Deserialize<ArchivoFrases>(json)
                          ?? throw new BancoFrasesException($"El banco de frases ({NombreArchivo}) está vacío.");
            }
            catch (JsonException ex)
            {
                throw new BancoFrasesException($"El banco de frases ({NombreArchivo}) está dañado o no tiene el formato esperado.", ex);
            }

            var resolvedor = new ResolvedorSenas(senas.ToList());
            var frases = new List<Frase>();
            var avisos = new List<string>();

            foreach (PlantillaJson plantilla in archivo.Frases ?? new List<PlantillaJson>())
                frases.AddRange(ArmarPlantilla(plantilla, archivo.Listas ?? new(), resolvedor, avisos));

            if (frases.Count == 0)
                throw new BancoFrasesException($"El banco de frases ({NombreArchivo}) no tiene ninguna frase válida.");

            for (int nivel = 1; nivel <= Niveles.Nombres.Count; nivel++)
            {
                int cantidad = frases.Count(f => f.Nivel == nivel);
                if (cantidad < MinimoPorNivel)
                    avisos.Add($"El nivel {nivel} tiene {cantidad} frases (se recomiendan {MinimoPorNivel} o más).");
            }

            return new BancoFrases(frases, avisos);
        }

        // Una plantilla -> todas sus frases (una por cada combinación de los huecos). Las inválidas se descartan.
        private static List<Frase> ArmarPlantilla(PlantillaJson plantilla, Dictionary<string, List<string>> listas,
                                                  ResolvedorSenas resolvedor, List<string> avisos)
        {
            var frases = new List<Frase>();
            string id = string.IsNullOrWhiteSpace(plantilla.Id) ? "(sin id)" : plantilla.Id;

            if (plantilla.Nivel is < 1 or > 4 || plantilla.Senas is not { Count: > 0 } || string.IsNullOrWhiteSpace(plantilla.Espanol))
            {
                avisos.Add($"{id}: le falta el nivel (1 a 4), las señas o la traducción.");
                return frases;
            }

            // Los huecos de la plantilla, en orden y sin repetir: {comida}, {familia_mi}...
            List<string> huecos = plantilla.Senas.SelectMany(s => Hueco.Matches(s).Select(m => m.Groups[1].Value)).Distinct().ToList();
            string? faltante = huecos.FirstOrDefault(h => !listas.ContainsKey(h) || listas[h].Count == 0);
            if (faltante is not null)
            {
                avisos.Add($"{id}: no existe la lista {{{faltante}}}.");
                return frases;
            }

            var noEncontradas = new HashSet<string>();
            foreach (Dictionary<string, ElementoLista> combinacion in Combinaciones(huecos, listas))
            {
                var senasFrase = new List<Sena>();
                bool valida = true;

                foreach (string parte in plantilla.Senas)
                {
                    Match hueco = Hueco.Match(parte);
                    string token = hueco.Success ? combinacion[hueco.Groups[1].Value].Sena : parte;

                    IReadOnlyList<Sena>? encontradas = resolvedor.Resolver(token);
                    if (encontradas is null)
                    {
                        noEncontradas.Add(token);
                        valida = false;
                        break;
                    }
                    senasFrase.AddRange(encontradas);
                }

                string texto = MayusculaInicial(Hueco.Replace(plantilla.Espanol!,
                    m => combinacion.TryGetValue(m.Groups[1].Value, out ElementoLista? e) ? e.Texto : m.Value));

                if (!valida)
                    continue;

                if (senasFrase.Any(s => s.Dificultad > plantilla.Nivel))
                {
                    avisos.Add($"{id}: \"{texto}\" usa señas de un nivel mayor a {plantilla.Nivel}.");
                    continue;
                }
                if (senasFrase.All(s => s.Dificultad != plantilla.Nivel))
                {
                    avisos.Add($"{id}: \"{texto}\" no tiene ninguna seña de nivel {plantilla.Nivel}.");
                    continue;
                }
                if (frases.Any(f => f.Texto == texto))
                    continue; // dos combinaciones que dan la misma frase

                frases.Add(new Frase(id, plantilla.Nivel, texto, senasFrase));
            }

            foreach (string token in noEncontradas)
                avisos.Add($"{id}: no se encontró la seña \"{token}\".");

            // Opciones incorrectas: las otras frases de la plantilla, o las que trae escritas si no tiene huecos
            List<string> distractores = (plantilla.Distractores ?? new()).Where(d => !string.IsNullOrWhiteSpace(d)).ToList();
            foreach (Frase frase in frases)
            {
                frase.Alternativas = distractores.Count > 0
                    ? distractores
                    : frases.Where(f => f != frase).Select(f => f.Texto).ToList();
            }

            if (frases.Count > 0 && frases[0].Alternativas.Count < 3)
            {
                avisos.Add($"{id}: tiene menos de 3 opciones incorrectas (hacen falta más palabras en sus listas, o \"distractores\").");
                return new List<Frase>();
            }
            return frases;
        }

        // Producto cartesiano de los huecos: {familia_mi} x {comida} -> (Mi mamá, pizza), (Mi mamá, empanadas)...
        private static IEnumerable<Dictionary<string, ElementoLista>> Combinaciones(List<string> huecos, Dictionary<string, List<string>> listas)
        {
            IEnumerable<Dictionary<string, ElementoLista>> resultado = new[] { new Dictionary<string, ElementoLista>() };
            foreach (string hueco in huecos)
            {
                List<ElementoLista> elementos = listas[hueco].Select(ElementoLista.Leer).ToList();
                resultado = resultado.SelectMany(parcial => elementos.Select(e => new Dictionary<string, ElementoLista>(parcial) { [hueco] = e }));
            }
            return resultado;
        }

        // "mi mamá cocina pizza." -> "Mi mamá cocina pizza." (respeta el ¿ o ¡ del principio)
        private static string MayusculaInicial(string texto)
        {
            int i = 0;
            while (i < texto.Length && !char.IsLetterOrDigit(texto[i]))
                i++;
            return i < texto.Length ? texto[..i] + char.ToUpper(texto[i]) + texto[(i + 1)..] : texto;
        }

        // ===== Encontrar la seña de cada palabra de la glosa =====

        // Busca la seña de una palabra de la glosa: primero la palabra exacta del banco ("PAPÁ", "Mañana (día siguiente)"),
        // después sin tildes ni signos ("buenos dias" -> "Buenos dias"), y si no, como lo haría el Traductor ("abuela" -> "ABUELO A").
        // Con # adelante se deletrea con el abecedario ("#Ana" -> A, N, A).
        private class ResolvedorSenas
        {
            private readonly List<Sena> _senas;
            private readonly Traductor _traductor;

            public ResolvedorSenas(List<Sena> senas)
            {
                _senas = senas.Where(s => !s.CategoriaId.Equals(CategoriaAbecedario, StringComparison.OrdinalIgnoreCase))
                              .OrderBy(s => s.Dificultad).ThenBy(s => s.Id)
                              .ToList();
                _traductor = new Traductor(senas);
            }

            public IReadOnlyList<Sena>? Resolver(string token)
            {
                token = token.Trim();
                if (token.StartsWith('#'))
                {
                    ElementoTraduccion deletreo = _traductor.Deletrear(token[1..]);
                    return deletreo.Letras.Count > 0 && deletreo.SinSena.Count == 0 ? deletreo.Letras : null;
                }

                Sena? sena = _senas.FirstOrDefault(s => string.Equals(s.Palabra.Trim(), token, StringComparison.OrdinalIgnoreCase))
                             ?? _senas.FirstOrDefault(s => Traductor.Normalizar(s.Palabra) == Traductor.Normalizar(token));
                if (sena is not null)
                    return new[] { sena };

                IReadOnlyList<ElementoTraduccion> traduccion = _traductor.Traducir(token);
                return traduccion.Count == 1 && traduccion[0].SenaElegida is Sena elegida ? new[] { elegida } : null;
            }
        }

        // ===== Formato de frases.json =====

        private class ArchivoFrases
        {
            [JsonPropertyName("listas")] public Dictionary<string, List<string>>? Listas { get; set; }
            [JsonPropertyName("frases")] public List<PlantillaJson>? Frases { get; set; }
        }

        private class PlantillaJson
        {
            [JsonPropertyName("id")] public string? Id { get; set; }
            [JsonPropertyName("nivel")] public int Nivel { get; set; }
            [JsonPropertyName("senas")] public List<string>? Senas { get; set; }
            [JsonPropertyName("espanol")] public string? Espanol { get; set; }
            [JsonPropertyName("distractores")] public List<string>? Distractores { get; set; }
        }

        // Un elemento de una lista: "seña|texto" ("empanada|empanadas"); sin "|", el texto es la misma seña (sin el # del deletreo)
        private class ElementoLista
        {
            public string Sena { get; private init; } = string.Empty;
            public string Texto { get; private init; } = string.Empty;

            public static ElementoLista Leer(string valor)
            {
                string[] partes = valor.Split('|', 2);
                string sena = partes[0].Trim();
                string texto = partes.Length > 1 ? partes[1].Trim() : sena.TrimStart('#');
                return new ElementoLista { Sena = sena, Texto = texto };
            }
        }
    }
}
