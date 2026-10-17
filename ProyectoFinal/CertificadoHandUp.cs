using System;

namespace ProyectoFinal
{
    // Datos del certificado que se obtiene al aprobar el examen final de HandUp (la prueba final de Avanzado)
    public record CertificadoHandUp(string Nombre, int Porcentaje, DateTime Fecha, int PerfilId)
    {
        // Número del certificado: fecha de aprobación + perfil ("HU-20261017-0004")
        public string Codigo => $"HU-{Fecha:yyyyMMdd}-{PerfilId:D4}";
    }
}
