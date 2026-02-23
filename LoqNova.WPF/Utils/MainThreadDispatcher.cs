using System;
using System.Threading.Tasks;
using System.Windows;
using LoqNova.Lib.Utils;

namespace LoqNova.WPF.Utils;

public class MainThreadDispatcher : IMainThreadDispatcher
{
    public void Dispatch(Action callback) => Application.Current.Dispatcher.Invoke(callback);

    public Task DispatchAsync(Func<Task> callback) => Application.Current.Dispatcher.Invoke(callback);
}
