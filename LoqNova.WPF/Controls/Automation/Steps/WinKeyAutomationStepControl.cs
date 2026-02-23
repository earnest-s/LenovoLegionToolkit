using LoqNova.Lib;
using LoqNova.Lib.Automation.Steps;
using LoqNova.WPF.Resources;
using Wpf.Ui.Common;

namespace LoqNova.WPF.Controls.Automation.Steps;

public class WinKeyAutomationStepControl : AbstractComboBoxAutomationStepCardControl<WinKeyState>
{
    public WinKeyAutomationStepControl(IAutomationStep<WinKeyState> step) : base(step)
    {
        Icon = SymbolRegular.Keyboard24;
        Title = Resource.WinKeyAutomationStepControl_Title;
        Subtitle = Resource.WinKeyAutomationStepControl_Message;
    }
}
