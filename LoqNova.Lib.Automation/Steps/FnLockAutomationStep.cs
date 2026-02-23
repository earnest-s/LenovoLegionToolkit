using Newtonsoft.Json;

namespace LoqNova.Lib.Automation.Steps;

[method: JsonConstructor]
public class FnLockAutomationStep(FnLockState state)
    : AbstractFeatureAutomationStep<FnLockState>(state)
{
    public override IAutomationStep DeepCopy() => new FnLockAutomationStep(State);
}
