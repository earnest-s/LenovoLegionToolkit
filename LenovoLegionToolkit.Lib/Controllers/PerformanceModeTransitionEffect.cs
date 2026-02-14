// ============================================================================
// PerformanceModeTransitionEffect.cs
//
// Premium performance-mode keyboard transition (ROG / Predator style).
// Plays 3 smooth breathing pulses over 3 seconds, then fades to black
// over 0.5 seconds before yielding control back.
//
// Each pulse uses a gamma-corrected sine curve:
//   brightness = sin(t * PI) ^ gamma
//
// All 4 zones glow the same mode color simultaneously.
// ============================================================================

using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using LenovoLegionToolkit.Lib.Extensions;
using LenovoLegionToolkit.Lib.System;
using LenovoLegionToolkit.Lib.Utils;
using Microsoft.Win32.SafeHandles;
using Windows.Win32;

namespace LenovoLegionToolkit.Lib.Controllers;

/// <summary>
/// Self-contained premium transition animation for performance mode changes.
/// Writes directly to the HID device — caller is responsible for pausing
/// and resuming any running RGB effect around this call.
/// </summary>
public sealed class PerformanceModeTransitionEffect
{
    // ── Animation timing ──────────────────────────────────────────────────
    private const int PulseCount = 3;
    private const float PulseDuration = 1.0f;                   // 1 s per pulse  → 3 s total
    private const float FadeOutDuration = 0.5f;                 // black fade after last pulse
    private const float TotalDuration = PulseCount * PulseDuration + FadeOutDuration;

    // ── Easing ────────────────────────────────────────────────────────────
    private const float Gamma = 2.2f;                           // perceptual gamma correction

    // ── Frame pacing ──────────────────────────────────────────────────────
    private const int FrameDelayMs = 16;                        // ≈ 60 fps

    /// <summary>
    /// Plays the full transition animation on the keyboard.
    /// Blocks asynchronously for ~3.5 s. Fully cancellable.
    /// </summary>
    public static async Task PlayAsync(
        SafeFileHandle deviceHandle,
        RGBColor modeColor,
        CancellationToken cancellationToken)
    {
        var sw = Stopwatch.StartNew();

        try
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                var t = (float)sw.Elapsed.TotalSeconds;
                if (t >= TotalDuration)
                    break;

                var brightness = ComputeBrightness(t);
                var r = (byte)(modeColor.R * brightness);
                var g = (byte)(modeColor.G * brightness);
                var b = (byte)(modeColor.B * brightness);

                await SendColorToDevice(deviceHandle, r, g, b).ConfigureAwait(false);
                await Task.Delay(FrameDelayMs, cancellationToken).ConfigureAwait(false);
            }

            // Ensure we end on pure black (no leftover glow)
            if (!cancellationToken.IsCancellationRequested)
                await SendColorToDevice(deviceHandle, 0, 0, 0).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            // Interrupted by a new mode change — send black and exit cleanly
            try { await SendColorToDevice(deviceHandle, 0, 0, 0).ConfigureAwait(false); }
            catch { /* best effort */ }
            throw;
        }
    }

    // ── Brightness curve ──────────────────────────────────────────────────

    private static float ComputeBrightness(float t)
    {
        var pulsePhase = t / PulseDuration;          // which pulse we're in (fractional)
        var pulseTime = PulseDuration;

        if (pulsePhase < PulseCount)
        {
            // Inside a pulse — sine envelope with gamma correction
            var local = t - MathF.Floor(pulsePhase) * pulseTime;
            var normalised = local / pulseTime;      // 0 → 1 within one pulse

            // sin(0..PI) gives 0 → 1 → 0  (smooth fade-in, hold, fade-out)
            var raw = MathF.Sin(normalised * MathF.PI);
            return MathF.Pow(raw, Gamma);
        }

        // After last pulse — fade to black over FadeOutDuration
        var fadeT = (t - PulseCount * PulseDuration) / FadeOutDuration;
        fadeT = Math.Clamp(fadeT, 0f, 1f);

        // Smoothstep-out: 1 → 0
        var inv = 1f - fadeT;
        return inv * inv * (3f - 2f * inv);          // smoothstep
    }

    // ── HID write ─────────────────────────────────────────────────────────

    private static unsafe Task SendColorToDevice(SafeFileHandle handle, byte r, byte g, byte b) => Task.Run(() =>
    {
        var state = new LENOVO_RGB_KEYBOARD_STATE
        {
            Header = [0xCC, 0x16],
            Effect = 1,           // Static
            Speed = 1,
            Brightness = 2,       // High
            Zone1Rgb = [r, g, b],
            Zone2Rgb = [r, g, b],
            Zone3Rgb = [r, g, b],
            Zone4Rgb = [r, g, b],
            Padding = 0,
            WaveLTR = 0,
            WaveRTL = 0,
            Unused = new byte[13]
        };

        var ptr = IntPtr.Zero;
        try
        {
            var size = Marshal.SizeOf<LENOVO_RGB_KEYBOARD_STATE>();
            ptr = Marshal.AllocHGlobal(size);
            Marshal.StructureToPtr(state, ptr, false);

            if (!PInvoke.HidD_SetFeature(handle, ptr.ToPointer(), (uint)size))
                PInvokeExtensions.ThrowIfWin32Error("HidD_SetFeature");
        }
        finally
        {
            Marshal.FreeHGlobal(ptr);
        }
    });
}
