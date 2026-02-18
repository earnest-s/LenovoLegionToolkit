using System;
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
/// Builds a fixed Canvas of Border elements (one per physical key) on first load.
/// All keys in the same zone share a single <see cref="SolidColorBrush"/> and
/// <see cref="DropShadowEffect"/> instance, so recoloring an entire zone is O(1).
///
/// No bitmap rendering, no DispatcherTimer, no simulation.
/// Colors propagate instantly when <see cref="UpdateZones"/> is called.
/// </summary>
public partial class LoqKeyboardPreview : UserControl
{
    // Shared per-zone: one brush + one glow effect, referenced by all keys in that zone.
    // Changing brush.Color or glow.Color instantly updates every key in the zone.
    private readonly SolidColorBrush[]  _zoneBrushes = new SolidColorBrush[4];
    private readonly DropShadowEffect[] _zoneGlows   = new DropShadowEffect[4];

    // Cached current colors for redundant-update elimination
    private readonly Color[] _currentColors = [Colors.Black, Colors.Black, Colors.Black, Colors.Black];

    private bool _keysBuilt;

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

        Loaded += OnLoaded;
    }

    // ── Public API ───────────────────────────────────────────────────────

    /// <summary>
    /// Real-time zone color update (called from FrameRendered or any thread).
    /// </summary>
    public void UpdateZones(ZoneColors colors)
    {
        var c0 = ToColor(colors.Zone1);
        var c1 = ToColor(colors.Zone2);
        var c2 = ToColor(colors.Zone3);
        var c3 = ToColor(colors.Zone4);
        ApplyColors(c0, c1, c2, c3);
    }

    /// <summary>
    /// Set static zone colors (firmware presets).
    /// </summary>
    public void SetStaticZones(RGBColor z1, RGBColor z2, RGBColor z3, RGBColor z4)
        => ApplyColors(ToColor(z1), ToColor(z2), ToColor(z3), ToColor(z4));

    /// <summary>
    /// All keys off — solid black.
    /// </summary>
    public void SetOff()
        => ApplyColors(Colors.Black, Colors.Black, Colors.Black, Colors.Black);

    // ── Key construction ─────────────────────────────────────────────────

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        if (_keysBuilt) return;
        _keysBuilt = true;
        BuildKeys();
    }

    private void BuildKeys()
    {
        var keys = LoqKeyboardLayout.CreateKeys();

        foreach (var def in keys)
        {
            var zone   = def.Zone;
            var brush  = _zoneBrushes[zone];
            var glow   = _zoneGlows[zone];

            var border = new Border
            {
                Width               = def.W,
                Height              = def.H,
                Background          = brush,
                CornerRadius        = new CornerRadius(6),
                Effect              = glow,
                SnapsToDevicePixels = true,
            };

            if (!string.IsNullOrEmpty(def.Label))
            {
                border.Child = new TextBlock
                {
                    Text                = def.Label,
                    FontSize            = 9,
                    FontWeight          = FontWeights.SemiBold,
                    Foreground          = Brushes.White,
                    HorizontalAlignment = HorizontalAlignment.Center,
                    VerticalAlignment   = VerticalAlignment.Center,
                    TextAlignment       = TextAlignment.Center,
                    IsHitTestVisible    = false,
                };
            }

            Canvas.SetLeft(border, def.X);
            Canvas.SetTop(border, def.Y);
            KeyCanvas.Children.Add(border);
        }
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

            _currentColors[z] = nc[z];
            _zoneBrushes[z].Color = nc[z];
            _zoneGlows[z].Color   = nc[z];
            _zoneGlows[z].Opacity = nc[z] == Colors.Black ? 0 : 0.55;
        }
    }

    private static Color ToColor(RGBColor c) => Color.FromRgb(c.R, c.G, c.B);
}

