using LoqNova.Lib;
using LoqNova.Lib.Automation.Steps;
using LoqNova.WPF.Resources;
using Wpf.Ui.Common;

namespace LoqNova.WPF.Controls.Automation.Steps;

public class SpeakerAutomationStepControl : AbstractComboBoxAutomationStepCardControl<SpeakerState>
{
    public SpeakerAutomationStepControl(IAutomationStep<SpeakerState> step) : base(step)
    {
        Icon = SymbolRegular.Speaker224;
        Title = Resource.SpeakerAutomationStepControl_Title;
        Subtitle = Resource.SpeakerAutomationStepControl_Message;
    }
}
