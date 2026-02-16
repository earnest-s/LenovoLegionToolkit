// ============================================================================
// CustomRGBEffectController.cs
// 
// Controller for running custom RGB effects on the 4-zone keyboard.
// Provides methods for setting colors and smooth transitions.
// 
// Original Rust source: https://github.com/4JX/L5P-Keyboard-RGB
// Maps to Rust legion_rgb_driver::Keyboard struct methods.
// ============================================================================

using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using LenovoLegionToolkit.Lib.Extensions;
using LenovoLegionToolkit.Lib.Settings;
using LenovoLegionToolkit.Lib.SoftwareDisabler;
using LenovoLegionToolkit.Lib.System;
using LenovoLegionToolkit.Lib.System.Management;
using LenovoLegionToolkit.Lib.Utils;
using Microsoft.Win32.SafeHandles;
using NeoSmart.AsyncLock;
using Windows.Win32;

namespace LenovoLegionToolkit.Lib.Controllers.CustomRGBEffects;

/// <summary>
/// Controller for running custom RGB effects on the 4-zone keyboard.
/// Provides methods for setting colors and smooth color transitions.
/// This is the C# equivalent of the Rust Keyboard struct.
/// </summary>
#pragma warning disable CS9113 // Parameter 'settings' is unread - reserved for future use
public class CustomRGBEffectController(RGBKeyboardSettings settings, VantageDisabler vantageDisabler)
#pragma warning restore CS9113
{
    private static readonly AsyncLock IoLock = new();

    private SafeFileHandle? _deviceHandle;
    private CancellationTokenSource? _effectCts;
    private Task? _effectTask;
    private ZoneColors _currentColors = ZoneColors.Black;
    private byte _currentBrightness = 2; // Default to High (2), Low = 1

    private SafeFileHandle? DeviceHandle
    {
        get
        {
            if (ForceDisable)
                return null;

            _deviceHandle ??= Devices.GetRGBKeyboard();
            return _deviceHandle;
        }
    }

    /// <summary>
    /// Gets the current zone colors.
    /// </summary>
    public ZoneColors CurrentColors => _currentColors;

    /// <summary>
    /// Force disable the RGB keyboard.
    /// </summary>
    public bool ForceDisable { get; set; }

    /// <summary>
    /// Whether a custom effect is currently running.
    /// </summary>
    public bool IsEffectRunning => _effectTask is not null && !_effectTask.IsCompleted;

    /// <summary>
    /// Gets or sets the current brightness level (1=Low, 2=High).
    /// This is sent to hardware and affects all custom effects.
    /// </summary>
    public byte CurrentBrightness
    {
        get => _currentBrightness;
        set => _currentBrightness = Math.Clamp(value, (byte)1, (byte)2);
    }

    /// <summary>
    /// When true, SetColorsAsync will skip writing to the device.
    /// Used by strobe override in RGBKeyboardBacklightController.
    /// </summary>
    public bool IsOverrideActive { get; set; }

    /// <summary>
    /// Checks if the RGB keyboard is supported.
    /// </summary>
    public Task<bool> IsSupportedAsync() => Task.FromResult(DeviceHandle is not null);

    /// <summary>
    /// Resumes from override by lifting the HID-write gate and immediately
    /// pushing the last computed frame to the device.  This must be called
    /// instead of toggling <see cref="IsOverrideActive"/> manually when the
    /// caller needs zero idle gap — the HID controller renders its default
    /// (white) frame if even one refresh cycle passes without a write.
    /// </summary>
    public async Task ResumeFromOverrideAsync()
    {
        // Lift the gate so the effect loop's subsequent frames go through.
        IsOverrideActive = false;

        // Immediately push the last frame the effect loop computed while
        // gated.  This overwrites the HID buffer in the same scheduling
        // quantum as the gate-lift, so the controller never idles.
        var colors = _currentColors;
        using (await IoLock.LockAsync().ConfigureAwait(false))
        {
            var handle = DeviceHandle;
            if (handle is null)
                return;

            var state = CreateState(colors);
            await SendToDeviceAsync(handle, state).ConfigureAwait(false);
        }
    }

    /// <summary>
    /// Starts a custom RGB effect.
    /// </summary>
    public async Task StartEffectAsync(ICustomRGBEffect effect)
    {
        await StopEffectAsync().ConfigureAwait(false);

        using (await IoLock.LockAsync().ConfigureAwait(false))
        {
            _ = DeviceHandle ?? throw new InvalidOperationException("RGB Keyboard unsupported");
            await ThrowIfVantageEnabled().ConfigureAwait(false);

            // Take light control ownership
            await WMI.LenovoGameZoneData.SetLightControlOwnerAsync(1).ConfigureAwait(false);

            // Set to static mode for manual control
            await SetStaticModeAsync().ConfigureAwait(false);
        }

        _effectCts = new CancellationTokenSource();
        _effectTask = RunEffectInternalAsync(effect, _effectCts.Token);

        if (Log.Instance.IsTraceEnabled)
            Log.Instance.Trace($"Started custom effect: {effect.Type}");
    }

    /// <summary>
    /// Stops the currently running effect.
    /// </summary>
    public async Task StopEffectAsync()
    {
        if (_effectCts is not null)
        {
            await _effectCts.CancelAsync().ConfigureAwait(false);

            if (_effectTask is not null)
            {
                try
                {
                    await _effectTask.ConfigureAwait(false);
                }
                catch (OperationCanceledException)
                {
                    // Expected
                }
            }

            _effectCts.Dispose();
            _effectCts = null;
            _effectTask = null;

            if (Log.Instance.IsTraceEnabled)
                Log.Instance.Trace($"Stopped custom effect");
        }
    }

    private async Task RunEffectInternalAsync(ICustomRGBEffect effect, CancellationToken cancellationToken)
    {
        try
        {
            await effect.RunAsync(this, cancellationToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            // Expected on cancellation
        }
        catch (Exception ex)
        {
            if (Log.Instance.IsTraceEnabled)
                Log.Instance.Trace($"Effect {effect.Type} threw an exception", ex);
        }
    }

    /// <summary>
    /// Sets all zones to the specified colors immediately.
    /// Maps to Rust Keyboard::set_colors_to().
    /// </summary>
    public async Task SetColorsAsync(ZoneColors colors, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        // Skip device write when strobe override is active
        if (IsOverrideActive)
        {
            _currentColors = colors;
            return;
        }

        using (await IoLock.LockAsync(cancellationToken).ConfigureAwait(false))
        {
            var handle = DeviceHandle ?? throw new InvalidOperationException("RGB Keyboard unsupported");

            _currentColors = colors;
            var state = CreateState(colors);
            await SendToDeviceAsync(handle, state).ConfigureAwait(false);
        }
    }

    /// <summary>
    /// Sets all zones to a single solid color.
    /// Maps to Rust Keyboard::solid_set_colors_to().
    /// </summary>
    public Task SetSolidColorAsync(RGBColor color, CancellationToken cancellationToken = default)
    {
        return SetColorsAsync(new ZoneColors
        {
            Zone1 = color,
            Zone2 = color,
            Zone3 = color,
            Zone4 = color
        }, cancellationToken);
    }

    /// <summary>
    /// Sets a specific zone (0-3) to the specified color.
    /// Maps to Rust Keyboard::set_zone_by_index().
    /// </summary>
    public Task SetZoneAsync(int zoneIndex, RGBColor color, CancellationToken cancellationToken = default)
    {
        if (zoneIndex < 0 || zoneIndex > 3)
            throw new ArgumentOutOfRangeException(nameof(zoneIndex), "Zone index must be 0-3");

        var newColors = _currentColors.WithZone(zoneIndex, color);
        return SetColorsAsync(newColors, cancellationToken);
    }

    /// <summary>
    /// Smoothly transitions to target colors over multiple steps.
    /// Maps to Rust Keyboard::transition_colors_to().
    /// Uses Stopwatch-based timing to ensure consistent speed regardless of app focus.
    /// </summary>
    /// <param name="targetColors">The target colors for all zones.</param>
    /// <param name="steps">Number of interpolation steps (higher = smoother).</param>
    /// <param name="delayBetweenStepsMs">Target delay between each step in milliseconds.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    public async Task TransitionColorsAsync(
        ZoneColors targetColors,
        int steps,
        int delayBetweenStepsMs,
        CancellationToken cancellationToken = default)
    {
        if (steps <= 0)
        {
            await SetColorsAsync(targetColors, cancellationToken).ConfigureAwait(false);
            return;
        }

        // Calculate color differences per step (as floats for precision)
        // This matches the Rust transition_colors_to() implementation
        var startArray = _currentColors.ToArray();
        var targetArray = targetColors.ToArray();

        // Use Stopwatch for precise timing that is not affected by app minimization
        var stopwatch = Stopwatch.StartNew();
        var totalDurationMs = steps * delayBetweenStepsMs;

        while (!cancellationToken.IsCancellationRequested)
        {
            var elapsedMs = stopwatch.ElapsedMilliseconds;

            // Calculate progress based on elapsed time (delta-time based)
            var progress = totalDurationMs > 0
                ? Math.Min(1.0f, elapsedMs / (float)totalDurationMs)
                : 1.0f;

            // Calculate current colors based on progress
            var stepArray = new byte[12];
            for (var i = 0; i < 12; i++)
            {
                stepArray[i] = (byte)Math.Clamp(
                    startArray[i] + (targetArray[i] - startArray[i]) * progress,
                    0, 255);
            }

            await SetColorsAsync(ZoneColors.FromArray(stepArray), cancellationToken).ConfigureAwait(false);

            if (progress >= 1.0f)
                break;

            // Use Task.Delay only as a yield mechanism (1ms minimum)
            // Actual timing is controlled by Stopwatch
            await Task.Delay(1, cancellationToken).ConfigureAwait(false);
        }

        // Ensure we end exactly at target colors
        await SetColorsAsync(targetColors, cancellationToken).ConfigureAwait(false);
    }

    private async Task ThrowIfVantageEnabled()
    {
        var vantageStatus = await vantageDisabler.GetStatusAsync().ConfigureAwait(false);
        if (vantageStatus == SoftwareStatus.Enabled)
            throw new InvalidOperationException("Can't manage RGB keyboard with Vantage enabled");
    }

    private async Task SetStaticModeAsync()
    {
        var handle = DeviceHandle ?? throw new InvalidOperationException("RGB Keyboard unsupported");

        var state = new LENOVO_RGB_KEYBOARD_STATE
        {
            Header = [0xCC, 0x16],
            Effect = 1, // Static
            Speed = 1,
            Brightness = _currentBrightness,
            Zone1Rgb = [0xFF, 0xFF, 0xFF],
            Zone2Rgb = [0xFF, 0xFF, 0xFF],
            Zone3Rgb = [0xFF, 0xFF, 0xFF],
            Zone4Rgb = [0xFF, 0xFF, 0xFF],
            Padding = 0,
            WaveLTR = 0,
            WaveRTL = 0,
            Unused = new byte[13]
        };

        await SendToDeviceAsync(handle, state).ConfigureAwait(false);
    }

    private LENOVO_RGB_KEYBOARD_STATE CreateState(ZoneColors colors)
    {
        return new LENOVO_RGB_KEYBOARD_STATE
        {
            Header = [0xCC, 0x16],
            Effect = 1, // Static mode for manual control
            Speed = 1,
            Brightness = _currentBrightness,
            Zone1Rgb = [colors.Zone1.R, colors.Zone1.G, colors.Zone1.B],
            Zone2Rgb = [colors.Zone2.R, colors.Zone2.G, colors.Zone2.B],
            Zone3Rgb = [colors.Zone3.R, colors.Zone3.G, colors.Zone3.B],
            Zone4Rgb = [colors.Zone4.R, colors.Zone4.G, colors.Zone4.B],
            Padding = 0,
            WaveLTR = 0,
            WaveRTL = 0,
            Unused = new byte[13]
        };
    }

    private static unsafe Task SendToDeviceAsync(SafeFileHandle handle, LENOVO_RGB_KEYBOARD_STATE state) => Task.Run(() =>
    {
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
