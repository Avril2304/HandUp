using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Threading;

namespace ProyectoFinal
{
    // Utilidades para mover el foco del teclado en listas de tarjetas (ItemsControl con botones)
    public static class AyudaFoco
    {
        // Enfoca el botón de la tarjeta número "indice". Espera a que WPF dibuje la lista
        // (un control recién mostrado todavía no se puede enfocar).
        public static void EnfocarTarjeta(ItemsControl lista, int indice)
        {
            lista.Dispatcher.BeginInvoke(DispatcherPriority.Input, () =>
            {
                if (lista.ItemContainerGenerator.ContainerFromIndex(indice) is DependencyObject contenedor
                    && BuscarBoton(contenedor) is Button boton)
                    boton.Focus();
            });
        }

        // Primer botón dentro de un elemento (la tarjeta está dentro del contenedor que genera el ItemsControl)
        private static Button? BuscarBoton(DependencyObject padre)
        {
            for (int i = 0; i < VisualTreeHelper.GetChildrenCount(padre); i++)
            {
                var hijo = VisualTreeHelper.GetChild(padre, i);
                if (hijo is Button boton)
                    return boton;
                if (BuscarBoton(hijo) is Button encontrado)
                    return encontrado;
            }
            return null;
        }
    }
}
