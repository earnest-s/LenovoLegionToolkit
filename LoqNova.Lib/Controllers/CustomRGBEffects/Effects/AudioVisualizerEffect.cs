// ============================================================================
// AudioVisualizerEffect.cs
//
// Minimal 4-zone audio visualizer for keyboards.
// Captures system audio via WASAPI loopback, computes per-zone frequency band
// energy using FFT, and displays binary on/off per zone with smooth fading.
//
// PIPELINE:
// 1. Audio Capture (WASAPI loopback ΓåÆ mono ring buffer)
// 2. Windowed FFT ΓåÆ magnitude spectrum
// 3. Per-zone RMS over frequency bands
// 4. Per-zone AGC normalization
// 5. Threshold ΓåÆ binary on/off
// 6. Smooth fade ΓåÆ output
// ============================================================================

using System;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using NAudio.CoreAudioApi;
using NAudio.Wave;

namespace LoqNova.Lib.Controllers.CustomRGBEffects.Effects;

public class AudioVisualizerEffect : ICustomRGBEffect, IDisposable
{
    // ========================================================================
    // CONSTANTS
    // ========================================================================
    private const int FftSize = 2048;
    private const int FftHalf = FftSize / 2;

    // ========================================================================
    // CONFIGURATION
    // ========================================================================
    private readonly int _speed;
    private readonly ZoneColors _zoneColors;
    private bool _disposed;

    // ========================================================================
    // AUDIO CAPTURE
    // ========================================================================
    private WasapiLoopbackCapture? _capture;
    private WaveFormatEncoding _encoding = WaveFormatEncoding.IeeeFloat;
    private int _bytesPerSample = 4;
    private int _channels = 2;
    private int _sampleRate = 48000;
    private readonly object _audioLock = new();

    // ========================================================================
    // RING BUFFER + FFT (pre-allocated, reused every frame)
    // ========================================================================
    private readonly float[] _ringBuffer = new float[FftSize];
    private int _ringWritePos;
    private bool _ringReady;
    private readonly float[] _hannWindow = new float[FftSize];
    private readonly float[] _fftTimeDomain = new float[FftSize];
    private readonly double[] _fftReal = new double[FftSize];
    private readonly double[] _fftImag = new double[FftSize];
    private readonly float[] _fftMagnitudes = new float[FftHalf];

    // ========================================================================
    // ZONE COLOR CACHE (pre-allocated)
    // ========================================================================
    private readonly RGBColor[] _zoneColorArray = new RGBColor[4];

    // ========================================================================
    // WAVE STATE
    // ========================================================================
    private float _bassAverage;

    // ========================================================================
    // CONSTRUCTOR
    // ========================================================================
    public AudioVisualizerEffect(ZoneColors? zoneColors = null, int speed = 2)
    {
        _speed = Math.Clamp(speed, 1, 4);
        _zoneColors = zoneColors ?? new ZoneColors(
            new RGBColor(255, 50, 0),
            new RGBColor(255, 200, 0),
            new RGBColor(0, 255, 100),
            new RGBColor(0, 150, 255)
        );

        _zoneColorArray[0] = _zoneColors.Zone1;
        _zoneColorArray[1] = _zoneColors.Zone2;
        _zoneColorArray[2] = _zoneColors.Zone3;
        _zoneColorArray[3] = _zoneColors.Zone4;

        for (var i = 0; i < FftSize; i++)
            _hannWindow[i] = 0.5f * (1f - MathF.Cos(2f * MathF.PI * i / (FftSize - 1)));
    }

    // ========================================================================
    // INTERFACE
    // ========================================================================
    public CustomRGBEffectType Type => CustomRGBEffectType.AudioVisualizer;
    public string Description => "Audio-driven frequency visualizer";
    public bool RequiresInputMonitoring => false;
    public bool RequiresSystemAccess => true;

