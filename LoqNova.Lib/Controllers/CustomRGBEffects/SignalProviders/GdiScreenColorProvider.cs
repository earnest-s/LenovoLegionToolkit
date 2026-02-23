using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;

namespace LoqNova.Lib.Controllers.CustomRGBEffects.SignalProviders;

/// <summary>
/// Provides screen color samples using GDI screen capture.
/// 
/// Rust origin: ambient.rs captures screen and averages colors.
/// 
/// Note: This uses GDI capture instead of DXGI Desktop Duplication for broader compatibility.
/// GDI is slightly less efficient but works on all Windows versions without additional dependencies.
/// 
/// Hardware note: Captures are downsampled before averaging to reduce CPU load.
/// Capture rate is limited to 30 FPS maximum.
/// 
/// Color science notes:
/// - Screen capture returns sRGB gamma-encoded pixels
/// - Averaging must be done in linear space to avoid color distortion
/// - sRGB gamma: approx 2.2, with linear segment near black
/// - This implementation uses proper sRGB → linear → sRGB conversion
/// </summary>
public sealed class GdiScreenColorProvider : IScreenColorProvider
{
    private const int MaxFps = 30;
    private const int DownsampleFactor = 16; // Sample every 16th pixel for performance
    private static readonly TimeSpan CaptureInterval = TimeSpan.FromMilliseconds(1000.0 / MaxFps);

    // Pre-computed sRGB to linear lookup table for performance
    private static readonly float[] SrgbToLinear = new float[256];
    // Pre-computed linear to sRGB lookup table (256 entries covering 0-1 range)
    private static readonly byte[] LinearToSrgb = new byte[256];

    static GdiScreenColorProvider()
    {
        // Build sRGB to linear LUT
        for (var i = 0; i < 256; i++)
        {
            var srgb = i / 255.0f;
            SrgbToLinear[i] = SrgbToLinearChannel(srgb);
        }

        // Build linear to sRGB LUT
        for (var i = 0; i < 256; i++)
        {
            var linear = i / 255.0f;
            LinearToSrgb[i] = (byte)Math.Clamp((int)(LinearToSrgbChannel(linear) * 255.0f + 0.5f), 0, 255);
        }
    }

    /// <summary>
    /// Converts a single sRGB channel value (0-1) to linear space.
    /// Uses the official sRGB transfer function with linear toe.
    /// </summary>
    private static float SrgbToLinearChannel(float srgb)
    {
        if (srgb <= 0.04045f)
            return srgb / 12.92f;
        return MathF.Pow((srgb + 0.055f) / 1.055f, 2.4f);
    }

    /// <summary>
    /// Converts a single linear channel value (0-1) to sRGB space.
    /// Uses the official sRGB transfer function with linear toe.
    /// </summary>
    private static float LinearToSrgbChannel(float linear)
    {
        if (linear <= 0.0031308f)
            return linear * 12.92f;
        return 1.055f * MathF.Pow(linear, 1.0f / 2.4f) - 0.055f;
    }

    [DllImport("user32.dll")]
    private static extern IntPtr GetDesktopWindow();

    [DllImport("user32.dll")]
    private static extern IntPtr GetWindowDC(IntPtr hWnd);

    [DllImport("user32.dll")]
    private static extern int ReleaseDC(IntPtr hWnd, IntPtr hDC);

    [DllImport("gdi32.dll")]
    private static extern bool BitBlt(IntPtr hdcDest, int xDest, int yDest, int wDest, int hDest,
        IntPtr hdcSource, int xSrc, int ySrc, int rop);

    [DllImport("user32.dll")]
    private static extern int GetSystemMetrics(int nIndex);

    private const int SM_CXSCREEN = 0;
    private const int SM_CYSCREEN = 1;
    private const int SRCCOPY = 0x00CC0020;

    private readonly object _lock = new();
    private CancellationTokenSource? _captureCts;
    private Task? _captureTask;
    private bool _disposed;

    private Color _lastCapturedColor = Color.Black;
    private Color[] _lastCapturedZoneColors = [Color.Black, Color.Black, Color.Black, Color.Black];

    /// <inheritdoc />
    public Color LastCapturedColor
    {
        get { lock (_lock) return _lastCapturedColor; }
    }

    /// <inheritdoc />
    public Color[] LastCapturedZoneColors
    {
        get { lock (_lock) return (Color[])_lastCapturedZoneColors.Clone(); }
    }

    /// <inheritdoc />
    public bool IsCapturing => _captureTask is { IsCompleted: false };

