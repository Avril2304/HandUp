using System;
using System.Collections.Generic;
using System.Linq;

namespace ProyectoFinal
{
    // Reglas y estado de una partida del Desafío (sin nada de pantalla: la UI está en PartidaDesafioView).
    // Contrarreloj con vidas: termina al quedarse sin vidas o sin tiempo.
    public class PartidaDesafio
    {
        public const int SegundosIniciales = 90;
        public const int VidasIniciales = 3;
        public const int PuntosPorAcierto = 10;
        public const int BonusRacha = 5;            // extra por acierto si venías con una racha de RachaParaBonus o más
        public const int RachaParaBonus = 3;

        private readonly ColaRepeticionEspaciada<Sena> _mazo;

        public int SegundosRestantes { get; private set; } = SegundosIniciales;
        public int Vidas { get; private set; } = VidasIniciales;
        public int Puntaje { get; private set; }
        public int Racha { get; private set; }
        public int MejorRacha { get; private set; }
        public int Aciertos { get; private set; }
        public int Fallos { get; private set; }

        public bool SinVidas => Vidas <= 0;
        public bool SinTiempo => SegundosRestantes <= 0;
        public bool Terminada => SinVidas || SinTiempo;

        // historial: aciertos y fallos guardados de cada seña (ProgresoSenas); las que más se fallaron salen más seguido
        public PartidaDesafio(IEnumerable<Sena> senas, IReadOnlyDictionary<int, (int Aciertos, int Fallos)> historial, Random random)
        {
            _mazo = new ColaRepeticionEspaciada<Sena>(senas, s => PesoSegunHistorial(s, historial), random);
        }

        // Peso 1 si nunca la falló; sube con los fallos que superan a los aciertos (máximo 4)
        private static int PesoSegunHistorial(Sena sena, IReadOnlyDictionary<int, (int Aciertos, int Fallos)> historial)
        {
            if (!historial.TryGetValue(sena.Id, out var h))
                return 1;
            return Math.Clamp(1 + h.Fallos - h.Aciertos / 2, 1, 4);
        }

        public int CantidadSenas => _mazo.Cantidad;

        // Lanza MazoVacioException si no hay señas
        public Sena SiguienteSena() => _mazo.Siguiente();

        // Devuelve los puntos que sumó el acierto
        public int RegistrarAcierto(Sena sena)
        {
            int puntos = PuntosPorAcierto + (Racha >= RachaParaBonus ? BonusRacha : 0);
            Puntaje += puntos;
            Aciertos++;
            Racha++;
            MejorRacha = Math.Max(MejorRacha, Racha);
            _mazo.Acerto(sena);
            return puntos;
        }

        public void RegistrarFallo(Sena sena)
        {
            Fallos++;
            Racha = 0;
            Vidas = Math.Max(0, Vidas - 1);
            _mazo.Fallo(sena);
        }

        // Lo llama el DispatcherTimer de la pantalla una vez por segundo
        public void PasarUnSegundo()
        {
            if (SegundosRestantes > 0)
                SegundosRestantes--;
        }
    }
}
