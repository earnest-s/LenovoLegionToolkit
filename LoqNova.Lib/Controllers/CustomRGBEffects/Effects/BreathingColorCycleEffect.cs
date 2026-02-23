// ============================================================================
// BreathingColorCycleEffect.cs
// 
// Breathing effect with color change on every breath cycle.
// Each full inhale → exhale cycle switches to the next color.
// ============================================================================

using System;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;

namespace LoqNova.Lib.Controllers.CustomRGBEffects.Effects;

/// <summary>
/// Breathing effect that cycles colors on each breath cycle.
/// Smooth easing with no abrupt transitions.
/// </summary>
public class BreathingColorCycleEffect : ICustomRGBEffect
{
    // Rainbow colors to cycle through
    private static readonly RGBColor[] CycleColors =
    [
        new(255, 0, 0),     // Red
        new(255, 127, 0),   // Orange
        new(255, 255, 0),   // Yellow
        new(0, 255, 0),     // Green
        new(0, 255, 255),   // Cyan
        new(0, 0, 255),     // Blue
        new(127, 0, 255),   // Purple
        new(255, 0, 255)    // Magenta
    ];

    private readonly int _speed;
    private readonly ZoneColors _zoneColors;

    /// <summary>
    /// Creates a new Breathing Color Cycle effect.
    /// </summary>
    /// <param name="zoneColors">Base zone colors (used for initial state).</param>
    /// <param name="speed">Speed 1-4 (1=slowest, 4=fastest). Affects breath cycle duration.</param>
    public BreathingColorCycleEffect(ZoneColors? zoneColors = null, int speed = 2)
    {
        _speed = Math.Clamp(speed, 1, 4);
        _zoneColors = zoneColors ?? ZoneColors.White;
    }

    public CustomRGBEffectType Type => CustomRGBEffectType.BreathingColorCycle;
    public string Description => "Breathing effect with color change on every cycle";
    public bool RequiresInputMonitoring => false;
    public bool RequiresSystemAccess => false;

    public async Task RunAsync(CustomRGBEffectController controller, CancellationToken cancellationToken)
    {
        // Breath cycle duration: 4000ms at speed 1, 1000ms at speed 4
        var cycleDurationMs = 4000 / _speed;
        var halfCycleMs = cycleDurationMs / 2;

        var stopwatch = Stopwatch.StartNew();
        var colorIndex = 0;

        while (!cancellationToken.IsCancellationRequested)
        {
            var cycleTimeMs = stopwatch.ElapsedMilliseconds % cycleDurationMs;
            
            // Calculate brightness using sine wave for smooth easing
            // 0 -> halfCycle: fade in (0 to 1)
            // halfCycle -> cycle: fade out (1 to 0)
            double brightness;
            if (cycleTimeMs < halfCycleMs)
            {
                // Fade in: use sine ease-in
                var progress = (double)cycleTimeMs / halfCycleMs;
                brightness = Math.Sin(progress * Math.PI / 2); // 0 to 1
            }
            else
            {
                // Fade out: use sine ease-out
                var progress = (double)(cycleTimeMs - halfCycleMs) / halfCycleMs;
                brightness = Math.Cos(progress * Math.PI / 2); // 1 to 0
            }

            // Check if we've completed a full cycle (brightness near 0)
            var previousCycle = (stopwatch.ElapsedMilliseconds - 16) / cycleDurationMs;
            var currentCycle = stopwatch.ElapsedMilliseconds / cycleDurationMs;
            if (currentCycle > previousCycle)
            {
                // Move to next color
                colorIndex = (colorIndex + 1) % CycleColors.Length;
            }

            var currentColor = CycleColors[colorIndex];
            
            // Apply brightness to color
            var r = (byte)(currentColor.R * brightness);
            var g = (byte)(currentColor.G * brightness);
            var b = (byte)(currentColor.B * brightness);
            var dimmedColor = new RGBColor(r, g, b);

            // Set all zones to the same color (global breathing)
            var colors = new ZoneColors(dimmedColor);
            await controller.SetColorsAsync(colors, cancellationToken).ConfigureAwait(false);

            // ~60 FPS update rate
            await Task.Delay(16, cancellationToken).ConfigureAwait(false);
        }
    }
}
