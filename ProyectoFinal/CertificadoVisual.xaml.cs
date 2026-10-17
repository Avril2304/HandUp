using System.Globalization;
using System.Windows.Controls;

namespace ProyectoFinal
{
    // El dibujo del certificado. No se muestra en pantalla tal cual: CertificadoExportador lo dibuja en una imagen
    // (para la vista previa y para guardarlo como PDF o PNG).
    public partial class CertificadoVisual : UserControl
    {
        private static readonly CultureInfo Espanol = new("es-AR");

        public CertificadoVisual(CertificadoHandUp certificado)
        {
            InitializeComponent();
            lblNombre.Text = certificado.Nombre;
            lblDetalle.Text = "por haber completado los cuatro niveles de HandUp (Principiante, Básico, Intermedio y Avanzado) " +
                              $"y aprobado el examen final con un {certificado.Porcentaje}%.";
            lblFecha.Text = certificado.Fecha.ToString("d 'de' MMMM 'de' yyyy", Espanol);
            lblCodigo.Text = certificado.Codigo;
        }
    }
}
