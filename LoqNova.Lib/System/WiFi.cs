using System;
using System.Linq;
using LoqNova.Lib.Extensions;
using LoqNova.Lib.Utils;
using ManagedNativeWifi;

namespace LoqNova.Lib.System;

public static class WiFi
{
    public static void TurnOn()
    {
        try
        {
            NativeWifi.EnumerateInterfaces()
            .ForEach(i => NativeWifi.TurnOnInterfaceRadio(i.Id));
        }
        catch (Exception ex)
        {
            if (Log.Instance.IsTraceEnabled)
                Log.Instance.Trace($"Failed to turn on WiFi.", ex);
        }
    }

    public static void TurnOff()
    {
        try
        {
            NativeWifi.EnumerateInterfaces()
                .ForEach(i => NativeWifi.TurnOffInterfaceRadio(i.Id));
        }
        catch (Exception ex)
        {
            if (Log.Instance.IsTraceEnabled)
                Log.Instance.Trace($"Failed to turn off WiFi.", ex);
        }
    }

    public static string? GetConnectedNetworkSsid()
    {
        return NativeWifi.EnumerateConnectedNetworkSsids()
            .Select(c => c.ToString())
            .FirstOrDefault();
    }
}
