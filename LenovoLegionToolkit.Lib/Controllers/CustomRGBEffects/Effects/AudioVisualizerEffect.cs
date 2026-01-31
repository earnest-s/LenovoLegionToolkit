// ============================================================================
// AudioVisualizerEffect.cs
// 
// FFT-based spectrum visualizer for 4-zone keyboards.
// Each zone represents a fixed frequency band (Bass, Low-Mid, High-Mid, Treble).
// Smooth analog meter behavior with attack/decay envelope.
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

    // Per-zone envelope state for smooth analog behavior
    private readonly double[] _zoneEnvelope = new double[4];           // Current smoothed level
    private readonly double[] _zoneRawLevel = new double[4];           // Raw FFT level (smoothed)
    private readonly double[] _zonePeakLevel = new double[4];          // Peak hold level
    private readonly double[] _zoneVelocity = new double[4];           // Rate of change for momentum

    // Frequency band boundaries (Hz) - mapped to zones
    private static readonly (double Low, double High)[] FrequencyBands =
    {
        (20, 150),      // Zone 0: Bass
        (150, 600),     // Zone 1: Low-Mid
        (600, 2500),    // Zone 2: High-Mid
        (2500, 16000)   // Zone 3: Treble
    };

    // Envelope parameters - tuned for smooth analog meter behavior
    private const double AttackTime = 0.015;         // 15ms attack (fast rise)
    private const double ReleaseTime = 0.35;         // 350ms release (slow fall)
    private const double PeakHoldTime = 0.08;        // 80ms peak hold before decay
    private const double PeakDecayTime = 0.5;        // 500ms peak decay
    private const double RawSmoothingFactor = 0.25;  // Smoothing on raw FFT input
    private const double MinBrightness = 0.03;       // Minimum visible glow
    private const double NeighborBleed = 0.15;       // How much neighbors affect each other

    // Sensitivity scaling per band (perceptual equalization)
    private static readonly double[] BandSensitivity = { 3.0, 2.0, 1.4, 1.0 };
    private static readonly double[] BandGamma = { 0.7, 0.75, 0.8, 0.85 }; // Gamma curve per band

    public AudioVisualizerEffect(ZoneColors? zoneColors = null, int speed = 2)
    {
        _speed = Math.Clamp(speed, 1, 4);
        _zoneColors = zoneColors ?? new ZoneColors(
            new RGBColor(255, 50, 0),   // Zone 0: Orange-Red (Bass)
            new RGBColor(255, 200, 0),  // Zone 1: Yellow (Low-Mid)
            new RGBColor(0, 255, 100),  // Zone 2: Green (High-Mid)
            new RGBColor(0, 150, 255)   // Zone 3: Cyan-Blue (Treble)
        );

        // Initialize envelopes to minimum
        for (var i = 0; i < 4; i++)
        {
            _zoneEnvelope[i] = MinBrightness;
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
        var lastTime = 0.0;

        // Speed multiplier affects envelope timing (1-4 maps to 0.7-1.6)
        var speedMultiplier = 0.4 + (_speed * 0.3);

        // Calculate time constants based on speed
        var attackCoeff = 1.0 - Math.Exp(-1.0 / (AttackTime * 60 / speedMultiplier));
        var releaseCoeff = 1.0 - Math.Exp(-1.0 / (ReleaseTime * 60 * speedMultiplier));

        try
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                var currentTime = stopwatch.Elapsed.TotalSeconds;
                var deltaTime = Math.Min(currentTime - lastTime, 0.05);
                lastTime = currentTime;

                // Process FFT and extract band levels
                var bandLevels = ProcessFftBands();

                // Update each zone with smooth envelope
                for (var i = 0; i < 4; i++)
                {
                    // Smooth the raw FFT level (low-pass filter on input)
                    _zoneRawLevel[i] += (bandLevels[i] - _zoneRawLevel[i]) * RawSmoothingFactor;

                    var targetLevel = _zoneRawLevel[i];

                    // Apply neighbor bleed for wave-like cohesion (not directional)
                    var neighborContribution = 0.0;
                    if (i > 0) neighborContribution += _zoneRawLevel[i - 1] * NeighborBleed;
                    if (i < 3) neighborContribution += _zoneRawLevel[i + 1] * NeighborBleed;
                    targetLevel = Math.Max(targetLevel, neighborContribution);

                    // Update peak hold with decay
                    if (targetLevel > _zonePeakLevel[i])
                    {
                        _zonePeakLevel[i] = targetLevel;
                    }
                    else
                    {
                        // Decay peak after hold time
                        var peakDecay = deltaTime / PeakDecayTime;
                        _zonePeakLevel[i] = Math.Max(targetLevel, _zonePeakLevel[i] - peakDecay);
                    }

                    // Envelope follower with asymmetric attack/release
                    var currentEnvelope = _zoneEnvelope[i];
                    double newEnvelope;

                    if (targetLevel > currentEnvelope)
                    {
                        // Attack phase - fast rise with slight overshoot
                        var delta = targetLevel - currentEnvelope;
                        _zoneVelocity[i] = Math.Max(_zoneVelocity[i], delta * attackCoeff * 2);
                        newEnvelope = currentEnvelope + _zoneVelocity[i];

                        // Clamp overshoot
                        if (newEnvelope > targetLevel)
                        {
                            newEnvelope = targetLevel;
                            _zoneVelocity[i] *= 0.5;
                        }
                    }
                    else
                    {
                        // Release phase - slow smooth decay
                        _zoneVelocity[i] *= 0.8; // Dampen velocity

                        // Blend toward target with peak influence
                        var effectiveTarget = Math.Max(targetLevel, _zonePeakLevel[i] * 0.6);
                        var delta = currentEnvelope - effectiveTarget;
                        newEnvelope = currentEnvelope - delta * releaseCoeff;

                        // Ensure we don't go below target
                        newEnvelope = Math.Max(newEnvelope, targetLevel);
                    }

                    // Apply minimum and clamp
                    _zoneEnvelope[i] = Math.Clamp(newEnvelope, MinBrightness, 1.0);
                }

                // Apply gamma correction and generate colors
                var finalBrightness = new double[4];
                for (var i = 0; i < 4; i++)
                {
                    // Gamma correction for perceptual linearity
                    finalBrightness[i] = Math.Pow(_zoneEnvelope[i], BandGamma[i]);
                    finalBrightness[i] = Math.Clamp(finalBrightness[i], MinBrightness, 1.0);
                }

                var colors = new ZoneColors(
                    ApplyBrightness(_zoneColors.Zone1, finalBrightness[0]),
                    ApplyBrightness(_zoneColors.Zone2, finalBrightness[1]),
                    ApplyBrightness(_zoneColors.Zone3, finalBrightness[2]),
                    ApplyBrightness(_zoneColors.Zone4, finalBrightness[3])
                );

                await controller.SetColorsAsync(colors, cancellationToken).ConfigureAwait(false);

                // ~60 FPS for smooth animation
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

                // Stereo to mono mix
                if (channels >= 2 && i + bytesPerFrame * 2 <= e.BytesRecorded)
                {
                    float sample2;
                    if (bytesPerSample == 4)
                    {
                        sample2 = BitConverter.ToSingle(e.Buffer, i + bytesPerSample);
                    }
                    else
                    {
                        sample2 = BitConverter.ToInt16(e.Buffer, i + bytesPerSample) / 32768f;
                    }
                    sample = (sample + sample2) * 0.5f;
                }

                _fftBuffer[_fftBufferIndex] = sample;
                _fftBufferIndex = (_fftBufferIndex + 1) % FftLength;
            }
        }
    }

    private double[] ProcessFftBands()
    {
        var levels = new double[4];

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

        // Perform FFT
        Fft(_fftComplex, FftLengthLog2);

        // Calculate frequency resolution
        var binWidth = (double)_sampleRate / FftLength;

        // Extract magnitude for each frequency band
        for (var band = 0; band < 4; band++)
        {
            var (lowFreq, highFreq) = FrequencyBands[band];
            var lowBin = Math.Max(1, (int)Math.Floor(lowFreq / binWidth));
            var highBin = Math.Min(FftLength / 2 - 1, (int)Math.Ceiling(highFreq / binWidth));

            // Use RMS of magnitudes for more stable reading
            var sumSquares = 0.0;
            var count = 0;

            for (var bin = lowBin; bin <= highBin; bin++)
            {
                var magnitude = _fftComplex[bin].Magnitude;
                sumSquares += magnitude * magnitude;
                count++;
            }

            // RMS magnitude
            var rmsMagnitude = count > 0 ? Math.Sqrt(sumSquares / count) : 0;

            // Apply sensitivity and boost
            var scaledMagnitude = rmsMagnitude * BandSensitivity[band] * 20;

            // Logarithmic/perceptual scaling
            double level;
            if (scaledMagnitude > 0)
            {
                // Attempt to map typical audio range to 0-1
                // Using soft compression curve
                level = 1.0 - (1.0 / (1.0 + scaledMagnitude * 2));
            }
            else
            {
                level = 0;
            }

            levels[band] = Math.Clamp(level, 0, 1);
        }

        return levels;
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
        // Fallback when no audio device - subtle smooth breathing
        var stopwatch = Stopwatch.StartNew();

        while (!cancellationToken.IsCancellationRequested)
        {
            var time = stopwatch.Elapsed.TotalSeconds;

            for (var i = 0; i < 4; i++)
            {
                // Staggered breathing with smooth sine waves
                var phase = time * 0.8 + i * 0.4;
                var breath = Math.Sin(phase) * 0.5 + 0.5;
                var target = MinBrightness + 0.2 * breath;

                // Smooth interpolation
                _zoneEnvelope[i] += (target - _zoneEnvelope[i]) * 0.05;
                _zoneEnvelope[i] = Math.Clamp(_zoneEnvelope[i], MinBrightness, 1.0);
            }

            var colors = new ZoneColors(
                ApplyBrightness(_zoneColors.Zone1, _zoneEnvelope[0]),
                ApplyBrightness(_zoneColors.Zone2, _zoneEnvelope[1]),
                ApplyBrightness(_zoneColors.Zone3, _zoneEnvelope[2]),
                ApplyBrightness(_zoneColors.Zone4, _zoneEnvelope[3])
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
