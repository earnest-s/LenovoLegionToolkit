// ============================================================================
// CustomRGBEffectFactory.cs
// 
// Factory for creating custom RGB effects.
// Ported from L5P-Keyboard-RGB (Rust) to LenovoLegionToolkit (C#).
// ============================================================================

using LenovoLegionToolkit.Lib.Controllers.CustomRGBEffects.Effects;
using LenovoLegionToolkit.Lib.Controllers.CustomRGBEffects.SignalProviders;
using LenovoLegionToolkit.Lib.Controllers.Sensors;

namespace LenovoLegionToolkit.Lib.Controllers.CustomRGBEffects;

/// <summary>
/// Factory for creating custom RGB effects.
/// 
/// Effect categorization (from L5P-Keyboard-RGB):
/// - Fully portable effects: Disco, Swipe, Lightning, Christmas, Temperature, RainbowWave
/// - Input-dependent effects: Fade, Ripple (require IInputSignalProvider)
/// - External-signal effects: Ambient (requires IScreenColorProvider)
/// </summary>
public static class CustomRGBEffectFactory
{
    /// <summary>
    /// Creates a Disco effect with random zone color cycling.
    /// Rust origin: disco.rs
    /// </summary>
    /// <param name="speed">Speed 1-4 (1=slowest, 4=fastest).</param>
    public static DiscoEffect CreateDisco(int speed = 2)
        => new(speed);

    /// <summary>
    /// Creates a Swipe effect with colors moving across zones (Change mode).
    /// Rust origin: swipe.rs with SwipeMode::Change
    /// Direction is forced to Right per Rust default behavior.
    /// </summary>
    /// <param name="colors">Zone colors to use.</param>
    /// <param name="speed">Speed 1-4.</param>
    public static SwipeEffect CreateSwipe(
        ZoneColors colors,
        int speed = 2)
        => new(colors, speed, EffectDirection.Right, SwipeMode.Change, false);

    /// <summary>
    /// Creates a Swipe effect with Fill mode (zones fill sequentially).
    /// Rust origin: swipe.rs with SwipeMode::Fill
    /// Direction is forced to Right per Rust default behavior.
    /// </summary>
    /// <param name="colors">Zone colors to use.</param>
    /// <param name="speed">Speed 1-4.</param>
    public static SwipeEffect CreateSwipeFill(
        ZoneColors colors,
        int speed = 2)
        => new(colors, speed, EffectDirection.Right, SwipeMode.Fill, false);

    /// <summary>
    /// Creates a Swipe effect with Fill mode and black clear.
    /// Rust origin: swipe.rs with SwipeMode::Fill and clean_with_black=true
    /// Direction is forced to Right per Rust default behavior.
    /// </summary>
    /// <param name="colors">Zone colors to use.</param>
    /// <param name="speed">Speed 1-4.</param>
    public static SwipeEffect CreateSwipeCleanWithBlack(
        ZoneColors colors,
        int speed = 2)
        => new(colors, speed, EffectDirection.Right, SwipeMode.Fill, true);

    /// <summary>
    /// Creates a Lightning effect with random zone flashes.
    /// Rust origin: lightning.rs
    /// </summary>
    /// <param name="colors">Zone colors to flash.</param>
    /// <param name="speed">Speed 1-4.</param>
    public static LightningEffect CreateLightning(ZoneColors colors, int speed = 2)
        => new(colors, speed);

    /// <summary>
    /// Creates a Christmas effect with holiday-themed animations.
    /// Rust origin: christmas.rs
    /// </summary>
    public static ChristmasEffect CreateChristmas()
        => new();

    /// <summary>
    /// Creates a Temperature effect that reflects CPU temperature.
    /// Rust origin: temperature.rs
    /// </summary>
    /// <param name="sensorsController">Optional sensors controller for reading CPU temp.</param>
    public static TemperatureEffect CreateTemperature(ISensorsController? sensorsController = null)
        => new(sensorsController);

    /// <summary>
    /// Creates a Rainbow Wave effect with spectrum cycling (smooth transitions).
    /// </summary>
    /// <param name="speed">Speed 1-4.</param>
    /// <param name="direction">Direction of wave.</param>
    public static RainbowWaveEffect CreateRainbowWave(int speed = 2, EffectDirection direction = EffectDirection.Right)
        => new(speed, direction);

    /// <summary>
    /// Creates a Fade effect that dims on keyboard inactivity.
    /// Rust origin: fade.rs
    /// </summary>
    /// <param name="inputProvider">Provider for keyboard input signals.</param>
    /// <param name="zoneColors">Colors for all 4 zones at full brightness.</param>
    /// <param name="speed">Speed 1-4 (fade time = 20/speed seconds).</param>
    public static FadeEffect CreateFade(
        IInputSignalProvider inputProvider,
        ZoneColors? zoneColors = null,
        int speed = 2)
        => new(inputProvider, zoneColors, speed);

