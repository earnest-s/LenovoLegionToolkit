// ============================================================================
// WavePreviewEffect.cs
//
// Simulates the firmware Wave (LTR / RTL) effect in the UI preview.
// A single lit zone sweeps across the keyboard in the chosen direction.
// Speed maps to step delay.
// ============================================================================

using System;
using System.Threading;
using System.Threading.Tasks;
using LenovoLegionToolkit.Lib.Controllers.CustomRGBEffects;

namespace LenovoLegionToolkit.Lib.Controllers.PreviewEffects;

public class WavePreviewEffect : IPreviewEffect
{
    private readonly bool _leftToRight;
    private readonly int _stepDelayMs;

    public WavePreviewEffect(bool leftToRight, RGBKeyboardBacklightSpeed speed)
    {
        _leftToRight = leftToRight;
        _stepDelayMs = speed switch
        {
            RGBKeyboardBacklightSpeed.Slowest => 400,
            RGBKeyboardBacklightSpeed.Slow => 280,
            RGBKeyboardBacklightSpeed.Fast => 160,
            RGBKeyboardBacklightSpeed.Fastest => 80,
            _ => 200
        };
    }

    public async Task RunPreviewAsync(RgbFrameDispatcher dispatcher, CancellationToken ct)
    {
        var pos = 0;

        while (!ct.IsCancellationRequested)
        {
            var zones = ZoneColors.Black;
            var activeIndex = _leftToRight ? pos : 3 - pos;
            zones = zones.WithZone(activeIndex, RGBColor.White);

            dispatcher.RenderPreviewOnly(zones);
            pos = (pos + 1) % 4;
            await Task.Delay(_stepDelayMs, ct).ConfigureAwait(false);
        }
    }
}