    // ========================================================================
    // MAIN LOOP
    // ========================================================================
    public async Task RunAsync(CustomRGBEffectController controller, CancellationToken cancellationToken)
    {
        // --- Start capture ---
        try
        {
            _capture = new WasapiLoopbackCapture();
            _encoding = _capture.WaveFormat.Encoding;
            _bytesPerSample = _capture.WaveFormat.BitsPerSample / 8;
            _channels = _capture.WaveFormat.Channels;
            _sampleRate = _capture.WaveFormat.SampleRate;
            _capture.DataAvailable += OnDataAvailable;
            _capture.StartRecording();
        }
        catch
        {
            await RunIdleFallbackAsync(controller, cancellationToken).ConfigureAwait(false);
            return;
        }

        // --- Pre-allocated frame buffers ---
        var bucketRms = new float[4];
        var zoneFreqLow = new float[] { 20f, 250f, 2000f, 6000f };
        var zoneFreqHigh = new float[] { 250f, 2000f, 6000f, 16000f };

        var stopwatch = Stopwatch.StartNew();
        var lastTicks = stopwatch.ElapsedTicks;
        var ticksPerSecond = (double)Stopwatch.Frequency;

        try
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                // --- Delta time ---
                var nowTicks = stopwatch.ElapsedTicks;
                var dt = (float)((nowTicks - lastTicks) / ticksPerSecond);
                lastTicks = nowTicks;
                dt = Math.Clamp(dt, 0.001f, 0.1f);

                // ========================================================
                // FFT + FREQUENCY BAND RMS
                // ========================================================
                bool haveData;
                lock (_audioLock)
                {
                    haveData = _ringReady;
                    if (haveData)
                    {
                        var pos = _ringWritePos;
                        for (var i = 0; i < FftSize; i++)
                            _fftTimeDomain[i] = _ringBuffer[(pos + i) % FftSize];
                    }
                }

                if (haveData)
                {
                    for (var i = 0; i < FftSize; i++)
                    {
                        _fftReal[i] = _fftTimeDomain[i] * _hannWindow[i];
                        _fftImag[i] = 0.0;
                    }

                    Fft(_fftReal, _fftImag, FftSize);

                    for (var i = 0; i < FftHalf; i++)
                    {
                        var re = _fftReal[i];
                        var im = _fftImag[i];
                        _fftMagnitudes[i] = (float)Math.Sqrt(re * re + im * im) / FftSize;
                    }

                    var binWidth = (float)_sampleRate / FftSize;
                    for (var z = 0; z < 4; z++)
                    {
                        var lo = (int)(zoneFreqLow[z] / binWidth);
                        var hi = (int)(zoneFreqHigh[z] / binWidth);
                        lo = Math.Clamp(lo, 1, FftHalf - 1);
                        hi = Math.Clamp(hi, lo + 1, FftHalf);

                        var sumSq = 0f;
                        var count = 0;
                        for (var b = lo; b < hi; b++)
                        {
                            var m = _fftMagnitudes[b];
                            sumSq += m * m;
                            count++;
                        }
                        bucketRms[z] = count > 0 ? MathF.Sqrt(sumSq / count) : 0f;
                    }
                }
                else
                {
                    Array.Clear(bucketRms, 0, 4);
                }

                // ========================================================
                // PROGRESSIVE FILL WITH REACTIVE HEAD
                // ========================================================

                // 1. Multi-band energy (balanced)
                float energy =
                    bucketRms[0] * 1.5f +
                    bucketRms[1] * 1.0f +
                    bucketRms[2] * 0.7f +
                    bucketRms[3] * 0.4f;

                // 2. AGC normalization
                _bassAverage = _bassAverage * 0.98f + energy * 0.02f;

                float normalized = 0f;
                if (_bassAverage > 0.000001f)
                    normalized = energy / _bassAverage;

                normalized = Math.Clamp(normalized, 0f, 4f);

                // 3. Determine level (0ΓÇô3)
                int level = (int)Math.Clamp(MathF.Floor(normalized), 0f, 3f);

                // 4. Determine if head is active this frame
                bool headOn = normalized > level + 0.2f;

                RGBColor off = new RGBColor(0, 0, 0);

                RGBColor c0 = off;
                RGBColor c1 = off;
                RGBColor c2 = off;
                RGBColor c3 = off;

                // Solid zones below head
                if (level >= 1) c0 = _zoneColorArray[0];
                if (level >= 2) c1 = _zoneColorArray[1];
                if (level >= 3) c2 = _zoneColorArray[2];

                // Reactive head
                if (headOn)
                {
                    if (level == 0) c0 = _zoneColorArray[0];
                    else if (level == 1) c1 = _zoneColorArray[1];
                    else if (level == 2) c2 = _zoneColorArray[2];
                    else if (level == 3) c3 = _zoneColorArray[3];
                }

                await controller.SetColorsAsync(
                    new ZoneColors(c0, c1, c2, c3),
                    cancellationToken
                ).ConfigureAwait(false);
                await Task.Delay(16, cancellationToken).ConfigureAwait(false);
            }
        }
        finally
        {
            StopCapture();
        }
    }

    // ========================================================================
    // AUDIO CALLBACK ΓÇö fills mono ring buffer
    // ========================================================================
    private void OnDataAvailable(object? sender, WaveInEventArgs e)
    {
        if (_capture == null || e.BytesRecorded == 0) return;

        var frameBytes = _bytesPerSample * _channels;
        var totalFrames = e.BytesRecorded / frameBytes;

        lock (_audioLock)
        {
            for (var f = 0; f < totalFrames; f++)
            {
                var offset = f * frameBytes;
                if (offset + _bytesPerSample > e.BytesRecorded) break;

                float sample;
                if (_encoding == WaveFormatEncoding.IeeeFloat && _bytesPerSample == 4)
                    sample = BitConverter.ToSingle(e.Buffer, offset);
                else if (_encoding == WaveFormatEncoding.Pcm && _bytesPerSample == 2)
                    sample = BitConverter.ToInt16(e.Buffer, offset) / 32768f;
                else if (_encoding == WaveFormatEncoding.Extensible && _bytesPerSample == 4)
                {
                    sample = BitConverter.ToSingle(e.Buffer, offset);
                    if (float.IsNaN(sample) || float.IsInfinity(sample) || sample < -10f || sample > 10f)
                        sample = BitConverter.ToInt32(e.Buffer, offset) / 2147483648f;
                }
                else
                    sample = 0f;

                sample = Math.Clamp(sample, -1f, 1f);
                _ringBuffer[_ringWritePos] = sample;
                _ringWritePos = (_ringWritePos + 1) % FftSize;
            }

            if (totalFrames > 0)
                _ringReady = true;
        }
    }

    // ========================================================================
    // IDLE FALLBACK (when capture fails)
    // ========================================================================
    private async Task RunIdleFallbackAsync(CustomRGBEffectController controller, CancellationToken cancellationToken)
    {
        var stopwatch = Stopwatch.StartNew();
        while (!cancellationToken.IsCancellationRequested)
        {
            var t = stopwatch.Elapsed.TotalSeconds;
            var colors = new ZoneColors(
                ScaleColor(_zoneColorArray[0], 0.3f + 0.2f * (float)Math.Sin(t * 1.2 + 0.0)),
                ScaleColor(_zoneColorArray[1], 0.3f + 0.2f * (float)Math.Sin(t * 1.2 + 0.5)),
                ScaleColor(_zoneColorArray[2], 0.3f + 0.2f * (float)Math.Sin(t * 1.2 + 1.0)),
                ScaleColor(_zoneColorArray[3], 0.3f + 0.2f * (float)Math.Sin(t * 1.2 + 1.5))
            );
            await controller.SetColorsAsync(colors, cancellationToken).ConfigureAwait(false);
            await Task.Delay(33, cancellationToken).ConfigureAwait(false);
        }
    }

    // ========================================================================
    // HELPERS
    // ========================================================================
    private static RGBColor ScaleColor(RGBColor color, float brightness)
    {
        brightness = Math.Clamp(brightness, 0f, 1f);
        return new RGBColor(
            (byte)(color.R * brightness),
            (byte)(color.G * brightness),
            (byte)(color.B * brightness)
        );
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

    // ========================================================================
    // COOLEY-TUKEY RADIX-2 DIT FFT (in-place)
    // ========================================================================
    private static void Fft(double[] real, double[] imag, int n)
    {
        var j = 0;
        for (var i = 0; i < n - 1; i++)
        {
            if (i < j)
            {
                (real[i], real[j]) = (real[j], real[i]);
                (imag[i], imag[j]) = (imag[j], imag[i]);
            }
            var k = n >> 1;
            while (k <= j)
            {
                j -= k;
                k >>= 1;
            }
            j += k;
        }

        for (var len = 2; len <= n; len <<= 1)
        {
            var halfLen = len >> 1;
            var angle = -2.0 * Math.PI / len;
            var wReal = Math.Cos(angle);
            var wImag = Math.Sin(angle);

            for (var i = 0; i < n; i += len)
            {
                var curReal = 1.0;
                var curImag = 0.0;
                for (var m = 0; m < halfLen; m++)
                {
                    var tReal = curReal * real[i + m + halfLen] - curImag * imag[i + m + halfLen];
                    var tImag = curReal * imag[i + m + halfLen] + curImag * real[i + m + halfLen];
                    real[i + m + halfLen] = real[i + m] - tReal;
                    imag[i + m + halfLen] = imag[i + m] - tImag;
                    real[i + m] += tReal;
                    imag[i + m] += tImag;
                    var newCurReal = curReal * wReal - curImag * wImag;
                    curImag = curReal * wImag + curImag * wReal;
                    curReal = newCurReal;
                }
            }
        }
    }

    // ========================================================================
    // DISPOSE
    // ========================================================================
    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        StopCapture();
        GC.SuppressFinalize(this);
    }
}
