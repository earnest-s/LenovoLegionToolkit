using System;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using LenovoLegionToolkit.Lib;
using LenovoLegionToolkit.Lib.Extensions;
using LenovoLegionToolkit.Lib.Features;
using LenovoLegionToolkit.Lib.Messaging;
using LenovoLegionToolkit.Lib.Messaging.Messages;
using LenovoLegionToolkit.Lib.System;
using LenovoLegionToolkit.Lib.Utils;
using LenovoLegionToolkit.WPF.Extensions;
using LenovoLegionToolkit.WPF.Resources;

namespace LenovoLegionToolkit.WPF.Controls.Dashboard.Modern;

public partial class QuickSettingsCard : UserControl
{
    private readonly IFeature<BatteryState> _batteryFeature = IoCContainer.Resolve<IFeature<BatteryState>>();
    private readonly IFeature<AlwaysOnUSBState> _alwaysOnUsbFeature = IoCContainer.Resolve<IFeature<AlwaysOnUSBState>>();
    private readonly IFeature<HybridModeState> _hybridModeFeature = IoCContainer.Resolve<IFeature<HybridModeState>>();

    private bool _isRefreshing;

    public QuickSettingsCard()
    {
        InitializeComponent();

        AutomationProperties.SetName(_batteryModeComboBox, Resource.BatteryModeControl_Title);
        AutomationProperties.SetName(_alwaysOnUsbComboBox, Resource.AlwaysOnUSBControl_Title);
        AutomationProperties.SetName(_hybridModeComboBox, Resource.ComboBoxHybridModeControl_Title);

        _batteryModeComboBox.SelectionChanged += BatteryModeComboBox_SelectionChanged;
        _alwaysOnUsbComboBox.SelectionChanged += AlwaysOnUsbComboBox_SelectionChanged;
        _hybridModeComboBox.SelectionChanged += HybridModeComboBox_SelectionChanged;

        Loaded += QuickSettingsCard_Loaded;
        IsVisibleChanged += QuickSettingsCard_IsVisibleChanged;
    }

    private async void QuickSettingsCard_Loaded(object sender, RoutedEventArgs e)
    {
        SubscribeToMessages();
        await RefreshAllAsync();
        await UpdatePowerStatusAsync();
    }

    private async void QuickSettingsCard_IsVisibleChanged(object sender, DependencyPropertyChangedEventArgs e)
    {
        if (IsVisible && IsLoaded)
        {
            await RefreshAllAsync();
            await UpdatePowerStatusAsync();
        }
    }

    private void SubscribeToMessages()
    {
        MessagingCenter.Subscribe<FeatureStateMessage<BatteryState>>(this, () => Dispatcher.InvokeTask(async () =>
        {
            if (IsVisible) await RefreshBatteryModeAsync();
        }));

        MessagingCenter.Subscribe<FeatureStateMessage<AlwaysOnUSBState>>(this, () => Dispatcher.InvokeTask(async () =>
        {
            if (IsVisible) await RefreshAlwaysOnUsbAsync();
        }));

        MessagingCenter.Subscribe<FeatureStateMessage<HybridModeState>>(this, () => Dispatcher.InvokeTask(async () =>
        {
            if (IsVisible) await RefreshHybridModeAsync();
        }));
    }

    private async Task RefreshAllAsync()
    {
        await RefreshBatteryModeAsync();
        await RefreshAlwaysOnUsbAsync();
        await RefreshHybridModeAsync();
    }

    private async Task RefreshBatteryModeAsync()
    {
        _isRefreshing = true;
        try
        {
            if (!await _batteryFeature.IsSupportedAsync())
            {
                _batteryModeComboBox.Visibility = Visibility.Collapsed;
                return;
            }

            var items = await _batteryFeature.GetAllStatesAsync();
            var selected = await _batteryFeature.GetStateAsync();
            _batteryModeComboBox.SetItems(items, selected, s => s.GetDisplayName());
            _batteryModeComboBox.IsEnabled = items.Length > 0;
            _batteryModeComboBox.Visibility = Visibility.Visible;
        }
        catch
        {
            _batteryModeComboBox.Visibility = Visibility.Collapsed;
        }
        finally
        {
            _isRefreshing = false;
        }
    }

