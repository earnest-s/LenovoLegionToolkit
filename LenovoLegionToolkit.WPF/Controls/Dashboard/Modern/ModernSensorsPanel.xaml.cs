using System;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using Humanizer;
using LenovoLegionToolkit.Lib;
using LenovoLegionToolkit.Lib.Controllers.Sensors;
using LenovoLegionToolkit.Lib.Settings;
using LenovoLegionToolkit.Lib.Utils;
using LenovoLegionToolkit.WPF.Resources;
using LenovoLegionToolkit.WPF.Settings;
using Wpf.Ui.Common;
using MenuItem = Wpf.Ui.Controls.MenuItem;

namespace LenovoLegionToolkit.WPF.Controls.Dashboard.Modern;

public partial class ModernSensorsPanel : UserControl
{
    private readonly ISensorsController _controller = IoCContainer.Resolve<ISensorsController>();
    private readonly ApplicationSettings _applicationSettings = IoCContainer.Resolve<ApplicationSettings>();
    private readonly DashboardSettings _dashboardSettings = IoCContainer.Resolve<DashboardSettings>();

    private CancellationTokenSource? _cts;
    private Task? _refreshTask;

    public ModernSensorsPanel()
    {
        InitializeComponent();
        InitializeCards();
        InitializeContextMenu();

        IsVisibleChanged += ModernSensorsPanel_IsVisibleChanged;
    }

    private void InitializeCards()
    {
        _cpuCard.Configure(SymbolRegular.TopSpeed24, Resource.SensorsControl_CPU_Title, "Processor");
        _gpuCard.Configure(SymbolRegular.DeveloperBoard24, Resource.SensorsControl_GPU_Title, "Graphics");
    }

    private void InitializeContextMenu()
    {
        ContextMenu = new ContextMenu();
        ContextMenu.Items.Add(new MenuItem { Header = Resource.SensorsControl_RefreshInterval, IsEnabled = false });

        foreach (var interval in new[] { 1, 2, 3, 5 })
        {
            var item = new MenuItem
            {
                SymbolIcon = _dashboardSettings.Store.SensorsRefreshIntervalSeconds == interval ? SymbolRegular.Checkmark24 : SymbolRegular.Empty,
                Header = TimeSpan.FromSeconds(interval).Humanize(culture: Resource.Culture)
            };
            item.Click += (_, _) =>
            {
                _dashboardSettings.Store.SensorsRefreshIntervalSeconds = interval;
                _dashboardSettings.SynchronizeStore();
                InitializeContextMenu();
            };
            ContextMenu.Items.Add(item);
        }
    }

    private async void ModernSensorsPanel_IsVisibleChanged(object sender, DependencyPropertyChangedEventArgs e)
    {
        if (IsVisible)
        {
            Refresh();
            return;
        }

        if (_cts is not null)
            await _cts.CancelAsync();

        _cts = null;

        if (_refreshTask is not null)
            await _refreshTask;

        _refreshTask = null;

        ResetValues();
    }

    private void Refresh()
    {
        _cts?.Cancel();
        _cts = new CancellationTokenSource();

        var token = _cts.Token;

        _refreshTask = Task.Run(async () =>
        {
            if (Log.Instance.IsTraceEnabled)
                Log.Instance.Trace($"Modern sensors refresh started...");

            if (!await _controller.IsSupportedAsync())
            {
                if (Log.Instance.IsTraceEnabled)
                    Log.Instance.Trace($"Sensors not supported.");

                Dispatcher.Invoke(() => Visibility = Visibility.Collapsed);
                return;
            }

            await _controller.PrepareAsync();

            while (!token.IsCancellationRequested)
            {
                try
                {
                    var data = await _controller.GetDataAsync();
                    Dispatcher.Invoke(() => UpdateValues(data));
                    await Task.Delay(TimeSpan.FromSeconds(_dashboardSettings.Store.SensorsRefreshIntervalSeconds), token);
                }
                catch (OperationCanceledException) { }
                catch (Exception ex)
                {
                    if (Log.Instance.IsTraceEnabled)
                        Log.Instance.Trace($"Modern sensors refresh failed.", ex);

                    Dispatcher.Invoke(ResetValues);
                }
            }

            if (Log.Instance.IsTraceEnabled)
                Log.Instance.Trace($"Modern sensors refresh stopped.");
        }, token);
    }

    private void UpdateValues(SensorsData data)
    {
        // CPU Card
        var cpuTemp = GetTemperatureText(data.CPU.Temperature);
        _cpuCard.UpdatePrimaryValue(cpuTemp);
        _cpuCard.UpdateUtilization(
            data.CPU.MaxUtilization > 0 ? (data.CPU.Utilization / data.CPU.MaxUtilization) * 100 : 0,
            $"{data.CPU.Utilization}%");
        _cpuCard.UpdateClock(
            data.CPU.MaxCoreClock > 0 ? (data.CPU.CoreClock / data.CPU.MaxCoreClock) * 100 : 0,
            $"{data.CPU.CoreClock / 1000.0:0.0} {Resource.GHz}");
        _cpuCard.UpdateTemperature(
            data.CPU.MaxTemperature > 0 ? (data.CPU.Temperature / data.CPU.MaxTemperature) * 100 : 0,
            cpuTemp);
        _cpuCard.UpdateFan(
            data.CPU.MaxFanSpeed > 0 ? (data.CPU.FanSpeed / data.CPU.MaxFanSpeed) * 100 : 0,
            $"{data.CPU.FanSpeed} {Resource.RPM}");

        // GPU Card
        var gpuTemp = GetTemperatureText(data.GPU.Temperature);
        _gpuCard.UpdatePrimaryValue(gpuTemp);
        _gpuCard.UpdateUtilization(
            data.GPU.MaxUtilization > 0 ? (data.GPU.Utilization / data.GPU.MaxUtilization) * 100 : 0,
            $"{data.GPU.Utilization}%");
        _gpuCard.UpdateClock(
            data.GPU.MaxCoreClock > 0 ? (data.GPU.CoreClock / data.GPU.MaxCoreClock) * 100 : 0,
            $"{data.GPU.CoreClock} {Resource.MHz}");
        _gpuCard.UpdateTemperature(
            data.GPU.MaxTemperature > 0 ? (data.GPU.Temperature / data.GPU.MaxTemperature) * 100 : 0,
            gpuTemp);
        _gpuCard.UpdateFan(
            data.GPU.MaxFanSpeed > 0 ? (data.GPU.FanSpeed / data.GPU.MaxFanSpeed) * 100 : 0,
            $"{data.GPU.FanSpeed} {Resource.RPM}");
    }

    private void ResetValues()
    {
        _cpuCard.Reset();
        _gpuCard.Reset();
    }

    private string GetTemperatureText(double temperature)
    {
        if (temperature < 0)
            return "--";

        if (_applicationSettings.Store.TemperatureUnit == TemperatureUnit.F)
        {
            temperature = temperature * 9.0 / 5.0 + 32;
            return $"{temperature:0}°F";
        }

        return $"{temperature:0}°C";
    }
}
