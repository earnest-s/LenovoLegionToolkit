// ============================================================================
// ICustomRGBEffect.cs
// 
// Base interface for all custom keyboard RGB effects.
// Ported from L5P-Keyboard-RGB (Rust) to LoqNova (C#).
// 
// Original Rust source: https://github.com/4JX/L5P-Keyboard-RGB
// ============================================================================

using System;
using System.Threading;
using System.Threading.Tasks;

namespace LoqNova.Lib.Controllers.CustomRGBEffects;

/// <summary>
/// Direction for wave/swipe effects.
/// Maps to Rust enums::Direction.
/// Extended to support vertical directions for 4-zone keyboard.
/// </summary>
public enum EffectDirection
{
    Left,
    Right,
    /// <summary>
    /// Top to Bottom direction (Zone1→Zone4).
    /// Note: For 4-zone keyboards arranged horizontally, this simulates
    /// a "forward" wave effect that propagates zone by zone.
    /// </summary>
    TopToBottom,
    /// <summary>
    /// Bottom to Top direction (Zone4→Zone1).
    /// Note: For 4-zone keyboards arranged horizontally, this simulates
    /// a "backward" wave effect that propagates zone by zone.
    /// </summary>
    BottomToTop
}

/// <summary>
/// Swipe mode for swipe/smooth wave effects.
/// Maps to Rust enums::SwipeMode.
/// </summary>
public enum SwipeMode
{
    /// <summary>
    /// Colors rotate/change positions
    /// </summary>
    Change,
    /// <summary>
    /// Colors fill zones sequentially
    /// </summary>
    Fill
}

