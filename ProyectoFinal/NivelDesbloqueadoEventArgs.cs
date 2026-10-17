using System;

namespace ProyectoFinal
{
    // Datos del evento ServicioProgreso.NivelDesbloqueado: quién aprobó y qué nivel se le abrió
    public class NivelDesbloqueadoEventArgs : EventArgs
    {
        public Perfil Perfil { get; }
        public int NivelAprobado { get; }
        public int NivelNuevo { get; }

        public NivelDesbloqueadoEventArgs(Perfil perfil, int nivelAprobado, int nivelNuevo)
        {
            Perfil = perfil;
            NivelAprobado = nivelAprobado;
            NivelNuevo = nivelNuevo;
        }
    }
}
