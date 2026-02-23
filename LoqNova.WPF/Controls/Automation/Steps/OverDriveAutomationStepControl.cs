using LoqNova.Lib;
using LoqNova.Lib.Automation.Steps;
using LoqNova.WPF.Resources;
using Wpf.Ui.Common;

namespace LoqNova.WPF.Controls.Automation.Steps;

public class OverDriveAutomationStepControl : AbstractComboBoxAutomationStepCardControl<OverDriveState>
{
    public OverDriveAutomationStepControl(IAutomationStep<OverDriveState> step) : base(step)
    {
        Icon = SymbolRegular.TopSpeed24;
        Title = Resource.OverDriveAutomationStepControl_Title;
        Subtitle = Resource.OverDriveAutomationStepControl_Message;
    }
}
