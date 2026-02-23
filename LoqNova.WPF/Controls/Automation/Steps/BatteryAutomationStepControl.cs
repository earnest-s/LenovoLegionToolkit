using LoqNova.Lib;
using LoqNova.Lib.Automation.Steps;
using LoqNova.WPF.Resources;
using Wpf.Ui.Common;

namespace LoqNova.WPF.Controls.Automation.Steps;

public class BatteryAutomationStepControl : AbstractComboBoxAutomationStepCardControl<BatteryState>
{
    public BatteryAutomationStepControl(IAutomationStep<BatteryState> step) : base(step)
    {
        Icon = SymbolRegular.BatteryCharge24;
        Title = Resource.BatteryAutomationStepControl_Title;
        Subtitle = Resource.BatteryAutomationStepControl_Message;
    }
}
