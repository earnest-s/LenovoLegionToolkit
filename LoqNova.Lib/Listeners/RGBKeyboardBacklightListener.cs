using System;
using System.Threading.Tasks;
using LoqNova.Lib.Controllers;
using LoqNova.Lib.Extensions;
using LoqNova.Lib.Messaging;
using LoqNova.Lib.Messaging.Messages;
using LoqNova.Lib.System.Management;
using LoqNova.Lib.Utils;

namespace LoqNova.Lib.Listeners;

public class RGBKeyboardBacklightListener(RGBKeyboardBacklightController controller)
    : AbstractWMIListener<EventArgs, RGBKeyboardBacklightChanged, int>(WMI.LenovoGameZoneLightProfileChangeEvent.Listen)
{
    protected override RGBKeyboardBacklightChanged GetValue(int value) => default;

    protected override EventArgs GetEventArgs(RGBKeyboardBacklightChanged value) => EventArgs.Empty;

    protected override async Task OnChangedAsync(RGBKeyboardBacklightChanged value)
    {
        try
        {
            if (!await controller.IsSupportedAsync().ConfigureAwait(false))
            {
                if (Log.Instance.IsTraceEnabled)
                    Log.Instance.Trace($"Not supported.");

                return;
            }

            if (Log.Instance.IsTraceEnabled)
                Log.Instance.Trace($"Taking ownership...");

            await controller.SetLightControlOwnerAsync(true).ConfigureAwait(false);

            if (Log.Instance.IsTraceEnabled)
                Log.Instance.Trace($"Setting next preset set...");

            var preset = await controller.SetNextPresetAsync().ConfigureAwait(false);

            MessagingCenter.Publish(preset == RGBKeyboardBacklightPreset.Off
                ? new NotificationMessage(NotificationType.RGBKeyboardBacklightOff, preset.GetDisplayName())
                : new NotificationMessage(NotificationType.RGBKeyboardBacklightChanged, preset.GetDisplayName()));

            if (Log.Instance.IsTraceEnabled)
                Log.Instance.Trace($"Next preset set");
        }
        catch (Exception ex)
        {
            if (Log.Instance.IsTraceEnabled)
                Log.Instance.Trace($"Failed to set next keyboard backlight preset.", ex);
        }
    }
}
