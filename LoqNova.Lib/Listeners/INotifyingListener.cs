using System;
using System.Threading.Tasks;

namespace LoqNova.Lib.Listeners;

public interface INotifyingListener<TEventArgs, in TValue> : IListener<TEventArgs> where TEventArgs : EventArgs
{
    Task NotifyAsync(TValue value);
}
