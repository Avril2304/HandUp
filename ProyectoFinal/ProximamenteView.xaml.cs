using System;
using System.Windows;
using System.Windows.Controls;

namespace ProyectoFinal
{
    public partial class ProximamenteView : UserControl
    {
        // La vista no sabe navegar: avisa con un evento y el menú principal decide qué hacer
        public event EventHandler? VolverSolicitado;

        public ProximamenteView(InfoSeccion info)
        {
            InitializeComponent();
            lblTitulo.Text = info.Titulo;
            lblIcono.Text = info.Glifo;
            lblDescripcion.Text = info.Descripcion;
        }

        private void BtnVolver_Click(object sender, RoutedEventArgs e)
        {
            VolverSolicitado?.Invoke(this, EventArgs.Empty);
        }
    }
}
