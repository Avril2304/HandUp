using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows.Threading;

namespace ProyectoFinal
{
    // Muestra una lista de señas una tras otra en un visor (como el Traductor): las que tienen video pasan a la siguiente
    // cuando el video termina (evento VideoTerminado del visor), y las imágenes (letras del deletreo) con un DispatcherTimer.
    // Lo usa la prueba final para las preguntas de frases.
    public class ReproductorSecuencia
    {
        private const double SegundosPorImagen = 1.2;
        private const double SegundosMaximosPorVideo = 15; // respaldo, por si un video nunca avisa que terminó

        // Se mostró la seña indicada (índice desde 0)
        public event EventHandler<int>? PasoMostrado;

        // Se mostró la última seña y terminó
        public event EventHandler? Terminado;

        private readonly VisorSena _visor;
        private readonly DispatcherTimer _timer = new();
        private IReadOnlyList<Sena> _pasos = Array.Empty<Sena>();
        private int _actual = -1;

        public bool Reproduciendo { get; private set; }
        public bool Pausado { get; private set; }
        public int Cantidad => _pasos.Count;

        public ReproductorSecuencia(VisorSena visor)
        {
            _visor = visor ?? throw new ArgumentNullException(nameof(visor));
            _visor.VideoTerminado += (s, e) =>
            {
                if (Reproduciendo && !Pausado)
                    Avanzar();
            };
            _timer.Tick += (s, e) => Avanzar();
        }

        public void Reproducir(IEnumerable<Sena> pasos)
        {
            _pasos = pasos.ToList();
            Repetir();
        }

        // Vuelve a empezar desde la primera seña
        public void Repetir()
        {
            if (_pasos.Count == 0)
                return;
            Pausado = false;
            Mostrar(0);
        }

        public void AlternarPausa()
        {
            if (!Reproduciendo)
                return;

            Pausado = !Pausado;
            if (Pausado)
                _timer.Stop();
            else
                _timer.Start();

            // El video se pausa y se reanuda junto con la secuencia
            if (_visor.MuestraVideo && _visor.EstaPausado != Pausado)
                _visor.AlternarPausa();
        }

        public void Detener()
        {
            _timer.Stop();
            Reproduciendo = false;
            Pausado = false;
        }

        private void Mostrar(int indice)
        {
            _timer.Stop();
            _actual = indice;
            Reproduciendo = true;

            // Si falta el archivo, el visor muestra su aviso y la secuencia sigue con el timer
            _visor.Mostrar(_pasos[indice]);
            _timer.Interval = TimeSpan.FromSeconds(_visor.MuestraVideo ? SegundosMaximosPorVideo : SegundosPorImagen);
            _timer.Start();

            PasoMostrado?.Invoke(this, indice);
        }

        private void Avanzar()
        {
            if (!Reproduciendo)
                return;

            if (_actual < _pasos.Count - 1)
            {
                Mostrar(_actual + 1);
                return;
            }

            // Terminó: la última seña queda en pantalla (si es un video, sigue en loop)
            _timer.Stop();
            Reproduciendo = false;
            Terminado?.Invoke(this, EventArgs.Empty);
        }
    }
}
