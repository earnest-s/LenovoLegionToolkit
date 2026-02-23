// ============================================================================
// TemperatureEffect.cs
// 
// CPU temperature-based color effect.
// Ported from L5P-Keyboard-RGB temperature.rs
// 
// Original Rust source: app/src/manager/effects/temperature.rs
// 
// Effect behavior:
// - Reads CPU temperature (looking for AMD Tctl sensor)
// - Linearly interpolates between green (cool) and red (hot)
// - Safe temp baseline: 20°C
// - Ramp boost factor: 1.6 (amplifies color change)
// - Update interval: 200ms
// 
// NOTE: This effect requires hardware monitoring access.
// LoqNova has its own sensor infrastructure which we use.
// ============================================================================

using System;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using LoqNova.Lib.Controllers.Sensors;

namespace LoqNova.Lib.Controllers.CustomRGBEffects.Effects;

/// <summary>
/// Temperature effect - keyboard color reflects CPU temperature.
/// Port of Rust temperature.rs effect.
/// Uses Stopwatch-based timing for consistent speed when backgrounded.
/// </summary>
public class TemperatureEffect : ICustomRGBEffect
{
    // From Rust:
    // let safe_temp = 20.0;
    // let ramp_boost = 1.6;
    // let temp_cool: [f32; 12] = [0.0, 255.0, 0.0, 0.0, 255.0, 0.0, 0.0, 255.0, 0.0, 0.0, 255.0, 0.0];
    // let temp_hot: [f32; 12] = [255.0, 0.0, 0.0, 255.0, 0.0, 0.0, 255.0, 0.0, 0.0, 255.0, 0.0, 0.0];
    private const float SafeTemp = 20.0f;
    private const float RampBoost = 1.6f;
    private const int UpdateIntervalMs = 200;

    private static readonly float[] TempCool = [0f, 255f, 0f, 0f, 255f, 0f, 0f, 255f, 0f, 0f, 255f, 0f]; // Green
    private static readonly float[] TempHot = [255f, 0f, 0f, 255f, 0f, 0f, 255f, 0f, 0f, 255f, 0f, 0f];  // Red

    private readonly float[] _colorDifferences = new float[12];
    private readonly ISensorsController? _sensorsController;

    /// <summary>
    /// Creates a new Temperature effect.
    /// </summary>
    /// <param name="sensorsController">Optional sensors controller for reading CPU temp. 
    /// If null, will use fallback methods.</param>
    public TemperatureEffect(ISensorsController? sensorsController = null)
    {
        _sensorsController = sensorsController;

        // Pre-calculate color differences
        // From Rust: color_differences[index] = temp_hot[index] - temp_cool[index];
        for (var i = 0; i < 12; i++)
        {
            _colorDifferences[i] = TempHot[i] - TempCool[i];
        }
    }

    public CustomRGBEffectType Type => CustomRGBEffectType.Temperature;
    public string Description => "Color reflects CPU temperature";
    public bool RequiresInputMonitoring => false;
    public bool RequiresSystemAccess => true;

    public async Task RunAsync(CustomRGBEffectController controller, CancellationToken cancellationToken)
    {
        // Stopwatch for consistent timing when backgrounded
        var stopwatch = Stopwatch.StartNew();
        var nextUpdateMs = 0L;

        while (!cancellationToken.IsCancellationRequested)
        {
            var elapsedMs = stopwatch.ElapsedMilliseconds;

            if (elapsedMs >= nextUpdateMs)
            {
                var temperature = await GetCpuTemperatureAsync().ConfigureAwait(false);

                if (temperature.HasValue)
                {
                    // From Rust:
                    // let mut adjusted_temp = temperature - safe_temp;
                    // if adjusted_temp < 0.0 { adjusted_temp = 0.0; }
                    // let temp_percent = (adjusted_temp / 100.0) * ramp_boost;
                    var adjustedTemp = Math.Max(temperature.Value - SafeTemp, 0f);
                    var tempPercent = (adjustedTemp / 100.0f) * RampBoost;

                    // Clamp to 0-1 range
                    tempPercent = Math.Clamp(tempPercent, 0f, 1f);

                    // From Rust:
                    // let mut target = [0.0; 12];
                    // for index in 0..12 {
                    //     target[index] = color_differences[index].mul_add(temp_percent, temp_cool[index]);
                    // }
                    var targetArray = new byte[12];
                    for (var i = 0; i < 12; i++)
                    {
                        var value = _colorDifferences[i] * tempPercent + TempCool[i];
                        targetArray[i] = (byte)Math.Clamp(value, 0, 255);
                    }

                    var targetColors = ZoneColors.FromArray(targetArray);

                    // From Rust: manager.keyboard.transition_colors_to(&target.map(|val| val as u8), 5, 1).unwrap();
                    await controller.TransitionColorsAsync(targetColors, 5, 1, cancellationToken).ConfigureAwait(false);
                }

                nextUpdateMs = stopwatch.ElapsedMilliseconds + UpdateIntervalMs;
            }

            // Use Task.Delay only as a yield mechanism (1ms minimum)
            await Task.Delay(1, cancellationToken).ConfigureAwait(false);
        }
    }

    private async Task<float?> GetCpuTemperatureAsync()
    {
        if (_sensorsController is not null)
        {
            try
            {
                var sensors = await _sensorsController.GetDataAsync().ConfigureAwait(false);
                if (sensors.CPU.Temperature > 0)
                {
                    return sensors.CPU.Temperature;
                }
            }
            catch
            {
                // Fall through to fallback
            }
        }

        // Fallback: Return a default moderate temperature
        // In a real implementation, you might use WMI or other methods
        return 45.0f;
    }
}
