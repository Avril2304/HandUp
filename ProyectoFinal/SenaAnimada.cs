namespace ProyectoFinal
{
    // Señas que se reproducen como video .mp4 (todas las categorías excepto abecedario y numeros)
    public class SenaAnimada : Sena
    {
        // Si tiene video, devuelve el video; si no (caso borde), cae a la imagen estática
        public override string ObtenerRutaMedio()
        {
            // 1. Intentar con el video
            if (!string.IsNullOrEmpty(RutaVideoAbsoluta) && System.IO.File.Exists(RutaVideoAbsoluta))
            {
                return RutaVideoAbsoluta;
            }
            
            // 2. Fallback a la imagen
            if (!string.IsNullOrEmpty(RutaImagenAbsoluta) && System.IO.File.Exists(RutaImagenAbsoluta))
            {
                return RutaImagenAbsoluta;
            }
            
            // 3. Ninguno existe
            throw new AssetNotFoundException($"No se encontró el video ni la imagen de fallback para la seña '{Palabra}'.");
        }
    }
}
