using System.Threading.Tasks;
using LoqNova.Lib.System.Management;

namespace LoqNova.Lib.Controllers;

public class DisplayBrightnessController
{
    public Task SetBrightnessAsync(int brightness) => WMI.WmiMonitorBrightnessMethods.WmiSetBrightness(brightness, 1);
}
