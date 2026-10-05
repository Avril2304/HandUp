namespace ProyectoFinal
{
    // Señas que se reproducen como video .mp4 (todas las categorías excepto abecedario y numeros)
    public class SenaAnimada : Sena
    {
        // Si tiene video, devuelve el video; si no (caso borde), cae a la imagen estática
        public override string ObtenerRutaMedio() => RutaVideoAbsoluta ?? RutaImagenAbsoluta;
    }
}
