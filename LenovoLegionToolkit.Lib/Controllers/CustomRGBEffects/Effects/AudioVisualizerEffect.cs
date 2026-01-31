// ============================================================================
// AudioVisualizerEffect.cs
// 
// 1D Ripple-based audio visualizer for 4-zone keyboards.
// Energy propagates from Zone 0 → Zone 3 with time-based decay.
// 
// PIPELINE:
// 1. Audio Capture (WASAPI loopback) - UNCHANGED
// 2. Compute RMS energy (low-frequency weighted)
// 3. Inject energy at Zone 0
// 4. Propagate energy Zone 0 → 1 → 2 → 3 (bucket brigade)
// 5. Apply time-based decay to all zones
// 6. Perceptual scaling → Zone brightness output
// ============================================================================

using System;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using NAudio.CoreAudioApi;
using NAudio.Wave;

namespace LenovoLegionToolkit.Lib.Controllers.CustomRGBEffects.Effects;

/// <summary>
/// Audio-driven ripple effect for 4-zone keyboards.
/// Energy ripples from left to right with smooth decay.
/// </summary>
public class AudioVisualizerEffect : ICustomRGBEffect, IDisposable
{
    private readonly int _speed;
    private readonly ZoneColors _zoneColors;
    private bool _disposed;

    // ========================================================================
    // AUDIO CAPTURE STATE (UNCHANGED)
    // ========================================================================
    private WasapiLoopbackCapture? _capture;
    private WaveFormatEncoding _encoding = WaveFormatEncoding.IeeeFloat;
    private int _bytesPerSample = 4;
    private int _channels = 2;
    private readonly object _audioLock = new();

    // ========================================================================
    // ENERGY COMPUTATION (RMS-based)
    // ========================================================================
    private float _currentRms;
    private const float InputGain = 4.0f;

    // ========================================================================
    // RIPPLE STATE (4 zones as 1D array)
    // zones[0] = input, ripples propagate → zones[3]
    // ========================================================================
    private const int ZoneCount = 4;
    private readonly float[] _zones = new float[ZoneCount];

    // ========================================================================
    // RIPPLE PARAMETERS (time-based)
    // ========================================================================
    private const float PropagationFactor = 0.65f;
    private const float DecayRate = 4.5f;
    private const float PerceptualExponent = 0.55f;

    public AudioVisualizerEffect(ZoneColors? zoneColors = null, int speed = 2)
    {
        _speed = Math.Clamp(speed, 1, 4);
        _zoneColors = zoneColors ?? new ZoneColors(
            new RGBColor(255, 50, 0),
            new RGBColor(255, 200, 0),
            new RGBColor(0, 255, 100),
            new RGBColor(0, 150, 255)
        );

        Array.Clear(_zones, 0, ZoneCount);
    }

    public CustomRGBEffectType Type => CustomRGBEffectType.AudioVisualizer;
    public string Description => "Audio-driven ripple visualizer";
    public bool RequiresInputMonitoring => false;
    public bool RequiresSystemAccess => true;

