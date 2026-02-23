using System;
using System.Threading.Tasks;
using LoqNova.Lib.Features;
using LoqNova.Lib.Features.PanelLogo;
using LoqNova.Lib.Messaging;
using LoqNova.Lib.Messaging.Messages;
using LoqNova.Lib.SoftwareDisabler;
using LoqNova.Lib.System.Management;
using LoqNova.Lib.Utils;

namespace LoqNova.Lib.Listeners;

public class LightingChangeListener(
    PanelLogoBacklightFeature panelLogoBacklightFeature,
    PortsBacklightFeature portsBacklightFeature,
    FnKeysDisabler fnKeysDisabler)
    : AbstractWMIListener<LightingChangeListener.ChangedEventArgs, LightingChangeState, int>(WMI.LenovoLightingEvent
        .Listen)
{
    public class ChangedEventArgs(LightingChangeState state) : EventArgs
    {
        public LightingChangeState State { get; } = state;
    }

    protected override LightingChangeState GetValue(int value)
    {
        if (Log.Instance.IsTraceEnabled)
            Log.Instance.Trace($"Event received. [value={value}]");

        var result = (LightingChangeState)value;
        return result;
    }

    protected override ChangedEventArgs GetEventArgs(LightingChangeState value) => new(value);

    protected override async Task OnChangedAsync(LightingChangeState value)
    {
        try
        {
            if (await fnKeysDisabler.GetStatusAsync().ConfigureAwait(false) == SoftwareStatus.Enabled)
            {
                if (Log.Instance.IsTraceEnabled)
                    Log.Instance.Trace($"Ignoring, FnKeys are enabled.");

                return;
            }

            switch (value)
            {
                case LightingChangeState.Panel when await panelLogoBacklightFeature.IsSupportedAsync().ConfigureAwait(false):
                    {
                        var type = await panelLogoBacklightFeature.GetStateAsync().ConfigureAwait(false) == PanelLogoBacklightState.On
                            ? NotificationType.PanelLogoLightingOn
                            : NotificationType.PanelLogoLightingOff;

                        MessagingCenter.Publish(new NotificationMessage(type));
                        break;
                    }
                case LightingChangeState.Ports when await portsBacklightFeature.IsSupportedAsync().ConfigureAwait(false):
                    {
                        var type = await portsBacklightFeature.GetStateAsync().ConfigureAwait(false) == PortsBacklightState.On
                            ? NotificationType.PortLightingOn
                            : NotificationType.PortLightingOff;

                        MessagingCenter.Publish(new NotificationMessage(type));
                        break;
                    }
            }
        }
        catch { /* Ignored. */ }
    }
}
