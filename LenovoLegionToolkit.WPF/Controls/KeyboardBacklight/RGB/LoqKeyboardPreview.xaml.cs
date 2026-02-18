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
    private static readonly Brush LabelBrush = new SolidColorBrush(Colors.White);
    private static readonly Brush SecondaryBrush = new SolidColorBrush(Color.FromArgb(170, 255, 255, 255));

    private readonly SolidColorBrush[]  _zoneBrushes = new SolidColorBrush[4];
    private readonly DropShadowEffect[] _zoneGlows   = new DropShadowEffect[4];
    private readonly Color[]            _currentColors = [Colors.Black, Colors.Black, Colors.Black, Colors.Black];
    private bool _keysBuilt;

    static LoqKeyboardPreview()
    {
        LabelBrush.Freeze();
        SecondaryBrush.Freeze();
    }

    public LoqKeyboardPreview()
    {
        InitializeComponent();

        for (var i = 0; i < 4; i++)
        {
            _zoneBrushes[i] = new SolidColorBrush(Colors.Black);
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
                Foreground          = SecondaryBrush,
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
                    Foreground          = LabelBrush,
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
                Foreground          = LabelBrush,
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
        }
    }

    private static Color ToColor(RGBColor c) => Color.FromRgb(c.R, c.G, c.B);
}

