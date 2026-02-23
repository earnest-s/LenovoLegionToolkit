using Newtonsoft.Json;

namespace LoqNova.Lib.Automation.Steps;

[method: JsonConstructor]
public class PortsBacklightAutomationStep(PortsBacklightState state)
    : AbstractFeatureAutomationStep<PortsBacklightState>(state)
{
    public override IAutomationStep DeepCopy() => new PortsBacklightAutomationStep(State);
}
