using System;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Automation;

namespace ProyectoFinal
{
    // Felicitaciones por completar HandUp, con la vista previa del certificado y el botón para descargarlo
    public partial class FelicitacionesWindow : Window
    {
        private readonly CertificadoHandUp _certificado;
        private string? _guardadoEn;

        private FelicitacionesWindow(CertificadoHandUp certificado)
        {
            InitializeComponent();
            TamanoTexto.AplicarA(this);
            Width *= TamanoTexto.Escala; // el alto se acomoda solo (SizeToContent)
            _certificado = certificado;

            string primerNombre = certificado.Nombre.Split(' ', StringSplitOptions.RemoveEmptyEntries) is { Length: > 0 } partes
                ? partes[0]
                : certificado.Nombre;
            lblTitulo.Text = $"¡Felicitaciones, {primerNombre}!";
            lblDetalle.Text = $"Aprobaste el examen final con {certificado.Porcentaje}% y ya conocés las señas de los cuatro niveles " +
                              "de HandUp. Descargá tu certificado para guardarlo o imprimirlo.";

            imgCertificado.Source = CertificadoExportador.Renderizar(certificado, escala: 1);
            AutomationProperties.SetName(imgCertificado,
                $"Vista previa del certificado de finalización de {certificado.Nombre}, número {certificado.Codigo}");
        }

        public static void Mostrar(Window? duenio, CertificadoHandUp certificado)
        {
            var ventana = new FelicitacionesWindow(certificado) { Owner = duenio };
            ventana.ShowDialog();
        }

        // El foco arranca en Descargar: es lo que se quiere hacer
        private void Window_Loaded(object sender, RoutedEventArgs e) => btnDescargar.Focus();

        private void BtnDescargar_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                string? ruta = CertificadoExportador.GuardarConDialogo(this, _certificado);
                if (ruta is null)
                    return; // canceló

                _guardadoEn = ruta;
                MostrarEstado($"✓ Certificado guardado en {ruta}", ok: true);
                btnAbrir.IsEnabled = true;
                btnAbrir.Focus();
            }
            catch (ExportacionException ex)
            {
                MostrarEstado($"✗ {ex.Message}", ok: false);
            }
        }

        // Abre el certificado guardado con el programa que tenga la compu para PDF o imágenes
        private void BtnAbrir_Click(object sender, RoutedEventArgs e)
        {
            if (_guardadoEn is null || !File.Exists(_guardadoEn))
                return;
            try
            {
                Process.Start(new ProcessStartInfo(_guardadoEn) { UseShellExecute = true });
            }
            catch (Win32Exception)
            {
                MostrarEstado("✗ No hay ningún programa para abrir ese archivo en esta compu.", ok: false);
            }
        }

        private void MostrarEstado(string mensaje, bool ok)
        {
            lblEstado.Text = mensaje;
            lblEstado.SetResourceReference(System.Windows.Controls.TextBlock.ForegroundProperty, ok ? "BrushSuccess" : "BrushError");
            lblEstado.Visibility = Visibility.Visible;
        }
    }
}
