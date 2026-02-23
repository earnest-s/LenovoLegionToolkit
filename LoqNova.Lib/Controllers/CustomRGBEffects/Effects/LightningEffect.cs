// ============================================================================
// LightningEffect.cs
// 
// Random lightning flash effect on keyboard zones.
// Ported from L5P-Keyboard-RGB lightning.rs
// 
// Original Rust source: app/src/manager/effects/lightning.rs
// 
// Effect behavior:
// - Randomly selects a zone
// - Flashes that zone with the zone's color
// - Fades back to black with random number of steps
// - Random sleep between flashes (100-2000ms)
// ============================================================================

using System;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;

namespace LoqNova.Lib.Controllers.CustomRGBEffects.Effects;

/// <summary>
/// Lightning effect - random lightning flashes on zones.
/// Port of Rust lightning.rs effect.
/// Uses Stopwatch-based timing for consistent speed when backgrounded.
/// </summary>
public class LightningEffect : ICustomRGBEffect
{
    private readonly Random _random = new();
    private readonly int _speed;
    private readonly ZoneColors _colors;

    /// <summary>
    /// Creates a new Lightning effect.
    /// </summary>
    /// <param name="colors">The zone colors to flash.</param>
    /// <param name="speed">Speed 1-4 (1=slowest, 4=fastest).</param>
    public LightningEffect(ZoneColors colors, int speed = 2)
    {
        _colors = colors;
        _speed = Math.Clamp(speed, 1, 4);
    }

    public CustomRGBEffectType Type => CustomRGBEffectType.Lightning;
    public string Description => "Random lightning flashes";
    public bool RequiresInputMonitoring => false;
    public bool RequiresSystemAccess => false;

    public async Task RunAsync(CustomRGBEffectController controller, CancellationToken cancellationToken)
    {
        var stopwatch = Stopwatch.StartNew();
        var nextFlashMs = 0L;

        while (!cancellationToken.IsCancellationRequested)
        {
            var elapsedMs = stopwatch.ElapsedMilliseconds;

            if (elapsedMs >= nextFlashMs)
            {
                // From Rust:
                // let zone_index = rng.random_range(0..4);
                // let steps = rng.random_range(50..=200);
                var zoneIndex = _random.Next(4);
                var steps = _random.Next(50, 201);

                // From Rust:
                // let mut arr = [0; 12];
                // let zone_start = zone_index * 3;
                // arr[zone_start] = profile_array[zone_start];
                // arr[zone_start + 1] = profile_array[zone_start + 1];
                // arr[zone_start + 2] = profile_array[zone_start + 2];
                var flashColors = ZoneColors.Black.WithZone(zoneIndex, _colors.GetZone(zoneIndex));

                // Flash on
                // From Rust: manager.keyboard.set_colors_to(&arr).unwrap();
                await controller.SetColorsAsync(flashColors, cancellationToken).ConfigureAwait(false);

                // Fade back to black
                // From Rust: manager.keyboard.transition_colors_to(&[0; 12], steps / p.speed, 5).unwrap();
                await controller.TransitionColorsAsync(
                    ZoneColors.Black,
                    steps / _speed,
                    5,
                    cancellationToken).ConfigureAwait(false);

                // Random sleep (100-2000ms based on elapsed time)
                // From Rust: let sleep_time = rng.random_range(100..=2000);
                var sleepTime = _random.Next(100, 2001);
                nextFlashMs = stopwatch.ElapsedMilliseconds + sleepTime;
            }

            // Use Task.Delay only as a yield mechanism (1ms minimum)
            await Task.Delay(1, cancellationToken).ConfigureAwait(false);
        }
    }
}
