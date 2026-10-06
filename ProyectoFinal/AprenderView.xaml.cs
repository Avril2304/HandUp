using System;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Threading;

namespace ProyectoFinal
{
    // Sección Aprender. Maneja los pasos: 1) elegir nivel, 2) elegir categoría, 3) recorrer las señas.
    public partial class AprenderView : UserControl, IVistaDeSeccion
    {
        // Pide volver al inicio del menú (desde el primer paso)
        public event EventHandler? VolverSolicitado;

        private readonly Perfil _perfil;
        private ElegirNivelView? _vistaNiveles;
        private NivelAprendizaje? _nivelElegido;

        public AprenderView(Perfil perfil)
        {
            InitializeComponent();
            _perfil = perfil;
            MostrarNiveles();
        }

        // ===== Pasos =====

        private void MostrarNiveles()
        {
            _nivelElegido = null;
            _vistaNiveles ??= CrearVistaNiveles();
            contenidoPaso.Content = _vistaNiveles;

            lblPaso.Text = "Paso 1 de 3 · Elegí tu nivel (teclas 1 a 4)";
            ActualizarBotonVolver("Volver al inicio");
        }

        private ElegirNivelView CrearVistaNiveles()
        {
            var vista = new ElegirNivelView(_perfil.Nivel);
            vista.NivelElegido += (s, nivel) => MostrarCategorias(nivel);
            return vista;
        }

        // Paso 2 (story "elegir una categoría"): por ahora un aviso, se reemplaza en F2
        private void MostrarCategorias(NivelAprendizaje nivel)
        {
            _nivelElegido = nivel;
            contenidoPaso.Content = new TextBlock
            {
                Text = $"Elegiste {nivel.Nombre}. La pantalla de categorías está en construcción.",
                FontSize = 15,
                TextWrapping = TextWrapping.Wrap
            };

            lblPaso.Text = $"Paso 2 de 3 · {nivel.Nombre}: elegí una categoría";
            ActualizarBotonVolver("Volver a los niveles");
            Dispatcher.BeginInvoke(DispatcherPriority.Input, () => btnVolver.Focus());
        }

        private void ActualizarBotonVolver(string texto)
        {
            lblVolver.Text = texto;
            btnVolver.ToolTip = $"{texto} (Esc)";
            AutomationProperties.SetName(btnVolver, texto);
        }

        // ===== IVistaDeSeccion =====

        public void EnfocarContenido()
        {
            if (_nivelElegido is null)
                _vistaNiveles?.EnfocarNivel();
            else
                btnVolver.Focus();
        }

        public bool VolverUnPaso()
        {
            if (_nivelElegido is null)
                return false; // ya está en el primer paso: que el menú vuelva al inicio

            MostrarNiveles();
            EnfocarContenido();
            return true;
        }

        private void BtnVolver_Click(object sender, RoutedEventArgs e)
        {
            if (!VolverUnPaso())
                VolverSolicitado?.Invoke(this, EventArgs.Empty);
        }
    }
}
