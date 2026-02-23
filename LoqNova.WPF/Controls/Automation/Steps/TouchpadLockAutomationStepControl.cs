using LoqNova.Lib;
using LoqNova.Lib.Automation.Steps;
using LoqNova.WPF.Resources;
using Wpf.Ui.Common;

namespace LoqNova.WPF.Controls.Automation.Steps;

public class TouchpadLockAutomationStepControl : AbstractComboBoxAutomationStepCardControl<TouchpadLockState>
{
    public TouchpadLockAutomationStepControl(IAutomationStep<TouchpadLockState> step) : base(step)
    {
        Icon = SymbolRegular.Tablet24;
        Title = Resource.TouchpadLockAutomationStepControl_Title;
        Subtitle = Resource.TouchpadLockAutomationStepControl_Message;
    }
}
