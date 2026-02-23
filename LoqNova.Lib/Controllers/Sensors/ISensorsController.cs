using System.Threading.Tasks;

namespace LoqNova.Lib.Controllers.Sensors;

public interface ISensorsController
{
    Task<bool> IsSupportedAsync();
    Task PrepareAsync();
    Task<SensorsData> GetDataAsync();
    Task<(int cpuFanSpeed, int gpuFanSpeed)> GetFanSpeedsAsync();
}
