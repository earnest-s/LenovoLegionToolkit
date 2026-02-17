using System.Threading;
using System.Threading.Tasks;

namespace LenovoLegionToolkit.Lib.Controllers.PreviewEffects;

/// <summary>
/// A preview-only simulation of a firmware keyboard effect.
/// Runs on a background thread, calling
/// <see cref="RgbFrameDispatcher.RenderPreviewOnly"/> at regular intervals.
/// Does NOT write to HID — the firmware handles the real animation.
/// </summary>
public interface IPreviewEffect
{
    /// <summary>
    /// Runs the preview loop until cancellation.
    /// </summary>
    Task RunPreviewAsync(RgbFrameDispatcher dispatcher, CancellationToken ct);
}
