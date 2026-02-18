using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;
using LenovoLegionToolkit.Lib;
using LenovoLegionToolkit.Lib.Controllers.CustomRGBEffects;

namespace LenovoLegionToolkit.WPF.Controls.KeyboardBacklight.RGB;

/// <summary>
/// Per-key LOQ keyboard preview. Renders real-time RGB colors exactly as
/// sent to hardware via <see cref="RgbFrameDispatcher.FrameRendered"/>.
/// Uses <see cref="KeyboardPreviewRenderer"/> (DrawingVisual + RenderTargetBitmap)
/// driven by a 60 FPS DispatcherTimer. No simulation, no fake animation.
/// </summary>
public partial class LoqKeyboardPreview : UserControl
{
    private readonly KeyboardPreviewRenderer _renderer = new();
    private readonly DispatcherTimer _renderTimer;

    public LoqKeyboardPreview()
    {
        InitializeComponent();

        _renderTimer = new DispatcherTimer(DispatcherPriority.Render)
        {
            Interval = TimeSpan.FromMilliseconds(16) // ~60 FPS
        };
        _renderTimer.Tick += OnRenderTick;

        Loaded += (_, _) => _renderTimer.Start();
        Unloaded += (_, _) => _renderTimer.Stop();
    }

    /// <summary>
    /// Called from any thread when zone colors change.
    /// Updates the renderer's color state; next tick redraws.
    /// </summary>
    public void UpdateZones(ZoneColors colors)
    {
        if (!Dispatcher.CheckAccess())
        {
            Dispatcher.BeginInvoke(() => _renderer.UpdateColors(colors));
            return;
        }

        _renderer.UpdateColors(colors);
    }

    /// <summary>
    /// Set static zone colors (for firmware presets).
    /// </summary>
    public void SetStaticZones(RGBColor z1, RGBColor z2, RGBColor z3, RGBColor z4)
    {
        _renderer.UpdateColors(new ZoneColors(z1, z2, z3, z4));
    }

    /// <summary>
    /// Turn off — all black.
    /// </summary>
    public void SetOff()
    {
        _renderer.SetOff();
    }

    private void OnRenderTick(object? sender, EventArgs e)
    {
        if (!IsVisible || ActualWidth < 1 || ActualHeight < 1)
            return;

        var bitmap = _renderer.Render(
            KeyboardPreviewSurface.ActualWidth > 0 ? KeyboardPreviewSurface.ActualWidth : 760,
            KeyboardPreviewSurface.ActualHeight > 0 ? KeyboardPreviewSurface.ActualHeight : 280);

        if (KeyboardPreviewSurface.Source != bitmap)
            KeyboardPreviewSurface.Source = bitmap;
    }
}