    /// <summary>
    /// Creates a Ripple effect triggered by keypresses.
    /// Rust origin: ripple.rs
    /// </summary>
    /// <param name="inputProvider">Provider for keyboard input signals.</param>
    /// <param name="zoneColors">Colors for each zone.</param>
    /// <param name="speed">Speed 1-4.</param>
    public static RippleEffect CreateRipple(
        IInputSignalProvider inputProvider,
        ZoneColors? zoneColors = null,
        int speed = 2)
        => new(inputProvider, zoneColors, speed);

    /// <summary>
    /// Creates an Ambient effect that syncs with screen colors.
    /// Rust origin: ambient.rs
    /// Forces 100% saturation and 30 FPS.
    /// </summary>
    /// <param name="screenProvider">Provider for screen color samples.</param>
    public static AmbientEffect CreateAmbient(IScreenColorProvider screenProvider)
        => new(screenProvider);

    /// <summary>
    /// Creates the default input signal provider using low-level keyboard hooks.
    /// Uses WH_KEYBOARD_LL - does NOT poll keyboard state.
    /// Caller is responsible for disposal.
    /// </summary>
    public static IInputSignalProvider CreateInputProvider()
        => new LowLevelKeyboardHookInputProvider();

    /// <summary>
    /// Creates the default screen color provider.
    /// Uses DXGI Desktop Duplication when available, falls back to GDI.
    /// Caller is responsible for disposal.
    /// </summary>
    public static IScreenColorProvider CreateScreenProvider()
        => new DxgiScreenColorProvider();

    /// <summary>
    /// Creates a GDI-based screen color provider.
    /// More compatible but less performant than DXGI.
    /// Caller is responsible for disposal.
    /// </summary>
    public static IScreenColorProvider CreateGdiScreenProvider()
        => new GdiScreenColorProvider();

    /// <summary>
    /// Creates a DXGI-based screen color provider using Desktop Duplication API.
    /// Better performance than GDI, limited to 30 FPS.
    /// Caller is responsible for disposal.
    /// </summary>
    public static IScreenColorProvider CreateDxgiScreenProvider()
        => new DxgiScreenColorProvider();

    /// <summary>
    /// Creates an effect by type.
    /// </summary>
    /// <param name="type">The effect type.</param>
    /// <param name="colors">Zone colors (used by applicable effects).</param>
    /// <param name="speed">Speed 1-4.</param>
    /// <param name="direction">Direction (used by applicable effects).</param>
    /// <param name="sensorsController">Sensors controller for temperature effect.</param>
    /// <param name="inputProvider">Input provider for Fade/Ripple effects (required for those types).</param>
    /// <param name="screenProvider">Screen provider for Ambient effect (required for that type).</param>
    public static ICustomRGBEffect CreateByType(
        CustomRGBEffectType type,
        ZoneColors? colors = null,
        int speed = 2,
        EffectDirection direction = EffectDirection.Right,
        ISensorsController? sensorsController = null,
        IInputSignalProvider? inputProvider = null,
        IScreenColorProvider? screenProvider = null)
    {
        var effectColors = colors ?? ZoneColors.White;

        return type switch
        {
            CustomRGBEffectType.Disco => CreateDisco(speed),
            CustomRGBEffectType.Swipe => CreateSwipe(effectColors, speed),
            CustomRGBEffectType.SwipeFill => CreateSwipeFill(effectColors, speed),
            CustomRGBEffectType.SwipeCleanWithBlack => CreateSwipeCleanWithBlack(effectColors, speed),
            CustomRGBEffectType.Lightning => CreateLightning(effectColors, speed),
            CustomRGBEffectType.Christmas => CreateChristmas(),
            CustomRGBEffectType.Temperature => CreateTemperature(sensorsController),
            CustomRGBEffectType.RainbowWave => CreateRainbowWave(speed, direction),
            CustomRGBEffectType.Fade => CreateFade(
                inputProvider ?? throw new global::System.ArgumentNullException(nameof(inputProvider), "Fade effect requires IInputSignalProvider"),
                effectColors,
                speed),
            CustomRGBEffectType.Ripple => CreateRipple(
                inputProvider ?? throw new global::System.ArgumentNullException(nameof(inputProvider), "Ripple effect requires IInputSignalProvider"),
                effectColors,
                speed),
            CustomRGBEffectType.Ambient => CreateAmbient(
                screenProvider ?? throw new global::System.ArgumentNullException(nameof(screenProvider), "Ambient effect requires IScreenColorProvider")),
            _ => CreateDisco(speed)
        };
    }
}
