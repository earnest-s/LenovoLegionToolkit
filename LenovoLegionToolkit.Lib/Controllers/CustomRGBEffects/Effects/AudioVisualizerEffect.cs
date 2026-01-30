// ============================================================================
// AudioVisualizerEffect.cs
// 
// Bidirectional wave-based audio visualizer for 4-zone keyboards.
// Zones oscillate like audio waves, reacting dynamically to live audio.
// ============================================================================

using System;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using NAudio.CoreAudioApi;

namespace LenovoLegionToolkit.Lib.Controllers.CustomRGBEffects.Effects;

/// <summary>
/// Audio visualizer effect with bidirectional wave motion.
/// Zones oscillate based on audio energy with smooth sine-wave animation.
/// </summary>
public class AudioVisualizerEffect : ICustomRGBEffect, IDisposable
{
    private readonly int _speed;
    private readonly ZoneColors _zoneColors;
    private readonly MMDeviceEnumerator _deviceEnumerator;
    private MMDevice? _audioDevice;
    private bool _disposed;

    // Zone brightness levels (0-1) for smooth transitions
    private readonly double[] _zoneBrightness = new double[4];
    
    // Wave phase offsets per zone for natural motion
    private readonly double[] _zonePhaseOffsets = { 0, 0.5, 1.0, 1.5 };
    
    // Audio energy tracking
    private double _smoothedEnergy;
    private double _peakEnergy;
    private double _wavePhase;
    
    // Wave parameters
    private const double BaseWaveFrequency = 1.5;     // Base oscillation Hz
    private const double MaxWaveFrequency = 6.0;      // Max oscillation Hz at peak audio
    private const double EnergySmoothing = 0.12;      // Audio smoothing factor
    private const double PeakDecay = 0.8;             // Peak energy decay per second
    private const double BrightnessDecay = 4.0;       // Brightness decay rate per second
    private const double MinBrightness = 0.05;        // Minimum idle brightness
    private const double WaveSpread = 0.7;            // How much wave affects adjacent zones

    public AudioVisualizerEffect(ZoneColors? zoneColors = null, int speed = 2)
    {
        _speed = Math.Clamp(speed, 1, 4);
        _zoneColors = zoneColors ?? new ZoneColors(
            new RGBColor(255, 0, 0),    // Zone 0: Red
            new RGBColor(255, 127, 0),  // Zone 1: Orange
            new RGBColor(0, 255, 0),    // Zone 2: Green
            new RGBColor(0, 127, 255)   // Zone 3: Blue
        );
        _deviceEnumerator = new MMDeviceEnumerator();
    }

    public CustomRGBEffectType Type => CustomRGBEffectType.AudioVisualizer;
    public string Description => "Bidirectional wave-based audio visualizer";
    public bool RequiresInputMonitoring => false;
    public bool RequiresSystemAccess => true;

