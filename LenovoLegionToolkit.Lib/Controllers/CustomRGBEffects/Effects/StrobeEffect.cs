// ============================================================================
// StrobeEffect.cs
//
// Performance-mode style strobe: flash → sine breath-out → dark gap.
// Speed parameter is ignored; timing matches the OEM transition look.
// ============================================================================

using System;
using System.Threading;
using System.Threading.Tasks;

namespace LenovoLegionToolkit.Lib.Controllers.CustomRGBEffects.Effects;

/// <summary>
/// Performance-mode strobe: 3-cycle flash → sine breath-out → dark gap.
/// Uses zone colours selected by the user. Speed is fixed.
/// </summary>
public class StrobeEffect : ICustomRGBEffect
{
    private readonly ZoneColors _zoneColors;

    /// <summary>
    /// Creates a new Strobe effect.
    /// </summary>
    /// <param name="zoneColors">Zone colors to flash.</param>
    /// <param name="speed">Ignored — kept for API compatibility.</param>
    public StrobeEffect(ZoneColors? zoneColors = null, int speed = 2)
    {
        _zoneColors = zoneColors ?? ZoneColors.White;
    }

    public CustomRGBEffectType Type => CustomRGBEffectType.Strobe;
    public string Description => "Performance-mode strobe effect";
    public bool RequiresInputMonitoring => false;
    public bool RequiresSystemAccess => false;

    // ── Timing constants (OEM-matched) ──────────────────────────────
    private const int Cycles = 3;
    private const int StrobeOnMs = 120;
    private const int BreathSteps = 10;
    private const int BreathMs = 180;          // total breath-out per cycle
    private const int BreathStepMs = BreathMs / BreathSteps;
    private const int OffGapMs = 90;

    public async Task RunAsync(CustomRGBEffectController controller, CancellationToken cancellationToken)
    {
        // Cache per-zone base colours for brightness scaling.
        var z = new[] { _zoneColors.Zone1, _zoneColors.Zone2, _zoneColors.Zone3, _zoneColors.Zone4 };

        while (!cancellationToken.IsCancellationRequested)
        {
            for (var cycle = 0; cycle < Cycles && !cancellationToken.IsCancellationRequested; cycle++)
            {
                // ── ON: full brightness ──────────────────────────────
                await controller.SetColorsAsync(_zoneColors, cancellationToken).ConfigureAwait(false);
                await Task.Delay(StrobeOnMs, cancellationToken).ConfigureAwait(false);

                // ── Breath-out: sine fade from 1 → 0 ────────────────
                for (var step = BreathSteps; step >= 0 && !cancellationToken.IsCancellationRequested; step--)
                {
                    var k = (float)step / BreathSteps;
                    // Apply a sine curve for a smooth perceived fade.
                    var brightness = MathF.Sin(k * MathF.PI / 2);

                    var scaled = new ZoneColors(
                        Scale(z[0], brightness),
                        Scale(z[1], brightness),
                        Scale(z[2], brightness),
                        Scale(z[3], brightness));

                    await controller.SetColorsAsync(scaled, cancellationToken).ConfigureAwait(false);
                    await Task.Delay(BreathStepMs, cancellationToken).ConfigureAwait(false);
                }

                // ── OFF gap ─────────────────────────────────────────
                await controller.SetColorsAsync(ZoneColors.Black, cancellationToken).ConfigureAwait(false);
                await Task.Delay(OffGapMs, cancellationToken).ConfigureAwait(false);
            }
        }
    }

    /// <summary>Scales an <see cref="RGBColor"/> by a 0-1 brightness factor.</summary>
    private static RGBColor Scale(RGBColor c, float k)
        => new((byte)(c.R * k), (byte)(c.G * k), (byte)(c.B * k));
}
