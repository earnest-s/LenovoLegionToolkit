using LoqNova.Lib;
using LoqNova.Lib.Automation.Steps;
using LoqNova.WPF.Resources;
using Wpf.Ui.Common;

namespace LoqNova.WPF.Controls.Automation.Steps;

public class PortsBacklightAutomationStepControl : AbstractComboBoxAutomationStepCardControl<PortsBacklightState>
{
    public PortsBacklightAutomationStepControl(IAutomationStep<PortsBacklightState> step) : base(step)
    {
        Icon = SymbolRegular.UsbPlug24;
        Title = Resource.PortsBacklightAutomationStepControl_Title;
        Subtitle = Resource.PortsBacklightAutomationStepControl_Message;
    }
}
