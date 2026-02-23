// ============================================================================
// RainbowWaveEffect.cs
// 
// Rainbow color wave effect across zones.
// Smooth cycling through the color spectrum.
// Inspired by L5P-Keyboard-RGB smooth wave behavior.
// 
// Effect behavior:
// - Cycles through hue values (0-360)
// - Each zone is offset in the hue cycle to create a wave
// - Speed controls the wave speed
// - Direction controls wave movement
// ============================================================================

using System;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;

namespace LoqNova.Lib.Controllers.CustomRGBEffects.Effects;

/// <summary>
/// Rainbow wave effect - smooth color spectrum wave.
/// Direction is forced to RIGHT only.
/// Uses Stopwatch-based timing for consistent speed when backgrounded.
/// </summary>
public class RainbowWaveEffect : ICustomRGBEffect
{
    private readonly int _speed;

    /// <summary>
    /// Creates a new Rainbow Wave effect.
    /// Direction is forced to RIGHT only.
    /// </summary>
    /// <param name="speed">Speed 1-4 (1=slowest, 4=fastest).</param>
    /// <param name="direction">Ignored - direction is forced to Right.</param>
    public RainbowWaveEffect(int speed = 2, EffectDirection direction = EffectDirection.Right)
    {
        _speed = Math.Clamp(speed, 1, 4);
        // direction parameter is ignored - always Right
    }

    public CustomRGBEffectType Type => CustomRGBEffectType.RainbowWave;
    public string Description => "Rainbow color wave (rightward)";
    public bool RequiresInputMonitoring => false;
    public bool RequiresSystemAccess => false;

    public async Task RunAsync(CustomRGBEffectController controller, CancellationToken cancellationToken)
    {
        // Hue degrees per second (speed 1 = 60°/s, speed 4 = 240°/s)
        var hueDegreesPerSecond = _speed * 60f;

        // Zone offsets for wave effect (90 degrees apart)
        // Physical LEFT→RIGHT: Zone 0 (left) trails, Zone 3 (right) leads
        // Zone4 gets base hue first, wave propagates leftward in hue space = rightward visually
        float[] zoneOffsets = [270, 180, 90, 0];

        var stopwatch = Stopwatch.StartNew();

        while (!cancellationToken.IsCancellationRequested)
        {
            var elapsedMs = stopwatch.ElapsedMilliseconds;
            var elapsedSeconds = elapsedMs / 1000f;

            // Calculate hue based on elapsed time (delta-time based animation)
            var hueOffset = (elapsedSeconds * hueDegreesPerSecond) % 360f;

            var colors = new ZoneColors
            {
                Zone1 = HsvToRgb((hueOffset + zoneOffsets[0]) % 360, 1f, 1f),
                Zone2 = HsvToRgb((hueOffset + zoneOffsets[1]) % 360, 1f, 1f),
                Zone3 = HsvToRgb((hueOffset + zoneOffsets[2]) % 360, 1f, 1f),
                Zone4 = HsvToRgb((hueOffset + zoneOffsets[3]) % 360, 1f, 1f)
            };

            await controller.SetColorsAsync(colors, cancellationToken).ConfigureAwait(false);

            // Use Task.Delay only as a yield mechanism (1ms minimum)
            // Actual timing is controlled by Stopwatch
            await Task.Delay(1, cancellationToken).ConfigureAwait(false);
        }
    }

    /// <summary>
    /// Converts HSV color to RGB.
    /// </summary>
    /// <param name="hue">Hue in degrees (0-360)</param>
    /// <param name="saturation">Saturation (0-1)</param>
    /// <param name="value">Value/Brightness (0-1)</param>
    private static RGBColor HsvToRgb(float hue, float saturation, float value)
    {
        var hi = (int)(hue / 60) % 6;
        var f = hue / 60 - (int)(hue / 60);
        var p = value * (1 - saturation);
        var q = value * (1 - f * saturation);
        var t = value * (1 - (1 - f) * saturation);

        float r, g, b;
        switch (hi)
        {
            case 0: r = value; g = t; b = p; break;
            case 1: r = q; g = value; b = p; break;
            case 2: r = p; g = value; b = t; break;
            case 3: r = p; g = q; b = value; break;
            case 4: r = t; g = p; b = value; break;
            default: r = value; g = p; b = q; break;
        }

        return new RGBColor(
            (byte)(r * 255),
            (byte)(g * 255),
            (byte)(b * 255)
        );
    }
}
