// ============================================================================
// RippleEffect.cs
// 
// Ripple effect triggered by keyboard input.
// Ported from L5P-Keyboard-RGB ripple.rs
// 
// Effect behavior (from Rust):
// - On keypress, the pressed key's zone enters "Center" state and stays lit
// - Center spawns Left and Right propagation to adjacent zones
// - Each zone uses its own color from the profile when lit
// - Zones are lit when in any non-Off state
// - Zone stays lit while key is held (zone_pressed tracking)
// 
// Timing (from Rust):
// - Main loop: 50ms sleep
// - State advance: every 200/speed ms
// - Output: transition_colors_to(&final_arr, 20, 0) = 20 steps, 0ms delay
// ============================================================================

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using LenovoLegionToolkit.Lib.Controllers.CustomRGBEffects.SignalProviders;

namespace LenovoLegionToolkit.Lib.Controllers.CustomRGBEffects.Effects;

/// <summary>
/// Ripple effect - zone-based ripple on keypress.
/// Port of Rust ripple.rs effect with smooth time-based animation.
/// </summary>
public class RippleEffect : ICustomRGBEffect
{
    private const int ZoneCount = 4;

    /// <summary>
    /// Ripple movement state matching Rust RippleMove enum.
    /// </summary>
    private enum RippleMove
    {
        Off,
        Center,
        Left,
        Right
    }

    private readonly IInputSignalProvider _inputProvider;
    private readonly ZoneColors _zoneColors;
    private readonly int _speed;
    private readonly int _stepIntervalMs;

    private DateTime _lastKeypressChecked = DateTime.MinValue;

    public CustomRGBEffectType Type => CustomRGBEffectType.Ripple;
    public string Description => "Ripple effect on keypress (ripple.rs)";
    public bool RequiresInputMonitoring => true;
    public bool RequiresSystemAccess => false;

    /// <summary>
    /// Creates a new ripple effect matching Rust ripple.rs behavior.
    /// </summary>
    public RippleEffect(
        IInputSignalProvider inputProvider,
        ZoneColors? zoneColors = null,
        int speed = 2,
        int updateIntervalMs = 50)
    {
        _inputProvider = inputProvider;
        _zoneColors = zoneColors ?? new ZoneColors(
            new RGBColor(0, 255, 255),
            new RGBColor(0, 255, 255),
            new RGBColor(0, 255, 255),
            new RGBColor(0, 255, 255));
        _speed = Math.Clamp(speed, 1, 4);
        // From Rust: Duration::from_millis((200 / speed) as u64)
        _stepIntervalMs = 200 / _speed;
    }

    public async Task RunAsync(CustomRGBEffectController controller, CancellationToken cancellationToken)
    {
        // Ensure input provider is active
        if (!_inputProvider.IsActive)
            _inputProvider.Start();

        // Persistent state (matching Rust: defined outside loop)
        var zonePressed = new HashSet<int>[ZoneCount];
        var zoneState = new RippleMove[ZoneCount];
        var outputColors = new byte[12];

        for (var i = 0; i < ZoneCount; i++)
        {
            zonePressed[i] = new HashSet<int>();
            zoneState[i] = RippleMove.Off;
        }

        // Stopwatch for consistent timing
        var stopwatch = Stopwatch.StartNew();
        var lastStepMs = 0L;
        var nextLoopMs = 0L;

        while (!cancellationToken.IsCancellationRequested)
        {
            var elapsedMs = stopwatch.ElapsedMilliseconds;

            if (elapsedMs >= nextLoopMs)
            {
                // Check for new keypress
                var lastKeypress = _inputProvider.LastKeypressTimestamp;
                if (lastKeypress > _lastKeypressChecked)
                {
                    _lastKeypressChecked = lastKeypress;
                    
                    var keyCode = _inputProvider.LastPressedKeyCode;
                    var zone = KeyboardZoneMapper.GetZone(keyCode);
                    
                    if (zone >= 0 && zone < ZoneCount)
                    {
                        zonePressed[zone].Add(keyCode);
                    }
                }

                // Advance zone state at step interval (matching Rust timing)
                if (elapsedMs - lastStepMs >= _stepIntervalMs)
                {
                    zoneState = AdvanceZoneState(zoneState);
                    lastStepMs = elapsedMs;
                }

                // Zones with pressed keys stay at Center (matching Rust)
                for (var i = 0; i < ZoneCount; i++)
                {
                    if (zonePressed[i].Count > 0)
                    {
                        zoneState[i] = RippleMove.Center;
                        zonePressed[i].Clear(); // Simulate key release
                    }
                }

                // Build output colors (matching Rust byte-level operations)
                var sourceColors = _zoneColors.ToArray();
                for (var i = 0; i < ZoneCount; i++)
                {
                    if (zoneState[i] != RippleMove.Off)
                    {
                        // Copy zone color bytes directly (matching Rust slice copy)
                        outputColors[i * 3] = sourceColors[i * 3];
                        outputColors[i * 3 + 1] = sourceColors[i * 3 + 1];
                        outputColors[i * 3 + 2] = sourceColors[i * 3 + 2];
                    }
                    else
                    {
                        outputColors[i * 3] = 0;
                        outputColors[i * 3 + 1] = 0;
                        outputColors[i * 3 + 2] = 0;
                    }
                }

                // From Rust: manager.keyboard.transition_colors_to(&final_arr, 20, 0)
                // 20 steps with 0ms delay = instant update, use SetColorsAsync directly
                var zoneColors = ZoneColors.FromArray(outputColors);
                await controller.SetColorsAsync(zoneColors, cancellationToken).ConfigureAwait(false);

                // From Rust: thread::sleep(Duration::from_millis(50));
                nextLoopMs = stopwatch.ElapsedMilliseconds + 50;
            }

            await Task.Delay(1, cancellationToken).ConfigureAwait(false);
        }
    }

    /// <summary>
    /// Advances zone states matching Rust advance_zone_state function exactly.
    /// </summary>
    private static RippleMove[] AdvanceZoneState(RippleMove[] currentState)
    {
        var newState = new RippleMove[ZoneCount];
        for (var i = 0; i < ZoneCount; i++)
            newState[i] = RippleMove.Off;

        // First pass: process Left and Right moves
        for (var i = 0; i < ZoneCount; i++)
        {
            switch (currentState[i])
            {
                case RippleMove.Left:
                    if (i > 0)
                        newState[i - 1] = RippleMove.Left;
                    break;
                case RippleMove.Right:
                    if (i < ZoneCount - 1)
                        newState[i + 1] = RippleMove.Right;
                    break;
            }
        }

        // Second pass: Center spawns Left and Right
        for (var i = 0; i < ZoneCount; i++)
        {
            if (currentState[i] == RippleMove.Center)
            {
                if (i > 0)
                    newState[i - 1] = RippleMove.Left;
                if (i < ZoneCount - 1)
                    newState[i + 1] = RippleMove.Right;
            }
        }

        return newState;
    }
}
