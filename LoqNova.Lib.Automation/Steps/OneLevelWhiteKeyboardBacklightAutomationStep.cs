using Newtonsoft.Json;

namespace LoqNova.Lib.Automation.Steps;

[method: JsonConstructor]
public class OneLevelWhiteKeyboardBacklightAutomationStep(OneLevelWhiteKeyboardBacklightState state)
    : AbstractFeatureAutomationStep<OneLevelWhiteKeyboardBacklightState>(state)
{
    public override IAutomationStep DeepCopy() => new OneLevelWhiteKeyboardBacklightAutomationStep(State);
}
