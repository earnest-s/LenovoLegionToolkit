// ============================================================================
// PerformanceModeTransitionEffect.cs
//
// Premium performance-mode keyboard transition (ROG / Predator style).
// Pattern per pulse: instant flash → sine breath-out → dark gap.
// 3 pulses total, then 0.5 s black hold before resuming.
//
// Pulse timing:
//   Flash:     0–50 ms   (full brightness, instant ON)
//   Breath-out: 50–550 ms (sine ease 1.0 → 0.0)
//   Dark gap:  550–670 ms (black)
//   → one cycle ≈ 670 ms, 3 cycles ≈ 2.0 s + 0.5 s hold = ~2.5 s total
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
    // ── Pulse structure ───────────────────────────────────────────────────
    private const int PulseCount = 3;
    private const float FlashDurationMs = 50f;         // instant-on flash
    private const float BreathOutDurationMs = 500f;    // sine fade 1→0
    private const float DarkGapMs = 120f;              // black pause between pulses
    private const float PulseCycleMs = FlashDurationMs + BreathOutDurationMs + DarkGapMs; // ≈ 670 ms

    // ── Post-animation hold ───────────────────────────────────────────────
    private const float BlackHoldMs = 500f;            // stay black before resuming effect

    // ── Total duration ────────────────────────────────────────────────────
    private const float TotalDurationMs = PulseCount * PulseCycleMs + BlackHoldMs;

    // ── Frame pacing ──────────────────────────────────────────────────────
    private const int FrameDelayMs = 16;               // ≈ 60 fps

    /// <summary>
    /// Plays the full transition animation on the keyboard.
    /// Blocks asynchronously for ~2.5 s. Fully cancellable.
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
                var elapsedMs = (float)sw.Elapsed.TotalMilliseconds;
                if (elapsedMs >= TotalDurationMs)
                    break;

                var brightness = ComputeBrightness(elapsedMs);
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

    private static float ComputeBrightness(float elapsedMs)
    {
        // Determine which pulse we are in
        var pulseIndex = (int)(elapsedMs / PulseCycleMs);

        // Past all pulses → black hold region
        if (pulseIndex >= PulseCount)
            return 0f;

        // Local time within this pulse cycle
        var localMs = elapsedMs - pulseIndex * PulseCycleMs;

        // ── Phase 1: Flash (instant ON) ──
        if (localMs < FlashDurationMs)
            return 1f;

        // ── Phase 2: Breath-out (sine ease 1→0) ──
        var breathLocal = localMs - FlashDurationMs;
        if (breathLocal < BreathOutDurationMs)
        {
            // progress: 0 → 1
            var progress = breathLocal / BreathOutDurationMs;
            // sin(0) = 0, sin(PI/2) = 1 → we want 1→0, so use cos or offset sine
            // brightness = sin((1 - progress) * PI/2)  gives smooth 1→0
            return MathF.Sin((1f - progress) * MathF.PI * 0.5f);
        }

        // ── Phase 3: Dark gap ──
        return 0f;
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
