using Newtonsoft.Json;

namespace LoqNova.Lib.Automation.Steps;

[method: JsonConstructor]
public class PanelLogoBacklightAutomationStep(PanelLogoBacklightState state)
    : AbstractFeatureAutomationStep<PanelLogoBacklightState>(state)
{
    public override IAutomationStep DeepCopy() => new PanelLogoBacklightAutomationStep(State);
}
