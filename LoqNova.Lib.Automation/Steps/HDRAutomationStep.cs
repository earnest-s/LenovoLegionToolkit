using Newtonsoft.Json;

namespace LoqNova.Lib.Automation.Steps;

[method: JsonConstructor]
public class HDRAutomationStep(HDRState state)
    : AbstractFeatureAutomationStep<HDRState>(state)
{
    public override IAutomationStep DeepCopy() => new HDRAutomationStep(State);
}
