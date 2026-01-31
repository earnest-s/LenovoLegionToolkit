// ============================================================================
// AudioVisualizerEffect.cs
// 
// Spectrum-bin-driven visualizer following KeyboardVisualizer logic.
// NOT an envelope-based meter. Zones sample fixed spectrum indices.
// 
// PIPELINE (KeyboardVisualizer-style):
// 1. Audio Capture (WASAPI loopback)
// 2. FFT → Full 256-bin magnitude spectrum
// 3. Per-bin temporal smoothing
// 4. Zone → Bin index mapping (fixed indices)
// 5. Perceptual scaling → Zone brightness output
// 
// Waves appear NATURALLY because adjacent spectrum bins move over time.
// NO envelope followers. NO spatial bleed. NO swipe logic.
// ============================================================================

using System;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using NAudio.CoreAudioApi;
using NAudio.Wave;

namespace LenovoLegionToolkit.Lib.Controllers.CustomRGBEffects.Effects;

/// <summary>
/// KeyboardVisualizer-style spectrum visualizer for 4-zone keyboards.
/// Each zone samples a fixed bin index from the 256-bin spectrum.
/// </summary>
public class AudioVisualizerEffect : ICustomRGBEffect, IDisposable
{
    private readonly int _speed;
    private readonly ZoneColors _zoneColors;
    private bool _disposed;

    // ========================================================================
    // FFT CONFIGURATION
    // 512-point FFT → 256 usable bins (Nyquist)
    // ========================================================================
    private const int FftLength = 512;
    private const int FftLengthLog2 = 9;
    private const int SpectrumBins = 256;
    private readonly float[] _fftBuffer = new float[FftLength];
    private readonly Complex[] _fftComplex = new Complex[FftLength];
    private int _fftBufferIndex;
    private readonly object _fftLock = new();

    // ========================================================================
    // SPECTRUM STATE (256 bins, temporally smoothed)
    // This is the core data structure - NOT per-zone envelopes
    // ========================================================================
    private readonly float[] _spectrum = new float[SpectrumBins];
    private readonly float[] _spectrumPrev = new float[SpectrumBins];

    // ========================================================================
    // AUDIO CAPTURE STATE
    // ========================================================================
    private WasapiLoopbackCapture? _capture;
    private int _sampleRate = 48000;
    private WaveFormatEncoding _encoding = WaveFormatEncoding.IeeeFloat;
    private int _bytesPerSample = 4;
    private int _channels = 2;

    // ========================================================================
    // ZONE → BIN INDEX MAPPING (KeyboardVisualizer formula)
    // binIndex = (zone * (256 / zoneCount)) + (128 / zoneCount)
    // Zone 0 → bin 32, Zone 1 → bin 96, Zone 2 → bin 160, Zone 3 → bin 224
    // ========================================================================
    private const int ZoneCount = 4;
    private static readonly int[] ZoneBinIndex = new int[ZoneCount];

    // ========================================================================
    // SMOOTHING & DECAY PARAMETERS
    // ========================================================================
    private const float SpectrumSmooth = 0.7f;       // Current frame weight
    private const float SpectrumPrevWeight = 0.3f;   // Previous frame weight
    private const float DecayRate = 7.0f;            // Fast decay on silence (6-8 range)
    private const float PerceptualExponent = 0.6f;   // pow(x, 0.6) for brightness scaling
    private const float InputGain = 2.5f;            // Amplify spectrum for visibility

    static AudioVisualizerEffect()
    {
        // Precompute zone → bin index mapping (KeyboardVisualizer formula)
        for (var z = 0; z < ZoneCount; z++)
        {
            ZoneBinIndex[z] = (z * (SpectrumBins / ZoneCount)) + (SpectrumBins / (2 * ZoneCount));
        }
        // Result: [32, 96, 160, 224]
    }

