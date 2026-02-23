using LoqNova.Lib.System.Management;

namespace LoqNova.Lib.Features;

public class WinKeyFeature()
    : AbstractWmiFeature<WinKeyState>(WMI.LenovoGameZoneData.GetWinKeyStatusAsync, WMI.LenovoGameZoneData.SetWinKeyStatusAsync, WMI.LenovoGameZoneData.IsSupportDisableWinKeyAsync);
