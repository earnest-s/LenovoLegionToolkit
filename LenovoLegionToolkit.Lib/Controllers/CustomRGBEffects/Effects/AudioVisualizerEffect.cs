// ============================================================================
// AudioVisualizerEffect.cs
// 
// Industry-standard 4-zone spectrum visualizer for keyboards.
// Behaves like a real audio spectrum analyzer, not meters or swipe bars.
// 
// PIPELINE (6-stage, all stages maintain persistent state):
// 1. FFT → Band RMS (instantaneous spectral power per band)
// 2. Short-Term Energy Integration (time-domain accumulator)
// 3. Adaptive Normalization (rolling max reference)
// 4. Perceptual Curve (wave-preserving compression)
// 5. Envelope Follower (exponential attack/decay)
// 6. Spatial Bleed → Output (wave formation + brightness)
// 
// FREQUENCY MAPPING (STANDARD):
// Zone 0: 20-250 Hz (Bass)
// Zone 1: 250-2000 Hz (Low-Mid)
// Zone 2: 2000-6000 Hz (High-Mid)
// Zone 3: 6000-20000 Hz (Treble)
// ============================================================================

using System;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using NAudio.CoreAudioApi;
using NAudio.Wave;

namespace LenovoLegionToolkit.Lib.Controllers.CustomRGBEffects.Effects;

/// <summary>
/// FFT-based audio spectrum visualizer with true analog behavior.
/// Bass rolls on the left, mids overlap naturally, highs shimmer on the right.
/// Waves move and breathe like a real spectrum analyzer.
/// </summary>
public class AudioVisualizerEffect : ICustomRGBEffect, IDisposable
{
    private readonly int _speed;
    private readonly ZoneColors _zoneColors;
    private bool _disposed;

    // ========================================================================
    // FFT CONFIGURATION
    // ========================================================================
    private const int FftLength = 2048;
    private const int FftLengthLog2 = 11;
    private readonly float[] _fftBuffer = new float[FftLength];
    private readonly Complex[] _fftComplex = new Complex[FftLength];
    private int _fftBufferIndex;
    private readonly object _fftLock = new();

    // ========================================================================
    // AUDIO CAPTURE STATE
    // Sample rate derived from device, never hardcoded
    // ========================================================================
    private WasapiLoopbackCapture? _capture;
    private int _sampleRate = 48000;
    private WaveFormatEncoding _encoding = WaveFormatEncoding.IeeeFloat;
    private int _bytesPerSample = 4;
    private int _channels = 2;

    // ========================================================================
    // FREQUENCY BAND MAPPING (STANDARD 4-ZONE)
    // Static mapping, no motion logic
    // ========================================================================
    private static readonly (float Low, float High)[] FrequencyBands =
    {
        (20f, 250f),       // Zone 0: Bass (far left)
        (250f, 2000f),     // Zone 1: Low-Mid (mid-left)
        (2000f, 6000f),    // Zone 2: High-Mid (mid-right)
        (6000f, 20000f)    // Zone 3: Treble (far right)
    };

    // Per-band sensitivity (compensate for spectral energy distribution)
    // Bass and low-mids typically have more energy, treble less
    private static readonly float[] BandSensitivity = { 1.0f, 1.2f, 1.8f, 2.5f };

    // ========================================================================
    // STAGE 2: SHORT-TERM ENERGY INTEGRATION
    // Integrates RMS² over time with decay, provides temporal mass
    // energy += rms² * dt * gain
    // energy -= energy * decay * dt
    // TUNED: Higher gain + decay for faster wave oscillation
    // ========================================================================
    private readonly float[] _energy = new float[4];
    private const float EnergyGain = 0.30f;          // Increased for faster response (was 0.18)
    private const float EnergyDecay = 3.0f;          // Increased for quicker fade (was 2.0)

    // ========================================================================
    // STAGE 3: ADAPTIVE NORMALIZATION
    // Rolling max reference prevents saturation without hard clamps
    // maxEnergy = max(maxEnergy * decay, energy)
    // ========================================================================
    private readonly float[] _maxEnergy = new float[4];
    private const float MaxEnergyDecay = 0.995f;     // Very slow decay for stable reference
    private const float MinMaxEnergy = 0.0001f;      // Prevent division by zero

    // ========================================================================
    // STAGE 5: ENVELOPE FOLLOWER
    // Exponential smoothing with asymmetric attack/decay
    // tau = input > env ? attackTau : decayTau
    // alpha = 1 - exp(-dt / tau)
    // env += (input - env) * alpha
    // TUNED: Tighter time constants for faster wave motion
    // ========================================================================
    private readonly float[] _envelope = new float[4];
    private const float AttackTau = 0.075f;          // 75ms attack (was 150ms) - snappier response
    private const float DecayTau = 0.22f;            // 220ms decay (was 450ms) - quicker fade

