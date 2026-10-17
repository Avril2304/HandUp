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

        // Propiedad calculada para mostrar en UI
        public double Precision => (AciertosTotales + FallosTotales) == 0 
            ? 0 
            : (double)AciertosTotales / (AciertosTotales + FallosTotales);

        // Sin ninguna respuesta ni progreso todavía (Mi perfil muestra una invitación a empezar en vez de ceros)
        public bool SinActividad => AciertosTotales + FallosTotales == 0
            && SenasDominadas == 0 && Puntos == 0 && Racha == 0 && MejorRacha == 0;

        // Primera letra del nombre, para el avatar del listado de perfiles (no se guarda en la base)
        public string Inicial => string.IsNullOrWhiteSpace(Nombre) ? "?" : Nombre.Trim()[0].ToString().ToUpper();

        // Accesibilidad: los lectores de pantalla anuncian cada ítem de las listas con este texto
        public override string ToString() => $"{Nombre}, nivel {Nivel}";
    }
}
