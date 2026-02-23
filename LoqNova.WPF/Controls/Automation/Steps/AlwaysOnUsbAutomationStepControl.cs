using LoqNova.Lib;
using LoqNova.Lib.Automation.Steps;
using LoqNova.WPF.Resources;
using Wpf.Ui.Common;

namespace LoqNova.WPF.Controls.Automation.Steps;

public class AlwaysOnUsbAutomationStepControl : AbstractComboBoxAutomationStepCardControl<AlwaysOnUSBState>
{
    public AlwaysOnUsbAutomationStepControl(IAutomationStep<AlwaysOnUSBState> step) : base(step)
    {
        Icon = SymbolRegular.UsbStick24;
        Title = Resource.AlwaysOnUsbAutomationStepControl_Title;
        Subtitle = Resource.AlwaysOnUsbAutomationStepControl_Message;
    }
}