    /// <inheritdoc />
    public Task StartAsync(CancellationToken cancellationToken = default)
    {
        if (IsCapturing)
            return Task.CompletedTask;

        _captureCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        _captureTask = CaptureLoopAsync(_captureCts.Token);
        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public void Stop()
    {
        _captureCts?.Cancel();

        try
        {
            _captureTask?.Wait(TimeSpan.FromSeconds(1));
        }
        catch (AggregateException)
        {
            // Task was cancelled, expected
        }

        _captureCts?.Dispose();
        _captureCts = null;
    }

    /// <inheritdoc />
    public Task<Color[]> CaptureZoneColorsAsync(CancellationToken cancellationToken = default)
    {
        return Task.Run(CaptureFrame, cancellationToken);
    }

    private async Task CaptureLoopAsync(CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            try
            {
                var colors = await Task.Run(CaptureFrame, cancellationToken).ConfigureAwait(false);

                lock (_lock)
                {
                    _lastCapturedZoneColors = colors;
                    // Calculate average for single-color access
                    int r = 0, g = 0, b = 0;
                    foreach (var c in colors)
                    {
                        r += c.R;
                        g += c.G;
                        b += c.B;
                    }
                    _lastCapturedColor = Color.FromArgb(r / 4, g / 4, b / 4);
                }

                await Task.Delay(CaptureInterval, cancellationToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch
            {
                // Ignore capture errors, try again next frame
                await Task.Delay(100, cancellationToken).ConfigureAwait(false);
            }
        }
    }

    private Color[] CaptureFrame()
    {
        var width = GetSystemMetrics(SM_CXSCREEN);
        var height = GetSystemMetrics(SM_CYSCREEN);

        if (width <= 0 || height <= 0)
            return [Color.Black, Color.Black, Color.Black, Color.Black];

        // Capture at reduced resolution for performance
        var captureWidth = width / 4;  // 1/4 resolution
        var captureHeight = height / 4;

        using var bitmap = new Bitmap(captureWidth, captureHeight, PixelFormat.Format24bppRgb);
        using var graphics = Graphics.FromImage(bitmap);

        var desktopWindow = GetDesktopWindow();
        var desktopDC = GetWindowDC(desktopWindow);

        try
        {
            var bitmapDC = graphics.GetHdc();
            try
            {
                // StretchBlt would be better but BitBlt with scaled destination works
                // We use GDI+ Graphics.CopyFromScreen as a simpler alternative
            }
            finally
            {
                graphics.ReleaseHdc(bitmapDC);
            }
        }
        finally
        {
            ReleaseDC(desktopWindow, desktopDC);
        }

        // Use GDI+ CopyFromScreen for simplicity and reliability
        using var fullBitmap = new Bitmap(width, height, PixelFormat.Format24bppRgb);
        using var fullGraphics = Graphics.FromImage(fullBitmap);
        fullGraphics.CopyFromScreen(0, 0, 0, 0, new Size(width, height));

        return SampleZoneColors(fullBitmap);
    }

    /// <summary>
    /// Samples colors from 4 vertical zones of the screen (left to right).
    /// Uses downsampling to reduce CPU load.
    /// 
    /// Color science: Averaging is done in LINEAR space, not sRGB gamma space.
    /// This prevents color distortion that would occur from naive averaging.
    /// 
    /// Rust origin: ambient.rs resizes to 4x1 pixels (bilinear interpolation
    /// in fast_image_resize operates in linear space by default).
    /// </summary>
    private static Color[] SampleZoneColors(Bitmap bitmap)
    {
        var colors = new Color[4];
        var width = bitmap.Width;
        var height = bitmap.Height;
        var zoneWidth = width / 4;

        // Lock bits for fast pixel access
        var rect = new Rectangle(0, 0, width, height);
        var bitmapData = bitmap.LockBits(rect, ImageLockMode.ReadOnly, PixelFormat.Format24bppRgb);

        try
        {
            var stride = bitmapData.Stride;
            var scan0 = bitmapData.Scan0;

            for (var zone = 0; zone < 4; zone++)
            {
                // Accumulate in LINEAR space (float) for gamma-correct averaging
                float linearR = 0, linearG = 0, linearB = 0;
                var sampleCount = 0;

                var startX = zone * zoneWidth;
                var endX = Math.Min((zone + 1) * zoneWidth, width);

                for (var y = 0; y < height; y += DownsampleFactor)
                {
                    for (var x = startX; x < endX; x += DownsampleFactor)
                    {
                        // Calculate pixel offset (BGR format, 3 bytes per pixel)
                        var offset = y * stride + x * 3;

                        var blue = Marshal.ReadByte(scan0, offset);
                        var green = Marshal.ReadByte(scan0, offset + 1);
                        var red = Marshal.ReadByte(scan0, offset + 2);

                        // Convert sRGB to linear using LUT before accumulating
                        linearR += SrgbToLinear[red];
                        linearG += SrgbToLinear[green];
                        linearB += SrgbToLinear[blue];
                        sampleCount++;
                    }
                }

                if (sampleCount > 0)
                {
                    // Average in linear space
                    var avgLinearR = linearR / sampleCount;
                    var avgLinearG = linearG / sampleCount;
                    var avgLinearB = linearB / sampleCount;

                    // Convert back to sRGB for output
                    // Use direct calculation for precision (LUT is 256 entries, loses precision)
                    var srgbR = (byte)Math.Clamp((int)(LinearToSrgbChannel(avgLinearR) * 255.0f + 0.5f), 0, 255);
                    var srgbG = (byte)Math.Clamp((int)(LinearToSrgbChannel(avgLinearG) * 255.0f + 0.5f), 0, 255);
                    var srgbB = (byte)Math.Clamp((int)(LinearToSrgbChannel(avgLinearB) * 255.0f + 0.5f), 0, 255);

                    colors[zone] = Color.FromArgb(srgbR, srgbG, srgbB);
                }
                else
                {
                    colors[zone] = Color.Black;
                }
            }
        }
        finally
        {
            bitmap.UnlockBits(bitmapData);
        }

        return colors;
    }

    public void Dispose()
    {
        if (_disposed)
            return;

        Stop();
        _disposed = true;
    }
}
