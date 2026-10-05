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

        public string Nivel { get; set; } = "Principiante";
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

        // Primera letra del nombre, para el avatar del listado de perfiles (no se guarda en la base)
        public string Inicial => string.IsNullOrWhiteSpace(Nombre) ? "?" : Nombre.Trim()[0].ToString().ToUpper();

        // Accesibilidad: los lectores de pantalla anuncian cada ítem de las listas con este texto
        public override string ToString() => $"{Nombre}, nivel {Nivel}";
    }
}
