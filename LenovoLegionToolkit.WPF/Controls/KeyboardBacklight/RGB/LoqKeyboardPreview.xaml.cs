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
/// Real-time LOQ 4-zone keyboard preview that renders current RGB zone
/// colors and animates in sync with the active effect. Subscribes to
/// <see cref="CustomRGBEffectController.PreviewFrame"/> for live frames
/// and can also be set statically for firmware-driven presets.
/// </summary>
public partial class LoqKeyboardPreview : UserControl
{
    private static readonly Duration AnimDuration = new(TimeSpan.FromMilliseconds(80));
    private static readonly QuadraticEase Ease = new();

    private readonly Color[] _current = [Colors.Black, Colors.Black, Colors.Black, Colors.Black];

    public LoqKeyboardPreview()
    {
        InitializeComponent();

        // Ensure each zone starts with an animatable frozen-clone-free brush
        Zone0.Fill = new SolidColorBrush(Colors.Black);
        Zone1.Fill = new SolidColorBrush(Colors.Black);
        Zone2.Fill = new SolidColorBrush(Colors.Black);
        Zone3.Fill = new SolidColorBrush(Colors.Black);
    }

    /// <summary>
    /// Update all four zones. Called from any thread — marshals to Dispatcher.
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
    /// Set all four zones to static colors without animation (for preset display).
    /// Must be called on the UI thread.
    /// </summary>
    public void SetStaticZones(RGBColor z1, RGBColor z2, RGBColor z3, RGBColor z4)
    {
        SetStatic(Zone0, 0, z1);
        SetStatic(Zone1, 1, z2);
        SetStatic(Zone2, 2, z3);
        SetStatic(Zone3, 3, z4);
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
        Animate(Zone0, 0, colors.Zone1);
        Animate(Zone1, 1, colors.Zone2);
        Animate(Zone2, 2, colors.Zone3);
        Animate(Zone3, 3, colors.Zone4);
    }

    private void Animate(Rectangle rect, int index, RGBColor target)
    {
        var to = Color.FromRgb(target.R, target.G, target.B);

        // Skip animation if color hasn't changed
        if (_current[index] == to)
            return;

        var brush = rect.Fill as SolidColorBrush;
        if (brush is null || brush.IsFrozen)
        {
            brush = new SolidColorBrush(_current[index]);
            rect.Fill = brush;
        }

        var anim = new ColorAnimation
        {
            To = to,
            Duration = AnimDuration,
            EasingFunction = Ease
        };

        brush.BeginAnimation(SolidColorBrush.ColorProperty, anim);
        _current[index] = to;
    }

    private void SetStatic(Rectangle rect, int index, RGBColor color)
    {
        var c = Color.FromRgb(color.R, color.G, color.B);
        _current[index] = c;

        var brush = rect.Fill as SolidColorBrush;
        if (brush is null || brush.IsFrozen)
        {
            brush = new SolidColorBrush(c);
            rect.Fill = brush;
        }
        else
        {
            brush.BeginAnimation(SolidColorBrush.ColorProperty, null); // clear any running animation
            brush.Color = c;
        }
    }
}
