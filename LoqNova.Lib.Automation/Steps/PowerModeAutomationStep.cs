using Newtonsoft.Json;

namespace LoqNova.Lib.Automation.Steps;

[method: JsonConstructor]
public class PowerModeAutomationStep(PowerModeState state)
    : AbstractFeatureAutomationStep<PowerModeState>(state)
{
    public override IAutomationStep DeepCopy() => new PowerModeAutomationStep(State);
}
