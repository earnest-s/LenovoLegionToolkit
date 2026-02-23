using LoqNova.Lib.Automation;
using LoqNova.Lib.Automation.Steps;
using LoqNova.WPF.Resources;
using Wpf.Ui.Common;

namespace LoqNova.WPF.Controls.Automation.Steps;

public class MacroAutomationStepControl : AbstractComboBoxAutomationStepCardControl<MacroAutomationStepState>
{
    public MacroAutomationStepControl(IAutomationStep<MacroAutomationStepState> step) : base(step)
    {
        Icon = SymbolRegular.ReceiptPlay24;
        Title = Resource.MacroAutomationStepControl_Title;
        Subtitle = Resource.MacroAutomationStepControl_Message;
    }
}
