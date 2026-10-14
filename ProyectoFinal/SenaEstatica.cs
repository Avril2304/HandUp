namespace ProyectoFinal
{
    // Señas que se muestran como imagen fija (categorías: abecedario, numeros)
    public class SenaEstatica : Sena
    {
        public override string ObtenerRutaMedio()
        {
            if (!string.IsNullOrEmpty(RutaImagenAbsoluta) && System.IO.File.Exists(RutaImagenAbsoluta))
            {
                return RutaImagenAbsoluta;
            }
            
            throw new AssetNotFoundException($"No se encontró la imagen para la seña estática '{Palabra}'.");
        }
    }
}
