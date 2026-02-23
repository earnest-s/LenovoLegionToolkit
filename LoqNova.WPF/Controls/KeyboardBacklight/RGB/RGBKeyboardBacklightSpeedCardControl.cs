using LoqNova.Lib;
using LoqNova.WPF.Resources;
using Wpf.Ui.Common;

namespace LoqNova.WPF.Controls.KeyboardBacklight.RGB;

public class RGBKeyboardBacklightSpeedCardControl : AbstractComboBoxRGBKeyboardCardControl<RGBKeyboardBacklightSpeed>
{
    public RGBKeyboardBacklightSpeedCardControl()
    {
        Icon = SymbolRegular.Keyboard24;
        Title = Resource.RGBKeyboardBacklightSpeedCardControl_Title;
    }
}
