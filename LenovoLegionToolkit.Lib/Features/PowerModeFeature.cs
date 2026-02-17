using System;
using System.Linq;
using System.Threading.Tasks;
using LenovoLegionToolkit.Lib.Controllers;
using LenovoLegionToolkit.Lib.Controllers.GodMode;
using LenovoLegionToolkit.Lib.Extensions;
using LenovoLegionToolkit.Lib.Listeners;
using LenovoLegionToolkit.Lib.Messaging;
using LenovoLegionToolkit.Lib.Messaging.Messages;
using LenovoLegionToolkit.Lib.System;
using LenovoLegionToolkit.Lib.System.Management;
using LenovoLegionToolkit.Lib.Utils;

namespace LenovoLegionToolkit.Lib.Features;

public class PowerModeUnavailableWithoutACException(PowerModeState powerMode) : Exception
{
    public PowerModeState PowerMode { get; } = powerMode;
}

public class PowerModeFeature(
    GodModeController godModeController,
    WindowsPowerModeController windowsPowerModeController,
    WindowsPowerPlanController windowsPowerPlanController,
    RGBKeyboardBacklightController rgbKeyboardBacklightController,
    ThermalModeListener thermalModeListener,
    PowerModeListener powerModeListener)
    : AbstractWmiFeature<PowerModeState>(WMI.LenovoGameZoneData.GetSmartFanModeAsync, WMI.LenovoGameZoneData.SetSmartFanModeAsync, WMI.LenovoGameZoneData.IsSupportSmartFanAsync, 1)
{
    public bool AllowAllPowerModesOnBattery { get; set; }

    public override async Task<PowerModeState[]> GetAllStatesAsync()
    {
        var mi = await Compatibility.GetMachineInformationAsync().ConfigureAwait(false);
        return mi.Properties.SupportsGodMode
            ? [PowerModeState.Quiet, PowerModeState.Balance, PowerModeState.Performance, PowerModeState.GodMode]
            : [PowerModeState.Quiet, PowerModeState.Balance, PowerModeState.Performance];
    }

    public override async Task SetStateAsync(PowerModeState state)
    {
        var allStates = await GetAllStatesAsync().ConfigureAwait(false);
        if (!allStates.Contains(state))
            throw new InvalidOperationException($"Unsupported power mode {state}");

        if (state is PowerModeState.Performance or PowerModeState.GodMode
            && !AllowAllPowerModesOnBattery
            && await Power.IsPowerAdapterConnectedAsync().ConfigureAwait(false) is PowerAdapterStatus.Disconnected)
            throw new PowerModeUnavailableWithoutACException(state);

        var currentState = await GetStateAsync().ConfigureAwait(false);

        var mi = await Compatibility.GetMachineInformationAsync().ConfigureAwait(false);

        if (mi.Properties.HasQuietToPerformanceModeSwitchingBug && currentState == PowerModeState.Quiet && state == PowerModeState.Performance)
        {
            thermalModeListener.SuppressNext();
            await base.SetStateAsync(PowerModeState.Balance).ConfigureAwait(false);
            await Task.Delay(TimeSpan.FromMilliseconds(500)).ConfigureAwait(false);
        }

        if (mi.Properties.HasGodModeToOtherModeSwitchingBug && currentState == PowerModeState.GodMode && state != PowerModeState.GodMode)
        {
            thermalModeListener.SuppressNext();

            switch (state)
            {
                case PowerModeState.Quiet:
                    await base.SetStateAsync(PowerModeState.Performance).ConfigureAwait(false);
                    break;
                case PowerModeState.Balance:
                    await base.SetStateAsync(PowerModeState.Quiet).ConfigureAwait(false);
                    break;
                case PowerModeState.Performance:
                    await base.SetStateAsync(PowerModeState.Balance).ConfigureAwait(false);
                    break;
            }

            await Task.Delay(TimeSpan.FromMilliseconds(500)).ConfigureAwait(false);

        }

        thermalModeListener.SuppressNext();
        await base.SetStateAsync(state).ConfigureAwait(false);

        // Central post-change logic: dependencies, RGB strobe, notification
        await ApplyPerformanceModeAsync(state).ConfigureAwait(false);

        // Raise listener Changed event for existing subscribers
        await powerModeListener.NotifyAsync(state).ConfigureAwait(false);
    }

    /// <summary>
    /// Central performance mode change logic. Called by both
    /// <see cref="SetStateAsync"/> (dropdown / automation) and
    /// <see cref="PowerModeListener"/> (Fn+Q hardware key).
    /// Handles dependencies, RGB strobe, and OSD notification.
    /// </summary>
    public async Task ApplyPerformanceModeAsync(PowerModeState mode)
    {
        // 1. Apply dependencies (GodMode preset, Windows power mode/plan)
        if (mode is PowerModeState.GodMode)
            await godModeController.ApplyStateAsync().ConfigureAwait(false);

        await windowsPowerModeController.SetPowerModeAsync(mode).ConfigureAwait(false);
        await windowsPowerPlanController.SetPowerPlanAsync(mode).ConfigureAwait(false);

        // 2. RGB strobe (fire-and-forget animation via PlayTransitionAsync)
        try
        {
            if (await rgbKeyboardBacklightController.IsSupportedAsync().ConfigureAwait(false))
                await rgbKeyboardBacklightController.PlayTransitionAsync(mode).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            if (Log.Instance.IsTraceEnabled)
                Log.Instance.Trace($"Failed to trigger RGB strobe for power mode {mode}", ex);
        }

        // 3. OSD notification
        PublishNotification(mode);
    }

    public async Task EnsureCorrectWindowsPowerSettingsAreSetAsync()
    {
        var state = await GetStateAsync().ConfigureAwait(false);
        await windowsPowerModeController.SetPowerModeAsync(state).ConfigureAwait(false);
        await windowsPowerPlanController.SetPowerPlanAsync(state, true).ConfigureAwait(false);
    }

    public async Task EnsureGodModeStateIsAppliedAsync()
    {
        var state = await GetStateAsync().ConfigureAwait(false);
        if (state != PowerModeState.GodMode)
            return;

        await godModeController.ApplyStateAsync().ConfigureAwait(false);
    }

    private static void PublishNotification(PowerModeState value)
    {
        var type = value switch
        {
            PowerModeState.Quiet => NotificationType.PowerModeQuiet,
            PowerModeState.Balance => NotificationType.PowerModeBalance,
            PowerModeState.Performance => NotificationType.PowerModePerformance,
            PowerModeState.GodMode => NotificationType.PowerModeGodMode,
            _ => (NotificationType?)null
        };

        if (type is { } t)
            MessagingCenter.Publish(new NotificationMessage(t, value.GetDisplayName()));
    }
}
