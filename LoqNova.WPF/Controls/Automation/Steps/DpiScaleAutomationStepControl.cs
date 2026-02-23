using System;
using LoqNova.Lib;
using LoqNova.Lib.Automation.Steps;
using LoqNova.Lib.Listeners;
using LoqNova.WPF.Resources;
using LoqNova.WPF.Utils;
using Wpf.Ui.Common;

namespace LoqNova.WPF.Controls.Automation.Steps;

public class DpiScaleAutomationStepControl : AbstractComboBoxAutomationStepCardControl<DpiScale>
{
    private readonly DisplayConfigurationListener _listener = IoCContainer.Resolve<DisplayConfigurationListener>();

    public DpiScaleAutomationStepControl(IAutomationStep<DpiScale> step) : base(step)
    {
        Icon = SymbolRegular.TextFontSize24;
        Title = Resource.DpiScaleAutomationStepControl_Title;
        Subtitle = Resource.DpiScaleAutomationStepControl_Message;

        _listener.Changed += Listener_Changed;
    }

    protected override string ComboBoxItemDisplayName(DpiScale value)
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
