using System;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using LenovoLegionToolkit.Lib;
using LenovoLegionToolkit.Lib.Controllers;
using LenovoLegionToolkit.Lib.Listeners;
using LenovoLegionToolkit.Lib.Utils;
using LenovoLegionToolkit.WPF.Resources;
using LenovoLegionToolkit.WPF.Windows.Dashboard;

namespace LenovoLegionToolkit.WPF.Controls.Dashboard.Modern;

public partial class GPUOverclockCard : UserControl
{
    private readonly GPUOverclockController _controller = IoCContainer.Resolve<GPUOverclockController>();
    private readonly NativeWindowsMessageListener _nativeWindowsMessageListener = IoCContainer.Resolve<NativeWindowsMessageListener>();

    public GPUOverclockCard()
    {
        InitializeComponent();

        AutomationProperties.SetName(_toggle, Resource.OverclockDiscreteGPUControl_Title);
        AutomationProperties.SetName(_configButton, Resource.OverclockDiscreteGPUControl_Title);

        _toggle.Click += Toggle_Click;
        _configButton.Click += ConfigButton_Click;

        _nativeWindowsMessageListener.Changed += NativeWindowsMessageListener_Changed;
        _controller.Changed += Controller_Changed;

        Loaded += GPUOverclockCard_Loaded;
        IsVisibleChanged += GPUOverclockCard_IsVisibleChanged;
    }

    private async void GPUOverclockCard_Loaded(object sender, RoutedEventArgs e)
    {
        await RefreshAsync();
    }

    private async void GPUOverclockCard_IsVisibleChanged(object sender, DependencyPropertyChangedEventArgs e)
    {
        if (IsVisible && IsLoaded)
            await RefreshAsync();
    }

    private async Task RefreshAsync()
    {
        try
        {
            if (!await _controller.IsSupportedAsync())
            {
                Visibility = Visibility.Collapsed;
                return;
            }

            var (enabled, _) = _controller.GetState();
            _toggle.IsChecked = enabled;
            _toggle.Visibility = Visibility.Visible;
            Visibility = Visibility.Visible;
        }
        catch
        {
            Visibility = Visibility.Collapsed;
        }
    }

    private async void NativeWindowsMessageListener_Changed(object? sender, NativeWindowsMessageListener.ChangedEventArgs e)
    {
        if (e.Message != NativeWindowsMessage.OnDisplayDeviceArrival)
            return;

        await Dispatcher.InvokeAsync(async () =>
        {
            Visibility = Visibility.Visible;
            await RefreshAsync();
        });
    }

    private void Controller_Changed(object? sender, EventArgs e)
    {
        Dispatcher.InvokeAsync(async () =>
        {
            Visibility = Visibility.Visible;
            await RefreshAsync();
        });
    }

    private async void Toggle_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            if (_toggle.IsChecked is null)
                return;

            var enabled = _toggle.IsChecked.Value;
            var (_, info) = _controller.GetState();
            _controller.SaveState(enabled, info);
            await _controller.ApplyStateAsync(true);
        }
        catch (Exception ex)
        {
            if (Log.Instance.IsTraceEnabled)
                Log.Instance.Trace($"Failed to change overclock state.", ex);
        }
    }

    private void ConfigButton_Click(object sender, RoutedEventArgs e)
    {
        var window = new OverclockDiscreteGPUSettingsWindow { Owner = Window.GetWindow(this) };
        window.Closed += async (_, _) => await RefreshAsync();
        window.ShowDialog();
    }
}
