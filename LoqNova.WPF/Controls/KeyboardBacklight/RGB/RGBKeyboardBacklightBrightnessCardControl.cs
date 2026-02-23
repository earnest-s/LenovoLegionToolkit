using LoqNova.Lib;
using LoqNova.WPF.Resources;
using Wpf.Ui.Common;

namespace LoqNova.WPF.Controls.KeyboardBacklight.RGB;

public class RGBKeyboardBacklightBrightnessCardControl : AbstractComboBoxRGBKeyboardCardControl<RGBKeyboardBacklightBrightness>
{
    public RGBKeyboardBacklightBrightnessCardControl()
    {
        Icon = SymbolRegular.Keyboard24;
        Title = Resource.RGBKeyboardBacklightBrightnessCardControl_Brightness;
    }
}