/// <summary>
/// Parameters for keyboard zone colors (4-zone RGB keyboard).
/// Maps to the Rust Profile struct's rgb_zones field.
/// </summary>
public readonly struct ZoneColors : IEquatable<ZoneColors>
{
    public RGBColor Zone1 { get; init; }
    public RGBColor Zone2 { get; init; }
    public RGBColor Zone3 { get; init; }
    public RGBColor Zone4 { get; init; }

    /// <summary>
    /// Creates a new ZoneColors with the specified zone colors.
    /// </summary>
    public ZoneColors(RGBColor zone1, RGBColor zone2, RGBColor zone3, RGBColor zone4)
    {
        Zone1 = zone1;
        Zone2 = zone2;
        Zone3 = zone3;
        Zone4 = zone4;
    }

    /// <summary>
    /// Creates a ZoneColors with all zones set to the same color.
    /// </summary>
    public ZoneColors(RGBColor allZones) : this(allZones, allZones, allZones, allZones) { }

    public static ZoneColors White => new()
    {
        Zone1 = RGBColor.White,
        Zone2 = RGBColor.White,
        Zone3 = RGBColor.White,
        Zone4 = RGBColor.White
    };

    public static ZoneColors Black => new()
    {
        Zone1 = new RGBColor(0, 0, 0),
        Zone2 = new RGBColor(0, 0, 0),
        Zone3 = new RGBColor(0, 0, 0),
        Zone4 = new RGBColor(0, 0, 0)
    };

    /// <summary>
    /// Converts to a 12-byte array [R1,G1,B1,R2,G2,B2,R3,G3,B3,R4,G4,B4].
    /// Matches the Rust Profile::rgb_array() method.
    /// </summary>
    public byte[] ToArray() =>
    [
        Zone1.R, Zone1.G, Zone1.B,
        Zone2.R, Zone2.G, Zone2.B,
        Zone3.R, Zone3.G, Zone3.B,
        Zone4.R, Zone4.G, Zone4.B
    ];

    /// <summary>
    /// Creates ZoneColors from a 12-byte array.
    /// </summary>
    public static ZoneColors FromArray(byte[] array)
    {
        if (array.Length != 12)
            throw new ArgumentException("Array must have exactly 12 elements", nameof(array));

        return new ZoneColors
        {
            Zone1 = new RGBColor(array[0], array[1], array[2]),
            Zone2 = new RGBColor(array[3], array[4], array[5]),
            Zone3 = new RGBColor(array[6], array[7], array[8]),
            Zone4 = new RGBColor(array[9], array[10], array[11])
        };
    }

    /// <summary>
    /// Creates ZoneColors from an array of 4 RGBColor values.
    /// </summary>
    public static ZoneColors FromRGBColors(RGBColor[] colors)
    {
        if (colors.Length != 4)
            throw new ArgumentException("Array must have exactly 4 colors", nameof(colors));

        return new ZoneColors(colors[0], colors[1], colors[2], colors[3]);
    }

    /// <summary>
    /// Gets the color of a specific zone by index (0-3).
    /// </summary>
    public RGBColor GetZone(int index) => index switch
    {
        0 => Zone1,
        1 => Zone2,
        2 => Zone3,
        3 => Zone4,
        _ => throw new ArgumentOutOfRangeException(nameof(index), "Zone index must be 0-3")
    };

    /// <summary>
    /// Creates a new ZoneColors with a specific zone modified.
    /// </summary>
    public ZoneColors WithZone(int index, RGBColor color) => index switch
    {
        0 => this with { Zone1 = color },
        1 => this with { Zone2 = color },
        2 => this with { Zone3 = color },
        3 => this with { Zone4 = color },
        _ => throw new ArgumentOutOfRangeException(nameof(index), "Zone index must be 0-3")
    };

    /// <summary>
    /// Rotates zone colors left (zone2→zone1, zone3→zone2, etc.).
    /// Matches the Rust rotate_left(3) operation on the rgb_array.
    /// </summary>
    public ZoneColors RotateLeft() => new()
    {
        Zone1 = Zone2,
        Zone2 = Zone3,
        Zone3 = Zone4,
        Zone4 = Zone1
    };

    /// <summary>
    /// Rotates zone colors right (zone1→zone2, zone2→zone3, etc.).
    /// Matches the Rust rotate_right(3) operation on the rgb_array.
    /// </summary>
    public ZoneColors RotateRight() => new()
    {
        Zone1 = Zone4,
        Zone2 = Zone1,
        Zone3 = Zone2,
        Zone4 = Zone3
    };

    #region Equality

    public override bool Equals(object? obj) => obj is ZoneColors other && Equals(other);

    public bool Equals(ZoneColors other) =>
        Zone1 == other.Zone1 && Zone2 == other.Zone2 &&
        Zone3 == other.Zone3 && Zone4 == other.Zone4;

    public override int GetHashCode() => HashCode.Combine(Zone1, Zone2, Zone3, Zone4);

    public static bool operator ==(ZoneColors left, ZoneColors right) => left.Equals(right);
    public static bool operator !=(ZoneColors left, ZoneColors right) => !left.Equals(right);

    #endregion
}

/// <summary>
/// Base interface for all custom keyboard RGB effects.
/// Each effect runs asynchronously and respects cancellation.
/// </summary>
public interface ICustomRGBEffect
{
    /// <summary>
    /// Gets the unique identifier of the effect.
    /// </summary>
    CustomRGBEffectType Type { get; }

    /// <summary>
    /// Gets the description of the effect.
    /// </summary>
    string Description { get; }

    /// <summary>
    /// Whether this effect requires keyboard input monitoring.
    /// Effects like Fade and Ripple need to detect key presses.
    /// </summary>
    bool RequiresInputMonitoring { get; }

    /// <summary>
    /// Whether this effect requires system information (CPU temp, screen capture, etc.).
    /// </summary>
    bool RequiresSystemAccess { get; }

    /// <summary>
    /// Runs the effect loop until cancellation is requested.
    /// The effect should check the cancellation token regularly.
    /// </summary>
    /// <param name="controller">The effect controller for setting colors.</param>
    /// <param name="cancellationToken">Token to signal effect termination.</param>
    Task RunAsync(CustomRGBEffectController controller, CancellationToken cancellationToken);
}
