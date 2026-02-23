using Newtonsoft.Json;

namespace LoqNova.Lib.Automation.Steps;

[method: JsonConstructor]
public class MicrophoneAutomationStep(MicrophoneState state)
    : AbstractFeatureAutomationStep<MicrophoneState>(state)
{
    public override IAutomationStep DeepCopy() => new MicrophoneAutomationStep(State);
}
