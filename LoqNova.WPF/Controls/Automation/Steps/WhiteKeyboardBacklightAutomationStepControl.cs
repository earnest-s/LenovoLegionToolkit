using LoqNova.Lib;
using LoqNova.Lib.Automation.Steps;
using LoqNova.WPF.Resources;
using Wpf.Ui.Common;

namespace LoqNova.WPF.Controls.Automation.Steps;

public class WhiteKeyboardBacklightAutomationStepControl : AbstractComboBoxAutomationStepCardControl<WhiteKeyboardBacklightState>
{
    public WhiteKeyboardBacklightAutomationStepControl(IAutomationStep<WhiteKeyboardBacklightState> step) : base(step)
    {
        Icon = SymbolRegular.Keyboard24;
        Title = Resource.WhiteKeyboardBacklightAutomationStepControl_Title;
        Subtitle = Resource.WhiteKeyboardBacklightAutomationStepControl_Message;
    }
}
