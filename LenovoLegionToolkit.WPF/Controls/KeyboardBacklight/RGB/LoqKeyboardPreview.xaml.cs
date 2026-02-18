using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Effects;
using LenovoLegionToolkit.Lib;
using LenovoLegionToolkit.Lib.Controllers.CustomRGBEffects;

namespace LenovoLegionToolkit.WPF.Controls.KeyboardBacklight.RGB;

/// <summary>
/// Per-key LOQ 15IRX9 keyboard preview.
///
/// Uses a fixed-coordinate Canvas with individual Border elements for every key.
/// Colors are applied directly to each key's SolidColorBrush background — no bitmaps,
/// no simulation, no timers.  Updates propagate immediately on <see cref="UpdateZones"/>.
/// </summary>
public partial class LoqKeyboardPreview : UserControl
{
    // Per-zone list of (brush, glowEffect) for O(n) bulk-update
    private readonly SolidColorBrush[]    _zoneBrushes = new SolidColorBrush[4];
    private readonly DropShadowEffect[]   _dummy = [];   // kept only for documentation

    // All key borders grouped by zone index
    private readonly List<Border>[] _zoneKeys = [new(), new(), new(), new()];

    // Current zone colors (ARGB cached to skip redundant updates)
    private Color[]  _currentColors = [Colors.Black, Colors.Black, Colors.Black, Colors.Black];

    public LoqKeyboardPreview()
    {
        InitializeComponent();

        // Pre-create one shared SolidColorBrush per zone (frozen later per update)
        for (var i = 0; i < 4; i++)
            _zoneBrushes[i] = new SolidColorBrush(Colors.Black);

        Loaded += OnLoaded;
    }

    // ── Public API ───────────────────────────────────────────────────────

    public void UpdateZones(ZoneColors colors)
    {
        var c0 = ToColor(colors.Zone1);
        var c1 = ToColor(colors.Zone2);
        var c2 = ToColor(colors.Zone3);
        var c3 = ToColor(colors.Zone4);
        ApplyColors(c0, c1, c2, c3);
    }

    public void SetStaticZones(RGBColor z1, RGBColor z2, RGBColor z3, RGBColor z4)
        => ApplyColors(ToColor(z1), ToColor(z2), ToColor(z3), ToColor(z4));

    public void SetOff()
        => ApplyColors(Colors.Black, Colors.Black, Colors.Black, Colors.Black);

    // ── Internal ─────────────────────────────────────────────────────────

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        if (KeyCanvas.Children.Count > 0)
            return;   // already built (style trigger may fire Loaded twice)

        BuildKeys();
    }

    private void BuildKeys()
    {
        var keys = LoqKeyboardLayout.CreateKeys();

        foreach (var def in keys)
        {
            var brush  = _zoneBrushes[def.Zone];
            var glow   = MakeGlow(brush.Color);
            var border = MakeKey(def, brush, glow);

            Canvas.SetLeft(border, def.X);
            Canvas.SetTop(border,  def.Y);
            KeyCanvas.Children.Add(border);
            _zoneKeys[def.Zone].Add(border);
        }
    }

    private static Border MakeKey(KeyDef def, SolidColorBrush brush, DropShadowEffect glow)
    {
        var border = new Border
        {
            Width         = def.W,
            Height        = def.H,
            Background    = brush,
            CornerRadius  = new CornerRadius(4),
            Effect        = glow,
            SnapsToDevicePixels = true,
        };

        // key label — only shown on keys wider than 30 px  
        if (!string.IsNullOrEmpty(def.Label))
        {
            var tb = new TextBlock
            {
                Text                = def.Label,
                FontSize            = def.W > 55 ? 9 : 8,
                FontWeight          = FontWeights.SemiBold,
                Foreground          = new SolidColorBrush(Color.FromArgb(200, 255, 255, 255)),
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment   = VerticalAlignment.Center,
                TextAlignment       = TextAlignment.Center,
                IsHitTestVisible    = false,
                TextWrapping        = TextWrapping.NoWrap,
            };
            border.Child = tb;
        }

        return border;
    }

    private static DropShadowEffect MakeGlow(Color color)
        => new()
        {
            Color       = color,
            ShadowDepth = 0,
            BlurRadius  = 8,
            Opacity     = color == Colors.Black ? 0 : 0.85,
        };

    private void ApplyColors(Color c0, Color c1, Color c2, Color c3)
    {
        if (!Dispatcher.CheckAccess())
        {
            Dispatcher.BeginInvoke(() => ApplyColors(c0, c1, c2, c3));
            return;
        }

        ReadOnlySpan<Color> newColors = [c0, c1, c2, c3];

        for (var zone = 0; zone < 4; zone++)
        {
            var nc = newColors[zone];
            if (nc == _currentColors[zone])
                continue;

            _currentColors[zone] = nc;
            _zoneBrushes[zone].Color = nc;

            // update glow on every key in this zone
            var glowOpacity = nc == Colors.Black ? 0.0 : 0.85;
            foreach (var border in _zoneKeys[zone])
            {
                if (border.Effect is DropShadowEffect dse)
                {
                    dse.Color   = nc;
                    dse.Opacity = glowOpacity;
                }
            }
        }
    }

    private static Color ToColor(RGBColor c)
        => Color.FromRgb(c.R, c.G, c.B);
}

