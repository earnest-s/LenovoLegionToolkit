using System;
using LoqNova.Lib;
using LoqNova.Lib.Automation.Steps;
using LoqNova.Lib.Listeners;
using LoqNova.WPF.Resources;
using Wpf.Ui.Common;

namespace LoqNova.WPF.Controls.Automation.Steps;

public class ResolutionAutomationStepControl : AbstractComboBoxAutomationStepCardControl<Resolution>
{
    private readonly DisplayConfigurationListener _listener = IoCContainer.Resolve<DisplayConfigurationListener>();

    public ResolutionAutomationStepControl(IAutomationStep<Resolution> step) : base(step)
    {
        Icon = SymbolRegular.ScaleFill24;
        Title = Resource.ResolutionAutomationStepControl_Title;
        Subtitle = Resource.ResolutionAutomationStepControl_Message;

        _listener.Changed += Listener_Changed;
    }

    private void Listener_Changed(object? sender, EventArgs e) => Dispatcher.Invoke(async () =>
    {
        if (IsLoaded)
            await RefreshAsync();
    });
}
