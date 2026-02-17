// ============================================================================
// SmoothPreviewEffect.cs
//
// Simulates the firmware Smooth (rainbow cycle) effect in the UI preview.
// All four zones cycle smoothly through the HSV spectrum with 90° offsets.
// Speed maps to hue rotation rate.
// ============================================================================

using System;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using LenovoLegionToolkit.Lib.Controllers.CustomRGBEffects;

namespace LenovoLegionToolkit.Lib.Controllers.PreviewEffects;

public class SmoothPreviewEffect : IPreviewEffect
{
    private readonly float _hueDegreesPerSecond;

    public SmoothPreviewEffect(RGBKeyboardBacklightSpeed speed)
    {
        _hueDegreesPerSecond = speed switch
        {
            RGBKeyboardBacklightSpeed.Slowest => 30f,
            RGBKeyboardBacklightSpeed.Slow => 60f,
            RGBKeyboardBacklightSpeed.Fast => 120f,
            RGBKeyboardBacklightSpeed.Fastest => 240f,
            _ => 60f
        };
    }

    public async Task RunPreviewAsync(RgbFrameDispatcher dispatcher, CancellationToken ct)
    {
        var sw = Stopwatch.StartNew();

        while (!ct.IsCancellationRequested)
        {
            var hue = (sw.ElapsedMilliseconds / 1000f * _hueDegreesPerSecond) % 360f;

            var preview = new ZoneColors
            {
                Zone1 = HsvToRgb((hue + 0) % 360, 1f, 1f),
                Zone2 = HsvToRgb((hue + 90) % 360, 1f, 1f),
                Zone3 = HsvToRgb((hue + 180) % 360, 1f, 1f),
                Zone4 = HsvToRgb((hue + 270) % 360, 1f, 1f)
            };

            dispatcher.RenderPreviewOnly(preview);
            await Task.Delay(16, ct).ConfigureAwait(false);
        }
    }

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
