using System;

namespace ProyectoFinal
{
    public class ProgresoEventArgs : EventArgs
    {
        public Perfil Perfil { get; }
        public string CampoActualizado { get; }

        public ProgresoEventArgs(Perfil perfil, string campoActualizado)
        {
            Perfil = perfil;
            CampoActualizado = campoActualizado;
        }
    }
}
