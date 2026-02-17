using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Shapes;
using System.Windows.Threading;
using LenovoLegionToolkit.Lib;
using LenovoLegionToolkit.Lib.Controllers.CustomRGBEffects;

namespace LenovoLegionToolkit.WPF.Controls.KeyboardBacklight.RGB;

/// <summary>
/// Real-time LOQ 4-zone keyboard preview that renders current RGB zone
/// colors and animates in sync with the active effect. Subscribes to
/// <see cref="CustomRGBEffectController.PreviewFrame"/> for live frames
/// from custom effects, and simulates firmware-driven effects (Breath,
/// Wave, Smooth) locally using a DispatcherTimer.
/// </summary>
public partial class LoqKeyboardPreview : UserControl
{
    private static readonly Duration AnimDuration = new(TimeSpan.FromMilliseconds(80));
    private static readonly QuadraticEase Ease = new();

    private readonly Color[] _current = [Colors.Black, Colors.Black, Colors.Black, Colors.Black];
    private readonly Rectangle[] _zones;

    // Firmware effect simulation
    private DispatcherTimer? _fwTimer;
    private RGBKeyboardBacklightEffect _activeEffect;
    private RGBKeyboardBacklightSpeed _activeSpeed;
    private Color[] _zoneColors = [Colors.White, Colors.White, Colors.White, Colors.White];
    private double _fwPhase;

    public LoqKeyboardPreview()
    {
        InitializeComponent();

        _zones = [Zone0, Zone1, Zone2, Zone3];

        foreach (var z in _zones)
            z.Fill = new SolidColorBrush(Colors.Black);
    }

    // ───────────────────────────────────────────────────────────
    //  Live custom-effect frames (called from any thread)
    // ───────────────────────────────────────────────────────────

    /// <summary>
    /// Update all four zones from a custom effect frame.
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

    private void UpdateZonesInternal(ZoneColors colors)
    {
        AnimateZone(0, colors.Zone1);
        AnimateZone(1, colors.Zone2);
        AnimateZone(2, colors.Zone3);
        AnimateZone(3, colors.Zone4);
    }

    // ───────────────────────────────────────────────────────────
    //  Static preset display
    // ───────────────────────────────────────────────────────────

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
        StopFirmwareSimulation();
        var black = new RGBColor(0, 0, 0);
        SetStaticZones(black, black, black, black);
    }

    // ───────────────────────────────────────────────────────────
    //  Firmware effect simulation (Breath, Wave, Smooth)
    // ───────────────────────────────────────────────────────────

    /// <summary>
    /// Start simulating a firmware-driven effect in the preview.
    /// The actual hardware animation is invisible to software; this
    /// renders an approximate visual match.
    /// </summary>
    public void StartFirmwareSimulation(
        RGBKeyboardBacklightEffect effect,
        RGBKeyboardBacklightSpeed speed,
        RGBColor z1, RGBColor z2, RGBColor z3, RGBColor z4)
    {
        StopFirmwareSimulation();

        _activeEffect = effect;
        _activeSpeed = speed;
        _zoneColors = [ToWpfColor(z1), ToWpfColor(z2), ToWpfColor(z3), ToWpfColor(z4)];
        _fwPhase = 0;

        // Clear WPF animations so timer has direct brush control
        for (var i = 0; i < 4; i++)
        {
            var brush = EnsureBrush(i);
            brush.BeginAnimation(SolidColorBrush.ColorProperty, null);
        }

        _fwTimer = new DispatcherTimer(DispatcherPriority.Render)
        {
            Interval = TimeSpan.FromMilliseconds(30)
        };
        _fwTimer.Tick += FirmwareTick;
        _fwTimer.Start();
    }

    /// <summary>
    /// Stop any running firmware effect simulation.
    /// </summary>
    public void StopFirmwareSimulation()
    {
        if (_fwTimer is null)
            return;

        _fwTimer.Tick -= FirmwareTick;
        _fwTimer.Stop();
        _fwTimer = null;
    }

    private void FirmwareTick(object? sender, EventArgs e)
    {
        var speedFactor = _activeSpeed switch
        {
            RGBKeyboardBacklightSpeed.Slowest => 0.5,
            RGBKeyboardBacklightSpeed.Slow => 1.0,
            RGBKeyboardBacklightSpeed.Fast => 2.0,
            RGBKeyboardBacklightSpeed.Fastest => 3.5,
            _ => 1.0
        };

        var dt = (_fwTimer?.Interval.TotalSeconds ?? 0.03) * speedFactor;

        switch (_activeEffect)
        {
            case RGBKeyboardBacklightEffect.Breath:
                SimulateBreath(dt);
                break;
            case RGBKeyboardBacklightEffect.WaveRTL:
                SimulateWave(dt, rtl: true);
                break;
            case RGBKeyboardBacklightEffect.WaveLTR:
                SimulateWave(dt, rtl: false);
                break;
            case RGBKeyboardBacklightEffect.Smooth:
                SimulateSmooth(dt);
                break;
        }
    }

    private void SimulateBreath(double dt)
    {
        _fwPhase = (_fwPhase + dt / 3.0) % 1.0;
        var brightness = (Math.Sin(_fwPhase * 2 * Math.PI - Math.PI / 2) + 1) / 2;

        for (var i = 0; i < 4; i++)
        {
            var c = _zoneColors[i];
            SetBrushColor(i, Color.FromRgb(
                (byte)(c.R * brightness),
                (byte)(c.G * brightness),
                (byte)(c.B * brightness)));
        }
    }

    private void SimulateWave(double dt, bool rtl)
    {
        _fwPhase = (_fwPhase + dt / 2.0) % 1.0;

        for (var i = 0; i < 4; i++)
        {
            var zoneOffset = rtl ? (3 - i) / 4.0 : i / 4.0;
            var zonePhase = (_fwPhase + zoneOffset) % 1.0;
            var brightness = (Math.Sin(zonePhase * 2 * Math.PI - Math.PI / 2) + 1) / 2;

            var c = _zoneColors[i];
            SetBrushColor(i, Color.FromRgb(
                (byte)(c.R * brightness),
                (byte)(c.G * brightness),
                (byte)(c.B * brightness)));
        }
    }

    private void SimulateSmooth(double dt)
    {
        _fwPhase = (_fwPhase + dt / 4.0) % 1.0;

        for (var i = 0; i < 4; i++)
        {
            var hue = (_fwPhase + i / 4.0) % 1.0;
            SetBrushColor(i, HsvToColor(hue * 360, 1.0, 1.0));
        }
    }

    // ───────────────────────────────────────────────────────────
    //  Helpers
    // ───────────────────────────────────────────────────────────

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

    private void SetBrushColor(int index, Color c)
    {
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

    private static Color ToWpfColor(RGBColor c) => Color.FromRgb(c.R, c.G, c.B);

    private static Color HsvToColor(double h, double s, double v)
    {
        var hi = (int)(h / 60) % 6;
        var f = h / 60 - Math.Floor(h / 60);
        var p = v * (1 - s);
        var q = v * (1 - f * s);
        var t = v * (1 - (1 - f) * s);

        double r, g, b;
        switch (hi)
        {
            case 0: r = v; g = t; b = p; break;
            case 1: r = q; g = v; b = p; break;
            case 2: r = p; g = v; b = t; break;
            case 3: r = p; g = q; b = v; break;
            case 4: r = t; g = p; b = v; break;
            default: r = v; g = p; b = q; break;
        }

        return Color.FromRgb(
            (byte)(r * 255),
            (byte)(g * 255),
            (byte)(b * 255));
    }
}
