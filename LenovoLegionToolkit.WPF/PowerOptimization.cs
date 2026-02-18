using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using LenovoLegionToolkit.Lib.Utils;

namespace LenovoLegionToolkit.WPF;

/// <summary>
/// Prevents Windows from power-throttling this process via EcoQoS / Efficiency Mode,
/// improves timer resolution, and sets a stable above-normal process priority.
///
/// Call <see cref="Apply"/> once at application startup before any animation,
/// RGB, or audio threads are created.
/// </summary>
internal static class PowerOptimization
{
    // ── Win32 types ───────────────────────────────────────────────────────

    [StructLayout(LayoutKind.Sequential)]
    private struct PROCESS_POWER_THROTTLING_STATE
    {
        public uint Version;
        public uint ControlMask;
        public uint StateMask;
    }

    private const uint PROCESS_POWER_THROTTLING_CURRENT_VERSION  = 1;
    private const uint PROCESS_POWER_THROTTLING_EXECUTION_SPEED  = 0x1;

    // ProcessPowerThrottling = 9
    private const int PROCESS_INFORMATION_CLASS_POWER_THROTTLING = 9;

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool SetProcessInformation(
        IntPtr hProcess,
        int    ProcessInformationClass,
        ref PROCESS_POWER_THROTTLING_STATE ProcessInformation,
        uint   ProcessInformationSize);

    [DllImport("winmm.dll")]
    private static extern uint timeBeginPeriod(uint uMilliseconds);

    // ── Public API ────────────────────────────────────────────────────────

    /// <summary>
    /// Applies all power optimizations in one call:
    /// <list type="bullet">
    ///   <item>Disables EcoQoS / Efficiency Mode throttling via <c>SetProcessInformation</c></item>
    ///   <item>Sets process priority to <see cref="ProcessPriorityClass.AboveNormal"/> for stable scheduling</item>
    ///   <item>Reduces multimedia timer resolution to 1 ms via <c>timeBeginPeriod(1)</c></item>
    /// </list>
    /// </summary>
    public static void Apply()
    {
        DisableEfficiencyMode();
        SetStableProcessPriority();
        ImproveTimerResolution();
    }

    // ── Implementation ────────────────────────────────────────────────────

    /// <summary>
    /// Disables Windows EcoQoS (Efficiency Mode) execution-speed throttling.
    ///
    /// Setting ControlMask = EXECUTION_SPEED and StateMask = 0 explicitly
    /// opts the process OUT of throttling — Windows will not auto-enable it
    /// when on battery, even when idle CPU usage is low.
    /// </summary>
    private static void DisableEfficiencyMode()
    {
        try
        {
            var state = new PROCESS_POWER_THROTTLING_STATE
            {
                Version     = PROCESS_POWER_THROTTLING_CURRENT_VERSION,
                ControlMask = PROCESS_POWER_THROTTLING_EXECUTION_SPEED,
                StateMask   = 0,   // 0 = disable throttling
            };

            var ok = SetProcessInformation(
                Process.GetCurrentProcess().Handle,
                PROCESS_INFORMATION_CLASS_POWER_THROTTLING,
                ref state,
                (uint)Marshal.SizeOf<PROCESS_POWER_THROTTLING_STATE>());

            if (Log.Instance.IsTraceEnabled)
                Log.Instance.Trace($"DisableEfficiencyMode: SetProcessInformation returned {ok}");
        }
        catch (Exception ex)
        {
            if (Log.Instance.IsTraceEnabled)
                Log.Instance.Trace($"DisableEfficiencyMode failed", ex);
        }
    }

    /// <summary>
    /// Sets process priority to AboveNormal for better scheduling stability
    /// without risking system thread starvation (Realtime / High are not used).
    /// </summary>
    private static void SetStableProcessPriority()
    {
        try
        {
            Process.GetCurrentProcess().PriorityClass = ProcessPriorityClass.AboveNormal;

            if (Log.Instance.IsTraceEnabled)
                Log.Instance.Trace($"SetStableProcessPriority: AboveNormal applied");
        }
        catch (Exception ex)
        {
            if (Log.Instance.IsTraceEnabled)
                Log.Instance.Trace($"SetStableProcessPriority failed", ex);
        }
    }

    /// <summary>
    /// Reduces the Windows multimedia timer interrupt period to 1 ms,
    /// improving DispatcherTimer and Task.Delay precision for animation loops.
    /// </summary>
    private static void ImproveTimerResolution()
    {
        try
        {
            var result = timeBeginPeriod(1);

            if (Log.Instance.IsTraceEnabled)
                Log.Instance.Trace($"ImproveTimerResolution: timeBeginPeriod(1) returned {result}");
        }
        catch (Exception ex)
        {
            if (Log.Instance.IsTraceEnabled)
                Log.Instance.Trace($"ImproveTimerResolution failed", ex);
        }
    }
}
