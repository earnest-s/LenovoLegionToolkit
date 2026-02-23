using System;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using LoqNova.Lib;
using LoqNova.Lib.Extensions;
using LoqNova.Lib.Features;
using LoqNova.Lib.Listeners;
using LoqNova.Lib.Messaging;
using LoqNova.Lib.Messaging.Messages;
using LoqNova.Lib.System;
using LoqNova.Lib.Utils;
using LoqNova.WPF.Resources;
using LoqNova.WPF.Utils;
using LoqNova.WPF.Windows.Dashboard;
using Wpf.Ui.Common;
using Button = Wpf.Ui.Controls.Button;

namespace LoqNova.WPF.Controls.Dashboard;

public class PowerModeControl : AbstractComboBoxFeatureCardControl<PowerModeState>
{
    private readonly ThermalModeListener _thermalModeListener = IoCContainer.Resolve<ThermalModeListener>();
    private readonly PowerModeListener _powerModeListener = IoCContainer.Resolve<PowerModeListener>();

    private readonly ThrottleLastDispatcher _throttleDispatcher = new(TimeSpan.FromMilliseconds(500), nameof(PowerModeControl));

    private readonly Button _configButton = new()
    {
        Icon = SymbolRegular.Settings24,
        FontSize = 20,
        Margin = new(8, 0, 0, 0),
        Visibility = Visibility.Collapsed,
    };

    public PowerModeControl()
    {
        Icon = SymbolRegular.Gauge24;
        Title = Resource.PowerModeControl_Title;
        Subtitle = Resource.PowerModeControl_Message;

        AutomationProperties.SetName(_configButton, Resource.PowerModeControl_Title);

        _thermalModeListener.Changed += ThermalModeListener_Changed;
        _powerModeListener.Changed += PowerModeListener_Changed;
    }

    private async void ThermalModeListener_Changed(object? sender, ThermalModeListener.ChangedEventArgs e) => await _throttleDispatcher.DispatchAsync(async () =>
    {
        await Dispatcher.InvokeAsync(async () =>
        {
            if (IsLoaded && IsVisible)
                await RefreshAsync();
        });
    });

    private async void PowerModeListener_Changed(object? sender, PowerModeListener.ChangedEventArgs e) => await _throttleDispatcher.DispatchAsync(async () =>
    {
        await Dispatcher.InvokeAsync(async () =>
        {
            if (IsLoaded && IsVisible)
                await RefreshAsync();
        });
    });

    protected override async Task OnRefreshAsync()
    {
        await base.OnRefreshAsync();

        if (await Power.IsPowerAdapterConnectedAsync() != PowerAdapterStatus.Connected
            && TryGetSelectedItem(out var state)
            && state is PowerModeState.Performance or PowerModeState.GodMode)
            Warning = Resource.PowerModeControl_Warning;
        else
            Warning = string.Empty;
    }

    protected override async Task OnStateChangeAsync(ComboBox comboBox, IFeature<PowerModeState> feature, PowerModeState? newValue, PowerModeState? oldValue)
    {
        // Visual feedback for sensor bar color immediately
        if (newValue is { } mode && newValue != oldValue)
            MessagingCenter.Publish(new PowerModeVisualMessage(mode));

        // base calls PowerModeFeature.SetStateAsync → ApplyPerformanceModeAsync
        // which handles dependencies, RGB strobe, and OSD notification centrally.
        await base.OnStateChangeAsync(comboBox, feature, newValue, oldValue);

        var mi = await Compatibility.GetMachineInformationAsync();

        switch (newValue)
        {
            case PowerModeState.Balance when mi.Properties.SupportsAIMode:
            case PowerModeState.GodMode when mi.Properties.SupportsGodMode:
                _configButton.ToolTip = Resource.PowerModeControl_Settings;
                _configButton.Visibility = Visibility.Visible;
                break;
            default:
                _configButton.ToolTip = null;
                _configButton.Visibility = Visibility.Collapsed;
                break;
        }
    }

    protected override void OnStateChangeException(Exception exception)
    {
        if (exception is PowerModeUnavailableWithoutACException ex1)
        {
            SnackbarHelper.Show(Resource.PowerModeUnavailableWithoutACException_Title,
                string.Format(Resource.PowerModeUnavailableWithoutACException_Message, ex1.PowerMode.GetDisplayName()),
                SnackbarType.Warning);
        }
    }

    protected override FrameworkElement GetAccessory(ComboBox comboBox)
    {
        _configButton.Click += ConfigButton_Click;

        var stackPanel = new StackPanel
        {
            Orientation = Orientation.Horizontal,
        };
        stackPanel.Children.Add(_configButton);
        stackPanel.Children.Add(comboBox);

        return stackPanel;
    }

    private void ConfigButton_Click(object sender, RoutedEventArgs e)
    {
        if (!TryGetSelectedItem(out var state))
            return;

        switch (state)
        {
            case PowerModeState.Balance:
                {
                    var window = new BalanceModeSettingsWindow { Owner = Window.GetWindow(this) };
                    window.ShowDialog();
                    break;
                }
            case PowerModeState.GodMode:
                {
                    var window = new GodModeSettingsWindow { Owner = Window.GetWindow(this) };
                    window.ShowDialog();
                    break;
                }
        }
    }
}
