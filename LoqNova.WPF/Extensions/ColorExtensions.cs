using System.Windows.Media;
using LoqNova.Lib;

namespace LoqNova.WPF.Extensions;

public static class ColorExtensions
{
    public static RGBColor ToRGBColor(this Color color) => new(color.R, color.G, color.B);
}