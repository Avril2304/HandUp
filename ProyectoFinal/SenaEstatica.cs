namespace ProyectoFinal
{
    // Señas que se muestran como imagen fija (categorías: abecedario, numeros)
    public class SenaEstatica : Sena
    {
        public override string ObtenerRutaMedio() => RutaImagenAbsoluta;
    }
}
