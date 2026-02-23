using Newtonsoft.Json;

namespace LoqNova.Lib.Automation.Steps;

[method: JsonConstructor]
public class WinKeyAutomationStep(WinKeyState state)
    : AbstractFeatureAutomationStep<WinKeyState>(state)
{
    public override IAutomationStep DeepCopy() => new WinKeyAutomationStep(State);
}
