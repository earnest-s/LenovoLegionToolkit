using LoqNova.Lib.Automation;
using LoqNova.Lib.Automation.Steps;
using LoqNova.WPF.Resources;
using Wpf.Ui.Common;

namespace LoqNova.WPF.Controls.Automation.Steps;

public class DelayAutomationStepControl : AbstractComboBoxAutomationStepCardControl<Delay>
{
    public DelayAutomationStepControl(IAutomationStep<Delay> step) : base(step)
    {
        Icon = SymbolRegular.Clock24;
        Title = Resource.DelayAutomationStepControl_Title;
        Subtitle = Resource.DelayAutomationStepControl_Message;
    }
}
