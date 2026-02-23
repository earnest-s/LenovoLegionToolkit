using LoqNova.Lib;
using LoqNova.Lib.Listeners;
using LoqNova.WPF.Resources;
using Wpf.Ui.Common;

namespace LoqNova.WPF.Controls.Dashboard;

public class MicrophoneControl : AbstractToggleFeatureCardControl<MicrophoneState>
{
    private readonly DriverKeyListener _listener = IoCContainer.Resolve<DriverKeyListener>();

    protected override MicrophoneState OnState => MicrophoneState.On;
    protected override MicrophoneState OffState => MicrophoneState.Off;

    public MicrophoneControl()
    {
        Icon = SymbolRegular.Mic24;
        Title = Resource.MicrophoneControl_Title;
        Subtitle = Resource.MicrophoneControl_Message;

        _listener.Changed += Listener_Changed;
    }

    private void Listener_Changed(object? sender, DriverKeyListener.ChangedEventArgs e) => Dispatcher.Invoke(async () =>
    {
        if (!IsLoaded || !IsVisible)
            return;

        if (e.DriverKey.HasFlag(DriverKey.FnF4))
            await RefreshAsync();
    });
}
