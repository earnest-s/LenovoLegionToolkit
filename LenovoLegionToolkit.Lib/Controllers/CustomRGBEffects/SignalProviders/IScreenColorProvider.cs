using System;
using System.Drawing;
using System.Threading;
using System.Threading.Tasks;

namespace LenovoLegionToolkit.Lib.Controllers.CustomRGBEffects.SignalProviders;

/// <summary>
/// Provides screen color samples for ambient lighting effects.
/// Effects consume this interface - they do not own the screen capture logic.
/// </summary>
public interface IScreenColorProvider : IDisposable
{
    /// <summary>
    /// Gets the most recently captured average screen color.
    /// </summary>
    Color LastCapturedColor { get; }

    /// <summary>
    /// Gets multiple zone colors by sampling different screen regions.
    /// Index 0 = leftmost region, Index 3 = rightmost region.
    /// </summary>
    Color[] LastCapturedZoneColors { get; }

    /// <summary>
    /// Gets whether the provider is currently capturing.
    /// </summary>
    bool IsCapturing { get; }

    /// <summary>
    /// Starts screen capture on a background task.
    /// </summary>
    /// <param name="cancellationToken">Token to cancel capture.</param>
    Task StartAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Stops screen capture.
    /// </summary>
    void Stop();

    /// <summary>
    /// Manually captures the current screen colors.
    /// </summary>
    /// <param name="cancellationToken">Token to cancel capture.</param>
    /// <returns>Array of 4 zone colors.</returns>
    Task<Color[]> CaptureZoneColorsAsync(CancellationToken cancellationToken = default);
}
