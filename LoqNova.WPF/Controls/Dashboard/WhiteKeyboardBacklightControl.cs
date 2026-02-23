using LoqNova.Lib;
using LoqNova.Lib.Listeners;
using LoqNova.WPF.Resources;
using Wpf.Ui.Common;

namespace LoqNova.WPF.Controls.Dashboard;

public class WhiteKeyboardBacklightControl : AbstractComboBoxFeatureCardControl<WhiteKeyboardBacklightState>
{
    private readonly DriverKeyListener _listener = IoCContainer.Resolve<DriverKeyListener>();

    public WhiteKeyboardBacklightControl()
    {
        Icon = SymbolRegular.Keyboard24;
        Title = Resource.WhiteKeyboardBacklightControl_Title;
        Subtitle = Resource.WhiteKeyboardBacklightControl_Message;

        _listener.Changed += ListenerChanged;
    }

    private void ListenerChanged(object? sender, DriverKeyListener.ChangedEventArgs e) => Dispatcher.Invoke(async () =>
    {
        if (!IsLoaded || !IsVisible)
            return;

        if (e.DriverKey.HasFlag(DriverKey.FnSpace))
            await RefreshAsync();
    });
}
