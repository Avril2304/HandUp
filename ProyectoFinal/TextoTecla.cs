using System.Windows.Input;

namespace ProyectoFinal
{
    // Convierte una tecla (enum Key de WPF) en un texto que entiende cualquier persona.
    // Sirve para mostrar los atajos en Configuración y en los juegos ("Presioná A para responder").
    public static class TextoTecla
    {
        public static string Nombre(Key tecla) => tecla switch
        {
            >= Key.D0 and <= Key.D9 => ((int)(tecla - Key.D0)).ToString(),
            >= Key.NumPad0 and <= Key.NumPad9 => $"Num {(int)(tecla - Key.NumPad0)}",
            Key.Return => "Enter",
            Key.Space => "Espacio",
            Key.Back => "Retroceso",
            Key.Delete => "Supr",
            Key.Insert => "Insert",
            Key.Home => "Inicio",
            Key.End => "Fin",
            Key.PageUp => "Re Pág",
            Key.PageDown => "Av Pág",
            Key.Left => "←",
            Key.Right => "→",
            Key.Up => "↑",
            Key.Down => "↓",
            Key.Add => "Num +",
            Key.Subtract => "Num -",
            Key.Multiply => "Num *",
            Key.Divide => "Num /",
            Key.Decimal => "Num .",
            Key.OemPlus => "+",
            Key.OemMinus => "-",
            Key.OemComma => ",",
            Key.OemPeriod => ".",
            _ => tecla.ToString()
        };
    }
}
