// ============================================================================
// BreathingPreviewEffect.cs
//
// Simulates the firmware Breath effect in the UI preview.
// All four zones fade in/out with a sine-squared curve using the
// preset zone colors.  Speed maps to cycle duration.
// ============================================================================

using System;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using LenovoLegionToolkit.Lib.Controllers.CustomRGBEffects;

namespace LenovoLegionToolkit.Lib.Controllers.PreviewEffects;

public class BreathingPreviewEffect : IPreviewEffect
{
    private readonly ZoneColors _colors;
    private readonly int _cycleDurationMs;

    public BreathingPreviewEffect(ZoneColors colors, RGBKeyboardBacklightSpeed speed)
    {
        _colors = colors;
        _cycleDurationMs = speed switch
        {
            RGBKeyboardBacklightSpeed.Slowest => 5000,
            RGBKeyboardBacklightSpeed.Slow => 3500,
            RGBKeyboardBacklightSpeed.Fast => 2000,
            RGBKeyboardBacklightSpeed.Fastest => 1200,
            _ => 3000
        };
    }

    public async Task RunPreviewAsync(RgbFrameDispatcher dispatcher, CancellationToken ct)
    {
        var sw = Stopwatch.StartNew();

        while (!ct.IsCancellationRequested)
        {
            var t = (float)(sw.ElapsedMilliseconds % _cycleDurationMs) / _cycleDurationMs;
            var brightness = MathF.Pow(MathF.Sin(t * MathF.PI), 2);

            var preview = new ZoneColors(
                Scale(_colors.Zone1, brightness),
                Scale(_colors.Zone2, brightness),
                Scale(_colors.Zone3, brightness),
                Scale(_colors.Zone4, brightness));

            dispatcher.RenderPreviewOnly(preview);
            await Task.Delay(16, ct).ConfigureAwait(false);
        }
    }

    private static RGBColor Scale(RGBColor c, float k) =>
        new((byte)(c.R * k), (byte)(c.G * k), (byte)(c.B * k));
}
