using System;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Shapes;
using Humanizer;
using LenovoLegionToolkit.Lib;
using LenovoLegionToolkit.Lib.Controllers.Sensors;
using LenovoLegionToolkit.Lib.Features;
using LenovoLegionToolkit.Lib.Listeners;
using LenovoLegionToolkit.Lib.Settings;
using LenovoLegionToolkit.Lib.Utils;
using LenovoLegionToolkit.WPF.Resources;
using LenovoLegionToolkit.WPF.Settings;
using LenovoLegionToolkit.WPF.Utils;
using Wpf.Ui.Common;
using MenuItem = Wpf.Ui.Controls.MenuItem;

namespace LenovoLegionToolkit.WPF.Controls.Dashboard;

public partial class SensorsControl
{
    private readonly ISensorsController _controller = IoCContainer.Resolve<ISensorsController>();
    private readonly ApplicationSettings _applicationSettings = IoCContainer.Resolve<ApplicationSettings>();
    private readonly DashboardSettings _dashboardSettings = IoCContainer.Resolve<DashboardSettings>();
    private readonly PowerModeListener _powerModeListener = IoCContainer.Resolve<PowerModeListener>();
    private readonly PowerModeFeature _powerModeFeature = IoCContainer.Resolve<PowerModeFeature>();

    private SolidColorBrush _accentBrush = new(PerformanceModeColors.GetAccent(PowerModeState.Balance));

    /// <summary>
    /// When true, <see cref="UpdateValues"/> is suppressed so the sweep
    /// animation can drive bar values without interference from the
    /// telemetry refresh loop.
    /// </summary>
    private bool _sweepInProgress;

    private CancellationTokenSource? _cts;
    private Task? _refreshTask;

    /// <summary>
    /// All progress bars whose foreground tracks the active PowerMode accent.
    /// </summary>
    private ProgressBar[] AllBars => [
        _cpuUtilizationBar, _cpuCoreClockBar, _cpuTemperatureBar, _cpuFanSpeedBar,
        _gpuUtilizationBar, _gpuCoreClockBar, _gpuMemoryClockBar, _gpuTemperatureBar, _gpuFanSpeedBar
    ];

    /// <summary>
    /// Overlay rectangles that produce the swipe-up color transition.
    /// Indices correspond 1:1 with <see cref="AllBars"/>.
    /// </summary>
    private Rectangle[] AllOverlays => [
        _cpuUtilizationOverlay, _cpuCoreClockOverlay, _cpuTemperatureOverlay, _cpuFanSpeedOverlay,
        _gpuUtilizationOverlay, _gpuCoreClockOverlay, _gpuMemoryClockOverlay, _gpuTemperatureOverlay, _gpuFanSpeedOverlay
    ];

    public SensorsControl()
    {
        InitializeComponent();
        InitializeContextMenu();

        // Apply the shared accent brush to every bar up-front so the
        // animated ColorAnimation on the single brush instance propagates
        // to all bars simultaneously with no per-bar overhead.
        foreach (var bar in AllBars)
            bar.Foreground = _accentBrush;

        IsVisibleChanged += SensorsControl_IsVisibleChanged;
        Loaded += SensorsControl_Loaded;
    }

    private async void SensorsControl_Loaded(object sender, RoutedEventArgs e)
    {
        _powerModeListener.Changed += PowerModeListener_Changed;

        // Seed accent with current mode.
        try
        {
            if (await _powerModeFeature.IsSupportedAsync())
            {
                var mode = await _powerModeFeature.GetStateAsync();
                UpdateAccent(mode);
            }
        }
        catch { /* best-effort on init */ }
    }

    private void PowerModeListener_Changed(object? sender, PowerModeListener.ChangedEventArgs e)
    {
        Dispatcher.Invoke(() => UpdateAccent(e.State, playTransition: true));
    }

    private void UpdateAccent(PowerModeState mode, bool playTransition = false)
    {
        var target = PerformanceModeColors.GetAccent(mode);
        var targetBrush = new SolidColorBrush(target);

        // Play the swipe-up overlay on every bar before the color changes.
        var bars = AllBars;
        var overlays = AllOverlays;
        for (var i = 0; i < bars.Length; i++)
            PlaySwipeUp(overlays[i], bars[i], targetBrush);

        // Animate the single shared brush so all bars transition together.
        var anim = new ColorAnimation
        {
            To = target,
            Duration = TimeSpan.FromMilliseconds(250),
            EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut }
        };

