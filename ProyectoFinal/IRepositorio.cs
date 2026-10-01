using System.Collections.Generic;

namespace ProyectoFinal
{
    // Contrato genérico de acceso a datos: cada entidad (Perfil, Seña, ...) tendrá su propio repositorio
    // que implemente estas operaciones sobre la base SQLite.
    public interface IRepositorio<T> where T : class
    {
        IReadOnlyList<T> ObtenerTodos();

        // Lanza una excepción propia si no existe un elemento con ese Id
        T BuscarPorId(int id);

        // Inserta el elemento y lo devuelve con el Id que le asignó la base
        T Agregar(T elemento);

        void Actualizar(T elemento);
    }
}