    public AudioVisualizerEffect(ZoneColors? zoneColors = null, int speed = 2)
    {
        _speed = Math.Clamp(speed, 1, 4);
        _zoneColors = zoneColors ?? new ZoneColors(
            new RGBColor(255, 50, 0),   // Zone 0: Orange-Red (Bass)
            new RGBColor(255, 200, 0),  // Zone 1: Yellow (Low-Mid)
            new RGBColor(0, 255, 100),  // Zone 2: Green (High-Mid)
            new RGBColor(0, 150, 255)   // Zone 3: Cyan-Blue (Treble)
        );

        // Initialize spectrum arrays to zero
        Array.Clear(_spectrum, 0, SpectrumBins);
        Array.Clear(_spectrumPrev, 0, SpectrumBins);
    }

    public CustomRGBEffectType Type => CustomRGBEffectType.AudioVisualizer;
    public string Description => "KeyboardVisualizer-style spectrum visualizer";
    public bool RequiresInputMonitoring => false;
    public bool RequiresSystemAccess => true;

    public async Task RunAsync(CustomRGBEffectController controller, CancellationToken cancellationToken)
    {
        // Initialize WASAPI loopback capture
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

        // Speed multiplier affects decay rate (1-4 → 0.7x-1.3x)
        var speedMult = 0.55f + (_speed * 0.2f);

        try
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                // ============================================================
                // DELTA TIME
                // ============================================================
                var nowTicks = stopwatch.ElapsedTicks;
                var dt = (float)((nowTicks - lastTicks) / ticksPerSecond);
                lastTicks = nowTicks;
                dt = Math.Clamp(dt, 0.001f, 0.1f);

                // ============================================================
                // STEP 1: COMPUTE FFT → 256-BIN MAGNITUDE SPECTRUM
                // ============================================================
                ComputeSpectrum();

                // ============================================================
                // STEP 2: PER-BIN TEMPORAL SMOOTHING + DECAY
                // spectrum[i] = current * 0.7 + previous * 0.3
                // spectrum[i] *= exp(-decayRate * dt)
                // ============================================================
                var decayFactor = MathF.Exp(-DecayRate * dt * speedMult);

                for (var i = 0; i < SpectrumBins; i++)
                {
                    // Temporal smoothing (blend with previous frame)
                    _spectrum[i] = _spectrum[i] * SpectrumSmooth + _spectrumPrev[i] * SpectrumPrevWeight;

                    // Apply decay (fast fade on silence)
                    _spectrum[i] *= decayFactor;

                    // Store for next frame
                    _spectrumPrev[i] = _spectrum[i];
                }

                // ============================================================
                // STEP 3: ZONE → BIN INDEX LOOKUP + PERCEPTUAL SCALING
                // Each zone samples its fixed bin index
                // brightness = pow(spectrum[binIndex], 0.6)
                // ============================================================
                var zoneBrightness = new float[ZoneCount];

                for (var z = 0; z < ZoneCount; z++)
                {
                    var binIndex = ZoneBinIndex[z];
                    var amplitude = _spectrum[binIndex];

                    // Apply input gain and clamp to reasonable range
                    amplitude *= InputGain;
                    amplitude = Math.Clamp(amplitude, 0f, 1f);

                    // Perceptual scaling: pow(x, 0.6) for brightness
                    zoneBrightness[z] = MathF.Pow(amplitude, PerceptualExponent);
                }

                // ============================================================
                // STEP 4: OUTPUT ZONE COLORS
                // ============================================================
                var c0 = ScaleColor(_zoneColors.Zone1, zoneBrightness[0]);
                var c1 = ScaleColor(_zoneColors.Zone2, zoneBrightness[1]);
                var c2 = ScaleColor(_zoneColors.Zone3, zoneBrightness[2]);
                var c3 = ScaleColor(_zoneColors.Zone4, zoneBrightness[3]);

                await controller.SetColorsAsync(new ZoneColors(c0, c1, c2, c3), cancellationToken).ConfigureAwait(false);

                // ~60 FPS update rate
                await Task.Delay(16, cancellationToken).ConfigureAwait(false);
            }
        }
        finally
        {
            StopCapture();
        }
    }

    /// <summary>
    /// Compute FFT and populate 256-bin magnitude spectrum.
    /// Magnitudes are written directly into _spectrum array (additive, for smoothing).
    /// </summary>
    private void ComputeSpectrum()
    {
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

        // In-place Cooley-Tukey FFT
        Fft(_fftComplex, FftLengthLog2);

        // Compute magnitude for each bin (0 to 255)
        // Bins 0..255 represent frequencies 0 to Nyquist
        for (var i = 0; i < SpectrumBins; i++)
        {
            var re = _fftComplex[i].Real;
            var im = _fftComplex[i].Imaginary;
            var magnitude = (float)Math.Sqrt(re * re + im * im);

            // Normalize by FFT length
            magnitude /= FftLength;

            // Write to spectrum (additive blend handled in main loop)
            _spectrum[i] = Math.Max(_spectrum[i], magnitude);
        }
    }

    /// <summary>
    /// Handle incoming audio data with proper format detection.
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

                // Decode based on WaveFormat.Encoding
                if (_encoding == WaveFormatEncoding.IeeeFloat)
                {
                    if (_bytesPerSample == 4)
                    {
                        sample = BitConverter.ToSingle(e.Buffer, offset);
                    }
                }
                else if (_encoding == WaveFormatEncoding.Pcm)
                {
                    if (_bytesPerSample == 2)
                    {
                        sample = BitConverter.ToInt16(e.Buffer, offset) / 32768f;
                    }
                    else if (_bytesPerSample == 3)
                    {
                        var val = e.Buffer[offset] | (e.Buffer[offset + 1] << 8) | (e.Buffer[offset + 2] << 16);
                        if ((val & 0x800000) != 0) val |= unchecked((int)0xFF000000);
                        sample = val / 8388608f;
                    }
                    else if (_bytesPerSample == 4)
                    {
                        sample = BitConverter.ToInt32(e.Buffer, offset) / 2147483648f;
                    }
                }
                else if (_encoding == WaveFormatEncoding.Extensible)
                {
                    if (_bytesPerSample == 4)
                    {
                        sample = BitConverter.ToSingle(e.Buffer, offset);
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

                // Clamp to valid range
                sample = Math.Clamp(sample, -1f, 1f);

                // Store mono sample (first channel)
                _fftBuffer[_fftBufferIndex] = sample;
                _fftBufferIndex = (_fftBufferIndex + 1) % FftLength;

                offset += frameBytes;
            }
        }
    }

    /// <summary>
    /// Scale RGB color by brightness factor.
    /// </summary>
    private static RGBColor ScaleColor(RGBColor color, float brightness)
    {
        brightness = Math.Clamp(brightness, 0f, 1f);
        return new RGBColor(
            (byte)(color.R * brightness),
            (byte)(color.G * brightness),
            (byte)(color.B * brightness)
        );
    }

    /// <summary>
    /// In-place Cooley-Tukey radix-2 FFT.
    /// </summary>
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
                    w *= wm;
                }
            }
        }
    }

    /// <summary>
    /// Bit-reversal for FFT permutation.
    /// </summary>
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

    /// <summary>
    /// Fallback animation when audio capture fails.
    /// Simulates spectrum movement with sine waves.
    /// </summary>
    private async Task RunIdleFallbackAsync(CustomRGBEffectController controller, CancellationToken cancellationToken)
    {
        var stopwatch = Stopwatch.StartNew();

        while (!cancellationToken.IsCancellationRequested)
        {
            var time = stopwatch.Elapsed.TotalSeconds;

            // Generate synthetic spectrum-like values
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
    /// Complex number struct for FFT computation.
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
