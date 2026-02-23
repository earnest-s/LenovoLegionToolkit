using LoqNova.Lib.System.Management;

namespace LoqNova.Lib.Features.Hybrid;

public class GSyncFeature()
    : AbstractWmiFeature<GSyncState>(WMI.LenovoGameZoneData.GetGSyncStatusAsync, WMI.LenovoGameZoneData.SetGSyncStatusAsync, WMI.LenovoGameZoneData.IsSupportGSyncAsync);
