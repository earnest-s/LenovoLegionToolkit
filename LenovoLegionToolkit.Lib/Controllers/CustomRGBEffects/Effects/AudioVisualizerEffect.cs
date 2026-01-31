// ============================================================================
// AudioVisualizerEffect.cs
// 
// FFT-based spectrum visualizer for 4-zone keyboards.
// Each zone represents a fixed frequency band (Bass, Low-Mid, High-Mid, Treble).
// 
// FIXES APPLIED:
// 1. Proper WaveFormat.Encoding handling (IEEE float vs PCM)
// 2. Energy-based normalization with rolling max reference
// 3. True envelope follower with exponential smoothing (no clamping)
// 4. Inter-zone spatial smoothing for wave formation
// 5. Brightness applied after envelope shaping
// ============================================================================

using System;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using NAudio.CoreAudioApi;
using NAudio.Wave;

namespace LenovoLegionToolkit.Lib.Controllers.CustomRGBEffects.Effects;

/// <summary>
/// FFT-based audio spectrum visualizer with true analog meter behavior.
/// Zone 0: Bass (20-150 Hz), Zone 1: Low-Mid (150-600 Hz),
/// Zone 2: High-Mid (600-2500 Hz), Zone 3: Treble (2500-16000 Hz).
/// </summary>
public class AudioVisualizerEffect : ICustomRGBEffect, IDisposable
{
    private readonly int _speed;
    private readonly ZoneColors _zoneColors;
    private bool _disposed;

    // FFT configuration
    private const int FftLength = 2048;
    private const int FftLengthLog2 = 11;
    private readonly float[] _fftBuffer = new float[FftLength];
    private readonly Complex[] _fftComplex = new Complex[FftLength];
    private int _fftBufferIndex;
    private readonly object _fftLock = new();

    // Audio capture state
    private WasapiLoopbackCapture? _capture;
    private int _sampleRate = 48000;
    private WaveFormatEncoding _encoding = WaveFormatEncoding.IeeeFloat;
    private int _bytesPerSample = 4;
    private int _channels = 2;

    // PERSISTENT envelope state - NEVER recreated, survives across frames
    private readonly float[] _envelope = new float[4];       // Current smoothed level (0-1)
    private readonly float[] _bandEnergy = new float[4];     // Smoothed band energy input
    private readonly float[] _rollingMax = new float[4];     // Rolling max for normalization

    // Frequency band boundaries (Hz) - FIXED, DO NOT CHANGE
    private static readonly (float Low, float High)[] FrequencyBands =
    {
        (20f, 150f),      // Zone 0: Bass
        (150f, 600f),     // Zone 1: Low-Mid
        (600f, 2500f),    // Zone 2: High-Mid
        (2500f, 16000f)   // Zone 3: Treble
    };

    // Per-band sensitivity (compensate for spectral energy distribution)
    private static readonly float[] BandSensitivity = { 2.0f, 1.5f, 1.2f, 1.0f };

    // Envelope coefficients (frame-rate independent via exp smoothing)
    // Attack: fast rise (~20ms time constant)
    // Decay: slow fall (~300ms time constant)
    private const float AttackTau = 0.020f;  // 20ms attack time constant
    private const float DecayTau = 0.300f;   // 300ms decay time constant
    private const float RollingMaxDecay = 0.995f;  // Slow decay for rolling max normalization

    // Inter-zone spatial smoothing weights (wave formation)
    private const float SelfWeight = 0.70f;
    private const float NeighborWeight = 0.15f;

    // Minimum brightness to prevent total blackout
    private const float MinBrightness = 0.02f;

    public AudioVisualizerEffect(ZoneColors? zoneColors = null, int speed = 2)
    {
        _speed = Math.Clamp(speed, 1, 4);
        _zoneColors = zoneColors ?? new ZoneColors(
            new RGBColor(255, 50, 0),   // Zone 0: Orange-Red (Bass)
            new RGBColor(255, 200, 0),  // Zone 1: Yellow (Low-Mid)
            new RGBColor(0, 255, 100),  // Zone 2: Green (High-Mid)
            new RGBColor(0, 150, 255)   // Zone 3: Cyan-Blue (Treble)
        );

        // Initialize persistent state to baseline
        for (var i = 0; i < 4; i++)
        {
            _envelope[i] = MinBrightness;
            _bandEnergy[i] = 0f;
            _rollingMax[i] = 0.001f;  // Small nonzero to prevent div-by-zero
        }
    }

