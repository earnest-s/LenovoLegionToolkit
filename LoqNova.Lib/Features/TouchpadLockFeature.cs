using LoqNova.Lib.System.Management;

namespace LoqNova.Lib.Features;

public class TouchpadLockFeature()
    : AbstractWmiFeature<TouchpadLockState>(WMI.LenovoGameZoneData.GetTPStatusStatusAsync, WMI.LenovoGameZoneData.SetTPStatusAsync, WMI.LenovoGameZoneData.IsSupportDisableTPAsync);
