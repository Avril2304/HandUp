namespace ProyectoFinal
{
    // Lo implementan las vistas de sección que tienen varios pasos (ej. Aprender: nivel → categoría → señas).
    // El menú principal la usa para el foco inicial y para la tecla Esc.
    public interface IVistaDeSeccion
    {
        // Pone el foco en el primer control útil de la vista (así el teclado funciona apenas se entra)
        void EnfocarContenido();

        // Esc: vuelve al paso anterior dentro de la sección.
        // Devuelve false si ya está en el primer paso (entonces el menú vuelve al inicio).
        bool VolverUnPaso();
    }
}