    public async Task RunAsync(CustomRGBEffectController controller, CancellationToken cancellationToken)
    {
        try
        {
            _capture = new WasapiLoopbackCapture();
            _encoding = _capture.WaveFormat.Encoding;
            _bytesPerSample = _capture.WaveFormat.BitsPerSample / 8;
            _channels = _capture.WaveFormat.Channels;
            _capture.DataAvailable += OnDataAvailable;
            _capture.StartRecording();
        }
        catch
        {
            await RunIdleFallbackAsync(controller, cancellationToken).ConfigureAwait(false);
            return;
        }

        var stopwatch = Stopwatch.StartNew();
        var lastTicks = stopwatch.ElapsedTicks;
        var ticksPerSecond = (double)Stopwatch.Frequency;
        var speedMult = 0.6f + (_speed * 0.2f);

        try
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                var nowTicks = stopwatch.ElapsedTicks;
                var dt = (float)((nowTicks - lastTicks) / ticksPerSecond);
                lastTicks = nowTicks;
                dt = Math.Clamp(dt, 0.001f, 0.1f);

                // ============================================================
                // STEP 1: GET NORMALIZED ENERGY (0.0 - 1.0)
                // ============================================================
                float energy;
                lock (_audioLock)
                {
                    energy = _currentRms * InputGain;
                    _currentRms = 0f;
                }
                energy = Math.Clamp(energy, 0f, 1f);

                // ============================================================
                // STEP 2: INJECT ENERGY AT ZONE 0
                // ============================================================
                _zones[0] = Math.Max(_zones[0], energy);

                // ============================================================
                // STEP 3: PROPAGATE ENERGY (bucket brigade: 0 → 1 → 2 → 3)
                // Time-scaled propagation factor
                // ============================================================
                var propagate = PropagationFactor * speedMult;
                for (var i = ZoneCount - 1; i >= 1; i--)
                {
                    var incoming = _zones[i - 1] * propagate;
                    _zones[i] = Math.Max(_zones[i], incoming);
                }

                // ============================================================
                // STEP 4: TIME-BASED DECAY (all zones)
                // ============================================================
                var decayFactor = MathF.Exp(-DecayRate * dt * speedMult);
                for (var i = 0; i < ZoneCount; i++)
                {
                    _zones[i] *= decayFactor;
                }

                // ============================================================
                // STEP 5: OUTPUT ZONE COLORS WITH PERCEPTUAL SCALING
                // ============================================================
                var b0 = MathF.Pow(Math.Clamp(_zones[0], 0f, 1f), PerceptualExponent);
                var b1 = MathF.Pow(Math.Clamp(_zones[1], 0f, 1f), PerceptualExponent);
                var b2 = MathF.Pow(Math.Clamp(_zones[2], 0f, 1f), PerceptualExponent);
                var b3 = MathF.Pow(Math.Clamp(_zones[3], 0f, 1f), PerceptualExponent);

                var c0 = ScaleColor(_zoneColors.Zone1, b0);
                var c1 = ScaleColor(_zoneColors.Zone2, b1);
                var c2 = ScaleColor(_zoneColors.Zone3, b2);
                var c3 = ScaleColor(_zoneColors.Zone4, b3);

                await controller.SetColorsAsync(new ZoneColors(c0, c1, c2, c3), cancellationToken).ConfigureAwait(false);
                await Task.Delay(16, cancellationToken).ConfigureAwait(false);
            }
        }
        finally
        {
            StopCapture();
        }
    }

    private void OnDataAvailable(object? sender, WaveInEventArgs e)
    {
        if (_capture == null || e.BytesRecorded == 0) return;

        var frameBytes = _bytesPerSample * _channels;
        var sumSquared = 0.0;
        var sampleCount = 0;

        var offset = 0;
        while (offset + frameBytes <= e.BytesRecorded)
        {
            float sample = 0f;

            if (_encoding == WaveFormatEncoding.IeeeFloat && _bytesPerSample == 4)
            {
                sample = BitConverter.ToSingle(e.Buffer, offset);
            }
            else if (_encoding == WaveFormatEncoding.Pcm && _bytesPerSample == 2)
            {
                sample = BitConverter.ToInt16(e.Buffer, offset) / 32768f;
            }
            else if (_encoding == WaveFormatEncoding.Extensible && _bytesPerSample == 4)
            {
                sample = BitConverter.ToSingle(e.Buffer, offset);
                if (float.IsNaN(sample) || float.IsInfinity(sample) || sample < -10f || sample > 10f)
                    sample = BitConverter.ToInt32(e.Buffer, offset) / 2147483648f;
            }

            sample = Math.Clamp(sample, -1f, 1f);
            sumSquared += sample * sample;
            sampleCount++;
            offset += frameBytes;
        }

        if (sampleCount > 0)
        {
            var rms = (float)Math.Sqrt(sumSquared / sampleCount);
            lock (_audioLock)
            {
                _currentRms = Math.Max(_currentRms, rms);
            }
        }
    }

    private static RGBColor ScaleColor(RGBColor color, float brightness)
    {
        brightness = Math.Clamp(brightness, 0f, 1f);
        return new RGBColor(
            (byte)(color.R * brightness),
            (byte)(color.G * brightness),
            (byte)(color.B * brightness)
        );
    }

    private async Task RunIdleFallbackAsync(CustomRGBEffectController controller, CancellationToken cancellationToken)
    {
        var stopwatch = Stopwatch.StartNew();

        while (!cancellationToken.IsCancellationRequested)
        {
            var time = stopwatch.Elapsed.TotalSeconds;
            var zoneBrightness = new float[ZoneCount];
            for (var z = 0; z < ZoneCount; z++)
            {
                var phase = time * 1.2 + z * 0.5;
                var amplitude = 0.3f + 0.2f * (float)Math.Sin(phase);
                zoneBrightness[z] = MathF.Pow(amplitude, PerceptualExponent);
            }

            var colors = new ZoneColors(
                ScaleColor(_zoneColors.Zone1, zoneBrightness[0]),
                ScaleColor(_zoneColors.Zone2, zoneBrightness[1]),
                ScaleColor(_zoneColors.Zone3, zoneBrightness[2]),
                ScaleColor(_zoneColors.Zone4, zoneBrightness[3])
            );

            await controller.SetColorsAsync(colors, cancellationToken).ConfigureAwait(false);
            await Task.Delay(33, cancellationToken).ConfigureAwait(false);
        }
    }

    private void StopCapture()
    {
        if (_capture != null)
        {
            _capture.DataAvailable -= OnDataAvailable;
            try { _capture.StopRecording(); } catch { }
            _capture.Dispose();
            _capture = null;
        }
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        StopCapture();
        GC.SuppressFinalize(this);
    }
}

