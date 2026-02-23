using LoqNova.Lib.System.Management;

namespace LoqNova.Lib.Features.Hybrid;

public class IGPUModeGamezoneFeature()
    : AbstractWmiFeature<IGPUModeState>(WMI.LenovoGameZoneData.GetIGPUModeStatusAsync, WMI.LenovoGameZoneData.SetIGPUModeStatusAsync, WMI.LenovoGameZoneData.IsSupportIGPUModeAsync);
