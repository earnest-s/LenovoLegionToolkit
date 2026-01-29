// ============================================================================
// ChristmasEffect.cs
// 
// Holiday-themed multi-mode RGB effect.
// Ported from L5P-Keyboard-RGB christmas.rs
// 
// Original Rust source: app/src/manager/effects/christmas.rs
// 
// Effect behavior:
// - 4 sub-effects randomly chosen (avoiding repeats):
//   0: Solid color cycling through all 4 Christmas colors (3 loops)
//   1: Alternating between 2 random colors (4 loops)
//   2: Swipe fill effect with each Christmas color
//   3: Strobe alternating white zones (checkerboard pattern)
// - Christmas colors: Red, Yellow, Green, Blue
// ============================================================================

using System;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;

namespace LenovoLegionToolkit.Lib.Controllers.CustomRGBEffects.Effects;

/// <summary>
/// Christmas effect - holiday-themed multi-mode animation.
/// Port of Rust christmas.rs effect.
/// Uses Stopwatch-based timing for consistent speed when backgrounded.
/// </summary>
public class ChristmasEffect : ICustomRGBEffect
{
    // From Rust: let xmas_color_array = [[255, 10, 10], [255, 255, 20], [30, 255, 30], [70, 70, 255]];
    private static readonly RGBColor[] ChristmasColors =
    [
        new(255, 10, 10),    // Red
        new(255, 255, 20),   // Yellow
        new(30, 255, 30),    // Green
        new(70, 70, 255)     // Blue
    ];

    private static readonly RGBColor Black = new(0, 0, 0);
    private static readonly RGBColor White = new(255, 255, 255);

    private readonly Random _random = new();
    private int _lastSubeffect = -1;

    public CustomRGBEffectType Type => CustomRGBEffectType.Christmas;
    public string Description => "Holiday-themed animation";
    public bool RequiresInputMonitoring => false;
    public bool RequiresSystemAccess => false;

