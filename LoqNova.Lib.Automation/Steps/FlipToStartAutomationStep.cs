using Newtonsoft.Json;

namespace LoqNova.Lib.Automation.Steps;

[method: JsonConstructor]
public class FlipToStartAutomationStep(FlipToStartState state)
    : AbstractFeatureAutomationStep<FlipToStartState>(state)
{
    public override IAutomationStep DeepCopy() => new FlipToStartAutomationStep(State);
}
