using System;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using LenovoLegionToolkit.Lib.Controllers.CustomRGBEffects.SignalProviders;

namespace LenovoLegionToolkit.Lib.Controllers.CustomRGBEffects.Effects;

/// <summary>
/// Fades keyboard brightness based on time since last keypress.
/// Full brightness on keypress, fades to off after idle timeout.
/// Uses Stopwatch-based timing for consistent speed when backgrounded.
/// 
/// Rust origin: fade.rs
/// - Fade time: 20 / speed seconds (speed 1 = 20s, speed 4 = 5s)
/// - Uses profile zone colors (all 4 zones)
/// - Transition: 230 steps, 3ms per step for smooth fade
/// 
/// C# implementation:
/// - Consumes IInputSignalProvider (does NOT own the hook)
/// - Uses TimeSinceLastKeypress for elapsed calculation
/// - Uses zone colors for all 4 zones
/// </summary>
public class FadeEffect : ICustomRGBEffect
{
    private readonly IInputSignalProvider _inputProvider;
    private readonly ZoneColors _zoneColors;
    private readonly double _fadeTimeSeconds;

    /// <inheritdoc />
    public CustomRGBEffectType Type => CustomRGBEffectType.Fade;

    /// <inheritdoc />
    public string Description => "Fades keyboard brightness based on keyboard inactivity (fade.rs)";

    /// <inheritdoc />
    public bool RequiresInputMonitoring => true;

    /// <inheritdoc />
    public bool RequiresSystemAccess => false;

    /// <summary>
    /// Creates a new fade effect matching Rust fade.rs behavior.
    /// </summary>
    /// <param name="inputProvider">Provider for keyboard input signals.</param>
    /// <param name="zoneColors">Colors for all 4 zones at full brightness.</param>
    /// <param name="speed">Speed 1-4 (fade time = 20/speed seconds).</param>
    /// <param name="updateIntervalMs">Ignored - uses Stopwatch-based timing.</param>
    public FadeEffect(
        IInputSignalProvider inputProvider,
        ZoneColors? zoneColors = null,
        int speed = 2,
        int updateIntervalMs = 20)
    {
        _inputProvider = inputProvider;
        _zoneColors = zoneColors ?? ZoneColors.White;
        // From Rust: Duration::from_secs(20 / u64::from(p.speed))
        _fadeTimeSeconds = 20.0 / Math.Clamp(speed, 1, 4);
    }

    /// <inheritdoc />
    public async Task RunAsync(CustomRGBEffectController controller, CancellationToken cancellationToken)
    {
        // Ensure input provider is active
        if (!_inputProvider.IsActive)
            _inputProvider.Start();

        // Track last keypress to detect changes immediately
        var lastKeypressTime = _inputProvider.LastKeypressTimestamp;
        var isFadedOut = false;

        // Stopwatch for consistent update timing
        var stopwatch = Stopwatch.StartNew();
        var nextUpdateMs = 0L;

        while (!cancellationToken.IsCancellationRequested)
            {
                var elapsedMs = stopwatch.ElapsedMilliseconds;

                if (elapsedMs >= nextUpdateMs)
                {
                    var currentKeypressTime = _inputProvider.LastKeypressTimestamp;
                    var elapsedSeconds = _inputProvider.TimeSinceLastKeypress.TotalSeconds;

                    // Check if a new keypress occurred - respond IMMEDIATELY
                    if (currentKeypressTime > lastKeypressTime)
                    {
                        lastKeypressTime = currentKeypressTime;
                        // From Rust: set_colors_to(&p.rgb_array()) - instant full brightness
                        await controller.SetColorsAsync(_zoneColors, cancellationToken).ConfigureAwait(false);
                        isFadedOut = false;
                    }
                    else if (elapsedSeconds > _fadeTimeSeconds && !isFadedOut)
                    {
                        // From Rust: transition_colors_to(&[0; 12], 230, 3) - fade to black
                        await controller.TransitionColorsAsync(ZoneColors.Black, 230, 3, cancellationToken).ConfigureAwait(false);
                        isFadedOut = true;
                    }
                    else if (elapsedSeconds <= _fadeTimeSeconds && !isFadedOut)
                    {
                        // Still within active period, ensure colors are set
                        await controller.SetColorsAsync(_zoneColors, cancellationToken).ConfigureAwait(false);
                    }

                    nextUpdateMs = stopwatch.ElapsedMilliseconds + 20;
                }

            // Use Task.Delay only as a yield mechanism (1ms minimum)
            await Task.Delay(1, cancellationToken).ConfigureAwait(false);
        }
    }
}