    public async Task RunAsync(CustomRGBEffectController controller, CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            // From Rust:
            // let mut subeffect = rng.random_range(0..subeffect_count);
            // while last_subeffect == subeffect {
            //     subeffect = rng.random_range(0..subeffect_count);
            // }
            int subeffect;
            do
            {
                subeffect = _random.Next(4);
            } while (subeffect == _lastSubeffect);

            _lastSubeffect = subeffect;

            switch (subeffect)
            {
                case 0:
                    await RunSolidCycleAsync(controller, cancellationToken).ConfigureAwait(false);
                    break;
                case 1:
                    await RunAlternatingAsync(controller, cancellationToken).ConfigureAwait(false);
                    break;
                case 2:
                    await RunSwipeFillAsync(controller, cancellationToken).ConfigureAwait(false);
                    break;
                case 3:
                    await RunStrobeAsync(controller, cancellationToken).ConfigureAwait(false);
                    break;
            }
        }
    }

    /// <summary>
    /// Subeffect 0: Solid color cycling through all 4 Christmas colors
    /// Uses Stopwatch-based timing for consistent speed.
    /// </summary>
    private async Task RunSolidCycleAsync(CustomRGBEffectController controller, CancellationToken cancellationToken)
    {
        var stopwatch = Stopwatch.StartNew();
        var colorIndex = 0;
        var loopCount = 0;
        var nextChangeMs = 0L;

        while (loopCount < 3)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var elapsedMs = stopwatch.ElapsedMilliseconds;

            if (elapsedMs >= nextChangeMs)
            {
                await controller.SetSolidColorAsync(ChristmasColors[colorIndex], cancellationToken).ConfigureAwait(false);
                nextChangeMs = elapsedMs + 500;
                colorIndex++;
                if (colorIndex >= ChristmasColors.Length)
                {
                    colorIndex = 0;
                    loopCount++;
                }
            }

            await Task.Delay(1, cancellationToken).ConfigureAwait(false);
        }
    }

    /// <summary>
    /// Subeffect 1: Alternating between 2 random colors
    /// Uses Stopwatch-based timing for consistent speed.
    /// </summary>
    private async Task RunAlternatingAsync(CustomRGBEffectController controller, CancellationToken cancellationToken)
    {
        var color1Index = _random.Next(4);
        int color2Index;
        do
        {
            color2Index = _random.Next(4);
        } while (color2Index == color1Index);

        var color1 = ChristmasColors[color1Index];
        var color2 = ChristmasColors[color2Index];

        var stopwatch = Stopwatch.StartNew();
        var nextChangeMs = 0L;
        var useColor1 = true;
        var changeCount = 0;

        while (changeCount < 8) // 4 loops * 2 colors
        {
            cancellationToken.ThrowIfCancellationRequested();
            var elapsedMs = stopwatch.ElapsedMilliseconds;

            if (elapsedMs >= nextChangeMs)
            {
                await controller.SetSolidColorAsync(useColor1 ? color1 : color2, cancellationToken).ConfigureAwait(false);
                nextChangeMs = elapsedMs + 400;
                useColor1 = !useColor1;
                changeCount++;
            }

            await Task.Delay(1, cancellationToken).ConfigureAwait(false);
        }
    }

    /// <summary>
    /// Subeffect 2: Swipe fill effect with each Christmas color
    /// </summary>
    private async Task RunSwipeFillAsync(CustomRGBEffectController controller, CancellationToken cancellationToken)
    {
        const int steps = 100;

        // Start from black
        await controller.TransitionColorsAsync(ZoneColors.Black, steps, 1, cancellationToken).ConfigureAwait(false);

        // From Rust: let left_or_right = rng.random_range(0..2);
        var leftToRight = _random.Next(2) == 0;
        var range = leftToRight ? new[] { 0, 1, 2, 3 } : new[] { 3, 2, 1, 0 };

        var usedColors = ZoneColors.Black;

        foreach (var color in ChristmasColors)
        {
            // Fill with color
            foreach (var zoneIndex in range)
            {
                cancellationToken.ThrowIfCancellationRequested();
                usedColors = usedColors.WithZone(zoneIndex, color);
                await controller.TransitionColorsAsync(usedColors, steps, 1, cancellationToken).ConfigureAwait(false);
            }

            // Clear with black
            foreach (var zoneIndex in range)
            {
                cancellationToken.ThrowIfCancellationRequested();
                usedColors = usedColors.WithZone(zoneIndex, Black);
                await controller.TransitionColorsAsync(usedColors, steps, 1, cancellationToken).ConfigureAwait(false);
            }
        }
    }

    /// <summary>
    /// Subeffect 3: Strobe alternating white zones (checkerboard pattern)
    /// Uses Stopwatch-based timing for consistent speed.
    /// </summary>
    private async Task RunStrobeAsync(CustomRGBEffectController controller, CancellationToken cancellationToken)
    {
        const int steps = 30;

        var state1 = new ZoneColors
        {
            Zone1 = White,
            Zone2 = Black,
            Zone3 = White,
            Zone4 = Black
        };

        var state2 = new ZoneColors
        {
            Zone1 = Black,
            Zone2 = White,
            Zone3 = Black,
            Zone4 = White
        };

        var stopwatch = Stopwatch.StartNew();
        var nextChangeMs = 0L;
        var useState1 = true;
        var changeCount = 0;

        while (changeCount < 8) // 4 loops * 2 states
        {
            cancellationToken.ThrowIfCancellationRequested();
            var elapsedMs = stopwatch.ElapsedMilliseconds;

            if (elapsedMs >= nextChangeMs)
            {
                await controller.TransitionColorsAsync(useState1 ? state1 : state2, steps, 1, cancellationToken).ConfigureAwait(false);
                nextChangeMs = stopwatch.ElapsedMilliseconds + 400;
                useState1 = !useState1;
                changeCount++;
            }

            await Task.Delay(1, cancellationToken).ConfigureAwait(false);
        }
    }
}
