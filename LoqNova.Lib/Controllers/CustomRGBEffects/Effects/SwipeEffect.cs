// ============================================================================
// SwipeEffect.cs
// 
// Color swipe across keyboard zones (always rightward).
// Ported from L5P-Keyboard-RGB swipe.rs
// 
// Effect behavior (from Rust, forced to Direction::Right):
// - Mode.Change: Rotates the color array left (for rightward visual movement)
// - Mode.Fill: Fills zones monotonically, persisting state across frames
// - Timing: 150/speed steps, 10ms per transition step, 20ms loop delay
// 
// Direction is ALWAYS rightward - no direction parameter.
// ============================================================================

using System;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;

namespace LoqNova.Lib.Controllers.CustomRGBEffects.Effects;

/// <summary>
/// Swipe effect - moves colors across zones (rightward only).
/// Port of Rust swipe.rs effect with Direction::Right.
/// </summary>
public class SwipeEffect : ICustomRGBEffect
{
    private readonly int _speed;
    private readonly SwipeMode _mode;
    private readonly bool _cleanWithBlack;
    private readonly ZoneColors _colors;

    // From Rust: const STEPS: u8 = 150;
    private const int BaseSteps = 150;

    public CustomRGBEffectType Type => CustomRGBEffectType.Swipe;
    public string Description => "Colors swipe across zones (rightward)";
    public bool RequiresInputMonitoring => false;
    public bool RequiresSystemAccess => false;

    /// <summary>
    /// Creates a new Swipe effect.
    /// Direction is always rightward to match L5P-Keyboard-RGB default.
    /// </summary>
    public SwipeEffect(
        ZoneColors colors,
        int speed = 2,
        EffectDirection direction = EffectDirection.Right,
        SwipeMode mode = SwipeMode.Change,
        bool cleanWithBlack = false)
    {
        _colors = colors;
        _speed = Math.Clamp(speed, 1, 4);
        _mode = mode;
        _cleanWithBlack = cleanWithBlack;
    }

    public async Task RunAsync(CustomRGBEffectController controller, CancellationToken cancellationToken)
    {
        // From Rust: let steps = STEPS / profile.speed;
        var steps = BaseSteps / _speed;

        // Persistent state across all frames (matching Rust)
        var changeColors = _colors;
        var fillColors = new byte[12]; // Matches Rust: let mut used_colors_array: [u8; 12] = [0; 12];

        // Stopwatch for consistent timing when backgrounded
        var stopwatch = Stopwatch.StartNew();
        var nextLoopMs = 0L;

        while (!cancellationToken.IsCancellationRequested)
        {
            var elapsedMs = stopwatch.ElapsedMilliseconds;

            if (elapsedMs >= nextLoopMs)
            {
                switch (_mode)
                {
                    case SwipeMode.Change:
                        changeColors = await RunChangeModeAsync(controller, changeColors, steps, cancellationToken).ConfigureAwait(false);
                        break;
                    case SwipeMode.Fill:
                        await RunFillModeAsync(controller, fillColors, steps, cancellationToken).ConfigureAwait(false);
                        break;
                }

                // From Rust: thread::sleep(Duration::from_millis(20));
                nextLoopMs = stopwatch.ElapsedMilliseconds + 20;
            }

            await Task.Delay(1, cancellationToken).ConfigureAwait(false);
        }
    }

    private static async Task<ZoneColors> RunChangeModeAsync(
        CustomRGBEffectController controller,
        ZoneColors currentColors,
        int steps,
        CancellationToken cancellationToken)
    {
        // From Rust Direction::Right: change_rgb_array.rotate_left(3)
        // This shifts colors rightward on physical keyboard
        var rotatedColors = currentColors.RotateRight();

        // From Rust: manager.keyboard.transition_colors_to(&change_rgb_array, steps, 10)
        await controller.TransitionColorsAsync(rotatedColors, steps, 10, cancellationToken).ConfigureAwait(false);
        
        return rotatedColors;
    }

    /// <summary>
    /// Runs Fill mode matching Rust exactly.
    /// Key insight: fillColors array persists across iterations, not reset each frame.
    /// For LEFT→RIGHT visual animation, use zone order [0,1,2,3].
    /// </summary>
    private async Task RunFillModeAsync(
        CustomRGBEffectController controller,
        byte[] fillColors,
        int steps,
        CancellationToken cancellationToken)
    {
        // Get source colors as byte array
        var sourceColors = _colors.ToArray();

        // For LEFT→RIGHT visual animation:
        // Iterate colors 0→1→2→3 (Zone1 color first, Zone4 color last)
        // Fill zones 0→1→2→3 (leftmost first, rightmost last)
        // This matches Rust Direction::Left behavior: (0..4).collect() = [0, 1, 2, 3]
        int[] range = [0, 1, 2, 3];

        foreach (var colorIndex in range)
        {
            // Get the RGB values for this color (3 bytes per zone)
            var r = sourceColors[colorIndex * 3];
            var g = sourceColors[colorIndex * 3 + 1];
            var b = sourceColors[colorIndex * 3 + 2];

            // Fill each zone with this color (left to right)
            foreach (var zoneIndex in range)
            {
                cancellationToken.ThrowIfCancellationRequested();

                // Set this zone's color (matching Rust byte-level operations)
                fillColors[zoneIndex * 3] = r;
                fillColors[zoneIndex * 3 + 1] = g;
                fillColors[zoneIndex * 3 + 2] = b;

                // Send the current state - one frame per zone update
                var zoneColors = ZoneColors.FromArray(fillColors);
                await controller.TransitionColorsAsync(zoneColors, steps, 1, cancellationToken).ConfigureAwait(false);
            }

            // Clean with black if enabled (also left to right)
            if (_cleanWithBlack)
            {
                foreach (var zoneIndex in range)
                {
                    cancellationToken.ThrowIfCancellationRequested();

                    fillColors[zoneIndex * 3] = 0;
                    fillColors[zoneIndex * 3 + 1] = 0;
                    fillColors[zoneIndex * 3 + 2] = 0;

                    var zoneColors = ZoneColors.FromArray(fillColors);
                    await controller.TransitionColorsAsync(zoneColors, steps, 1, cancellationToken).ConfigureAwait(false);
                }
            }
        }
    }
}
