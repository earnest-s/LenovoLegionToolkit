using System;
using System.Collections.Generic;
using System.Globalization;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using LenovoLegionToolkit.Lib;
using LenovoLegionToolkit.Lib.Controllers.CustomRGBEffects;

namespace LenovoLegionToolkit.WPF.Controls.KeyboardBacklight.RGB;

/// <summary>
/// High-performance per-key keyboard preview renderer.
/// Uses a single <see cref="WriteableBitmap"/> that is redrawn each frame
/// via <see cref="DrawingVisual"/> + <see cref="RenderTargetBitmap"/>.
/// The render loop is driven by a <see cref="System.Windows.Threading.DispatcherTimer"/> at ~60 FPS.
///
/// This renderer does NOT simulate any effects. It simply maps the 4 zone
/// colors received from <see cref="RgbFrameDispatcher.FrameRendered"/> onto
/// the physical key layout and draws them.
/// </summary>
public sealed class KeyboardPreviewRenderer
{
    // ── Layout ──────────────────────────────────────────────────────
    private readonly List<KeyDefinition> _keys;
    private readonly double _canvasW;
    private readonly double _canvasH;

    // ── Render target ───────────────────────────────────────────────
    private RenderTargetBitmap? _renderTarget;
    private int _pixelWidth;
    private int _pixelHeight;

    // ── Drawing resources (reused every frame) ──────────────────────
    private static readonly Pen KeyBorderPen = new(new SolidColorBrush(Color.FromArgb(100, 0, 0, 0)), 1.2);
    private static readonly Pen GlowPen = new(Brushes.Transparent, 0);
    private static readonly Typeface LabelTypeface = new(new FontFamily("Segoe UI"), FontStyles.Normal, FontWeights.Medium, FontStretches.Normal);
    private static readonly Brush LabelBrush = new SolidColorBrush(Color.FromArgb(200, 255, 255, 255));
    private static readonly Brush ChassisBrush = new SolidColorBrush(Color.FromRgb(0x11, 0x11, 0x11));
    private static readonly Brush ChassisStroke = new SolidColorBrush(Color.FromRgb(0x33, 0x33, 0x33));

    private bool _isDirty = true;

    static KeyboardPreviewRenderer()
    {
        KeyBorderPen.Freeze();
        LabelBrush.Freeze();
        ChassisBrush.Freeze();
        ChassisStroke.Freeze();
    }

    public KeyboardPreviewRenderer()
    {
        _keys = LoqKeyboardLayout.CreateKeys();
        _canvasW = LoqKeyboardLayout.CanvasWidth;
        _canvasH = LoqKeyboardLayout.CanvasHeight;
    }

    /// <summary>
    /// Updates all key colors from the 4 hardware zones.
    /// Must be called on UI thread.
    /// </summary>
    public void UpdateColors(ZoneColors zones)
    {
        Span<Color> zoneMap =
        [
            Color.FromRgb(zones.Zone1.R, zones.Zone1.G, zones.Zone1.B),
            Color.FromRgb(zones.Zone2.R, zones.Zone2.G, zones.Zone2.B),
            Color.FromRgb(zones.Zone3.R, zones.Zone3.G, zones.Zone3.B),
            Color.FromRgb(zones.Zone4.R, zones.Zone4.G, zones.Zone4.B)
        ];

        var changed = false;
        foreach (var key in _keys)
        {
            var newColor = zoneMap[key.Zone];
            if (key.CurrentColor != newColor)
            {
                key.CurrentColor = newColor;
                changed = true;
            }
        }

        if (changed)
            _isDirty = true;
    }

    /// <summary>
    /// Sets all keys to black.
    /// </summary>
    public void SetOff()
    {
        foreach (var key in _keys)
            key.CurrentColor = Colors.Black;
        _isDirty = true;
    }

    /// <summary>
    /// Renders one frame to the shared <see cref="RenderTargetBitmap"/>
    /// and returns it. The bitmap is reused across frames.
    /// Only redraws if colors changed since last render.
    /// </summary>
    public RenderTargetBitmap Render(double controlWidth, double controlHeight, double dpiX = 96, double dpiY = 96)
    {
        var pw = Math.Max(1, (int)(controlWidth * dpiX / 96));
        var ph = Math.Max(1, (int)(controlHeight * dpiY / 96));

        // Recreate bitmap only on size change
        if (_renderTarget is null || pw != _pixelWidth || ph != _pixelHeight)
        {
            _pixelWidth = pw;
            _pixelHeight = ph;
            _renderTarget = new RenderTargetBitmap(pw, ph, dpiX, dpiY, PixelFormats.Pbgra32);
            _isDirty = true; // force full redraw on resize
        }

        if (!_isDirty)
            return _renderTarget;

        _isDirty = false;

        var visual = new DrawingVisual();
        using (var dc = visual.RenderOpen())
        {
            // Scale canvas to fit control
            var scaleX = controlWidth / (_canvasW + 40); // 20px padding each side
            var scaleY = controlHeight / (_canvasH + 40);
            var scale = Math.Min(scaleX, scaleY);

            var offsetX = (controlWidth - _canvasW * scale) / 2;
            var offsetY = (controlHeight - _canvasH * scale) / 2;

            dc.PushTransform(new TranslateTransform(offsetX, offsetY));
            dc.PushTransform(new ScaleTransform(scale, scale));

            // Chassis background
            dc.DrawRoundedRectangle(ChassisBrush, new Pen(ChassisStroke, 2),
                new Rect(-16, -16, _canvasW + 32, _canvasH + 32), 12, 12);

            // Keys
            foreach (var key in _keys)
            {
                var color = key.CurrentColor;
                var brush = new SolidColorBrush(color);

                // Subtle glow behind key
                if (color != Colors.Black)
                {
                    var glowBrush = new RadialGradientBrush(
                        Color.FromArgb(60, color.R, color.G, color.B),
                        Colors.Transparent);
                    var glowRect = key.Bounds;
                    glowRect.Inflate(4, 4);
                    dc.DrawRoundedRectangle(glowBrush, GlowPen, glowRect, 6, 6);
                }

                // Key face
                dc.DrawRoundedRectangle(brush, KeyBorderPen, key.Bounds, 5, 5);

                // Label (only draw if key is large enough)
                if (key.Label.Length > 0 && key.Bounds.Width >= 40)
                {
                    var fontSize = key.Bounds.Width < 80 ? 9.5 : 11;
                    var ft = new FormattedText(
                        key.Label,
                        CultureInfo.InvariantCulture,
                        FlowDirection.LeftToRight,
                        LabelTypeface,
                        fontSize,
                        LabelBrush,
                        1.0);
                    var textX = key.Bounds.X + (key.Bounds.Width - ft.Width) / 2;
                    var textY = key.Bounds.Y + (key.Bounds.Height - ft.Height) / 2;
                    dc.DrawText(ft, new Point(textX, textY));
                }
            }

            dc.Pop(); // scale
            dc.Pop(); // translate
        }

        _renderTarget.Clear();
        _renderTarget.Render(visual);
        return _renderTarget;
    }
}
