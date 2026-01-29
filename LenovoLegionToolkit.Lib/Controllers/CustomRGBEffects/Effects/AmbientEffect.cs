using System;
using System.Diagnostics;
using System.Drawing;
using System.Threading;
using System.Threading.Tasks;
using LenovoLegionToolkit.Lib.Controllers.CustomRGBEffects.SignalProviders;

namespace LenovoLegionToolkit.Lib.Controllers.CustomRGBEffects.Effects;

/// <summary>
/// Syncs keyboard lighting with screen colors for ambient lighting.
/// Each keyboard zone matches the corresponding screen region.
/// Uses Stopwatch-based timing for consistent speed when backgrounded.
/// 
/// Rust origin: ambient.rs
/// - Captures screen using scrap crate
/// - Resizes to 4x1 pixels (one per zone)
/// - Applies saturation boost via photon_rs::colour_spaces::saturate_hsv
/// - Runs at fixed FPS (30 default)
/// 
/// C# implementation:
/// - Forces 100% saturation for vibrant colors
/// - Locked to 30 FPS (matching Rust default)
/// - Uses DXGI Desktop Duplication API
/// - No adaptive FPS or smoothing - direct color mapping
/// </summary>
public class AmbientEffect : ICustomRGBEffect
{
    private readonly IScreenColorProvider _screenProvider;

    // Fixed 30 FPS, no smoothing, 100% saturation - matching Rust behavior
    private const int UpdateIntervalMs = 33; // ~30 FPS

    /// <inheritdoc />
    public CustomRGBEffectType Type => CustomRGBEffectType.Ambient;

    /// <inheritdoc />
    public string Description => "Syncs keyboard lighting with screen colors (ambient.rs)";

    /// <inheritdoc />
    public bool RequiresInputMonitoring => false;

    /// <inheritdoc />
    public bool RequiresSystemAccess => true; // Requires screen capture

    /// <summary>
    /// Creates a new ambient effect matching Rust ambient.rs behavior.
    /// Forces 100% saturation and 30 FPS.
    /// </summary>
    /// <param name="screenProvider">Provider for screen color samples.</param>
    public AmbientEffect(IScreenColorProvider screenProvider)
    {
        _screenProvider = screenProvider;
    }

    /// <summary>
    /// Legacy constructor for backward compatibility - ignored parameters.
    /// </summary>
    [Obsolete("Use single-parameter constructor. Saturation is locked to 100%, FPS to 30.")]
    public AmbientEffect(
        IScreenColorProvider screenProvider,
        bool useSmoothing,
        double smoothingFactor,
        int updateIntervalMs,
        double saturationMultiplier) : this(screenProvider) { }

    /// <inheritdoc />
    public async Task RunAsync(CustomRGBEffectController controller, CancellationToken cancellationToken)
    {
        // Start screen capture if not already running
        if (!_screenProvider.IsCapturing)
            await _screenProvider.StartAsync(cancellationToken).ConfigureAwait(false);

        // Stopwatch for consistent timing when backgrounded
        var stopwatch = Stopwatch.StartNew();
        var nextUpdateMs = 0L;

        try
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                var elapsedMs = stopwatch.ElapsedMilliseconds;

                if (elapsedMs >= nextUpdateMs)
                {
                    // Get latest zone colors from screen (System.Drawing.Color)
                    var capturedColors = _screenProvider.LastCapturedZoneColors;

                    // Apply 100% saturation to each color
                    var outputColors = new RGBColor[4];
                    for (var i = 0; i < 4; i++)
                    {
                        outputColors[i] = MaximizeSaturation(capturedColors[i]);
                    }

                    var zoneColors = ZoneColors.FromRGBColors(outputColors);
                    await controller.SetColorsAsync(zoneColors, cancellationToken).ConfigureAwait(false);

                    nextUpdateMs = elapsedMs + UpdateIntervalMs;
                }

                // Use Task.Delay only as a yield mechanism (1ms minimum)
                await Task.Delay(1, cancellationToken).ConfigureAwait(false);
            }
        }
        finally
        {
            _screenProvider.Stop();
        }
    }

    /// <summary>
    /// Maximizes saturation of a color while preserving hue and value.
    /// Matches Rust: photon_rs::colour_spaces::saturate_hsv with full boost.
    /// 
    /// Uses HSV (not HSL) to match Rust behavior:
    /// - HSV saturation affects how vivid the color is
    /// - Value (brightness) is preserved
    /// </summary>
    private static RGBColor MaximizeSaturation(Color color)
    {
        // Convert RGB to HSV
        var r = color.R / 255.0;
        var g = color.G / 255.0;
        var b = color.B / 255.0;

        var max = Math.Max(r, Math.Max(g, b));
        var min = Math.Min(r, Math.Min(g, b));
        var delta = max - min;

        // V = max (value/brightness)
        var v = max;

        // If V is 0, color is black - no saturation possible
        if (v < 0.0001)
        {
            return new RGBColor(0, 0, 0);
        }

        // Calculate Hue
        double h = 0;
        if (delta > 0.0001)
        {
            if (max == r)
                h = 60.0 * (((g - b) / delta) % 6);
            else if (max == g)
                h = 60.0 * ((b - r) / delta + 2);
            else
                h = 60.0 * ((r - g) / delta + 4);

            if (h < 0) h += 360;
        }

        // Force saturation to 100% in HSV
        const double s = 1.0;

        // Convert HSV back to RGB
        // With S=1 and V=max, the formula simplifies
        var c = v * s; // chroma = V * S
        var x = c * (1 - Math.Abs((h / 60) % 2 - 1));
        var m = v - c;

        double r1, g1, b1;
        if (h < 60) { r1 = c; g1 = x; b1 = 0; }
        else if (h < 120) { r1 = x; g1 = c; b1 = 0; }
        else if (h < 180) { r1 = 0; g1 = c; b1 = x; }
        else if (h < 240) { r1 = 0; g1 = x; b1 = c; }
        else if (h < 300) { r1 = x; g1 = 0; b1 = c; }
        else { r1 = c; g1 = 0; b1 = x; }

        return new RGBColor(
            (byte)Math.Clamp((int)((r1 + m) * 255 + 0.5), 0, 255),
            (byte)Math.Clamp((int)((g1 + m) * 255 + 0.5), 0, 255),
            (byte)Math.Clamp((int)((b1 + m) * 255 + 0.5), 0, 255));
    }
}
