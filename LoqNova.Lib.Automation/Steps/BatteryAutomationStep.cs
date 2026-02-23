using Newtonsoft.Json;

namespace LoqNova.Lib.Automation.Steps;

[method: JsonConstructor]
public class BatteryAutomationStep(BatteryState state)
    : AbstractFeatureAutomationStep<BatteryState>(state)
{
    public override IAutomationStep DeepCopy() => new BatteryAutomationStep(State);
}
