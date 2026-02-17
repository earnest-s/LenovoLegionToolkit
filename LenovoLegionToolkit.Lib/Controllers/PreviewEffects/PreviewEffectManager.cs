// ============================================================================
// PreviewEffectManager.cs
//
// Manages the lifecycle of firmware-effect preview simulators.
// Starts the appropriate IPreviewEffect when a firmware preset
// (Breath, Wave, Smooth) is selected; stops it when the preset changes.
// ============================================================================

using System;
using System.Threading;
using System.Threading.Tasks;
using LenovoLegionToolkit.Lib.Controllers.CustomRGBEffects;

namespace LenovoLegionToolkit.Lib.Controllers.PreviewEffects;

/// <summary>
/// Manages start/stop of firmware-effect preview simulators.
/// Call <see cref="Start"/> when a firmware animated preset is selected;
/// call <see cref="Stop"/> when switching to Off, Static, or custom effects.
/// </summary>
public class PreviewEffectManager(RgbFrameDispatcher dispatcher)
{
    private CancellationTokenSource? _cts;
    private Task? _task;

    /// <summary>
    /// Starts the appropriate preview simulator for the given firmware effect.
    /// Stops any previously running simulator first.
    /// </summary>
    public void Start(RGBKeyboardBacklightEffect effect, RGBKeyboardBacklightSpeed speed, ZoneColors colors)
    {
        Stop();

        IPreviewEffect? preview = effect switch
        {
            RGBKeyboardBacklightEffect.Breath => new BreathingPreviewEffect(colors, speed),
            RGBKeyboardBacklightEffect.WaveLTR => new WavePreviewEffect(leftToRight: true, speed),
            RGBKeyboardBacklightEffect.WaveRTL => new WavePreviewEffect(leftToRight: false, speed),
            RGBKeyboardBacklightEffect.Smooth => new SmoothPreviewEffect(speed),
            _ => null
        };

        if (preview is null)
            return;

        _cts = new CancellationTokenSource();
        var ct = _cts.Token;
        _task = Task.Run(() => RunSafeAsync(preview, ct), ct);
    }

    /// <summary>
    /// Stops the currently running preview simulator, if any.
    /// </summary>
    public void Stop()
    {
        if (_cts is null)
            return;

        _cts.Cancel();
        _cts.Dispose();
        _cts = null;
        _task = null; // fire-and-forget — task will wind down on its own
    }

    private async Task RunSafeAsync(IPreviewEffect effect, CancellationToken ct)
    {
        try
        {
            // Small delay so the initial static preview frame from the
            // controller is visible before animation starts.
            await Task.Delay(50, ct).ConfigureAwait(false);
            await effect.RunPreviewAsync(dispatcher, ct).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            // Expected on Stop()
        }
    }
}
