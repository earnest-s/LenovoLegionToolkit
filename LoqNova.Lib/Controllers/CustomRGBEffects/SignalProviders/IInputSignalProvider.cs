using System;

namespace LoqNova.Lib.Controllers.CustomRGBEffects.SignalProviders;

/// <summary>
/// Provides keyboard input signals for input-reactive effects.
/// Effects consume this interface - they do not own the input hooks.
/// </summary>
public interface IInputSignalProvider : IDisposable
{
    /// <summary>
    /// Gets the timestamp of the last keyboard input event.
    /// Thread-safe access is guaranteed.
    /// </summary>
    DateTime LastKeypressTimestamp { get; }

    /// <summary>
    /// Gets the virtual key code of the last pressed key.
    /// Used for zone detection in ripple effect.
    /// </summary>
    int LastPressedKeyCode { get; }

    /// <summary>
    /// Gets the elapsed time since the last keypress.
    /// </summary>
    TimeSpan TimeSinceLastKeypress { get; }

    /// <summary>
    /// Gets whether the provider is currently active and receiving input.
    /// </summary>
    bool IsActive { get; }

    /// <summary>
    /// Starts listening for keyboard input.
    /// </summary>
    void Start();

    /// <summary>
    /// Stops listening for keyboard input.
    /// </summary>
    void Stop();
}
