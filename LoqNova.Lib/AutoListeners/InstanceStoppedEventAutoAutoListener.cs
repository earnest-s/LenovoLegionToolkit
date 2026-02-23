using System;
using System.Threading.Tasks;
using LoqNova.Lib.System.Management;

namespace LoqNova.Lib.AutoListeners;

public class InstanceStoppedEventAutoAutoListener : AbstractAutoListener<InstanceStoppedEventAutoAutoListener.ChangedEventArgs>
{
    public class ChangedEventArgs(int processId, string processName) : EventArgs
    {
        public int ProcessId { get; } = processId;
        public string ProcessName { get; } = processName;
    }

    private IDisposable? _disposable;

    protected override Task StartAsync()
    {
        _disposable = WMI.Win32.ProcessStopTrace.Listen(Handle);
        return Task.CompletedTask;
    }


    protected override Task StopAsync()
    {
        _disposable?.Dispose();
        _disposable = null;
        return Task.CompletedTask;
    }

    private void Handle(int processId, string processName) => RaiseChanged(new ChangedEventArgs(processId, processName));
}
