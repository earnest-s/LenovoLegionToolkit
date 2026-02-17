using System;
using System.Threading.Tasks;
using LenovoLegionToolkit.Lib.Features;
using LenovoLegionToolkit.Lib.System.Management;
using LenovoLegionToolkit.Lib.Utils;

namespace LenovoLegionToolkit.Lib.Listeners;

/// <summary>
/// Listens to WMI <c>LenovoGameZoneSmartFanModeEvent</c> (hardware Fn+Q key).
/// Forwards to <see cref="PowerModeFeature.ApplyPerformanceModeAsync"/> — the
/// single central method shared by both Fn+Q and dropdown paths.
/// Uses <see cref="Lazy{T}"/> to break the circular dependency with the feature.
/// </summary>
public class PowerModeListener(Lazy<PowerModeFeature> powerModeFeature)
    : AbstractWMIListener<PowerModeListener.ChangedEventArgs, PowerModeState, int>(WMI.LenovoGameZoneSmartFanModeEvent.Listen), INotifyingListener<PowerModeListener.ChangedEventArgs, PowerModeState>
{
    public class ChangedEventArgs(PowerModeState state) : EventArgs
    {
        public PowerModeState State { get; } = state;
    }

    protected override PowerModeState GetValue(int value) => (PowerModeState)(value - 1);

    protected override ChangedEventArgs GetEventArgs(PowerModeState value) => new(value);

    protected override async Task OnChangedAsync(PowerModeState value)
    {
        // Fn+Q: hardware already set the mode via EC/WMI.
        // Central logic handles dependencies, RGB strobe, and notification.
        // After this returns, base class auto-raises Changed event.
        if (Log.Instance.IsTraceEnabled)
            Log.Instance.Trace($"Fn+Q power mode change: {value}");

        await powerModeFeature.Value.ApplyPerformanceModeAsync(value).ConfigureAwait(false);
    }

    public Task NotifyAsync(PowerModeState value)
    {
        // Called by PowerModeFeature.SetStateAsync after ApplyPerformanceModeAsync.
        // Just raises Changed event for existing subscribers (UI refresh, automation).
        RaiseChanged(value);
        return Task.CompletedTask;
    }
}
