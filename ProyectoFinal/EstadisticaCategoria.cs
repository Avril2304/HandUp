namespace ProyectoFinal
{
    // Cómo le va a un perfil en una categoría (sale de sumar ProgresoSenas por categoría)
    public record EstadisticaCategoria(string CategoriaId, string Nombre, string? Emoji, int TotalSenas,
                                       int Aciertos, int Fallos, int Dominadas)
    {
        public int Respondidas => Aciertos + Fallos;
        public bool Practicada => Respondidas > 0;
        public double Precision => Respondidas == 0 ? 0 : (double)Aciertos / Respondidas;

        // La precisión también en palabras, para no depender solo de un número o un color
        public string Valoracion => Precision switch
        {
            >= 0.8 => "muy bien",
            >= 0.5 => "bien",
            _ => "para reforzar"
        };

        // Menos de 50 %: se marca en rojo (coincide con la palabra "para reforzar")
        public bool ParaReforzar => Practicada && Precision < 0.5;

        // Menos de 80 %: se sugiere practicarla más (aunque vaya "bien")
        public bool PuedeMejorar => Practicada && Precision < 0.8;
    }
}
