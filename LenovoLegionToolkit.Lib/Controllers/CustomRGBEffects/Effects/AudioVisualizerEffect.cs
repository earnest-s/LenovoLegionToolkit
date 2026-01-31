// ============================================================================
// AudioVisualizerEffect.cs
// 
// FFT-based spectrum visualizer for 4-zone keyboards.
// Each zone represents a fixed frequency band (Bass, Low-Mid, High-Mid, Treble).
// Smooth analog meter behavior with proper attack/decay envelope.
// ============================================================================

using System;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using NAudio.CoreAudioApi;
using NAudio.Wave;

namespace LenovoLegionToolkit.Lib.Controllers.CustomRGBEffects.Effects;

/// <summary>
/// FFT-based audio spectrum visualizer with smooth analog meter behavior.
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

    // Audio capture
    private WasapiLoopbackCapture? _capture;
    private int _sampleRate = 48000;

    // PERSISTENT per-zone envelope state - NEVER recreated
    private readonly float[] _envelope = new float[4];  // Current displayed brightness (0-1)
    private readonly float[] _peak = new float[4];      // Peak hold for each zone

    // Frequency band boundaries (Hz) - mapped to zones
    private static readonly (float Low, float High)[] FrequencyBands =
    {
        (20f, 150f),      // Zone 0: Bass
        (150f, 600f),     // Zone 1: Low-Mid
        (600f, 2500f),    // Zone 2: High-Mid
        (2500f, 16000f)   // Zone 3: Treble
    };

    // Envelope timing (in units per second)
    private const float AttackSpeed = 12f;    // Rise 12 units/sec = full rise in ~83ms
    private const float DecaySpeed = 1.5f;    // Fall 1.5 units/sec = full fall in ~667ms
    private const float PeakDecaySpeed = 2f;  // Peak falls at 2 units/sec
    private const float MinBrightness = 0.04f;

    // Sensitivity scaling per band
    private static readonly float[] BandGain = { 4.0f, 2.5f, 1.8f, 1.2f };

    public AudioVisualizerEffect(ZoneColors? zoneColors = null, int speed = 2)
    {
        _speed = Math.Clamp(speed, 1, 4);
        _zoneColors = zoneColors ?? new ZoneColors(
            new RGBColor(255, 50, 0),   // Zone 0: Orange-Red (Bass)
            new RGBColor(255, 200, 0),  // Zone 1: Yellow (Low-Mid)
            new RGBColor(0, 255, 100),  // Zone 2: Green (High-Mid)
            new RGBColor(0, 150, 255)   // Zone 3: Cyan-Blue (Treble)
        );

        // Initialize persistent state
        for (var i = 0; i < 4; i++)
        {
            _envelope[i] = MinBrightness;
            _peak[i] = 0f;
        }
    }

    public CustomRGBEffectType Type => CustomRGBEffectType.AudioVisualizer;
    public string Description => "FFT-based spectrum visualizer with smooth analog response";
    public bool RequiresInputMonitoring => false;
    public bool RequiresSystemAccess => true;

    public async Task RunAsync(CustomRGBEffectController controller, CancellationToken cancellationToken)
    {
        try
        {
            _capture = new WasapiLoopbackCapture();
            _sampleRate = _capture.WaveFormat.SampleRate;
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
        var ticksPerSecond = (float)Stopwatch.Frequency;

        // Speed multiplier (1-4 maps to 0.6 - 1.5)
        var speedMult = 0.45f + (_speed * 0.25f);

        try
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                // Calculate delta time from stopwatch ticks (high precision)
                var nowTicks = stopwatch.ElapsedTicks;
                var dt = (nowTicks - lastTicks) / ticksPerSecond;
                lastTicks = nowTicks;

                // Clamp delta to prevent jumps
                dt = Math.Clamp(dt, 0.001f, 0.1f);

                // Get FFT band levels (0-1 range, already log-scaled)
                var bandLevels = GetBandLevels();

                // Update envelope for each zone - THIS IS THE CRITICAL PATH
                for (var z = 0; z < 4; z++)
                {
                    var input = bandLevels[z];
                    var current = _envelope[z];

                    // Simple attack/decay envelope - NO THRESHOLDS
                    float next;
                    if (input > current)
                    {
                        // ATTACK: rise toward input
                        next = current + AttackSpeed * speedMult * dt;
                        if (next > input) next = input; // Don't overshoot
                    }
                    else
                    {
                        // DECAY: fall toward input (or minimum)
                        next = current - DecaySpeed * speedMult * dt;
                        if (next < input) next = input; // Don't undershoot
                    }

                    // Enforce minimum brightness (never fully off)
                    if (next < MinBrightness) next = MinBrightness;
                    if (next > 1f) next = 1f;

                    // Store back to persistent state
                    _envelope[z] = next;
                }

                // Build colors from envelope values
                var c0 = ScaleColor(_zoneColors.Zone1, _envelope[0]);
                var c1 = ScaleColor(_zoneColors.Zone2, _envelope[1]);
                var c2 = ScaleColor(_zoneColors.Zone3, _envelope[2]);
                var c3 = ScaleColor(_zoneColors.Zone4, _envelope[3]);

                var colors = new ZoneColors(c0, c1, c2, c3);
                await controller.SetColorsAsync(colors, cancellationToken).ConfigureAwait(false);

                // ~60 FPS
                await Task.Delay(16, cancellationToken).ConfigureAwait(false);
            }
        }
        finally
        {
            StopCapture();
        }
    }

    private float[] GetBandLevels()
    {
        var levels = new float[4];

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

        // Extract each band
        for (var b = 0; b < 4; b++)
        {
            var (lo, hi) = FrequencyBands[b];
            var loBin = Math.Max(1, (int)(lo / binHz));
            var hiBin = Math.Min(FftLength / 2 - 1, (int)(hi / binHz));

            // Sum magnitudes (not RMS, just sum for sensitivity)
            var sum = 0f;
            for (var k = loBin; k <= hiBin; k++)
            {
                sum += (float)_fftComplex[k].Magnitude;
            }

            // Normalize by bin count and apply gain
            var avg = sum / Math.Max(1, hiBin - loBin + 1);
            var scaled = avg * BandGain[b] * 25f;

            // Logarithmic scaling: log10(1 + x) maps 0->0, large->~1
            float level;
            if (scaled > 0.001f)
            {
                level = MathF.Log10(1f + scaled * 10f) / 2f; // /2 to normalize range
            }
            else
            {
                level = 0f;
            }

            levels[b] = Math.Clamp(level, 0f, 1f);
        }

        return levels;
    }

    private void OnDataAvailable(object? sender, WaveInEventArgs e)
    {
        var bps = _capture?.WaveFormat.BitsPerSample / 8 ?? 4;
        var ch = _capture?.WaveFormat.Channels ?? 2;
        var frameBytes = bps * ch;

        lock (_fftLock)
        {
            var offset = 0;
            while (offset + frameBytes <= e.BytesRecorded)
            {
                float sample;
                if (bps == 4)
                {
                    sample = BitConverter.ToSingle(e.Buffer, offset);
                }
                else if (bps == 2)
                {
                    sample = BitConverter.ToInt16(e.Buffer, offset) / 32768f;
                }
                else
                {
                    offset += frameBytes;
                    continue;
                }

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
        var ticksPerSecond = (float)Stopwatch.Frequency;

        while (!cancellationToken.IsCancellationRequested)
        {
            var nowTicks = stopwatch.ElapsedTicks;
            var dt = (nowTicks - lastTicks) / ticksPerSecond;
            lastTicks = nowTicks;
            dt = Math.Clamp(dt, 0.001f, 0.1f);

            var time = stopwatch.Elapsed.TotalSeconds;

            for (var i = 0; i < 4; i++)
            {
                var phase = time * 0.8 + i * 0.4;
                var target = MinBrightness + 0.2f * (float)(Math.Sin(phase) * 0.5 + 0.5);

                // Smooth toward target using same envelope logic
                if (target > _envelope[i])
                {
                    _envelope[i] += 2f * dt;
                    if (_envelope[i] > target) _envelope[i] = target;
                }
                else
                {
                    _envelope[i] -= 0.5f * dt;
                    if (_envelope[i] < target) _envelope[i] = target;
                }
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
