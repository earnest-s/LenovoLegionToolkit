using LoqNova.Lib;
using LoqNova.Lib.Automation.Steps;
using LoqNova.WPF.Resources;
using Wpf.Ui.Common;

namespace LoqNova.WPF.Controls.Automation.Steps;

public class FnLockAutomationStepControl : AbstractComboBoxAutomationStepCardControl<FnLockState>
{
    public FnLockAutomationStepControl(IAutomationStep<FnLockState> step) : base(step)
    {
        Icon = SymbolRegular.Keyboard24;
        Title = Resource.FnLockAutomationStepControl_Title;
        Subtitle = Resource.FnLockAutomationStepControl_Message;
    }
}
