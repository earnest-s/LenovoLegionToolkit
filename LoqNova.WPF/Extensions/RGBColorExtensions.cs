using System.Windows.Media;
using LoqNova.Lib;

namespace LoqNova.WPF.Extensions;

public static class RGBColorExtensions
{
    public static Color ToColor(this RGBColor color) => Color.FromRgb(color.R, color.G, color.B);
}