    private async Task RefreshAlwaysOnUsbAsync()
    {
        _isRefreshing = true;
        try
        {
            if (!await _alwaysOnUsbFeature.IsSupportedAsync())
            {
                _alwaysOnUsbComboBox.Visibility = Visibility.Collapsed;
                return;
            }

            var items = await _alwaysOnUsbFeature.GetAllStatesAsync();
            var selected = await _alwaysOnUsbFeature.GetStateAsync();
            _alwaysOnUsbComboBox.SetItems(items, selected, s => s.GetDisplayName());
            _alwaysOnUsbComboBox.IsEnabled = items.Length > 0;
            _alwaysOnUsbComboBox.Visibility = Visibility.Visible;
        }
        catch
        {
            _alwaysOnUsbComboBox.Visibility = Visibility.Collapsed;
        }
        finally
        {
            _isRefreshing = false;
        }
    }

    private async Task RefreshHybridModeAsync()
    {
        _isRefreshing = true;
        try
        {
            if (!await _hybridModeFeature.IsSupportedAsync())
            {
                _hybridModeComboBox.Visibility = Visibility.Collapsed;
                return;
            }

            var items = await _hybridModeFeature.GetAllStatesAsync();
            var selected = await _hybridModeFeature.GetStateAsync();
            _hybridModeComboBox.SetItems(items, selected, s => s.GetDisplayName());
            _hybridModeComboBox.IsEnabled = items.Length > 0;
            _hybridModeComboBox.Visibility = Visibility.Visible;

            UpdateDiscreteGpuStatus(selected);
        }
        catch
        {
            _hybridModeComboBox.Visibility = Visibility.Collapsed;
        }
        finally
        {
            _isRefreshing = false;
        }
    }

    private void UpdateDiscreteGpuStatus(HybridModeState state)
    {
        _discreteGpuStatus.Text = state switch
        {
            HybridModeState.On => "Hybrid",
            HybridModeState.OnIGPUOnly => "iGPU Only",
            HybridModeState.Off => "Discrete",
            _ => "-"
        };
    }

    private async Task UpdatePowerStatusAsync()
    {
        try
        {
            var powerStatus = await Power.IsPowerAdapterConnectedAsync();
            _powerStatus.Text = powerStatus switch
            {
                PowerAdapterStatus.Connected => "AC Connected",
                PowerAdapterStatus.ConnectedLowWattage => "Low Wattage AC",
                PowerAdapterStatus.Disconnected => "On Battery",
                _ => "-"
            };
        }
        catch
        {
            _powerStatus.Text = "-";
        }
    }

    private async void BatteryModeComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_isRefreshing) return;
        await SetFeatureStateAsync(_batteryModeComboBox, _batteryFeature, e);
    }

    private async void AlwaysOnUsbComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_isRefreshing) return;
        await SetFeatureStateAsync(_alwaysOnUsbComboBox, _alwaysOnUsbFeature, e);
    }

    private async void HybridModeComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_isRefreshing) return;
        await SetFeatureStateAsync(_hybridModeComboBox, _hybridModeFeature, e);

        if (_hybridModeComboBox.TryGetSelectedItem(out HybridModeState state))
            UpdateDiscreteGpuStatus(state);
    }

    private static async Task SetFeatureStateAsync<T>(ComboBox comboBox, IFeature<T> feature, SelectionChangedEventArgs e) where T : struct
    {
        var newValue = e.GetNewValue<T>();
        if (newValue is null) return;

        comboBox.IsEnabled = false;

        try
        {
            await feature.SetStateAsync(newValue.Value);
        }
        catch (Exception ex)
        {
            if (Log.Instance.IsTraceEnabled)
                Log.Instance.Trace($"Failed to set feature state.", ex);
        }
        finally
        {
            comboBox.IsEnabled = true;
        }
    }
}