    // ========================================================================
    // STAGE 6: SPATIAL BLEED (wave formation)
    // Applied AFTER envelope for proper wave propagation
    // TUNED: Lighter retention prevents smearing while keeping motion
    // ========================================================================
    private readonly float[] _output = new float[4];
    private const float SelfWeight = 0.60f;          // Reduced from 0.70 for less retention
    private const float NeighborWeight = 0.20f;      // Increased from 0.15 for more wave flow

    // Per-zone gain boost: makes bass/mids more animated
    private static readonly float[] ZoneGain = { 1.3f, 1.15f, 1.0f, 0.9f };

    // ========================================================================
    // STAGE 4: PERCEPTUAL CURVE
    // pow(x, 0.6) expands low-level detail, preserves waves
    // ========================================================================
    private const float PerceptualExponent = 0.6f;

    public AudioVisualizerEffect(ZoneColors? zoneColors = null, int speed = 2)
    {
        _speed = Math.Clamp(speed, 1, 4);
        _zoneColors = zoneColors ?? new ZoneColors(
            new RGBColor(255, 50, 0),   // Zone 0: Orange-Red (Bass)
            new RGBColor(255, 200, 0),  // Zone 1: Yellow (Low-Mid)
            new RGBColor(0, 255, 100),  // Zone 2: Green (High-Mid)
            new RGBColor(0, 150, 255)   // Zone 3: Cyan-Blue (Treble)
        );

        // Initialize all persistent state
        for (var i = 0; i < 4; i++)
        {
            _energy[i] = 0f;
            _maxEnergy[i] = MinMaxEnergy;
            _envelope[i] = 0f;
            _output[i] = 0f;
        }
    }

    public CustomRGBEffectType Type => CustomRGBEffectType.AudioVisualizer;
    public string Description => "FFT-based spectrum visualizer with wave-like motion";
    public bool RequiresInputMonitoring => false;
    public bool RequiresSystemAccess => true;

