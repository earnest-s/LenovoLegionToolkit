using System;

namespace LoqNova.Lib.Features.Hybrid;

public class IGPUModeChangeException(IGPUModeState igpuMode) : Exception
{
    public IGPUModeState IGPUMode { get; } = igpuMode;
}