    public async Task RunAsync(CustomRGBEffectController controller, CancellationToken cancellationToken)
    {
        try
        {
            _audioDevice = _deviceEnumerator.GetDefaultAudioEndpoint(DataFlow.Render, Role.Multimedia);
        }
        catch
        {
            await RunIdleWaveAsync(controller, cancellationToken).ConfigureAwait(false);
            return;
        }

        if (_audioDevice?.AudioMeterInformation == null)
        {
            await RunIdleWaveAsync(controller, cancellationToken).ConfigureAwait(false);
            return;
        }

        var meterInfo = _audioDevice.AudioMeterInformation;
        var stopwatch = Stopwatch.StartNew();
        var lastTime = 0.0;
        
        // Speed multiplier from slider (1-4 maps to 0.5 - 1.25)
        var speedMultiplier = 0.25 + (_speed * 0.25);

        while (!cancellationToken.IsCancellationRequested)
        {
            var currentTime = stopwatch.Elapsed.TotalSeconds;
            var deltaTime = Math.Min(currentTime - lastTime, 0.1); // Cap delta to avoid jumps
            lastTime = currentTime;

            try
            {
                // Get audio energy (0-1)
                var rawEnergy = meterInfo.MasterPeakValue;
                
                // Smooth the energy for base animation
                _smoothedEnergy += (rawEnergy - _smoothedEnergy) * EnergySmoothing;
                _smoothedEnergy = Math.Clamp(_smoothedEnergy, 0, 1);
                
                // Track peak energy for amplitude (decays over time)
                if (rawEnergy > _peakEnergy)
                {
                    _peakEnergy = rawEnergy;
                }
                else
                {
                    _peakEnergy -= PeakDecay * deltaTime;
                    _peakEnergy = Math.Max(_peakEnergy, _smoothedEnergy * 0.5);
                }
                
                // Calculate wave frequency based on audio energy
                var waveFrequency = BaseWaveFrequency + (_smoothedEnergy * (MaxWaveFrequency - BaseWaveFrequency));
                waveFrequency *= speedMultiplier;
                
                // Advance wave phase
                _wavePhase += waveFrequency * deltaTime * Math.PI * 2;
                if (_wavePhase > Math.PI * 20) _wavePhase -= Math.PI * 20; // Prevent overflow
                
                // Calculate wave amplitude based on peak energy
                var waveAmplitude = 0.3 + (_peakEnergy * 0.7);
                
                // Calculate target brightness for each zone using wave function
                var targetBrightness = new double[4];
                
                for (var i = 0; i < 4; i++)
                {
                    // Each zone has a phase offset for wave propagation
                    var zonePhase = _wavePhase + (_zonePhaseOffsets[i] * Math.PI);
                    
                    // Primary wave: sine oscillation
                    var primaryWave = Math.Sin(zonePhase);
                    
                    // Secondary wave: adds complexity (different frequency)
                    var secondaryWave = Math.Sin(zonePhase * 0.7 + Math.PI * 0.3) * 0.3;
                    
                    // Combine waves and normalize to 0-1
                    var combinedWave = (primaryWave + secondaryWave) * 0.5 + 0.5;
                    combinedWave = Math.Clamp(combinedWave, 0, 1);
                    
                    // Apply amplitude (audio-driven)
                    targetBrightness[i] = MinBrightness + (combinedWave * waveAmplitude * (1 - MinBrightness));
                    
                    // Add direct audio reactivity - beats cause spikes
                    if (rawEnergy > 0.6)
                    {
                        var beatBoost = (rawEnergy - 0.6) * 2.5; // 0 to 1 for energy 0.6 to 1.0
                        targetBrightness[i] = Math.Min(1.0, targetBrightness[i] + beatBoost * 0.4);
                    }
                }
                
                // Apply inter-zone blending for smoother wave appearance
                var blendedBrightness = new double[4];
                for (var i = 0; i < 4; i++)
                {
                    var sum = targetBrightness[i];
                    var count = 1.0;
                    
                    // Blend with neighbors
                    if (i > 0)
                    {
                        sum += targetBrightness[i - 1] * WaveSpread;
                        count += WaveSpread;
                    }
                    if (i < 3)
                    {
                        sum += targetBrightness[i + 1] * WaveSpread;
                        count += WaveSpread;
                    }
                    
                    blendedBrightness[i] = sum / count;
                }
                
                // Smooth transitions - rise quickly, decay smoothly
                for (var i = 0; i < 4; i++)
                {
                    var target = blendedBrightness[i];
                    
                    if (target > _zoneBrightness[i])
                    {
                        // Rise: interpolate quickly toward target
                        _zoneBrightness[i] += (target - _zoneBrightness[i]) * 0.4;
                    }
                    else
                    {
                        // Decay: smooth falloff
                        _zoneBrightness[i] -= BrightnessDecay * deltaTime * (1 + _smoothedEnergy);
                        _zoneBrightness[i] = Math.Max(_zoneBrightness[i], target);
                    }
                    
                    _zoneBrightness[i] = Math.Clamp(_zoneBrightness[i], MinBrightness, 1.0);
                }

                // Apply brightness to zone colors
                var colors = new ZoneColors(
                    ApplyBrightness(_zoneColors.Zone1, _zoneBrightness[0]),
                    ApplyBrightness(_zoneColors.Zone2, _zoneBrightness[1]),
                    ApplyBrightness(_zoneColors.Zone3, _zoneBrightness[2]),
                    ApplyBrightness(_zoneColors.Zone4, _zoneBrightness[3])
                );

                await controller.SetColorsAsync(colors, cancellationToken).ConfigureAwait(false);
            }
            catch
            {
                // Audio device error - graceful decay
                for (var i = 0; i < 4; i++)
                {
                    _zoneBrightness[i] = Math.Max(MinBrightness, _zoneBrightness[i] - BrightnessDecay * deltaTime);
                }
            }

            // ~60 FPS for smooth wave animation
            await Task.Delay(16, cancellationToken).ConfigureAwait(false);
        }
    }

    private static RGBColor ApplyBrightness(RGBColor color, double brightness)
    {
        return new RGBColor(
            (byte)(color.R * brightness),
            (byte)(color.G * brightness),
            (byte)(color.B * brightness)
        );
    }

    private async Task RunIdleWaveAsync(CustomRGBEffectController controller, CancellationToken cancellationToken)
    {
        var stopwatch = Stopwatch.StartNew();
        var lastTime = 0.0;
        var speedMultiplier = 0.25 + (_speed * 0.25);

        while (!cancellationToken.IsCancellationRequested)
        {
            var currentTime = stopwatch.Elapsed.TotalSeconds;
            var deltaTime = Math.Min(currentTime - lastTime, 0.1);
            lastTime = currentTime;

            // Slow idle wave
            _wavePhase += BaseWaveFrequency * speedMultiplier * deltaTime * Math.PI * 2;
            if (_wavePhase > Math.PI * 20) _wavePhase -= Math.PI * 20;

            for (var i = 0; i < 4; i++)
            {
                var zonePhase = _wavePhase + (_zonePhaseOffsets[i] * Math.PI);
                var wave = Math.Sin(zonePhase) * 0.5 + 0.5;
                var target = MinBrightness + (wave * 0.25);
                
                _zoneBrightness[i] += (target - _zoneBrightness[i]) * 0.1;
                _zoneBrightness[i] = Math.Clamp(_zoneBrightness[i], MinBrightness, 1.0);
            }

            var colors = new ZoneColors(
                ApplyBrightness(_zoneColors.Zone1, _zoneBrightness[0]),
                ApplyBrightness(_zoneColors.Zone2, _zoneBrightness[1]),
                ApplyBrightness(_zoneColors.Zone3, _zoneBrightness[2]),
                ApplyBrightness(_zoneColors.Zone4, _zoneBrightness[3])
            );

            await controller.SetColorsAsync(colors, cancellationToken).ConfigureAwait(false);
            await Task.Delay(16, cancellationToken).ConfigureAwait(false);
        }
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        
        _audioDevice?.Dispose();
        _deviceEnumerator.Dispose();
        
        GC.SuppressFinalize(this);
    }
}
