using System;

namespace ProyectoFinal
{
    public class Perfil
    {
        public int Id { get; set; }
        public string Nombre { get; set; } = string.Empty;
        public string Usuario { get; set; } = string.Empty;

        // La contraseña se guarda hasheada (ver PasswordHasher), nunca en texto plano
        public string PasswordHash { get; set; } = string.Empty;
        public string PasswordSalt { get; set; } = string.Empty;

        // Nivel más alto que tiene desbloqueado (1 = Principiante ... 4 = Avanzado). Solo sube aprobando la prueba final.
        private int _nivelDesbloqueado = 1;
        public int NivelDesbloqueado
        {
            get => _nivelDesbloqueado;
            set => _nivelDesbloqueado = Math.Clamp(value, 1, Niveles.Nombres.Count);
        }

        // Nivel que eligió al registrarse (2 a 4) y todavía no validó con la prueba de nivelación; null si no hay ninguna.
        // Mientras está pendiente, el perfil sigue en el nivel desbloqueado (Principiante al registrarse).
        public int? NivelacionPendiente { get; set; }

        // Qué prueba final se ofrece: la del nivel anterior al de la nivelación pendiente, o si no, la del nivel actual
        public int NivelDePruebaFinal => NivelacionPendiente is int objetivo ? objetivo - 1 : NivelDesbloqueado;

        // Nombre del nivel desbloqueado ("Principiante", "Básico"...). Es otra forma de leer y escribir NivelDesbloqueado.
        public string Nivel
        {
            get => Niveles.NombreDe(NivelDesbloqueado);
            set => NivelDesbloqueado = Niveles.NumeroDe(value);
        }
        public int Racha { get; set; } = 0;
        public int MejorRacha { get; set; } = 0;
        public int SenasDominadas { get; set; } = 0;
        public int Puntos { get; set; } = 0;
        public int AciertosTotales { get; set; } = 0;
        public int FallosTotales { get; set; } = 0;
        public DateTime FechaRegistro { get; set; } = DateTime.Now;

        // Racha por días: cuántos días seguidos practicó (respondió al menos una pregunta), su récord y el último día
        public int RachaDias { get; set; } = 0;
        public int MejorRachaDias { get; set; } = 0;
        public DateTime? UltimoDiaPractica { get; set; }

        // Anota que hoy practicó: si ayer también, la racha sigue; si pasó más de un día, vuelve a empezar.
        // Devuelve true si es la primera práctica del día (la racha cambió).
        public bool RegistrarDiaDePractica(DateTime hoy)
        {
            DateTime dia = hoy.Date;
            if (UltimoDiaPractica?.Date == dia)
                return false;

            RachaDias = UltimoDiaPractica?.Date == dia.AddDays(-1) ? RachaDias + 1 : 1;
            MejorRachaDias = Math.Max(MejorRachaDias, RachaDias);
            UltimoDiaPractica = dia;
            return true;
        }

        // La racha que vale hoy: si no practicó ni hoy ni ayer, ya se cortó (aunque en la base siga el número viejo)
        public int RachaDiasVigente(DateTime hoy) =>
            UltimoDiaPractica is DateTime ultimo && (hoy.Date - ultimo.Date).TotalDays <= 1 ? RachaDias : 0;

        // Propiedad calculada para mostrar en UI
        public double Precision => (AciertosTotales + FallosTotales) == 0 
            ? 0 
            : (double)AciertosTotales / (AciertosTotales + FallosTotales);

        // Sin ninguna respuesta ni progreso todavía (Mi perfil muestra una invitación a empezar en vez de ceros)
        public bool SinActividad => AciertosTotales + FallosTotales == 0
            && SenasDominadas == 0 && Puntos == 0 && Racha == 0 && MejorRacha == 0 && MejorRachaDias == 0;

        // Primera letra del nombre, para el avatar del listado de perfiles (no se guarda en la base)
        public string Inicial => string.IsNullOrWhiteSpace(Nombre) ? "?" : Nombre.Trim()[0].ToString().ToUpper();

        // Accesibilidad: los lectores de pantalla anuncian cada ítem de las listas con este texto
        public override string ToString() => $"{Nombre}, nivel {Nivel}";
    }
}
