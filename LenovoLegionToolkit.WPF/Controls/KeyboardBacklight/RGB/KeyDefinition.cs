using System.Windows;
using System.Windows.Media;

namespace LenovoLegionToolkit.WPF.Controls.KeyboardBacklight.RGB;

/// <summary>
/// A single physical key on the LOQ keyboard preview.
/// </summary>
public sealed class KeyDefinition
{
    public string Label { get; }
    public Rect Bounds { get; }
    public int Zone { get; }
    public Color CurrentColor { get; set; } = Colors.Black;

    public KeyDefinition(string label, double x, double y, double w, double h, int zone)
    {
        Label = label;
        Bounds = new Rect(x, y, w, h);
        Zone = zone;
    }
}
