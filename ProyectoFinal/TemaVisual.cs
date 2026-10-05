using System;
using System.Windows;

namespace ProyectoFinal
{
    // Utilitario que la UI llama al cargar las preferencias para aplicar el tema visual.
    // Solo provee los diccionarios de recursos; NO modifica ventanas directamente.
    public static class TemaVisual
    {
        // Devuelve un ResourceDictionary con los brushes de alto contraste
        public static ResourceDictionary ObtenerDiccionarioAltoContraste()
        {
            var dict = new ResourceDictionary();
            // Fondo negro, texto blanco, acento amarillo brillante
            dict["BrushBackground"] = BrushDesdeHex("#000000");
            dict["BrushCard"] = BrushDesdeHex("#1A1A1A");
            dict["BrushBorder"] = BrushDesdeHex("#FFFFFF");
            dict["BrushInk"] = BrushDesdeHex("#FFFFFF");
            dict["BrushMuted"] = BrushDesdeHex("#CCCCCC");
            dict["BrushAccent"] = BrushDesdeHex("#FFD700");
            dict["BrushAccentDark"] = BrushDesdeHex("#FFC000");
            dict["BrushAccentSoft"] = BrushDesdeHex("#333300");
            dict["BrushError"] = BrushDesdeHex("#FF4444");
            dict["BrushErrorBg"] = BrushDesdeHex("#330000");
            dict["BrushSuccess"] = BrushDesdeHex("#44FF44");
            dict["BrushSuccessBg"] = BrushDesdeHex("#003300");
            return dict;
        }

        // Devuelve el diccionario normal (para revertir el alto contraste)
        public static ResourceDictionary ObtenerDiccionarioNormal()
        {
            var dict = new ResourceDictionary();
            dict["BrushBackground"] = BrushDesdeHex("#FAF8F4");
            dict["BrushCard"] = BrushDesdeHex("#FFFFFF");
            dict["BrushBorder"] = BrushDesdeHex("#E7E2D6");
            dict["BrushInk"] = BrushDesdeHex("#14312B");
            dict["BrushMuted"] = BrushDesdeHex("#6B7B76");
            dict["BrushAccent"] = BrushDesdeHex("#1F7A6C");
            dict["BrushAccentDark"] = BrushDesdeHex("#175C52");
            dict["BrushAccentSoft"] = BrushDesdeHex("#EAF4F1");
            dict["BrushError"] = BrushDesdeHex("#DC2626");
            dict["BrushErrorBg"] = BrushDesdeHex("#FEE2E2");
            dict["BrushSuccess"] = BrushDesdeHex("#15803D");
            dict["BrushSuccessBg"] = BrushDesdeHex("#DCFCE7");
            return dict;
        }

        private static System.Windows.Media.SolidColorBrush BrushDesdeHex(string hex)
        {
            var color = (System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString(hex);
            return new System.Windows.Media.SolidColorBrush(color);
        }
    }
}
