// ============================================================================
// AudioVisualizerEffect.cs
// 
// FFT-based spectrum visualizer for 4-zone keyboards.
// Each zone represents a fixed frequency band (Bass, Low-Mid, High-Mid, Treble).
// ============================================================================

using System;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using NAudio.CoreAudioApi;
using NAudio.Wave;

namespace LenovoLegionToolkit.Lib.Controllers.CustomRGBEffects.Effects;

/// <summary>
/// FFT-based audio spectrum visualizer. Each zone represents a frequency band:
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
    private const int FftLengthLog2 = 11; // log2(2048) = 11
    private readonly float[] _fftBuffer = new float[FftLength];
    private readonly Complex[] _fftComplex = new Complex[FftLength];
    private int _fftBufferIndex;
    private readonly object _fftLock = new();

    // Audio capture
    private WasapiLoopbackCapture? _capture;
    private int _sampleRate = 48000;

    // Zone brightness with smoothing
    private readonly double[] _zoneBrightness = new double[4];
    private readonly double[] _zonePeakHold = new double[4];
    private readonly double[] _zoneTargetLevel = new double[4];

    // Frequency band boundaries (Hz) - mapped to zones
    private static readonly (double Low, double High)[] FrequencyBands =
    {
        (20, 150),      // Zone 0: Bass
        (150, 600),     // Zone 1: Low-Mid
        (600, 2500),    // Zone 2: High-Mid
        (2500, 16000)   // Zone 3: Treble
    };

    // Smoothing parameters
    private const double AttackRate = 0.35;      // How fast brightness rises
    private const double DecayRate = 2.5;        // Brightness decay per second
    private const double PeakHoldDecay = 0.8;    // Peak hold decay per second
    private const double MinBrightness = 0.02;   // Minimum visible brightness

    // Sensitivity scaling per band (bass needs boost, treble needs attenuation)
    private static readonly double[] BandSensitivity = { 2.5, 1.8, 1.2, 0.9 };

    public AudioVisualizerEffect(ZoneColors? zoneColors = null, int speed = 2)
    {
        _speed = Math.Clamp(speed, 1, 4);
        _zoneColors = zoneColors ?? new ZoneColors(
            new RGBColor(255, 50, 0),   // Zone 0: Orange-Red (Bass)
            new RGBColor(255, 200, 0),  // Zone 1: Yellow (Low-Mid)
            new RGBColor(0, 255, 100),  // Zone 2: Green (High-Mid)
            new RGBColor(0, 150, 255)   // Zone 3: Cyan-Blue (Treble)
        );
    }

    public CustomRGBEffectType Type => CustomRGBEffectType.AudioVisualizer;
    public string Description => "FFT-based spectrum visualizer for 4-zone keyboards";
    public bool RequiresInputMonitoring => false;
    public bool RequiresSystemAccess => true;

    public async Task RunAsync(CustomRGBEffectController controller, CancellationToken cancellationToken)
    {
        // Initialize WASAPI loopback capture
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
        var lastTime = 0.0;

        // Speed multiplier affects responsiveness (1-4 maps to 0.6-1.5)
        var speedMultiplier = 0.35 + (_speed * 0.3);

        try
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                var currentTime = stopwatch.Elapsed.TotalSeconds;
                var deltaTime = Math.Min(currentTime - lastTime, 0.1);
                lastTime = currentTime;

                // Process FFT and get band magnitudes
                ProcessFft();

                // Update zone brightness with smoothing
                for (var i = 0; i < 4; i++)
                {
                    var target = _zoneTargetLevel[i];

                    // Peak hold
                    if (target > _zonePeakHold[i])
                    {
                        _zonePeakHold[i] = target;
                    }
                    else
                    {
                        _zonePeakHold[i] -= PeakHoldDecay * deltaTime * speedMultiplier;
                        _zonePeakHold[i] = Math.Max(_zonePeakHold[i], target);
                    }

                    // Smooth brightness transitions
                    var effectiveTarget = Math.Max(target, _zonePeakHold[i] * 0.7);

                    if (effectiveTarget > _zoneBrightness[i])
                    {
                        // Attack - rise toward target
                        _zoneBrightness[i] += (effectiveTarget - _zoneBrightness[i]) * AttackRate * speedMultiplier;
                    }
                    else
                    {
                        // Decay - fall smoothly
                        _zoneBrightness[i] -= DecayRate * deltaTime * speedMultiplier;
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

                // ~60 FPS
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
        var bytesPerSample = _capture?.WaveFormat.BitsPerSample / 8 ?? 4;
        var channels = _capture?.WaveFormat.Channels ?? 2;
        var bytesPerFrame = bytesPerSample * channels;

        lock (_fftLock)
        {
            for (var i = 0; i + bytesPerFrame <= e.BytesRecorded; i += bytesPerFrame)
            {
                // Read sample (assuming 32-bit float format from WASAPI)
                float sample;
                if (bytesPerSample == 4)
                {
                    sample = BitConverter.ToSingle(e.Buffer, i);
                }
                else if (bytesPerSample == 2)
                {
                    sample = BitConverter.ToInt16(e.Buffer, i) / 32768f;
                }
                else
                {
                    continue;
                }

                // Add to FFT buffer (mono mix if stereo)
                _fftBuffer[_fftBufferIndex] = sample;
                _fftBufferIndex++;

                if (_fftBufferIndex >= FftLength)
                {
                    _fftBufferIndex = 0;
                }
            }
        }
    }

    private void ProcessFft()
    {
        lock (_fftLock)
        {
            // Copy buffer to complex array with Hann window
            for (var i = 0; i < FftLength; i++)
            {
                var windowIndex = (_fftBufferIndex + i) % FftLength;
                var window = 0.5 * (1 - Math.Cos(2 * Math.PI * i / (FftLength - 1)));
                _fftComplex[i] = new Complex(_fftBuffer[windowIndex] * window, 0);
            }
        }

        // Perform in-place FFT
        Fft(_fftComplex, FftLengthLog2);

        // Calculate frequency resolution
        var binWidth = (double)_sampleRate / FftLength;

        // Calculate magnitude for each frequency band
        for (var band = 0; band < 4; band++)
        {
            var (lowFreq, highFreq) = FrequencyBands[band];
            var lowBin = (int)Math.Floor(lowFreq / binWidth);
            var highBin = (int)Math.Ceiling(highFreq / binWidth);

            lowBin = Math.Max(1, lowBin);
            highBin = Math.Min(FftLength / 2 - 1, highBin);

            // Sum magnitudes in this frequency range
            var sum = 0.0;
            var count = 0;

            for (var bin = lowBin; bin <= highBin; bin++)
            {
                var magnitude = _fftComplex[bin].Magnitude;
                sum += magnitude;
                count++;
            }

            // Average magnitude with sensitivity scaling
            var avgMagnitude = count > 0 ? sum / count : 0;
            var scaledMagnitude = avgMagnitude * BandSensitivity[band] * 15; // Boost for visibility

            // Apply logarithmic scaling for better perception
            var level = scaledMagnitude > 0 ? Math.Log10(1 + scaledMagnitude * 9) : 0;
            level = Math.Clamp(level, 0, 1);

            _zoneTargetLevel[band] = level;
        }
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

    private static RGBColor ApplyBrightness(RGBColor color, double brightness)
    {
        return new RGBColor(
            (byte)(color.R * brightness),
            (byte)(color.G * brightness),
            (byte)(color.B * brightness)
        );
    }

    private async Task RunIdleFallbackAsync(CustomRGBEffectController controller, CancellationToken cancellationToken)
    {
        // Fallback when no audio device - subtle breathing
        var stopwatch = Stopwatch.StartNew();

        while (!cancellationToken.IsCancellationRequested)
        {
            var time = stopwatch.Elapsed.TotalSeconds;

            for (var i = 0; i < 4; i++)
            {
                var phase = time * 0.5 + i * 0.3;
                _zoneBrightness[i] = MinBrightness + 0.15 * (Math.Sin(phase) * 0.5 + 0.5);
            }

            var colors = new ZoneColors(
                ApplyBrightness(_zoneColors.Zone1, _zoneBrightness[0]),
                ApplyBrightness(_zoneColors.Zone2, _zoneBrightness[1]),
                ApplyBrightness(_zoneColors.Zone3, _zoneBrightness[2]),
                ApplyBrightness(_zoneColors.Zone4, _zoneBrightness[3])
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