    public async Task RunAsync(CustomRGBEffectController controller, CancellationToken cancellationToken)
    {
        // Initialize WASAPI loopback capture
        try
        {
            _capture = new WasapiLoopbackCapture();
            _sampleRate = _capture.WaveFormat.SampleRate;  // Derive from device
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

        // Speed multiplier affects envelope timing (1-4 → 0.7x-1.3x)
        var speedMult = 0.55f + (_speed * 0.2f);

        try
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                // ============================================================
                // DELTA TIME (high precision, frame-rate independent)
                // ============================================================
                var nowTicks = stopwatch.ElapsedTicks;
                var dt = (float)((nowTicks - lastTicks) / ticksPerSecond);
                lastTicks = nowTicks;
                dt = Math.Clamp(dt, 0.001f, 0.1f);

                // ============================================================
                // STAGE 1: FFT → BAND RMS
                // Compute RMS power per band (square magnitudes before averaging)
                // ============================================================
                var bandRms = ComputeBandRms();

                // ============================================================
                // STAGE 2: SHORT-TERM ENERGY INTEGRATION
                // Provides temporal mass, smooths transients
                // Energy persists across frames (NOT reset)
                // ============================================================
                for (var z = 0; z < 4; z++)
                {
                    // Integrate RMS² (power) over time
                    var power = bandRms[z] * bandRms[z];
                    _energy[z] += power * dt * EnergyGain;

                    // Decay energy continuously
                    _energy[z] -= _energy[z] * EnergyDecay * dt;

                    // Prevent negative (numerical stability)
                    if (_energy[z] < 0f) _energy[z] = 0f;
                }

                // ============================================================
                // STAGE 3: ADAPTIVE NORMALIZATION
                // Rolling max reference for auto-gain without hard clamps
                // ============================================================
                for (var z = 0; z < 4; z++)
                {
                    // Update rolling max (instant rise, slow decay)
                    if (_energy[z] > _maxEnergy[z])
                    {
                        _maxEnergy[z] = _energy[z];
                    }
                    else
                    {
                        _maxEnergy[z] *= MaxEnergyDecay;
                        if (_maxEnergy[z] < MinMaxEnergy) _maxEnergy[z] = MinMaxEnergy;
                    }
                }

                // ============================================================
                // STAGE 4 + 5: PERCEPTUAL CURVE + ENVELOPE FOLLOWER
                // Normalize → pow(x, 0.6) → exponential envelope
                // ============================================================
                var attackAlpha = 1f - MathF.Exp(-dt * speedMult / AttackTau);
                var decayAlpha = 1f - MathF.Exp(-dt * speedMult / DecayTau);

                for (var z = 0; z < 4; z++)
                {
                    // Normalize against rolling max (no hard clamp to 1.0)
                    var normalized = _energy[z] / _maxEnergy[z];

                    // Per-zone gain boost: bass/mids more animated
                    normalized *= ZoneGain[z];

                    // Perceptual curve: preserves low-level detail and wave motion
                    var perceptual = MathF.Pow(normalized, PerceptualExponent);

                    // Envelope follower: asymmetric attack/decay
                    var alpha = perceptual > _envelope[z] ? attackAlpha : decayAlpha;
                    _envelope[z] += (perceptual - _envelope[z]) * alpha;
                }

                // ============================================================
                // STAGE 6: SPATIAL BLEED (wave formation)
                // Creates wave flow between zones, applied AFTER envelope
                // ============================================================
                for (var z = 0; z < 4; z++)
                {
                    var self = _envelope[z] * SelfWeight;
                    var left = (z > 0) ? _envelope[z - 1] * NeighborWeight : 0f;
                    var right = (z < 3) ? _envelope[z + 1] * NeighborWeight : 0f;
                    _output[z] = self + left + right;
                }

                // ============================================================
                // FINAL: OUTPUT BRIGHTNESS
                // Envelope × zone color (no per-frame clearing)
                // ============================================================
                var c0 = ScaleColor(_zoneColors.Zone1, _output[0]);
                var c1 = ScaleColor(_zoneColors.Zone2, _output[1]);
                var c2 = ScaleColor(_zoneColors.Zone3, _output[2]);
                var c3 = ScaleColor(_zoneColors.Zone4, _output[3]);

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
    /// STAGE 1: Compute RMS power for each frequency band.
    /// Uses squared magnitudes before averaging (energy, not amplitude).
    /// NO thresholds applied.
    /// </summary>
    private float[] ComputeBandRms()
    {
        var rms = new float[4];

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

        // Frequency resolution (Hz per bin)
        var binHz = (float)_sampleRate / FftLength;

        // Compute RMS for each band
        for (var b = 0; b < 4; b++)
        {
            var (lo, hi) = FrequencyBands[b];
            var loBin = Math.Max(1, (int)(lo / binHz));
            var hiBin = Math.Min(FftLength / 2 - 1, (int)(hi / binHz));

            // Sum squared magnitudes (energy)
            var sumSquared = 0.0;
            var binCount = 0;
            for (var k = loBin; k <= hiBin; k++)
            {
                var re = _fftComplex[k].Real;
                var im = _fftComplex[k].Imaginary;
                sumSquared += re * re + im * im;  // mag² (energy)
                binCount++;
            }

            // RMS = sqrt(sum(mag²) / count) with sensitivity scaling
            var bandRms = binCount > 0 ? (float)Math.Sqrt(sumSquared / binCount) : 0f;
            rms[b] = bandRms * BandSensitivity[b];
        }

        return rms;
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
    /// Uses same 6-stage pipeline with synthetic input.
    /// </summary>
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

            // Generate synthetic band input (phase-offset sine waves)
            var syntheticRms = new float[4];
            for (var z = 0; z < 4; z++)
            {
                var phase = time * 0.8 + z * 0.4;
                syntheticRms[z] = 0.12f * (float)(Math.Sin(phase) * 0.5 + 0.5);
            }

            // STAGE 2: Energy integration
            for (var z = 0; z < 4; z++)
            {
                var power = syntheticRms[z] * syntheticRms[z];
                _energy[z] += power * dt * EnergyGain;
                _energy[z] -= _energy[z] * EnergyDecay * dt;
                if (_energy[z] < 0f) _energy[z] = 0f;
            }

            // STAGE 3: Adaptive normalization
            for (var z = 0; z < 4; z++)
            {
                if (_energy[z] > _maxEnergy[z])
                    _maxEnergy[z] = _energy[z];
                else
                {
                    _maxEnergy[z] *= MaxEnergyDecay;
                    if (_maxEnergy[z] < MinMaxEnergy) _maxEnergy[z] = MinMaxEnergy;
                }
            }

            // STAGE 4+5: Perceptual + envelope (with zone gain boost)
            var attackAlpha = 1f - MathF.Exp(-dt / AttackTau);
            var decayAlpha = 1f - MathF.Exp(-dt / DecayTau);

            for (var z = 0; z < 4; z++)
            {
                var normalized = _energy[z] / _maxEnergy[z];
                normalized *= ZoneGain[z];  // Per-zone gain boost
                var perceptual = MathF.Pow(normalized, PerceptualExponent);
                var alpha = perceptual > _envelope[z] ? attackAlpha : decayAlpha;
                _envelope[z] += (perceptual - _envelope[z]) * alpha;
            }

            // STAGE 6: Spatial bleed
            for (var z = 0; z < 4; z++)
            {
                var self = _envelope[z] * SelfWeight;
                var left = (z > 0) ? _envelope[z - 1] * NeighborWeight : 0f;
                var right = (z < 3) ? _envelope[z + 1] * NeighborWeight : 0f;
                _output[z] = self + left + right;
            }

            // Output
            var colors = new ZoneColors(
                ScaleColor(_zoneColors.Zone1, _output[0]),
                ScaleColor(_zoneColors.Zone2, _output[1]),
                ScaleColor(_zoneColors.Zone3, _output[2]),
                ScaleColor(_zoneColors.Zone4, _output[3])
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
