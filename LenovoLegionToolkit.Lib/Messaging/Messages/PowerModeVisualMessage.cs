namespace LenovoLegionToolkit.Lib.Messaging.Messages;

/// <summary>
/// Published by the UI immediately when a power mode is selected,
/// BEFORE the hardware write completes.  Subscribers should use
/// this to trigger instant visual feedback (OSD, accent, sweep).
/// </summary>
public readonly struct PowerModeVisualMessage(PowerModeState state) : IMessage
{
    public PowerModeState State { get; } = state;
}
