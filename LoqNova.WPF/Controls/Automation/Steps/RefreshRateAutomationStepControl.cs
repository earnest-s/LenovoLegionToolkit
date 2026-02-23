using System;
using LoqNova.Lib;
using LoqNova.Lib.Automation.Steps;
using LoqNova.Lib.Listeners;
using LoqNova.WPF.Resources;
using LoqNova.WPF.Utils;
using Wpf.Ui.Common;

namespace LoqNova.WPF.Controls.Automation.Steps;

public class RefreshRateAutomationStepControl : AbstractComboBoxAutomationStepCardControl<RefreshRate>
{
    private readonly DisplayConfigurationListener _listener = IoCContainer.Resolve<DisplayConfigurationListener>();

    public RefreshRateAutomationStepControl(IAutomationStep<RefreshRate> step) : base(step)
    {
        Icon = SymbolRegular.DesktopPulse24;
        Title = Resource.RefreshRateAutomationStepControl_Title;
        Subtitle = Resource.RefreshRateAutomationStepControl_Message;

        _listener.Changed += Listener_Changed;
    }

    protected override string ComboBoxItemDisplayName(RefreshRate value)
    {
        var str = base.ComboBoxItemDisplayName(value);
        return LocalizationHelper.ForceLeftToRight(str);
    }

    private void Listener_Changed(object? sender, EventArgs e) => Dispatcher.Invoke(async () =>
    {
        if (IsLoaded)
            await RefreshAsync();
    });
}
