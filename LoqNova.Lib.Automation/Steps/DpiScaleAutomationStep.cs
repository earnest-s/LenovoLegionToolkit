using Newtonsoft.Json;

namespace LoqNova.Lib.Automation.Steps;

[method: JsonConstructor]
public class DpiScaleAutomationStep(DpiScale state)
    : AbstractFeatureAutomationStep<DpiScale>(state)
{
    public override IAutomationStep DeepCopy() => new DpiScaleAutomationStep(State);
}
