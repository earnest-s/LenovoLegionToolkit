using System;
using System.Threading.Tasks;
using System.Windows;
using LoqNova.Lib;
using LoqNova.Lib.Controllers;
using LoqNova.WPF.Controls.KeyboardBacklight.RGB;
using LoqNova.WPF.Controls.KeyboardBacklight.Spectrum;

namespace LoqNova.WPF.Pages;

public partial class KeyboardBacklightPage
{
    public KeyboardBacklightPage() => InitializeComponent();

    private async void KeyboardBacklightPage_Initialized(object? sender, EventArgs e)
    {
        _titleTextBlock.Visibility = Visibility.Collapsed;

        await Task.Delay(TimeSpan.FromSeconds(1));

        _titleTextBlock.Visibility = Visibility.Visible;

        var spectrumController = IoCContainer.Resolve<SpectrumKeyboardBacklightController>();
        if (await spectrumController.IsSupportedAsync())
        {
            var control = new SpectrumKeyboardBacklightControl();
            _content.Children.Add(control);
            _loader.IsLoading = false;
            return;
        }

        var rgbController = IoCContainer.Resolve<RGBKeyboardBacklightController>();
        if (await rgbController.IsSupportedAsync())
        {
            var control = new RGBKeyboardBacklightControl();
            _content.Children.Add(control);
            _loader.IsLoading = false;
            return;
        }

        _noKeyboardsText.Visibility = Visibility.Visible;
        _loader.IsLoading = false;
    }
    public static async Task<bool> IsSupportedAsync()
    {
        var spectrumController = IoCContainer.Resolve<SpectrumKeyboardBacklightController>();
        if (await spectrumController.IsSupportedAsync())
            return true;

        var rgbController = IoCContainer.Resolve<RGBKeyboardBacklightController>();
        if (await rgbController.IsSupportedAsync())
            return true;

        return false;
    }
}
