using LoqNova.Lib;
using LoqNova.Lib.Automation.Steps;
using LoqNova.WPF.Resources;
using Wpf.Ui.Common;

namespace LoqNova.WPF.Controls.Automation.Steps;

internal class InstantBootAutomationStepControl : AbstractComboBoxAutomationStepCardControl<InstantBootState>
{
    public InstantBootAutomationStepControl(IAutomationStep<InstantBootState> step) : base(step)
    {
        Icon = SymbolRegular.PlugDisconnected24;
        Title = Resource.InstantBootAutomationStepControl_Title;
        Subtitle = Resource.InstantBootAutomationStepControl_Message;
    }
}
