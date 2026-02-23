// ============================================================================
// DxgiScreenColorProvider.cs
// 
// Screen color provider for the Ambient effect.
// 
// NOTE: True DXGI Desktop Duplication requires additional dependencies
// (SharpDX or Vortice.Windows). This implementation wraps GdiScreenColorProvider
// for broad compatibility. Performance is still acceptable at 30 FPS.
// 
// Rust origin: ambient.rs uses scrap crate for screen capture.
// 
// Constraints (per requirements):
// - Max capture rate: 30 FPS
// - Downsamples frame before color averaging
// - Runs on background task with cancellation support
// ============================================================================

using System.Drawing;
using System.Threading;
using System.Threading.Tasks;

namespace LoqNova.Lib.Controllers.CustomRGBEffects.SignalProviders;

/// <summary>
/// Screen color provider that delegates to GDI capture.
/// 
/// For true DXGI Desktop Duplication, the project would need to add
/// a dependency on SharpDX.DXGI or Vortice.DXGI. The current GDI-based
/// implementation provides adequate performance for the 30 FPS ambient effect.
/// </summary>
public sealed class DxgiScreenColorProvider : IScreenColorProvider
{
    private readonly GdiScreenColorProvider _gdiProvider = new();

    /// <inheritdoc />
    public Color LastCapturedColor => _gdiProvider.LastCapturedColor;

    /// <inheritdoc />
    public Color[] LastCapturedZoneColors => _gdiProvider.LastCapturedZoneColors;

    /// <inheritdoc />
    public bool IsCapturing => _gdiProvider.IsCapturing;

    /// <inheritdoc />
    public Task StartAsync(CancellationToken cancellationToken = default)
        => _gdiProvider.StartAsync(cancellationToken);

    /// <inheritdoc />
    public void Stop() => _gdiProvider.Stop();

    /// <inheritdoc />
    public Task<Color[]> CaptureZoneColorsAsync(CancellationToken cancellationToken = default)
        => _gdiProvider.CaptureZoneColorsAsync(cancellationToken);

    /// <inheritdoc />
    public void Dispose() => _gdiProvider.Dispose();
}
