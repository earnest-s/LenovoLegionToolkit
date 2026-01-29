using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Threading;

namespace LenovoLegionToolkit.Lib.Controllers.CustomRGBEffects.SignalProviders;

/// <summary>
/// Provides keyboard input signals using a low-level keyboard hook (WH_KEYBOARD_LL).
/// This does NOT poll - it receives system callbacks on keypress.
/// 
/// IMPORTANT: WH_KEYBOARD_LL requires a message pump on the hook thread.
/// This implementation runs a dedicated STA thread with a message loop.
/// 
/// Rust origin: fade.rs uses Instant::now() on every effect tick and compares to last input.
/// This implementation provides the timestamp; the effect calculates elapsed time.
/// </summary>
public sealed class LowLevelKeyboardHookInputProvider : IInputSignalProvider
{
    private const int WH_KEYBOARD_LL = 13;
    private const int WM_KEYDOWN = 0x0100;
    private const int WM_SYSKEYDOWN = 0x0104;
    private const int WM_QUIT = 0x0012;

    private delegate IntPtr LowLevelKeyboardProc(int nCode, IntPtr wParam, IntPtr lParam);

    [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
    private static extern IntPtr SetWindowsHookEx(int idHook, LowLevelKeyboardProc lpfn, IntPtr hMod, uint dwThreadId);

    [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool UnhookWindowsHookEx(IntPtr hhk);

    [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
    private static extern IntPtr CallNextHookEx(IntPtr hhk, int nCode, IntPtr wParam, IntPtr lParam);

    [DllImport("kernel32.dll", CharSet = CharSet.Auto, SetLastError = true)]
    private static extern IntPtr GetModuleHandle(string lpModuleName);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetMessage(out MSG lpMsg, IntPtr hWnd, uint wMsgFilterMin, uint wMsgFilterMax);

    [DllImport("user32.dll")]
    private static extern bool TranslateMessage(ref MSG lpMsg);

    [DllImport("user32.dll")]
    private static extern IntPtr DispatchMessage(ref MSG lpMsg);

    [DllImport("user32.dll")]
    private static extern bool PostThreadMessage(uint idThread, uint Msg, IntPtr wParam, IntPtr lParam);

    [DllImport("kernel32.dll")]
    private static extern uint GetCurrentThreadId();

    [StructLayout(LayoutKind.Sequential)]
    private struct MSG
    {
        public IntPtr hwnd;
        public uint message;
        public IntPtr wParam;
        public IntPtr lParam;
        public uint time;
        public POINT pt;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct POINT
    {
        public int x;
        public int y;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct KBDLLHOOKSTRUCT
    {
        public uint vkCode;
        public uint scanCode;
        public uint flags;
        public uint time;
        public IntPtr dwExtraInfo;
    }

    private IntPtr _hookId = IntPtr.Zero;
    private LowLevelKeyboardProc? _proc;
    private long _lastKeypressTicks;
    private int _lastKeyCode;
    private bool _disposed;
    private Thread? _hookThread;
    private uint _hookThreadId;
    private readonly ManualResetEventSlim _hookInstalledEvent = new(false);
    private readonly ManualResetEventSlim _hookThreadExitedEvent = new(false);
    private Exception? _hookInstallException;

    /// <inheritdoc />
    public DateTime LastKeypressTimestamp => new(Interlocked.Read(ref _lastKeypressTicks), DateTimeKind.Utc);

    /// <inheritdoc />
    public int LastPressedKeyCode => Interlocked.CompareExchange(ref _lastKeyCode, 0, 0);

    /// <inheritdoc />
    public TimeSpan TimeSinceLastKeypress => DateTime.UtcNow - LastKeypressTimestamp;

    /// <inheritdoc />
    public bool IsActive => _hookId != IntPtr.Zero;

    public LowLevelKeyboardHookInputProvider()
    {
        // Initialize to current time so effects don't start with huge elapsed times
        _lastKeypressTicks = DateTime.UtcNow.Ticks;
    }

    /// <inheritdoc />
    public void Start()
    {
        if (_hookThread is not null)
            return;

        _hookInstalledEvent.Reset();
        _hookThreadExitedEvent.Reset();
        _hookInstallException = null;

        // WH_KEYBOARD_LL hooks require a message pump on the hook thread.
        // Create a dedicated STA thread with a message loop.
        _hookThread = new Thread(HookThreadProc)
        {
            Name = "KeyboardHookThread",
            IsBackground = true
        };
        _hookThread.SetApartmentState(ApartmentState.STA);
        _hookThread.Start();

        // Wait for hook to be installed (or fail)
        _hookInstalledEvent.Wait(TimeSpan.FromSeconds(5));

        if (_hookInstallException is not null)
        {
            throw _hookInstallException;
        }

        if (_hookId == IntPtr.Zero)
        {
            throw new InvalidOperationException("Failed to install keyboard hook within timeout.");
        }
    }

    /// <inheritdoc />
    public void Stop()
    {
        if (_hookThread is null)
            return;

        // Post WM_QUIT to the hook thread to exit the message loop
        if (_hookThreadId != 0)
        {
            PostThreadMessage(_hookThreadId, WM_QUIT, IntPtr.Zero, IntPtr.Zero);
        }

        // Wait for thread to exit
        _hookThreadExitedEvent.Wait(TimeSpan.FromSeconds(2));

        _hookThread = null;
        _hookThreadId = 0;
    }

    private void HookThreadProc()
    {
        try
        {
            _hookThreadId = GetCurrentThreadId();

            _proc = HookCallback;
            using var curProcess = Process.GetCurrentProcess();
            using var curModule = curProcess.MainModule;

            if (curModule is null)
            {
                _hookInstallException = new InvalidOperationException("Could not get main module for keyboard hook.");
                _hookInstalledEvent.Set();
                return;
            }

            _hookId = SetWindowsHookEx(WH_KEYBOARD_LL, _proc, GetModuleHandle(curModule.ModuleName), 0);

            if (_hookId == IntPtr.Zero)
            {
                var error = Marshal.GetLastWin32Error();
                _hookInstallException = new InvalidOperationException($"Failed to install keyboard hook. Error: {error}");
                _hookInstalledEvent.Set();
                return;
            }

            // Signal that hook is installed
            _hookInstalledEvent.Set();

            // Run message loop - this is REQUIRED for WH_KEYBOARD_LL to receive callbacks
            while (GetMessage(out var msg, IntPtr.Zero, 0, 0))
            {
                TranslateMessage(ref msg);
                DispatchMessage(ref msg);
            }
        }
        finally
        {
            // Cleanup hook
            if (_hookId != IntPtr.Zero)
            {
                UnhookWindowsHookEx(_hookId);
                _hookId = IntPtr.Zero;
            }
            _proc = null;
            _hookThreadExitedEvent.Set();
        }
    }

    private IntPtr HookCallback(int nCode, IntPtr wParam, IntPtr lParam)
    {
        if (nCode >= 0)
        {
            var message = (int)wParam;
            if (message == WM_KEYDOWN || message == WM_SYSKEYDOWN)
            {
                // Extract the virtual key code from the hook data
                var hookData = Marshal.PtrToStructure<KBDLLHOOKSTRUCT>(lParam);
                Interlocked.Exchange(ref _lastKeyCode, (int)hookData.vkCode);
                // Thread-safe update of last keypress timestamp
                Interlocked.Exchange(ref _lastKeypressTicks, DateTime.UtcNow.Ticks);
            }
        }

        return CallNextHookEx(_hookId, nCode, wParam, lParam);
    }

    public void Dispose()
    {
        if (_disposed)
            return;

        Stop();
        _hookInstalledEvent.Dispose();
        _hookThreadExitedEvent.Dispose();
        _disposed = true;
    }
}
