using LoqNova.Lib;
using LoqNova.WPF.Resources;
using Wpf.Ui.Common;

namespace LoqNova.WPF.Controls.Dashboard;

public class AlwaysOnUSBControl : AbstractComboBoxFeatureCardControl<AlwaysOnUSBState>
{
    public AlwaysOnUSBControl()
    {
        Icon = SymbolRegular.UsbStick24;
        Title = Resource.AlwaysOnUSBControl_Title;
        Subtitle = Resource.AlwaysOnUSBControl_Message;
    }
}
