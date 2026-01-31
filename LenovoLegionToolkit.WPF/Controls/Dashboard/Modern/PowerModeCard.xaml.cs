using System;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using LenovoLegionToolkit.Lib;
using LenovoLegionToolkit.Lib.Extensions;
using LenovoLegionToolkit.Lib.Features;
using LenovoLegionToolkit.Lib.Listeners;
using LenovoLegionToolkit.Lib.Messaging;
using LenovoLegionToolkit.Lib.Messaging.Messages;
using LenovoLegionToolkit.Lib.System;
using LenovoLegionToolkit.Lib.Utils;
using LenovoLegionToolkit.WPF.Extensions;
using LenovoLegionToolkit.WPF.Resources;
using LenovoLegionToolkit.WPF.Utils;
using LenovoLegionToolkit.WPF.Windows.Dashboard;
using Wpf.Ui.Common;

namespace LenovoLegionToolkit.WPF.Controls.Dashboard.Modern;

public partial class PowerModeCard : UserControl
{
    private readonly IFeature<PowerModeState> _feature = IoCContainer.Resolve<IFeature<PowerModeState>>();
    private readonly ThermalModeListener _thermalModeListener = IoCContainer.Resolve<ThermalModeListener>();
    private readonly PowerModeListener _powerModeListener = IoCContainer.Resolve<PowerModeListener>();
    private readonly ThrottleLastDispatcher _throttleDispatcher = new(TimeSpan.FromMilliseconds(500), nameof(PowerModeCard));

    private bool _isRefreshing;

    public PowerModeCard()
    {
        InitializeComponent();

        AutomationProperties.SetName(_comboBox, Resource.PowerModeControl_Title);
        AutomationProperties.SetName(_configButton, Resource.PowerModeControl_Settings);

        _comboBox.SelectionChanged += ComboBox_SelectionChanged;
        _configButton.Click += ConfigButton_Click;

        _thermalModeListener.Changed += ThermalModeListener_Changed;
        _powerModeListener.Changed += PowerModeListener_Changed;

        Loaded += PowerModeCard_Loaded;
        IsVisibleChanged += PowerModeCard_IsVisibleChanged;
    }

    private async void PowerModeCard_Loaded(object sender, RoutedEventArgs e)
    {
        try
        {
            if (!await _feature.IsSupportedAsync())
            {
                Visibility = Visibility.Collapsed;
                return;
            }

            MessagingCenter.Subscribe<FeatureStateMessage<PowerModeState>>(this, () => Dispatcher.InvokeTask(async () =>
            {
                if (!IsVisible)
                    return;

                await RefreshAsync();
            }));

            await RefreshAsync();
        }
        catch
        {
            Visibility = Visibility.Collapsed;
        }
    }

    private async void PowerModeCard_IsVisibleChanged(object sender, DependencyPropertyChangedEventArgs e)
    {
        if (IsVisible && IsLoaded)
            await RefreshAsync();
    }

    private async void ThermalModeListener_Changed(object? sender, ThermalModeListener.ChangedEventArgs e)
    {
        await _throttleDispatcher.DispatchAsync(async () =>
        {
            await Dispatcher.InvokeAsync(async () =>
            {
                if (IsLoaded && IsVisible)
                    await RefreshAsync();
            });
        });
    }

    private async void PowerModeListener_Changed(object? sender, PowerModeListener.ChangedEventArgs e)
    {
        await _throttleDispatcher.DispatchAsync(async () =>
        {
            await Dispatcher.InvokeAsync(async () =>
            {
                if (IsLoaded && IsVisible)
                    await RefreshAsync();
            });
        });
    }

    private async Task RefreshAsync()
    {
        _isRefreshing = true;

        try
        {
            var items = await _feature.GetAllStatesAsync();
            var selectedItem = await _feature.GetStateAsync();

            _comboBox.SetItems(items, selectedItem, ComboBoxItemDisplayName);
            _comboBox.IsEnabled = items.Length != 0;
            _comboBox.Visibility = Visibility.Visible;

            await UpdateWarningAsync();
            await UpdateConfigButtonAsync(selectedItem);
        }
        catch
        {
            Visibility = Visibility.Collapsed;
        }
        finally
        {
            _isRefreshing = false;
        }
    }

    private async Task UpdateWarningAsync()
    {
        if (await Power.IsPowerAdapterConnectedAsync() != PowerAdapterStatus.Connected
            && _comboBox.TryGetSelectedItem(out PowerModeState state)
            && state is PowerModeState.Performance or PowerModeState.GodMode)
        {
            _warning.Text = Resource.PowerModeControl_Warning;
            _warning.Visibility = Visibility.Visible;
        }
        else
        {
            _warning.Text = string.Empty;
            _warning.Visibility = Visibility.Collapsed;
        }
    }

    private async Task UpdateConfigButtonAsync(PowerModeState state)
    {
        var mi = await Compatibility.GetMachineInformationAsync();

        switch (state)
        {
            case PowerModeState.Balance when mi.Properties.SupportsAIMode:
            case PowerModeState.GodMode when mi.Properties.SupportsGodMode:
                _configButton.Visibility = Visibility.Visible;
                break;
            default:
                _configButton.Visibility = Visibility.Collapsed;
                break;
        }
    }

    private async void ComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_isRefreshing)
            return;

        var newValue = e.GetNewValue<PowerModeState>();
        var oldValue = e.GetOldValue<PowerModeState>();

        if (newValue is null || oldValue is null)
            return;

        _comboBox.IsEnabled = false;

        try
        {
            if (!_comboBox.TryGetSelectedItem(out PowerModeState selectedState))
                return;

            await _feature.SetStateAsync(selectedState);
            await UpdateWarningAsync();
            await UpdateConfigButtonAsync(selectedState);
        }
        catch (PowerModeUnavailableWithoutACException ex)
        {
            SnackbarHelper.Show(Resource.PowerModeUnavailableWithoutACException_Title,
                string.Format(Resource.PowerModeUnavailableWithoutACException_Message, ex.PowerMode.GetDisplayName()),
                SnackbarType.Warning);

            await RefreshAsync();
        }
        catch (Exception ex)
        {
            if (Log.Instance.IsTraceEnabled)
                Log.Instance.Trace($"Failed to set power mode.", ex);

            await RefreshAsync();
        }
        finally
        {
            _comboBox.IsEnabled = true;
        }
    }

    private void ConfigButton_Click(object sender, RoutedEventArgs e)
    {
        if (!_comboBox.TryGetSelectedItem(out PowerModeState state))
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

    private static string ComboBoxItemDisplayName(PowerModeState value) => value.GetDisplayName();
}
