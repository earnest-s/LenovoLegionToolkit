using LoqNova.Lib;
using LoqNova.WPF.Resources;
using Wpf.Ui.Common;

namespace LoqNova.WPF.Controls.Dashboard;

public class BatteryModeControl : AbstractComboBoxFeatureCardControl<BatteryState>
{
    public BatteryModeControl()
    {
        Icon = SymbolRegular.BatteryCharge24;
        Title = Resource.BatteryModeControl_Title;
        Subtitle = Resource.BatteryModeControl_Message;
    }
}
