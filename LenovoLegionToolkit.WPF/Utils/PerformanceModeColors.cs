using System.Windows.Media;
using LenovoLegionToolkit.Lib;

namespace LenovoLegionToolkit.WPF.Utils;

/// <summary>
/// Centralized PowerModeState → accent color mapping used by the
/// dashboard sensor bars.  Kept separate from PowerModeStateExtensions
/// so the exact tints can evolve independently.
/// </summary>
public static class PerformanceModeColors
{
    public static Color GetAccent(PowerModeState mode) => mode switch
    {
        PowerModeState.Quiet => Color.FromRgb(0, 120, 255),       // Blue
        PowerModeState.Balance => Color.FromRgb(240, 240, 240),    // White
        PowerModeState.Performance => Color.FromRgb(255, 60, 60),  // Red
        PowerModeState.GodMode => Color.FromRgb(180, 80, 255),     // Purple (Custom)
        _ => Color.FromRgb(255, 140, 90),                          // Fallback orange
    };
}
