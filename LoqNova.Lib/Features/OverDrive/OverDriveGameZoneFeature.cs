using LoqNova.Lib.System.Management;

namespace LoqNova.Lib.Features.OverDrive;

public class OverDriveGameZoneFeature()
    : AbstractWmiFeature<OverDriveState>(WMI.LenovoGameZoneData.GetODStatusAsync, WMI.LenovoGameZoneData.SetODStatusAsync, WMI.LenovoGameZoneData.IsSupportODAsync);
