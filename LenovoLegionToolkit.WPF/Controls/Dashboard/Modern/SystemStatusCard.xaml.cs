using System.Windows.Controls;
using Wpf.Ui.Common;

namespace LenovoLegionToolkit.WPF.Controls.Dashboard.Modern;

public partial class SystemStatusCard : UserControl
{
    public SystemStatusCard()
    {
        InitializeComponent();
    }

    public void Configure(SymbolRegular icon, string title, string subtitle)
    {
        _icon.Symbol = icon;
        _title.Text = title;
        _subtitle.Text = subtitle;
    }

    public void UpdatePrimaryValue(string value)
    {
        _primaryValue.Text = value;
    }

    public void UpdateUtilization(double percentage, string displayValue)
    {
        _utilizationBar.Value = percentage;
        _utilizationValue.Text = displayValue;
    }

    public void UpdateClock(double percentage, string displayValue)
    {
        _clockBar.Value = percentage;
        _clockValue.Text = displayValue;
    }

    public void UpdateTemperature(double percentage, string displayValue)
    {
        _temperatureBar.Value = percentage;
        _temperatureValue.Text = displayValue;
    }

    public void UpdateFan(double percentage, string displayValue)
    {
        _fanBar.Value = percentage;
        _fanValue.Text = displayValue;
    }

    public void Reset()
    {
        _primaryValue.Text = "--";
        _utilizationBar.Value = 0;
        _utilizationValue.Text = "-";
        _clockBar.Value = 0;
        _clockValue.Text = "-";
        _temperatureBar.Value = 0;
        _temperatureValue.Text = "-";
        _fanBar.Value = 0;
        _fanValue.Text = "-";
    }
}
