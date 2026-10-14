using System;

namespace ProyectoFinal
{
    // Servicio central que gestiona el progreso del usuario y notifica cambios a la UI
    public class ServicioProgreso
    {
        private readonly Perfil _perfil;
        private readonly RepositorioPerfiles _repositorio;

        public event EventHandler<ProgresoEventArgs>? ProgresoActualizado;

        public ServicioProgreso(Perfil perfil, RepositorioPerfiles repositorio)
        {
            _perfil = perfil ?? throw new ArgumentNullException(nameof(perfil));
            _repositorio = repositorio ?? throw new ArgumentNullException(nameof(repositorio));
        }

        public void RegistrarAcierto(int senaId, int puntosGanados)
        {
            _perfil.AciertosTotales++;
            _perfil.Puntos += puntosGanados;
            
            _perfil.Racha++;
            if (_perfil.Racha > _perfil.MejorRacha)
            {
                _perfil.MejorRacha = _perfil.Racha;
            }

            // TODO: Podríamos marcar la seña como dominada si alcanza X aciertos consecutivos,
            // registrándola en ProgresoSenas (Story 19).

            GuardarProgreso("Acierto");
        }

        public void RegistrarFallo(int senaId)
        {
            _perfil.FallosTotales++;
            _perfil.Racha = 0; // Se pierde la racha actual

            GuardarProgreso("Fallo");
        }

        public void MarcarSenaDominada(int senaId)
        {
            // En una implementación real (Story 19) acá se guardaría en ProgresoSenas
            _perfil.SenasDominadas++;
            RecalcularNivel();
            
            GuardarProgreso("SenasDominadas");
        }

        private void RecalcularNivel()
        {
            _perfil.Nivel = Niveles.SegunSenasDominadas(_perfil.SenasDominadas);
        }

        // Guarda automáticamente en la base y notifica a la UI
        public void GuardarProgreso(string campoContexto = "General")
        {
            _repositorio.Actualizar(_perfil);
            OnProgresoActualizado(campoContexto);
        }

        protected virtual void OnProgresoActualizado(string campoActualizado)
        {
            ProgresoActualizado?.Invoke(this, new ProgresoEventArgs(_perfil, campoActualizado));
        }
    }
}
