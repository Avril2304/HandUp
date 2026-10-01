using System;

namespace ProyectoFinal
{
    public class UserNotFoundException : Exception
    {
        public UserNotFoundException(string usuario)
            : base($"No existe un perfil registrado para el usuario '{usuario}'.")
        {
        }

        public UserNotFoundException(int id)
            : base($"No existe un perfil con el Id {id}.")
        {
        }
    }
}
