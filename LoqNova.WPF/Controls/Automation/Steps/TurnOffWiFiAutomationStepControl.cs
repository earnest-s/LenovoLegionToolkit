using System.Threading.Tasks;
using System.Windows;
using LoqNova.Lib.Automation.Steps;
using LoqNova.WPF.Resources;
using Wpf.Ui.Common;

namespace LoqNova.WPF.Controls.Automation.Steps;

public class TurnOffWiFiAutomationStepControl : AbstractAutomationStepControl
{
    public TurnOffWiFiAutomationStepControl(TurnOffWiFiAutomationStep automationStep) : base(automationStep)
    {
        Icon = SymbolRegular.WifiOff24;
        Title = Resource.TurnOffWiFiAutomationStepControl_Title;
    }

    public override IAutomationStep CreateAutomationStep() => new TurnOffWiFiAutomationStep();

    protected override UIElement? GetCustomControl() => null;

    protected override void OnFinishedLoading() { }

    protected override Task RefreshAsync() => Task.CompletedTask;
}
