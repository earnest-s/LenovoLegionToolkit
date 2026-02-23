using LoqNova.Lib;
using LoqNova.Lib.Listeners;
using LoqNova.WPF.Resources;
using Wpf.Ui.Common;

namespace LoqNova.WPF.Controls.Dashboard;

public class PanelLogoBacklightControl : AbstractToggleFeatureCardControl<PanelLogoBacklightState>
{
    private readonly LightingChangeListener _listener = IoCContainer.Resolve<LightingChangeListener>();

    protected override PanelLogoBacklightState OnState => PanelLogoBacklightState.On;

    protected override PanelLogoBacklightState OffState => PanelLogoBacklightState.Off;

    public PanelLogoBacklightControl()
    {
        Icon = SymbolRegular.LightbulbCircle24;
        Title = Resource.PanelLogoBacklightControl_Title;
        Subtitle = Resource.PanelLogoBacklightControl_Message;

        _listener.Changed += Listener_Changed;
    }

    private void Listener_Changed(object? sender, LightingChangeListener.ChangedEventArgs e) => Dispatcher.Invoke(async () =>
    {
        if (e.State != LightingChangeState.Panel)
            return;

        await RefreshAsync();
    });
}