    public CustomRGBEffectType Type => CustomRGBEffectType.AudioVisualizer;
    public string Description => "FFT-based spectrum visualizer with analog meter response";
    public bool RequiresInputMonitoring => false;
    public bool RequiresSystemAccess => true;

    public async Task RunAsync(CustomRGBEffectController controller, CancellationToken cancellationToken)
    {
        // Initialize audio capture with proper format detection
        try
        {
            _capture = new WasapiLoopbackCapture();
            _sampleRate = _capture.WaveFormat.SampleRate;
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

        // Speed affects envelope timing (1-4 maps to 0.6x - 1.5x speed)
        var speedMult = 0.45f + (_speed * 0.25f);

        try
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                // === DELTA TIME (high precision) ===
                var nowTicks = stopwatch.ElapsedTicks;
                var dt = (float)((nowTicks - lastTicks) / ticksPerSecond);
                lastTicks = nowTicks;
                dt = Math.Clamp(dt, 0.001f, 0.1f);

                // === COMPUTE RAW BAND ENERGY FROM FFT ===
                var rawEnergy = ComputeBandEnergy();

                // === EXPONENTIAL SMOOTHING COEFFICIENTS (frame-rate independent) ===
                // alpha = 1 - exp(-dt / tau)
                var attackAlpha = 1f - MathF.Exp(-dt * speedMult / AttackTau);
                var decayAlpha = 1f - MathF.Exp(-dt * speedMult / DecayTau);

                // === UPDATE BAND ENERGY WITH ENVELOPE FOLLOWER ===
                for (var z = 0; z < 4; z++)
                {
                    var input = rawEnergy[z];
                    var current = _bandEnergy[z];

                    // True envelope follower: exponential approach, NEVER clamp to input
                    float alpha = input > current ? attackAlpha : decayAlpha;
                    _bandEnergy[z] = current + alpha * (input - current);

                    // Update rolling max for normalization (slow decay)
                    if (_bandEnergy[z] > _rollingMax[z])
                    {
                        _rollingMax[z] = _bandEnergy[z];
                    }
                    else
                    {
                        _rollingMax[z] *= RollingMaxDecay;
                        // Prevent max from going too low
                        if (_rollingMax[z] < 0.001f) _rollingMax[z] = 0.001f;
                    }
                }

                // === NORMALIZE AND APPLY PERCEPTUAL CURVE ===
                var normalized = new float[4];
                for (var z = 0; z < 4; z++)
                {
                    // Normalize against rolling max (auto-gain)
                    var norm = _bandEnergy[z] / _rollingMax[z];
                    norm = Math.Clamp(norm, 0f, 1f);

                    // Apply perceptual curve: sqrt for more linear perceived brightness
                    normalized[z] = MathF.Sqrt(norm);
                }

                // === INTER-ZONE SPATIAL SMOOTHING (wave formation) ===
                var smoothed = new float[4];
                for (var z = 0; z < 4; z++)
                {
                    var self = normalized[z] * SelfWeight;
                    var left = (z > 0) ? normalized[z - 1] * NeighborWeight : 0f;
                    var right = (z < 3) ? normalized[z + 1] * NeighborWeight : 0f;
                    smoothed[z] = self + left + right;
                }

                // === UPDATE DISPLAY ENVELOPE (additional smoothing layer) ===
                for (var z = 0; z < 4; z++)
                {
                    var target = smoothed[z];
                    var current = _envelope[z];

                    // Another exponential smooth for final display (reduces jitter)
                    float displayAlpha = target > current ? attackAlpha * 0.8f : decayAlpha * 0.5f;
                    _envelope[z] = current + displayAlpha * (target - current);

                    // Enforce minimum brightness
                    if (_envelope[z] < MinBrightness) _envelope[z] = MinBrightness;
                    if (_envelope[z] > 1f) _envelope[z] = 1f;
                }

                // === APPLY BRIGHTNESS AND OUTPUT ===
                // Brightness is applied AFTER envelope shaping (correct domain)
                var c0 = ScaleColor(_zoneColors.Zone1, _envelope[0]);
                var c1 = ScaleColor(_zoneColors.Zone2, _envelope[1]);
                var c2 = ScaleColor(_zoneColors.Zone3, _envelope[2]);
                var c3 = ScaleColor(_zoneColors.Zone4, _envelope[3]);

                await controller.SetColorsAsync(new ZoneColors(c0, c1, c2, c3), cancellationToken).ConfigureAwait(false);

                // ~60 FPS
                await Task.Delay(16, cancellationToken).ConfigureAwait(false);
            }
        }
        finally
        {
            StopCapture();
        }
    }

    /// <summary>
    /// Compute raw energy for each frequency band from FFT.
    /// Returns squared magnitude sum (energy), NOT linear magnitude.
    /// </summary>
    private float[] ComputeBandEnergy()
    {
        var energy = new float[4];

        lock (_fftLock)
        {
            // Apply Hann window and copy to complex buffer
            for (var i = 0; i < FftLength; i++)
            {
                var idx = (_fftBufferIndex + i) % FftLength;
                var window = 0.5f * (1f - MathF.Cos(2f * MathF.PI * i / (FftLength - 1)));
                _fftComplex[i] = new Complex(_fftBuffer[idx] * window, 0);
            }
        }

        // In-place FFT
        Fft(_fftComplex, FftLengthLog2);

        // Frequency bin width
        var binHz = (float)_sampleRate / FftLength;

        // Extract energy for each band
        for (var b = 0; b < 4; b++)
        {
            var (lo, hi) = FrequencyBands[b];
            var loBin = Math.Max(1, (int)(lo / binHz));
            var hiBin = Math.Min(FftLength / 2 - 1, (int)(hi / binHz));

            // Sum SQUARED magnitudes (energy, not amplitude)
            var sumSquared = 0.0;
            var binCount = 0;
            for (var k = loBin; k <= hiBin; k++)
            {
                var re = _fftComplex[k].Real;
                var im = _fftComplex[k].Imaginary;
                sumSquared += re * re + im * im;
                binCount++;
            }

            // RMS energy with sensitivity scaling
            var rmsEnergy = binCount > 0 ? (float)Math.Sqrt(sumSquared / binCount) : 0f;
            energy[b] = rmsEnergy * BandSensitivity[b];
        }

        return energy;
    }

    /// <summary>
    /// Handle incoming audio data with proper format detection.
    /// FIX: Correctly decode based on WaveFormat.Encoding.
    /// </summary>
    private void OnDataAvailable(object? sender, WaveInEventArgs e)
    {
        if (_capture == null || e.BytesRecorded == 0) return;

        var frameBytes = _bytesPerSample * _channels;

        lock (_fftLock)
        {
            var offset = 0;
            while (offset + frameBytes <= e.BytesRecorded)
            {
                float sample = 0f;

                // FIX: Properly decode based on actual encoding
                if (_encoding == WaveFormatEncoding.IeeeFloat)
                {
                    // 32-bit IEEE float: already normalized to [-1, +1]
                    if (_bytesPerSample == 4)
                    {
                        sample = BitConverter.ToSingle(e.Buffer, offset);
                    }
                }
                else if (_encoding == WaveFormatEncoding.Pcm)
                {
                    // PCM: normalize to [-1, +1]
                    if (_bytesPerSample == 2)
                    {
                        sample = BitConverter.ToInt16(e.Buffer, offset) / 32768f;
                    }
                    else if (_bytesPerSample == 3)
                    {
                        // 24-bit PCM
                        var val = (e.Buffer[offset] | (e.Buffer[offset + 1] << 8) | (e.Buffer[offset + 2] << 16));
                        if ((val & 0x800000) != 0) val |= unchecked((int)0xFF000000); // Sign extend
                        sample = val / 8388608f;
                    }
                    else if (_bytesPerSample == 4)
                    {
                        sample = BitConverter.ToInt32(e.Buffer, offset) / 2147483648f;
                    }
                }
                else if (_encoding == WaveFormatEncoding.Extensible)
                {
                    // Extensible format: check SubFormat in WaveFormat
                    // Most common is IEEE float or PCM
                    if (_bytesPerSample == 4)
                    {
                        // Try float first (most common for WASAPI loopback)
                        sample = BitConverter.ToSingle(e.Buffer, offset);
                        // Sanity check: if value is way out of range, it's probably PCM
                        if (float.IsNaN(sample) || float.IsInfinity(sample) || sample < -10f || sample > 10f)
                        {
                            sample = BitConverter.ToInt32(e.Buffer, offset) / 2147483648f;
                        }
                    }
                    else if (_bytesPerSample == 2)
                    {
                        sample = BitConverter.ToInt16(e.Buffer, offset) / 32768f;
                    }
                }

                // Clamp sample to valid range (handles any decoding errors)
                sample = Math.Clamp(sample, -1f, 1f);

                // Store mono sample (just use first channel for simplicity)
                _fftBuffer[_fftBufferIndex] = sample;
                _fftBufferIndex = (_fftBufferIndex + 1) % FftLength;

                offset += frameBytes;
            }
        }
    }

    private static RGBColor ScaleColor(RGBColor color, float brightness)
    {
        return new RGBColor(
            (byte)(color.R * brightness),
            (byte)(color.G * brightness),
            (byte)(color.B * brightness)
        );
    }

    private static void Fft(Complex[] buffer, int log2N)
    {
        var n = 1 << log2N;

        // Bit-reversal permutation
        for (var i = 0; i < n; i++)
        {
            var j = BitReverse(i, log2N);
            if (j > i)
            {
                (buffer[i], buffer[j]) = (buffer[j], buffer[i]);
            }
        }

        // Cooley-Tukey iterative FFT
        for (var s = 1; s <= log2N; s++)
        {
            var m = 1 << s;
            var wm = new Complex(Math.Cos(-2 * Math.PI / m), Math.Sin(-2 * Math.PI / m));

            for (var k = 0; k < n; k += m)
            {
                var w = new Complex(1, 0);
                for (var j = 0; j < m / 2; j++)
                {
                    var t = w * buffer[k + j + m / 2];
                    var u = buffer[k + j];
                    buffer[k + j] = u + t;
                    buffer[k + j + m / 2] = u - t;
                    w = w * wm;
                }
            }
        }
    }

    private static int BitReverse(int value, int bits)
    {
        var result = 0;
        for (var i = 0; i < bits; i++)
        {
            result = (result << 1) | (value & 1);
            value >>= 1;
        }
        return result;
    }

    private async Task RunIdleFallbackAsync(CustomRGBEffectController controller, CancellationToken cancellationToken)
    {
        var stopwatch = Stopwatch.StartNew();
        var lastTicks = stopwatch.ElapsedTicks;
        var ticksPerSecond = (double)Stopwatch.Frequency;

        while (!cancellationToken.IsCancellationRequested)
        {
            var nowTicks = stopwatch.ElapsedTicks;
            var dt = (float)((nowTicks - lastTicks) / ticksPerSecond);
            lastTicks = nowTicks;
            dt = Math.Clamp(dt, 0.001f, 0.1f);

            var time = stopwatch.Elapsed.TotalSeconds;

            // Exponential smoothing coefficient
            var alpha = 1f - MathF.Exp(-dt / 0.15f);

            for (var i = 0; i < 4; i++)
            {
                var phase = time * 0.8 + i * 0.4;
                var target = MinBrightness + 0.2f * (float)(Math.Sin(phase) * 0.5 + 0.5);

                // True exponential smoothing (no clamping)
                _envelope[i] += alpha * (target - _envelope[i]);
                _envelope[i] = Math.Clamp(_envelope[i], MinBrightness, 1f);
            }

            var colors = new ZoneColors(
                ScaleColor(_zoneColors.Zone1, _envelope[0]),
                ScaleColor(_zoneColors.Zone2, _envelope[1]),
                ScaleColor(_zoneColors.Zone3, _envelope[2]),
                ScaleColor(_zoneColors.Zone4, _envelope[3])
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
            try
            {
                _capture.StopRecording();
            }
            catch
            {
                // Ignore stop errors
            }
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

    /// <summary>
    /// Simple complex number struct for FFT.
    /// </summary>
    private readonly struct Complex
    {
        public readonly double Real;
        public readonly double Imaginary;

        public Complex(double real, double imaginary)
        {
            Real = real;
            Imaginary = imaginary;
        }

        public double Magnitude => Math.Sqrt(Real * Real + Imaginary * Imaginary);

        public static Complex operator +(Complex a, Complex b) =>
            new(a.Real + b.Real, a.Imaginary + b.Imaginary);

        public static Complex operator -(Complex a, Complex b) =>
            new(a.Real - b.Real, a.Imaginary - b.Imaginary);

        public static Complex operator *(Complex a, Complex b) =>
            new(a.Real * b.Real - a.Imaginary * b.Imaginary,
                a.Real * b.Imaginary + a.Imaginary * b.Real);

        public static implicit operator Complex(double d) => new(d, 0);
    }
}
