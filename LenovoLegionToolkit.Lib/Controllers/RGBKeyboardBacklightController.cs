// #define MOCK_RGB

using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using LenovoLegionToolkit.Lib.Controllers.CustomRGBEffects;
using LenovoLegionToolkit.Lib.Controllers.CustomRGBEffects.SignalProviders;
using LenovoLegionToolkit.Lib.Controllers.Sensors;
using LenovoLegionToolkit.Lib.Extensions;
using LenovoLegionToolkit.Lib.Settings;
using LenovoLegionToolkit.Lib.SoftwareDisabler;
using LenovoLegionToolkit.Lib.System;
using LenovoLegionToolkit.Lib.System.Management;
using LenovoLegionToolkit.Lib.Utils;
using Microsoft.Win32.SafeHandles;
using NeoSmart.AsyncLock;
using Windows.Win32;

namespace LenovoLegionToolkit.Lib.Controllers
{
    public class RGBKeyboardBacklightController(
        RGBKeyboardSettings settings,
        VantageDisabler vantageDisabler,
        CustomRGBEffectController customEffectController,
        ISensorsController sensorsController)
    {
        private static readonly AsyncLock IoLock = new();

        private SafeFileHandle? _deviceHandle;

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

        public bool ForceDisable { get; set; }

        // --- Performance mode transition state ---
        private CancellationTokenSource? _transitionCts;
        private Task? _transitionTask;

        public Task<bool> IsSupportedAsync()
        {
#if MOCK_RGB
            return Task.FromResult(true);
#else
            return Task.FromResult(DeviceHandle is not null);
#endif
        }

        public async Task SetLightControlOwnerAsync(bool enable, bool restorePreset = false)
        {
            using (await IoLock.LockAsync().ConfigureAwait(false))
            {
                try
                {
#if !MOCK_RGB
                    _ = DeviceHandle ?? throw new InvalidOperationException("RGB Keyboard unsupported");
#endif

                    await ThrowIfVantageEnabled().ConfigureAwait(false);

                    if (Log.Instance.IsTraceEnabled)
                        Log.Instance.Trace($"Taking ownership...");

#if !MOCK_RGB
                    await WMI.LenovoGameZoneData.SetLightControlOwnerAsync(enable ? 1 : 0).ConfigureAwait(false);
#endif

                    if (Log.Instance.IsTraceEnabled)
                        Log.Instance.Trace($"Ownership set to {enable}, restoring profile...");

                    if (restorePreset)
                    {
                        if (Log.Instance.IsTraceEnabled)
                            Log.Instance.Trace($"Restoring preset...");

                        await SetCurrentPresetAsync().ConfigureAwait(false);

                        if (Log.Instance.IsTraceEnabled)
                            Log.Instance.Trace($"Restored preset");
                    }
                }
                catch (Exception ex)
                {
                    if (Log.Instance.IsTraceEnabled)
                        Log.Instance.Trace($"Can't take ownership.", ex);

                    throw;
                }
            }
        }

        public async Task<RGBKeyboardBacklightState> GetStateAsync()
        {
            using (await IoLock.LockAsync().ConfigureAwait(false))
            {
#if !MOCK_RGB
                _ = DeviceHandle ?? throw new InvalidOperationException("RGB Keyboard unsupported");
#endif

                await ThrowIfVantageEnabled().ConfigureAwait(false);

                return settings.Store.State;
            }
        }

        public async Task SetStateAsync(RGBKeyboardBacklightState state)
        {
            using (await IoLock.LockAsync().ConfigureAwait(false))
            {
#if !MOCK_RGB
                _ = DeviceHandle ?? throw new InvalidOperationException("RGB Keyboard unsupported");
#endif

                await ThrowIfVantageEnabled().ConfigureAwait(false);

                settings.Store.State = state;
                settings.SynchronizeStore();

                var selectedPreset = state.SelectedPreset;

                if (Log.Instance.IsTraceEnabled)
                    Log.Instance.Trace($"Selected preset: {selectedPreset}");

                LENOVO_RGB_KEYBOARD_STATE str;
                RGBKeyboardBacklightBacklightPresetDescription presetDescription;

                if (selectedPreset == RGBKeyboardBacklightPreset.Off)
                {
                    if (Log.Instance.IsTraceEnabled)
                        Log.Instance.Trace($"Creating off state.");

                    str = CreateOffState();

                    // Stop any custom effect when turning off
                    await customEffectController.StopEffectAsync().ConfigureAwait(false);

                    await SendToDevice(str).ConfigureAwait(false);
                    return;
                }

                presetDescription = state.Presets.GetValueOrDefault(selectedPreset, RGBKeyboardBacklightBacklightPresetDescription.Default);

                if (Log.Instance.IsTraceEnabled)
                    Log.Instance.Trace($"Creating state: {presetDescription}");

                str = Convert(presetDescription);

                await SendToDevice(str).ConfigureAwait(false);

                // Start custom effect if applicable
                await HandleCustomEffectAsync(presetDescription).ConfigureAwait(false);
            }
        }

        public async Task SetPresetAsync(RGBKeyboardBacklightPreset preset)
        {
            using (await IoLock.LockAsync().ConfigureAwait(false))
            {
#if !MOCK_RGB
                _ = DeviceHandle ?? throw new InvalidOperationException("RGB Keyboard unsupported");
#endif

                await ThrowIfVantageEnabled().ConfigureAwait(false);

                var state = settings.Store.State;
                var presets = state.Presets;

                settings.Store.State = new(preset, presets);
                settings.SynchronizeStore();

                if (Log.Instance.IsTraceEnabled)
                    Log.Instance.Trace($"Preset is {preset}.");

                LENOVO_RGB_KEYBOARD_STATE str;
                RGBKeyboardBacklightBacklightPresetDescription presetDescription;

                if (preset == RGBKeyboardBacklightPreset.Off)
                {
                    if (Log.Instance.IsTraceEnabled)
                        Log.Instance.Trace($"Creating off state.");

                    str = CreateOffState();

                    // Stop any custom effect when turning off
                    await customEffectController.StopEffectAsync().ConfigureAwait(false);

                    await SendToDevice(str).ConfigureAwait(false);
                    return;
                }

                presetDescription = state.Presets.GetValueOrDefault(preset, RGBKeyboardBacklightBacklightPresetDescription.Default);

                if (Log.Instance.IsTraceEnabled)
                    Log.Instance.Trace($"Creating state: {presetDescription}");

                str = Convert(presetDescription);

                await SendToDevice(str).ConfigureAwait(false);

                // Start custom effect if applicable
                await HandleCustomEffectAsync(presetDescription).ConfigureAwait(false);
            }
        }

        public async Task<RGBKeyboardBacklightPreset> SetNextPresetAsync()
        {
            using (await IoLock.LockAsync().ConfigureAwait(false))
            {
#if !MOCK_RGB
                _ = DeviceHandle ?? throw new InvalidOperationException("RGB Keyboard unsupported");
#endif

                await ThrowIfVantageEnabled().ConfigureAwait(false);

                var state = settings.Store.State;

                var newPreset = state.SelectedPreset.Next();
                var presets = state.Presets;

                settings.Store.State = new(newPreset, presets);
                settings.SynchronizeStore();

                if (Log.Instance.IsTraceEnabled)
                    Log.Instance.Trace($"New preset is {newPreset}.");

                LENOVO_RGB_KEYBOARD_STATE str;
                RGBKeyboardBacklightBacklightPresetDescription presetDescription;

                if (newPreset == RGBKeyboardBacklightPreset.Off)
                {
                    if (Log.Instance.IsTraceEnabled)
                        Log.Instance.Trace($"Creating off state.");

                    str = CreateOffState();

                    // Stop any custom effect when turning off
                    await customEffectController.StopEffectAsync().ConfigureAwait(false);

                    await SendToDevice(str).ConfigureAwait(false);
                    return newPreset;
                }

                presetDescription = state.Presets.GetValueOrDefault(newPreset, RGBKeyboardBacklightBacklightPresetDescription.Default);

                if (Log.Instance.IsTraceEnabled)
                    Log.Instance.Trace($"Creating state: {presetDescription}");

                str = Convert(presetDescription);

                await SendToDevice(str).ConfigureAwait(false);

                // Start custom effect if applicable
                await HandleCustomEffectAsync(presetDescription).ConfigureAwait(false);

                return newPreset;
            }
        }

        private async Task SetCurrentPresetAsync()
        {
#if !MOCK_RGB
            _ = DeviceHandle ?? throw new InvalidOperationException("RGB Keyboard unsupported");
#endif

            await ThrowIfVantageEnabled().ConfigureAwait(false);

            var state = settings.Store.State;

            var preset = state.SelectedPreset;

            if (Log.Instance.IsTraceEnabled)
                Log.Instance.Trace($"Current preset is {preset}.");

            LENOVO_RGB_KEYBOARD_STATE str;
            RGBKeyboardBacklightBacklightPresetDescription presetDescription;

            if (preset == RGBKeyboardBacklightPreset.Off)
            {
                if (Log.Instance.IsTraceEnabled)
                    Log.Instance.Trace($"Creating off state.");

                str = CreateOffState();

                // Stop any custom effect when turning off
                await customEffectController.StopEffectAsync().ConfigureAwait(false);

                await SendToDevice(str).ConfigureAwait(false);

                // Notify preview: keyboard is off (all black)
                customEffectController.RaisePreviewFrame(ZoneColors.Black);
                return;
            }

            presetDescription = state.Presets.GetValueOrDefault(preset, RGBKeyboardBacklightBacklightPresetDescription.Default);

            if (Log.Instance.IsTraceEnabled)
                Log.Instance.Trace($"Creating state: {presetDescription}");

            str = Convert(presetDescription);

            await SendToDevice(str).ConfigureAwait(false);

            // Notify preview for firmware-driven presets (Static/Breath/Wave/Smooth)
            // Custom effects fire their own PreviewFrame from the effect loop.
            if (!presetDescription.Effect.IsCustomEffect())
            {
                customEffectController.RaisePreviewFrame(new ZoneColors
                {
                    Zone1 = presetDescription.Zone1,
                    Zone2 = presetDescription.Zone2,
                    Zone3 = presetDescription.Zone3,
                    Zone4 = presetDescription.Zone4
                });
            }

            // Start custom effect if applicable
            await HandleCustomEffectAsync(presetDescription).ConfigureAwait(false);
        }

        private async Task ThrowIfVantageEnabled()
        {
            var vantageStatus = await vantageDisabler.GetStatusAsync().ConfigureAwait(false);
            if (vantageStatus == SoftwareStatus.Enabled)
                throw new InvalidOperationException("Can't manage RGB keyboard with Vantage enabled");
        }

        private unsafe Task SendToDevice(LENOVO_RGB_KEYBOARD_STATE str) => Task.Run(() =>
        {
#if !MOCK_RGB
            var handle = DeviceHandle ?? throw new InvalidOperationException("RGB Keyboard unsupported");

            var ptr = IntPtr.Zero;
            try
            {
                var size = Marshal.SizeOf<LENOVO_RGB_KEYBOARD_STATE>();
                ptr = Marshal.AllocHGlobal(size);
                Marshal.StructureToPtr(str, ptr, false);

                if (!PInvoke.HidD_SetFeature(handle, ptr.ToPointer(), (uint)size))
                    PInvokeExtensions.ThrowIfWin32Error("HidD_SetFeature");
            }
            finally
            {
                Marshal.FreeHGlobal(ptr);
            }
#endif
        });

        private static LENOVO_RGB_KEYBOARD_STATE CreateOffState()
        {
            return new()
            {
                Header = [0xCC, 0x16],
                Unused = new byte[13],
                Padding = 0,
                Effect = 0,
                WaveLTR = 0,
                WaveRTL = 0,
                Brightness = 0,
                Zone1Rgb = new byte[3],
                Zone2Rgb = new byte[3],
                Zone3Rgb = new byte[3],
                Zone4Rgb = new byte[3],
            };
        }

        private static LENOVO_RGB_KEYBOARD_STATE Convert(RGBKeyboardBacklightBacklightPresetDescription preset)
        {
            // For custom effects, use Static mode as base - the CustomRGBEffectController handles the animation
            var effectForHardware = preset.Effect.IsCustomEffect() ? RGBKeyboardBacklightEffect.Static : preset.Effect;

            var result = new LENOVO_RGB_KEYBOARD_STATE
            {
                Header = [0xCC, 0x16],
                Unused = new byte[13],
                Padding = 0x0,
                Zone1Rgb = [0xFF, 0xFF, 0xFF],
                Zone2Rgb = [0xFF, 0xFF, 0xFF],
                Zone3Rgb = [0xFF, 0xFF, 0xFF],
                Zone4Rgb = [0xFF, 0xFF, 0xFF],
                Effect = effectForHardware switch
                {
                    RGBKeyboardBacklightEffect.Static => 1,
                    RGBKeyboardBacklightEffect.Breath => 3,
                    RGBKeyboardBacklightEffect.WaveRTL => 4,
                    RGBKeyboardBacklightEffect.WaveLTR => 4,
                    RGBKeyboardBacklightEffect.Smooth => 6,
                    _ => 1 // Default to static for custom effects
                },
                WaveRTL = (byte)(effectForHardware == RGBKeyboardBacklightEffect.WaveRTL ? 1 : 0),
                WaveLTR = (byte)(effectForHardware == RGBKeyboardBacklightEffect.WaveLTR ? 1 : 0),
                Brightness = preset.Brightness switch
                {
                    RGBKeyboardBacklightBrightness.Low => 1,
                    RGBKeyboardBacklightBrightness.High => 2,
                    _ => 0
                }
            };


            if (effectForHardware != RGBKeyboardBacklightEffect.Static)
            {
                result.Speed = preset.Speed switch
                {
                    RGBKeyboardBacklightSpeed.Slowest => 1,
                    RGBKeyboardBacklightSpeed.Slow => 2,
                    RGBKeyboardBacklightSpeed.Fast => 3,
                    RGBKeyboardBacklightSpeed.Fastest => 4,
                    _ => 0
                };
            }

            if (effectForHardware is RGBKeyboardBacklightEffect.Static or RGBKeyboardBacklightEffect.Breath)
            {
                result.Zone1Rgb = [preset.Zone1.R, preset.Zone1.G, preset.Zone1.B];
                result.Zone2Rgb = [preset.Zone2.R, preset.Zone2.G, preset.Zone2.B];
                result.Zone3Rgb = [preset.Zone3.R, preset.Zone3.G, preset.Zone3.B];
                result.Zone4Rgb = [preset.Zone4.R, preset.Zone4.G, preset.Zone4.B];
            }

            return result;
        }

        /// <summary>
        /// Plays a premium performance-mode transition animation (3 breathing pulses + fade to black).
        /// Pauses the current effect, plays the animation, then seamlessly resumes.
        /// Double-trigger safe: a new call cancels any running transition.
        /// </summary>
        public async Task PlayTransitionAsync(PowerModeState mode)
        {
            var modeColor = mode switch
            {
                PowerModeState.Quiet => new RGBColor(0, 120, 255),       // Blue
                PowerModeState.Balance => new RGBColor(255, 255, 255),   // White
                PowerModeState.Performance => new RGBColor(255, 0, 0),   // Red
                PowerModeState.GodMode => new RGBColor(180, 0, 255),     // Purple
                _ => new RGBColor(255, 255, 255)
            };

            // Cancel any in-flight transition
            if (_transitionCts is not null)
            {
                await _transitionCts.CancelAsync().ConfigureAwait(false);
                if (_transitionTask is not null)
                {
                    try { await _transitionTask.ConfigureAwait(false); }
                    catch (OperationCanceledException) { }
                }
                _transitionCts.Dispose();
            }

            // Pause running custom effect (it stays alive in memory)
            customEffectController.IsOverrideActive = true;

            _transitionCts = new CancellationTokenSource();
            _transitionTask = RunTransitionAsync(modeColor, _transitionCts.Token);

            if (Log.Instance.IsTraceEnabled)
                Log.Instance.Trace($"Transition triggered for power mode: {mode}");
        }

        private async Task RunTransitionAsync(RGBColor modeColor, CancellationToken cancellationToken)
        {
            try
            {
#if !MOCK_RGB
                var handle = DeviceHandle ?? throw new InvalidOperationException("RGB Keyboard unsupported");
                await PerformanceModeTransitionEffect.PlayAsync(
                    handle,
                    modeColor,
                    cancellationToken,
                    onFrameRendered: frameColor =>
                    {
                        // Notify preview with uniform zone colors for every strobe frame
                        customEffectController.RaisePreviewFrame(new ZoneColors
                        {
                            Zone1 = frameColor,
                            Zone2 = frameColor,
                            Zone3 = frameColor,
                            Zone4 = frameColor
                        });
                    })
                    .ConfigureAwait(false);

                // ── Final black frame: ensure HID buffer is black ──
                // PlayAsync already ends on black after the 0.5 s hold, but
                // we send one more explicit black frame to guarantee the
                // controller buffer is zeroed.  NO delay after this — the
                // profile effect must resume in the very next operation so
                // the HID controller never idles (which causes a white flash).
                await PerformanceModeTransitionEffect.SendBlackFrame(handle).ConfigureAwait(false);
                customEffectController.RaisePreviewFrame(new ZoneColors
                {
                    Zone1 = new RGBColor(0, 0, 0),
                    Zone2 = new RGBColor(0, 0, 0),
                    Zone3 = new RGBColor(0, 0, 0),
                    Zone4 = new RGBColor(0, 0, 0)
                });
#else
                await Task.Delay(3500, cancellationToken).ConfigureAwait(false);
#endif
            }
            catch (OperationCanceledException) { }
            finally
            {
                // Resume the current preset WITHOUT the zone-colour flash.
                //
                // For custom (software-driven) effects the standard
                // SetCurrentPresetAsync() writes the preset's static zone
                // colours as an HID frame BEFORE starting the effect loop.
                // That produces a visible 1-frame flash of the preset palette
                // between the black hold and the first effect frame.
                //
                // Fix: keep the keyboard black, start the effect loop while
                // the override is still active (so its warm-up frames are
                // gated), then lift the override so the very first HID write
                // comes from the effect itself — no intermediate preset frame.
                try
                {
                    await ResumeAfterTransitionAsync().ConfigureAwait(false);
                }
                catch (Exception ex)
                {
                    if (Log.Instance.IsTraceEnabled)
                        Log.Instance.Trace($"Failed to resume after transition", ex);
                }

                if (Log.Instance.IsTraceEnabled)
                    Log.Instance.Trace($"Transition ended, effect resumed");
            }
        }

        /// <summary>
        /// Resumes the current RGB preset after a transition animation ends.
        /// For firmware-driven effects (Static, Breath, Wave) the HID preset
        /// state is written directly (the firmware handles the animation).
        /// For custom (software-driven) effects the keyboard stays black and
        /// the effect loop is restarted while <see cref="CustomRGBEffectController.IsOverrideActive"/>
        /// is still <c>true</c>; the override is lifted only after the loop
        /// is running so the first HID frame comes from the effect, not from
        /// a static preset write.
        /// </summary>
        private async Task ResumeAfterTransitionAsync()
        {
            await ThrowIfVantageEnabled().ConfigureAwait(false);

            var state = settings.Store.State;
            var preset = state.SelectedPreset;

            if (preset == RGBKeyboardBacklightPreset.Off)
            {
                customEffectController.IsOverrideActive = false;
                await customEffectController.StopEffectAsync().ConfigureAwait(false);
                await SendToDevice(CreateOffState()).ConfigureAwait(false);
                return;
            }

            var presetDescription = state.Presets.GetValueOrDefault(
                preset, RGBKeyboardBacklightBacklightPresetDescription.Default);

            if (presetDescription.Effect.IsCustomEffect())
            {
                // ── Custom effect path (zero-gap resume) ─────────────────
                // The effect loop was never stopped — PlayTransitionAsync
                // only set IsOverrideActive = true to gate HID writes.
                // The loop has been computing frames the whole time,
                // storing results in CurrentColors.
                //
                // ResumeFromOverrideAsync lifts the gate AND immediately
                // pushes the last computed frame to HID in one operation,
                // so the controller never idles between the black frame
                // and the first effect frame (which would show white).
                //
                // We do NOT call HandleCustomEffectAsync here because
                // StartEffectAsync → SetStaticModeAsync writes 0xFF white
                // to all zones as part of taking ownership — that IS the
                // white flash.
                if (customEffectController.IsEffectRunning)
                {
                    await customEffectController.ResumeFromOverrideAsync().ConfigureAwait(false);
                }
                else
                {
                    // Edge case: effect was not running (e.g. app restart
                    // during transition).  Must start fresh.
                    customEffectController.IsOverrideActive = false;
                    await HandleCustomEffectAsync(presetDescription).ConfigureAwait(false);
                }
            }
            else
            {
                // ── Firmware-driven effect path (Static/Breath/Wave) ─────
                // The preset HID write IS the desired state — no software
                // loop is involved so there is no flash risk.
                customEffectController.IsOverrideActive = false;
                await SendToDevice(Convert(presetDescription)).ConfigureAwait(false);
                await HandleCustomEffectAsync(presetDescription).ConfigureAwait(false);
            }
        }

        private async Task HandleCustomEffectAsync(RGBKeyboardBacklightBacklightPresetDescription preset)
        {
            // Stop any running custom effect first
            await customEffectController.StopEffectAsync().ConfigureAwait(false);

            if (!preset.Effect.IsCustomEffect())
                return;

            var customEffectType = preset.Effect.ToCustomEffectType();
            if (customEffectType is null)
                return;

            // Set brightness from preset (1=Low, 2=High) - must be set before starting effect
            customEffectController.CurrentBrightness = preset.Brightness switch
            {
                RGBKeyboardBacklightBrightness.Low => 1,
                RGBKeyboardBacklightBrightness.High => 2,
                _ => 2
            };

            // Create zone colors from preset
            var zoneColors = new ZoneColors
            {
                Zone1 = preset.Zone1,
                Zone2 = preset.Zone2,
                Zone3 = preset.Zone3,
                Zone4 = preset.Zone4
            };

            // Map speed (1-4) from RGBKeyboardBacklightSpeed
            var speed = preset.Speed switch
            {
                RGBKeyboardBacklightSpeed.Slowest => 1,
                RGBKeyboardBacklightSpeed.Slow => 2,
                RGBKeyboardBacklightSpeed.Fast => 3,
                RGBKeyboardBacklightSpeed.Fastest => 4,
                _ => 2
            };

            // Create input/screen providers for effects that need them
            IInputSignalProvider? inputProvider = null;
            IScreenColorProvider? screenProvider = null;

            if (preset.Effect.RequiresInputProvider())
                inputProvider = CustomRGBEffectFactory.CreateInputProvider();

            if (preset.Effect.RequiresScreenProvider())
                screenProvider = CustomRGBEffectFactory.CreateScreenProvider();

            try
            {
                var effect = CustomRGBEffectFactory.CreateByType(
                    customEffectType.Value,
                    zoneColors,
                    speed,
                    EffectDirection.Right,
                    sensorsController,
                    inputProvider,
                    screenProvider);

                await customEffectController.StartEffectAsync(effect).ConfigureAwait(false);

                if (Log.Instance.IsTraceEnabled)
                    Log.Instance.Trace($"Started custom effect: {customEffectType}");
            }
            catch (Exception ex)
            {
                if (Log.Instance.IsTraceEnabled)
                    Log.Instance.Trace($"Failed to start custom effect {customEffectType}", ex);

                // Clean up providers on failure
                inputProvider?.Dispose();
                screenProvider?.Dispose();
            }
        }
    }
}
