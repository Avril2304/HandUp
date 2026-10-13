using System.Collections.Generic;

namespace ProyectoFinal
{
    public class NivelInfo
    {
        public int Numero { get; set; }
        public string Nombre { get; set; } = string.Empty;
        public int CantidadSenas { get; set; }
        public List<string> Categorias { get; set; } = new List<string>();
    }
}
