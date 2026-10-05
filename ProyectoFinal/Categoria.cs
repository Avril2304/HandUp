namespace ProyectoFinal
{
    // Representa una de las 22 categorías temáticas del banco LSA
    public class Categoria
    {
        public string Id { get; set; } = string.Empty;      // ej: "saludos"
        public string Nombre { get; set; } = string.Empty;   // ej: "Saludos"
        public string? Emoji { get; set; }                    // ej: "👋"
        public string? Descripcion { get; set; }
        public string? Color { get; set; }                    // ej: "#4CAF50"
        public int Orden { get; set; }
        public int Dificultad { get; set; }                   // 1-4, se calcula al cargar desde SQLite

        public override string ToString() => $"{Emoji} {Nombre}";
    }
}
