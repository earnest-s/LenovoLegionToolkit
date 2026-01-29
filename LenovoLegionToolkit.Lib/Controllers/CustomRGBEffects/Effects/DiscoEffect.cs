// ============================================================================
// DiscoEffect.cs
// 
// Random zone color cycling effect.
// Ported from L5P-Keyboard-RGB disco.rs
// 
// Original Rust source: app/src/manager/effects/disco.rs
// 
// Effect behavior:
// - Randomly selects one of 6 rainbow colors
// - Randomly selects a zone (0-3)
// - Sets that zone to the selected color
// - Waits for a speed-dependent delay
// - Timing: 2000ms / (speed * 4) between changes
// ============================================================================

using System;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;

namespace LenovoLegionToolkit.Lib.Controllers.CustomRGBEffects.Effects;

/// <summary>
/// Disco effect - rapidly cycles random colors on random zones.
/// Port of Rust disco.rs effect.
/// Uses Stopwatch-based timing for consistent speed when backgrounded.
/// </summary>
public class DiscoEffect : ICustomRGBEffect
{
    // Rainbow colors from Rust disco.rs
    // let colors = [[255, 0, 0], [255, 255, 0], [0, 255, 0], [0, 255, 255], [0, 0, 255], [255, 0, 255]];
    private static readonly RGBColor[] DiscoColors =
    [
        new(255, 0, 0),     // Red
        new(255, 255, 0),   // Yellow
        new(0, 255, 0),     // Green
        new(0, 255, 255),   // Cyan
        new(0, 0, 255),     // Blue
        new(255, 0, 255)    // Magenta
    ];

    private readonly Random _random = new();
    private readonly int _speed;

    /// <summary>
    /// Creates a new Disco effect.
    /// </summary>
    /// <param name="speed">Speed 1-4 (1=slowest, 4=fastest). Maps to Rust Profile.speed.</param>
    public DiscoEffect(int speed = 2)
    {
        _speed = Math.Clamp(speed, 1, 4);
    }

    public CustomRGBEffectType Type => CustomRGBEffectType.Disco;
    public string Description => "Random zone color cycling";
    public bool RequiresInputMonitoring => false;
    public bool RequiresSystemAccess => false;

    public async Task RunAsync(CustomRGBEffectController controller, CancellationToken cancellationToken)
    {
        // From Rust: thread::sleep(Duration::from_millis(2000 / (u64::from(p.speed) * 4)));
        var intervalMs = 2000 / (_speed * 4);

        var stopwatch = Stopwatch.StartNew();
        var nextChangeMs = 0L;

        while (!cancellationToken.IsCancellationRequested)
        {
            var elapsedMs = stopwatch.ElapsedMilliseconds;

            // Check if it's time for the next color change (delta-time based)
            if (elapsedMs >= nextChangeMs)
            {
                // From Rust:
                // let colors_index = rng.random_range(0..6);
                // let new_values = colors[colors_index];
                // let zone_index = rng.random_range(0..4);
                var colorIndex = _random.Next(DiscoColors.Length);
                var zoneIndex = _random.Next(4);
                var color = DiscoColors[colorIndex];

                // From Rust: manager.keyboard.set_zone_by_index(zone_index, new_values).unwrap();
                await controller.SetZoneAsync(zoneIndex, color, cancellationToken).ConfigureAwait(false);

                nextChangeMs = elapsedMs + intervalMs;
            }

            // Use Task.Delay only as a yield mechanism (1ms minimum)
            await Task.Delay(1, cancellationToken).ConfigureAwait(false);
        }
    }
}
