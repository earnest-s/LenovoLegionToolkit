using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Effects;
using LenovoLegionToolkit.Lib;
using LenovoLegionToolkit.Lib.Controllers.CustomRGBEffects;

namespace LenovoLegionToolkit.WPF.Controls.KeyboardBacklight.RGB;

/// <summary>
/// Per-key LOQ 15IRX9 keyboard preview with dual-legend key labels.
///
/// Architecture:
///   - Fixed Canvas (1172×292) scaled via ViewBox
///   - One Border per physical key, positioned with Canvas.SetLeft/Top
///   - 4 shared SolidColorBrush + 4 shared DropShadowEffect (one per zone)
///   - Recoloring is O(1): set brush.Color → all keys in zone update instantly
///   - Dual labels: secondary (top, small, dim) + primary (center/bottom, bold)
///   - No bitmaps, no timers, no simulation
/// </summary>
public partial class LoqKeyboardPreview : UserControl
{
    // Per-zone label brushes — auto-invert to contrast with zone background
    private readonly SolidColorBrush[]  _zoneLabelBrushes     = new SolidColorBrush[4];
    private readonly SolidColorBrush[]  _zoneSecondaryBrushes = new SolidColorBrush[4];

    private readonly SolidColorBrush[]  _zoneBrushes = new SolidColorBrush[4];
    private readonly DropShadowEffect[] _zoneGlows   = new DropShadowEffect[4];
    private readonly Color[]            _currentColors = [Colors.Black, Colors.Black, Colors.Black, Colors.Black];
    private bool _keysBuilt;

    public LoqKeyboardPreview()
    {
        InitializeComponent();

        for (var i = 0; i < 4; i++)
        {
            _zoneBrushes[i] = new SolidColorBrush(Colors.Black);
            _zoneLabelBrushes[i] = new SolidColorBrush(Colors.White);
            _zoneSecondaryBrushes[i] = new SolidColorBrush(Color.FromArgb(180, 255, 255, 255));
            _zoneGlows[i] = new DropShadowEffect
            {
                Color       = Colors.Black,
                ShadowDepth = 0,
                BlurRadius  = 8,
                Opacity     = 0,
            };
        }

        Loaded += (_, _) => BuildKeysOnce();
    }

    // ── Public API ───────────────────────────────────────────────────────

    public void UpdateZones(ZoneColors colors)
        => ApplyColors(ToColor(colors.Zone1), ToColor(colors.Zone2),
                        ToColor(colors.Zone3), ToColor(colors.Zone4));

    public void SetStaticZones(RGBColor z1, RGBColor z2, RGBColor z3, RGBColor z4)
        => ApplyColors(ToColor(z1), ToColor(z2), ToColor(z3), ToColor(z4));

    public void SetOff()
        => ApplyColors(Colors.Black, Colors.Black, Colors.Black, Colors.Black);

    // ── Key construction ─────────────────────────────────────────────────

    private void BuildKeysOnce()
    {
        if (_keysBuilt) return;
        _keysBuilt = true;

        foreach (var def in LoqKeyboardLayout.CreateKeys())
        {
            var border = CreateKey(def);
            Canvas.SetLeft(border, def.X);
            Canvas.SetTop(border, def.Y);
            KeyCanvas.Children.Add(border);
        }
    }

    private Border CreateKey(KeyDef def)
    {
        var border = new Border
        {
            Width               = def.W,
            Height              = def.H,
            Background          = _zoneBrushes[def.Zone],
            CornerRadius        = new CornerRadius(6),
            Effect              = _zoneGlows[def.Zone],
            SnapsToDevicePixels = true,
        };

        bool hasPrimary   = !string.IsNullOrEmpty(def.Primary);
        bool hasSecondary = !string.IsNullOrEmpty(def.Secondary);

        if (!hasPrimary && !hasSecondary)
            return border;  // spacebar — no label

        bool isShortKey = def.H < 40;  // F-row keys are shorter

        if (hasSecondary)
        {
            // Dual legend: secondary at top, primary at bottom/center
            var grid = new Grid();

            grid.Children.Add(new TextBlock
            {
                Text                = def.Secondary,
                FontSize            = isShortKey ? 5.5 : 7,
                Foreground          = _zoneSecondaryBrushes[def.Zone],
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment   = VerticalAlignment.Top,
                TextAlignment       = TextAlignment.Center,
                Margin              = new Thickness(1, isShortKey ? 1 : 3, 1, 0),
                IsHitTestVisible    = false,
            });

            if (hasPrimary)
            {
                grid.Children.Add(new TextBlock
                {
                    Text                = def.Primary,
                    FontSize            = isShortKey ? 7 : 10,
                    FontWeight          = FontWeights.SemiBold,
                    Foreground          = _zoneLabelBrushes[def.Zone],
                    HorizontalAlignment = HorizontalAlignment.Center,
                    VerticalAlignment   = VerticalAlignment.Bottom,
                    TextAlignment       = TextAlignment.Center,
                    Margin              = new Thickness(1, 0, 1, isShortKey ? 1 : 3),
                    IsHitTestVisible    = false,
                });
            }

            border.Child = grid;
        }
        else
        {
            // Single legend: centered
            border.Child = new TextBlock
            {
                Text                = def.Primary,
                FontSize            = isShortKey ? 8 : 10,
                FontWeight          = FontWeights.SemiBold,
                Foreground          = _zoneLabelBrushes[def.Zone],
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment   = VerticalAlignment.Center,
                TextAlignment       = TextAlignment.Center,
                IsHitTestVisible    = false,
            };
        }

        return border;
    }

    // ── Color application ────────────────────────────────────────────────

    private void ApplyColors(Color c0, Color c1, Color c2, Color c3)
    {
        if (!Dispatcher.CheckAccess())
        {
            Dispatcher.BeginInvoke(() => ApplyColors(c0, c1, c2, c3));
            return;
        }

        Span<Color> nc = [c0, c1, c2, c3];

        for (var z = 0; z < 4; z++)
        {
            if (nc[z] == _currentColors[z]) continue;

            _currentColors[z]         = nc[z];
            _zoneBrushes[z].Color     = nc[z];
            _zoneGlows[z].Color       = nc[z];
            _zoneGlows[z].Opacity     = nc[z] == Colors.Black ? 0.0 : 0.55;

            // Invert label color based on perceived luminance of zone background
            var label = ContrastColor(nc[z]);
            _zoneLabelBrushes[z].Color     = label;
            _zoneSecondaryBrushes[z].Color = Color.FromArgb(180, label.R, label.G, label.B);
        }
    }

    /// <summary>
    /// Returns white or black depending on perceived luminance of <paramref name="bg"/>.
    /// Uses ITU-R BT.601 luma:  L = 0.299R + 0.587G + 0.114B
    /// </summary>
    private static Color ContrastColor(Color bg)
    {
        var luma = 0.299 * bg.R + 0.587 * bg.G + 0.114 * bg.B;
        return luma > 140 ? Colors.Black : Colors.White;
    }

    private static Color ToColor(RGBColor c) => Color.FromRgb(c.R, c.G, c.B);
}

