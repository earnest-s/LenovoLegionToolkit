// ============================================================================
// StrobeEffect.cs
// 
// ASUS ROG-style strobing/flashing RGB effect.
// Rapid on/off pulses with configurable speed.
// ============================================================================

using System;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;

namespace LenovoLegionToolkit.Lib.Controllers.CustomRGBEffects.Effects;

/// <summary>
/// ASUS ROG-style strobing effect with rapid on/off pulses.
/// Uses selected zone colors with configurable flash speed.
/// </summary>
public class StrobeEffect : ICustomRGBEffect
{
    private readonly int _speed;
    private readonly ZoneColors _zoneColors;

    /// <summary>
    /// Creates a new Strobe effect.
    /// </summary>
    /// <param name="zoneColors">Zone colors to flash.</param>
    /// <param name="speed">Speed 1-4 (1=slowest, 4=fastest). Controls flash rate.</param>
    public StrobeEffect(ZoneColors? zoneColors = null, int speed = 2)
    {
        _speed = Math.Clamp(speed, 1, 4);
        _zoneColors = zoneColors ?? ZoneColors.White;
    }

    public CustomRGBEffectType Type => CustomRGBEffectType.Strobe;
    public string Description => "ASUS ROG-style rapid flashing effect";
    public bool RequiresInputMonitoring => false;
    public bool RequiresSystemAccess => false;

    public async Task RunAsync(CustomRGBEffectController controller, CancellationToken cancellationToken)
    {
        // Flash interval: 200ms at speed 1, 50ms at speed 4
        // This creates a strobe frequency of 2.5Hz to 10Hz
        var flashIntervalMs = 200 / _speed;
        
        var stopwatch = Stopwatch.StartNew();
        var isOn = true;
        var nextToggleMs = 0L;

        while (!cancellationToken.IsCancellationRequested)
        {
            var elapsedMs = stopwatch.ElapsedMilliseconds;

            if (elapsedMs >= nextToggleMs)
            {
                if (isOn)
                {
                    // Flash on - show zone colors
                    await controller.SetColorsAsync(_zoneColors, cancellationToken).ConfigureAwait(false);
                }
                else
                {
                    // Flash off - all black
                    await controller.SetColorsAsync(ZoneColors.Black, cancellationToken).ConfigureAwait(false);
                }

                isOn = !isOn;
                nextToggleMs = elapsedMs + flashIntervalMs;
            }

            // Minimal delay for tight timing
            await Task.Delay(1, cancellationToken).ConfigureAwait(false);
        }
    }
}