        _accentBrush.BeginAnimation(SolidColorBrush.ColorProperty, anim);

        // Fire-and-forget the 3-phase sweep (collapse → expand → settle).
        if (playTransition)
            _ = SweepAllBarsAsync();
    }

    /// <summary>
    /// Runs a 3-phase value sweep on every bar via a single
    /// <see cref="Storyboard"/> per bar (no <c>Task.Delay</c> gaps):
    ///   Phase 1: current value → 0   (260 ms, QuinticEase EaseInOut)
    ///   Phase 2: 0 → Maximum         (320 ms, QuinticEase EaseOut)
    ///   Phase 3: Maximum → actual     (320 ms, QuinticEase EaseOut)
    /// Total = 900 ms.  Telemetry updates and the
    /// <see cref="Behaviors.ProgressBarAnimateBehavior"/> are suspended
    /// for the full duration so nothing fights the timeline.
    /// </summary>
    private async Task SweepAllBarsAsync()
    {
        // Guard: do not stack overlapping sweeps.
        if (_sweepInProgress)
            return;

        _sweepInProgress = true;

        try
        {
            var bars = AllBars;

            // Snapshot current values and maximums.
            var targets = new double[bars.Length];
            var maxes = new double[bars.Length];
            for (var i = 0; i < bars.Length; i++)
            {
                targets[i] = bars[i].Value;
                maxes[i] = bars[i].Maximum;
            }

            // Suspend the per-bar animate behavior so it cannot
            // start its own 250 ms DoubleAnimation on each frame.
            foreach (var bar in bars)
                Behaviors.ProgressBarAnimateBehavior.SetIsSuspended(bar, true);

            // Phase durations
            var collapseDur = TimeSpan.FromMilliseconds(260);
            var expandDur = TimeSpan.FromMilliseconds(320);
            var settleDur = TimeSpan.FromMilliseconds(320);

            // Easing – QuinticEase gives a smoother, more "weighted" feel.
            var collapseEase = new QuinticEase { EasingMode = EasingMode.EaseInOut };
            var expandEase = new QuinticEase { EasingMode = EasingMode.EaseOut };
            var settleEase = new QuinticEase { EasingMode = EasingMode.EaseOut };

            // Use a TaskCompletionSource tied to the LAST storyboard so we
            // can await the entire sweep without any Task.Delay.
            var tcs = new TaskCompletionSource();
            var pendingCount = bars.Length;

            for (var i = 0; i < bars.Length; i++)
            {
                var bar = bars[i];
                var start = targets[i];
                var max = maxes[i];
                var target = targets[i];

                // Phase 1 – collapse to 0
                var collapse = new DoubleAnimation
                {
                    From = start,
                    To = 0,
                    Duration = collapseDur,
                    EasingFunction = collapseEase
                };

                // Phase 2 – expand to maximum
                var expand = new DoubleAnimation
                {
                    From = 0,
                    To = max,
                    Duration = expandDur,
                    EasingFunction = expandEase,
                    BeginTime = collapseDur
                };

                // Phase 3 – settle to actual telemetry value
                var settle = new DoubleAnimation
                {
                    From = max,
                    To = target,
                    Duration = settleDur,
                    EasingFunction = settleEase,
                    BeginTime = collapseDur + expandDur
                };

                var sb = new Storyboard { FillBehavior = FillBehavior.Stop };
                Storyboard.SetTarget(collapse, bar);
                Storyboard.SetTargetProperty(collapse, new PropertyPath(RangeBase.ValueProperty));
                Storyboard.SetTarget(expand, bar);
                Storyboard.SetTargetProperty(expand, new PropertyPath(RangeBase.ValueProperty));
                Storyboard.SetTarget(settle, bar);
                Storyboard.SetTargetProperty(settle, new PropertyPath(RangeBase.ValueProperty));

                sb.Children.Add(collapse);
                sb.Children.Add(expand);
                sb.Children.Add(settle);

                sb.Completed += (_, _) =>
                {
                    // Clear animation layer so the behavior + UpdateValues
                    // can drive the property again.
                    bar.BeginAnimation(RangeBase.ValueProperty, null);

                    if (Interlocked.Decrement(ref pendingCount) == 0)
                        tcs.TrySetResult();
                };

                sb.Begin();
            }

            // Wait for every bar's storyboard to finish.
            await tcs.Task;

            // Re-enable the per-bar animate behavior.
            foreach (var bar in bars)
                Behaviors.ProgressBarAnimateBehavior.SetIsSuspended(bar, false);
        }
        finally
        {
            _sweepInProgress = false;
        }
    }

    /// <summary>
    /// Plays a bottom-to-top color wipe on <paramref name="overlay"/> that
    /// matches the height of <paramref name="bar"/>.  The overlay grows
    /// upward while fading out, revealing the new accent underneath.
    /// Duration matches the bar color animation (250 ms).
    /// </summary>
    private static void PlaySwipeUp(Rectangle overlay, FrameworkElement bar, SolidColorBrush fill)
    {
        var height = bar.ActualHeight;
        if (height <= 0)
            return;

        // Stop any in-flight animations so we start from a clean state.
        overlay.BeginAnimation(FrameworkElement.HeightProperty, null);
        overlay.BeginAnimation(UIElement.OpacityProperty, null);

        overlay.Fill = fill;
        overlay.Height = 0;
        overlay.Opacity = 0.9;

        var ease = new CubicEase { EasingMode = EasingMode.EaseOut };
        var duration = TimeSpan.FromMilliseconds(250);

        // Swipe height: 0 → bar height
        var heightAnim = new DoubleAnimation
        {
            From = 0,
            To = height,
            Duration = duration,
            EasingFunction = ease
        };

        // Fade out: 0.9 → 0  (overlay disappears as it reaches full height)
        var fadeAnim = new DoubleAnimation
        {
            From = 0.9,
            To = 0,
            Duration = duration,
            EasingFunction = ease
        };

        // Reset overlay to invisible once the animation completes.
        fadeAnim.Completed += (_, _) =>
        {
            overlay.BeginAnimation(FrameworkElement.HeightProperty, null);
            overlay.BeginAnimation(UIElement.OpacityProperty, null);
            overlay.Height = 0;
            overlay.Opacity = 0;
        };

        overlay.BeginAnimation(FrameworkElement.HeightProperty, heightAnim);
        overlay.BeginAnimation(UIElement.OpacityProperty, fadeAnim);
    }

    private void InitializeContextMenu()
    {
        ContextMenu = new ContextMenu();
        ContextMenu.Items.Add(new MenuItem { Header = Resource.SensorsControl_RefreshInterval, IsEnabled = false });

        foreach (var interval in new[] { 1, 2, 3, 5 })
        {
            var item = new MenuItem
            {
                SymbolIcon = _dashboardSettings.Store.SensorsRefreshIntervalSeconds == interval ? SymbolRegular.Checkmark24 : SymbolRegular.Empty,
                Header = TimeSpan.FromSeconds(interval).Humanize(culture: Resource.Culture)
            };
            item.Click += (_, _) =>
            {
                _dashboardSettings.Store.SensorsRefreshIntervalSeconds = interval;
                _dashboardSettings.SynchronizeStore();
                InitializeContextMenu();
            };
            ContextMenu.Items.Add(item);
        }
    }

    private async void SensorsControl_IsVisibleChanged(object sender, DependencyPropertyChangedEventArgs e)
    {
        if (IsVisible)
        {
            Refresh();
            return;
        }

        if (_cts is not null)
            await _cts.CancelAsync();

        _cts = null;

        if (_refreshTask is not null)
            await _refreshTask;

        _refreshTask = null;

        UpdateValues(SensorsData.Empty);
    }

    private void Refresh()
    {
        _cts?.Cancel();
        _cts = new CancellationTokenSource();

        var token = _cts.Token;

        _refreshTask = Task.Run(async () =>
        {
            if (Log.Instance.IsTraceEnabled)
                Log.Instance.Trace($"Sensors refresh started...");

            if (!await _controller.IsSupportedAsync())
            {
                if (Log.Instance.IsTraceEnabled)
                    Log.Instance.Trace($"Sensors not supported.");

                Dispatcher.Invoke(() => Visibility = Visibility.Collapsed);
                return;
            }

            await _controller.PrepareAsync();

            while (!token.IsCancellationRequested)
            {
                try
                {
                    var data = await _controller.GetDataAsync();
                    Dispatcher.Invoke(() => UpdateValues(data));
                    await Task.Delay(TimeSpan.FromSeconds(_dashboardSettings.Store.SensorsRefreshIntervalSeconds), token);
                }
                catch (OperationCanceledException) { }
                catch (Exception ex)
                {
                    if (Log.Instance.IsTraceEnabled)
                        Log.Instance.Trace($"Sensors refresh failed.", ex);

                    Dispatcher.Invoke(() => UpdateValues(SensorsData.Empty));
                }
            }

            if (Log.Instance.IsTraceEnabled)
                Log.Instance.Trace($"Sensors refresh stopped.");
        }, token);
    }

    private void UpdateValues(SensorsData data)
    {
        // During the sweep animation, skip telemetry updates so the
        // animation can drive bar values uninterrupted.
        if (_sweepInProgress)
            return;

        UpdateValue(_cpuUtilizationBar, _cpuUtilizationLabel, data.CPU.MaxUtilization, data.CPU.Utilization,
            $"{data.CPU.Utilization}%");
        UpdateValue(_cpuCoreClockBar, _cpuCoreClockLabel, data.CPU.MaxCoreClock, data.CPU.CoreClock,
            $"{data.CPU.CoreClock / 1000.0:0.0} {Resource.GHz}", $"{data.CPU.MaxCoreClock / 1000.0:0.0} {Resource.GHz}");
        UpdateValue(_cpuTemperatureBar, _cpuTemperatureLabel, data.CPU.MaxTemperature, data.CPU.Temperature,
            GetTemperatureText(data.CPU.Temperature), GetTemperatureText(data.CPU.MaxTemperature));
        UpdateValue(_cpuFanSpeedBar, _cpuFanSpeedLabel, data.CPU.MaxFanSpeed, data.CPU.FanSpeed,
            $"{data.CPU.FanSpeed} {Resource.RPM}", $"{data.CPU.MaxFanSpeed} {Resource.RPM}");

        UpdateValue(_gpuUtilizationBar, _gpuUtilizationLabel, data.GPU.MaxUtilization, data.GPU.Utilization,
            $"{data.GPU.Utilization} %");
        UpdateValue(_gpuCoreClockBar, _gpuCoreClockLabel, data.GPU.MaxCoreClock, data.GPU.CoreClock,
            $"{data.GPU.CoreClock} {Resource.MHz}", $"{data.GPU.MaxCoreClock} {Resource.MHz}");
        UpdateValue(_gpuMemoryClockBar, _gpuMemoryClockLabel, data.GPU.MaxMemoryClock, data.GPU.MemoryClock,
            $"{data.GPU.MemoryClock} {Resource.MHz}", $"{data.GPU.MaxMemoryClock} {Resource.MHz}");
        UpdateValue(_gpuTemperatureBar, _gpuTemperatureLabel, data.GPU.MaxTemperature, data.GPU.Temperature,
            GetTemperatureText(data.GPU.Temperature), GetTemperatureText(data.GPU.MaxTemperature));
        UpdateValue(_gpuFanSpeedBar, _gpuFanSpeedLabel, data.GPU.MaxFanSpeed, data.GPU.FanSpeed,
            $"{data.GPU.FanSpeed} {Resource.RPM}", $"{data.GPU.MaxFanSpeed} {Resource.RPM}");
    }

    private string GetTemperatureText(double temperature)
    {
        if (_applicationSettings.Store.TemperatureUnit == TemperatureUnit.F)
        {
            temperature *= 9.0 / 5.0;
            temperature += 32;
            return $"{temperature:0} {Resource.Fahrenheit}";
        }

        return $"{temperature:0} {Resource.Celsius}";
    }

    private static void UpdateValue(RangeBase bar, ContentControl label, double max, double value, string text, string? toolTipText = null)
    {
        if (max < 0 || value < 0)
        {
            bar.Minimum = 0;
            bar.Maximum = 1;
            bar.Value = 0;
            label.Content = "-";
            label.ToolTip = null;
            label.Tag = 0;
        }
        else
        {
            bar.Minimum = 0;
            bar.Maximum = max;
            bar.Value = value;
            label.Content = text;
            label.ToolTip = toolTipText is null ? null : string.Format(Resource.SensorsControl_Maximum, toolTipText);
            label.Tag = value;
        }
    }
}
