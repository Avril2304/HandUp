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

            // Registramos el acierto en ProgresoSenas y actualizamos el total de señas dominadas
            if (senaId > 0)
            {
                int dominadasEnBase = _repositorio.RegistrarResultadoSena(_perfil.Id, senaId, correcto: true);
                _perfil.SenasDominadas = Math.Max(_perfil.SenasDominadas, dominadasEnBase);
                RecalcularNivel();
            }

            GuardarProgreso("Acierto");
        }

        public void RegistrarFallo(int senaId)
        {
            _perfil.FallosTotales++;
            _perfil.Racha = 0; // Se pierde la racha actual

            if (senaId > 0)
            {
                _repositorio.RegistrarResultadoSena(_perfil.Id, senaId, correcto: false);
            }

            GuardarProgreso("Fallo");
        }

        public void MarcarSenaDominada(int senaId)
        {
            if (senaId > 0)
            {
                int dominadasEnBase = _repositorio.RegistrarResultadoSena(_perfil.Id, senaId, correcto: true);
                _perfil.SenasDominadas = Math.Max(_perfil.SenasDominadas, dominadasEnBase);
            }
            else
            {
                _perfil.SenasDominadas++;
            }
            RecalcularNivel();
            
            GuardarProgreso("SenasDominadas");
        }

        // Cambia el nombre visible del perfil (Mi perfil). El menú se entera por ProgresoActualizado y actualiza el saludo.
        public void CambiarNombre(string nuevoNombre)
        {
            string nombre = (nuevoNombre ?? string.Empty).Trim();
            if (nombre.Length == 0)
                throw new InvalidInputException("El nombre no puede quedar vacío.");

            string anterior = _perfil.Nombre;
            _perfil.Nombre = nombre;
            try
            {
                GuardarProgreso("Nombre");
            }
            catch (Exception ex) when (ex is DatabaseException or UserNotFoundException)
            {
                _perfil.Nombre = anterior; // si no se pudo guardar, en pantalla queda el nombre de antes
                throw;
            }
        }

        private void RecalcularNivel()
        {
            _perfil.Nivel = Niveles.Actualizar(_perfil.Nivel, _perfil.SenasDominadas);
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
