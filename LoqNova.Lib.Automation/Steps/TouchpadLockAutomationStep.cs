using Newtonsoft.Json;

namespace LoqNova.Lib.Automation.Steps;

[method: JsonConstructor]
public class TouchpadLockAutomationStep(TouchpadLockState state)
    : AbstractFeatureAutomationStep<TouchpadLockState>(state)
{
    public override IAutomationStep DeepCopy() => new TouchpadLockAutomationStep(State);
}
