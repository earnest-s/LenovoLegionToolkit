using LoqNova.Lib.Automation.Steps;
using LoqNova.WPF.Resources;
using Wpf.Ui.Common;

namespace LoqNova.WPF.Controls.Automation.Steps;

public class SpectrumKeyboardBacklightProfileAutomationStepControl : AbstractComboBoxAutomationStepCardControl<int>
{
    public SpectrumKeyboardBacklightProfileAutomationStepControl(IAutomationStep<int> step) : base(step)
    {
        Icon = SymbolRegular.BrightnessHigh24;
        Title = Resource.SpectrumKeyboardBacklightProfileAutomationStepControl_Title;
        Subtitle = Resource.SpectrumKeyboardBacklightProfileAutomationStepControl_Message;
    }
}
