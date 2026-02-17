using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Shapes;
using LenovoLegionToolkit.Lib;
using LenovoLegionToolkit.Lib.Controllers.CustomRGBEffects;

namespace LenovoLegionToolkit.WPF.Controls.KeyboardBacklight.RGB;

/// <summary>
/// Real-time LOQ 4-zone keyboard preview that mirrors the exact RGB data
/// being sent to hardware. Subscribes to <see cref="CustomRGBEffectController.PreviewFrame"/>
/// which fires on every HID write — custom effects, firmware presets,
/// performance strobe, everything.  No simulation, no local animation logic.
/// </summary>
public partial class LoqKeyboardPreview : UserControl
{
    private static readonly Duration AnimDuration = new(TimeSpan.FromMilliseconds(80));
    private static readonly QuadraticEase Ease = new();

    private readonly Color[] _current = [Colors.Black, Colors.Black, Colors.Black, Colors.Black];
    private readonly Rectangle[] _zones;

    public LoqKeyboardPreview()
    {
        InitializeComponent();

        _zones = [Zone0, Zone1, Zone2, Zone3];

        foreach (var z in _zones)
            z.Fill = new SolidColorBrush(Colors.Black);
    }

    /// <summary>
    /// Update all four zones with smooth animation.
    /// Called from any thread — marshals to Dispatcher.
    /// </summary>
    public void UpdateZones(ZoneColors colors)
    {
        if (!Dispatcher.CheckAccess())
        {
            Dispatcher.BeginInvoke(() => UpdateZonesInternal(colors));
            return;
        }

        UpdateZonesInternal(colors);
    }

    /// <summary>
    /// Set all four zones to static colors without animation.
    /// Must be called on the UI thread.
    /// </summary>
    public void SetStaticZones(RGBColor z1, RGBColor z2, RGBColor z3, RGBColor z4)
    {
        SetStatic(0, z1);
        SetStatic(1, z2);
        SetStatic(2, z3);
        SetStatic(3, z4);
    }

    /// <summary>
    /// Set all zones to black (keyboard off).
    /// </summary>
    public void SetOff()
    {
        var black = new RGBColor(0, 0, 0);
        SetStaticZones(black, black, black, black);
    }

    private void UpdateZonesInternal(ZoneColors colors)
    {
        AnimateZone(0, colors.Zone1);
        AnimateZone(1, colors.Zone2);
        AnimateZone(2, colors.Zone3);
        AnimateZone(3, colors.Zone4);
    }

    private void AnimateZone(int index, RGBColor target)
    {
        var to = Color.FromRgb(target.R, target.G, target.B);
        if (_current[index] == to)
            return;

        var brush = EnsureBrush(index);
        brush.BeginAnimation(SolidColorBrush.ColorProperty, new ColorAnimation
        {
            To = to,
            Duration = AnimDuration,
            EasingFunction = Ease
        });
        _current[index] = to;
    }

    private void SetStatic(int index, RGBColor color)
    {
        var c = Color.FromRgb(color.R, color.G, color.B);
        _current[index] = c;
        var brush = EnsureBrush(index);
        brush.BeginAnimation(SolidColorBrush.ColorProperty, null);
        brush.Color = c;
    }

    private SolidColorBrush EnsureBrush(int index)
    {
        var brush = _zones[index].Fill as SolidColorBrush;
        if (brush is null || brush.IsFrozen)
        {
            brush = new SolidColorBrush(_current[index]);
            _zones[index].Fill = brush;
        }
        return brush;
    }
}
