using LoqNova.Lib.Settings;

namespace LoqNova.WPF.Settings;

public class DashboardSettings() : AbstractSettings<DashboardSettings.DashboardSettingsStore>("dashboard.json")
{
    public class DashboardSettingsStore
    {
        public bool ShowSensors { get; set; } = true;
        public int SensorsRefreshIntervalSeconds { get; set; } = 1;
        public DashboardGroup[]? Groups { get; set; }
    }

    protected override DashboardSettingsStore Default => new();
}
