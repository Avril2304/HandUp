using System;

namespace ProyectoFinal
{
    // Servicio central que gestiona el progreso del usuario (y su cuenta, desde Mi perfil) y notifica cambios a la UI
    public class ServicioProgreso
    {
        private readonly Perfil _perfil;
        private readonly RepositorioPerfiles _repositorio;

        public event EventHandler<ProgresoEventArgs>? ProgresoActualizado;

        // Se dispara cuando el perfil aprueba la prueba final de su nivel y se le abre el siguiente (lo escucha el menú)
        public event EventHandler<NivelDesbloqueadoEventArgs>? NivelDesbloqueado;

        // Después de eliminar el perfil ya no hay nada que guardar (ej. al cerrar el menú)
        public bool PerfilEliminado { get; private set; }

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

            // Registramos el acierto en ProgresoSenas y actualizamos el total de señas dominadas.
            // El nivel no cambia por dominar señas: solo sube al aprobar la prueba final.
            if (senaId > 0)
            {
                int dominadasEnBase = _repositorio.RegistrarResultadoSena(_perfil.Id, senaId, correcto: true);
                _perfil.SenasDominadas = Math.Max(_perfil.SenasDominadas, dominadasEnBase);
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

            GuardarProgreso("SenasDominadas");
        }

        // Registra el resultado de la prueba final de un nivel. Guarda el mejor porcentaje (uno peor no lo pisa) y,
        // si aprobó la prueba de su nivel actual, le desbloquea el siguiente y dispara NivelDesbloqueado.
        public ResultadoPruebaFinal RegistrarPruebaFinal(int nivel, int aciertos, int total)
        {
            int porcentaje = PruebaFinal.Porcentaje(aciertos, total);
            int? mejorAnterior = _repositorio.ObtenerMejorPorcentaje(_perfil.Id, nivel);
            _repositorio.GuardarResultadoPrueba(_perfil.Id, nivel, porcentaje);

            bool desbloquea = PruebaFinal.Aprueba(porcentaje)
                              && nivel == _perfil.NivelDesbloqueado
                              && nivel < Niveles.Nombres.Count;
            if (desbloquea)
            {
                _perfil.NivelDesbloqueado = nivel + 1;
                GuardarProgreso("Nivel");
                OnNivelDesbloqueado(nivel);
            }

            return new ResultadoPruebaFinal(nivel, aciertos, total, porcentaje, mejorAnterior, desbloquea);
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

        // Para avisar enseguida si la contraseña actual está mal (antes de apretar Guardar)
        public bool VerificarPassword(string password) =>
            PasswordHasher.Verificar(password ?? string.Empty, _perfil.PasswordHash, _perfil.PasswordSalt);

        // Cambiar la contraseña: pide la actual, valida la nueva con PoliticaPassword y la guarda hasheada.
        // Cualquier dato incorrecto lanza InvalidInputException con un mensaje para mostrar.
        public void CambiarPassword(string actual, string nueva, string confirmacion)
        {
            if (!PasswordHasher.Verificar(actual ?? string.Empty, _perfil.PasswordHash, _perfil.PasswordSalt))
                throw new InvalidInputException("La contraseña actual no es correcta.");

            if (nueva != confirmacion)
                throw new InvalidInputException("La contraseña nueva y su confirmación no coinciden.");

            PoliticaPassword.Validar(nueva);

            if (PasswordHasher.Verificar(nueva, _perfil.PasswordHash, _perfil.PasswordSalt))
                throw new InvalidInputException("La contraseña nueva tiene que ser distinta de la actual.");

            (string hashAnterior, string salAnterior) = (_perfil.PasswordHash, _perfil.PasswordSalt);
            (_perfil.PasswordHash, _perfil.PasswordSalt) = PasswordHasher.Hashear(nueva);
            try
            {
                GuardarProgreso("Contraseña");
            }
            catch (Exception ex) when (ex is DatabaseException or UserNotFoundException)
            {
                (_perfil.PasswordHash, _perfil.PasswordSalt) = (hashAnterior, salAnterior);
                throw;
            }
        }

        // Vuelve a Principiante y pone en cero las pruebas finales, las señas dominadas, los puntos, los aciertos, los fallos y las rachas.
        // No toca el usuario, la contraseña ni las preferencias.
        public void ReiniciarProgreso()
        {
            _repositorio.BorrarProgresoSenas(_perfil.Id);

            _perfil.NivelDesbloqueado = 1;
            _perfil.SenasDominadas = 0;
            _perfil.Puntos = 0;
            _perfil.AciertosTotales = 0;
            _perfil.FallosTotales = 0;
            _perfil.Racha = 0;
            _perfil.MejorRacha = 0;

            GuardarProgreso("Reinicio");
        }

        // Elimina el perfil (con su progreso y sus preferencias) si la contraseña es correcta
        public void EliminarPerfil(string password)
        {
            if (!PasswordHasher.Verificar(password ?? string.Empty, _perfil.PasswordHash, _perfil.PasswordSalt))
                throw new InvalidInputException("La contraseña no es correcta.");

            _repositorio.Eliminar(_perfil.Id);
            PerfilEliminado = true;
        }

        // Guarda automáticamente en la base y notifica a la UI
        public void GuardarProgreso(string campoContexto = "General")
        {
            if (PerfilEliminado)
                return;

            _repositorio.Actualizar(_perfil);
            OnProgresoActualizado(campoContexto);
        }

        protected virtual void OnNivelDesbloqueado(int nivelAprobado)
        {
            NivelDesbloqueado?.Invoke(this, new NivelDesbloqueadoEventArgs(_perfil, nivelAprobado, _perfil.NivelDesbloqueado));
        }

        protected virtual void OnProgresoActualizado(string campoActualizado)
        {
            ProgresoActualizado?.Invoke(this, new ProgresoEventArgs(_perfil, campoActualizado));
        }
    }
}
