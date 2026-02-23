using System;
using System.Threading.Tasks;

namespace LoqNova.Lib.AutoListeners;

public interface IAutoListener<T> where T : EventArgs
{
    Task SubscribeChangedAsync(EventHandler<T> eventHandler);
    Task UnsubscribeChangedAsync(EventHandler<T> eventHandler);
}
