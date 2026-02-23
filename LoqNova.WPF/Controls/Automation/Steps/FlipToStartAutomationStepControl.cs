using LoqNova.Lib;
using LoqNova.Lib.Automation.Steps;
using LoqNova.WPF.Resources;
using Wpf.Ui.Common;

namespace LoqNova.WPF.Controls.Automation.Steps;

public class FlipToStartAutomationStepControl : AbstractComboBoxAutomationStepCardControl<FlipToStartState>
{
    public FlipToStartAutomationStepControl(IAutomationStep<FlipToStartState> step) : base(step)
    {
        Icon = SymbolRegular.Power24;
        Title = Resource.FlipToStartAutomationStepControl_Title;
        Subtitle = Resource.FlipToStartAutomationStepControl_Message;
    }
}
