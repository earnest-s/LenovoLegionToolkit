// ============================================================================
// AudioVisualizerEffect.cs
// 
// Audio-reactive swipe effect for 4-zone keyboards.
// Sweeps from Zone 0 → Zone 3 driven by live audio energy.
// ============================================================================

using System;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using NAudio.CoreAudioApi;

namespace LenovoLegionToolkit.Lib.Controllers.CustomRGBEffects.Effects;

/// <summary>
/// Audio visualizer effect with left-to-right swipe driven by audio energy.
/// Higher audio = faster sweep + higher brightness. Smooth decay on zones.
/// </summary>
public class AudioVisualizerEffect : ICustomRGBEffect, IDisposable
{
    private readonly int _speed;
    private readonly ZoneColors _zoneColors;
    private readonly MMDeviceEnumerator _deviceEnumerator;
    private MMDevice? _audioDevice;
    private bool _disposed;

    // Zone brightness levels (0-1) for smooth decay
    private readonly double[] _zoneBrightness = new double[4];
    
    // Swipe position (0.0 to 4.0, wraps around)
    private double _swipePosition;
    
    // Audio energy smoothing
    private double _smoothedEnergy;
    
    // Constants for animation tuning
    private const double MinSwipeSpeed = 0.5;    // Zones per second at silence
    private const double MaxSwipeSpeed = 8.0;    // Zones per second at max audio
    private const double DecayRate = 2.5;        // Brightness decay per second
    private const double EnergySmoothing = 0.15; // Smoothing factor for audio energy
    private const double SwipeWidth = 1.5;       // Width of the swipe glow in zones

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
    public string Description => "Audio-reactive swipe effect for 4-zone keyboards";
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
            await RunIdleAnimationAsync(controller, cancellationToken).ConfigureAwait(false);
            return;
        }

        if (_audioDevice?.AudioMeterInformation == null)
        {
            await RunIdleAnimationAsync(controller, cancellationToken).ConfigureAwait(false);
            return;
        }

        var meterInfo = _audioDevice.AudioMeterInformation;
        var stopwatch = Stopwatch.StartNew();
        var lastTime = 0.0;
        
        // Speed multiplier from slider (1-4 maps to 0.6 - 1.4)
        var speedMultiplier = 0.4 + (_speed * 0.25);

        while (!cancellationToken.IsCancellationRequested)
        {
            var currentTime = stopwatch.Elapsed.TotalSeconds;
            var deltaTime = currentTime - lastTime;
            lastTime = currentTime;

            try
            {
                // Get audio energy (0-1)
                var peakValue = meterInfo.MasterPeakValue;
                
                // Smooth the energy to avoid jitter
                _smoothedEnergy += (peakValue - _smoothedEnergy) * EnergySmoothing;
                _smoothedEnergy = Math.Clamp(_smoothedEnergy, 0, 1);
                
                // Calculate swipe speed based on audio energy
                var swipeSpeed = MinSwipeSpeed + (_smoothedEnergy * (MaxSwipeSpeed - MinSwipeSpeed));
                swipeSpeed *= speedMultiplier;
                
                // Advance swipe position (Zone 0 → Zone 3)
                _swipePosition += swipeSpeed * deltaTime;
                
                // Wrap around when swipe completes
                if (_swipePosition >= 4.0 + SwipeWidth)
                {
                    _swipePosition = -SwipeWidth;
                }

                // Calculate brightness for each zone based on swipe position
                var baseBrightness = 0.2 + (_smoothedEnergy * 0.8); // 0.2 to 1.0 based on audio
                
                for (var i = 0; i < 4; i++)
                {
                    // Distance from swipe center to this zone
                    var distance = Math.Abs(_swipePosition - i);
                    
                    // Calculate target brightness based on distance from swipe
                    double targetBrightness;
                    if (distance < SwipeWidth)
                    {
                        // Within swipe glow - use smooth falloff
                        var falloff = 1.0 - (distance / SwipeWidth);
                        targetBrightness = baseBrightness * falloff * falloff; // Quadratic falloff
                    }
                    else
                    {
                        targetBrightness = 0;
                    }
                    
                    // Apply smooth decay - never jump up instantly, but decay smoothly
                    if (targetBrightness > _zoneBrightness[i])
                    {
                        // Rise quickly to target
                        _zoneBrightness[i] = targetBrightness;
                    }
                    else
                    {
                        // Decay smoothly
                        _zoneBrightness[i] -= DecayRate * deltaTime;
                        _zoneBrightness[i] = Math.Max(_zoneBrightness[i], targetBrightness);
                    }
                    
                    _zoneBrightness[i] = Math.Clamp(_zoneBrightness[i], 0, 1);
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
                // Audio device error - continue with decay
                for (var i = 0; i < 4; i++)
                {
                    _zoneBrightness[i] = Math.Max(0, _zoneBrightness[i] - DecayRate * deltaTime);
                }
            }

            // ~60 FPS for smooth animation
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

    private async Task RunIdleAnimationAsync(CustomRGBEffectController controller, CancellationToken cancellationToken)
    {
        var stopwatch = Stopwatch.StartNew();
        var lastTime = 0.0;
        var position = -SwipeWidth;
        var speedMultiplier = 0.4 + (_speed * 0.25);

        while (!cancellationToken.IsCancellationRequested)
        {
            var currentTime = stopwatch.Elapsed.TotalSeconds;
            var deltaTime = currentTime - lastTime;
            lastTime = currentTime;

            // Slow idle swipe
            position += MinSwipeSpeed * speedMultiplier * deltaTime;
            if (position >= 4.0 + SwipeWidth)
            {
                position = -SwipeWidth;
            }

            for (var i = 0; i < 4; i++)
            {
                var distance = Math.Abs(position - i);
                if (distance < SwipeWidth)
                {
                    var falloff = 1.0 - (distance / SwipeWidth);
                    _zoneBrightness[i] = 0.3 * falloff * falloff;
                }
                else
                {
                    _zoneBrightness[i] = Math.Max(0, _zoneBrightness[i] - DecayRate * deltaTime);
                }
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
