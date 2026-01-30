using LenovoLegionToolkit.Lib.Controllers.CustomRGBEffects;

namespace LenovoLegionToolkit.Lib.Extensions;

/// <summary>
/// Extension methods for RGBKeyboardBacklightEffect enum.
/// </summary>
public static class RGBKeyboardBacklightEffectExtensions
{
    /// <summary>
    /// Determines if the effect is a custom software-driven effect.
    /// Custom effects run via CustomRGBEffectController and require software rendering loop.
    /// </summary>
    public static bool IsCustomEffect(this RGBKeyboardBacklightEffect effect) => effect switch
    {
        RGBKeyboardBacklightEffect.Disco => true,
        RGBKeyboardBacklightEffect.Swipe => true,
        RGBKeyboardBacklightEffect.SwipeFill => true,
        RGBKeyboardBacklightEffect.SwipeCleanWithBlack => true,
        RGBKeyboardBacklightEffect.Lightning => true,
        RGBKeyboardBacklightEffect.Christmas => true,
        RGBKeyboardBacklightEffect.Temperature => true,
        RGBKeyboardBacklightEffect.RainbowWave => true,
        RGBKeyboardBacklightEffect.Fade => true,
        RGBKeyboardBacklightEffect.Ripple => true,
        RGBKeyboardBacklightEffect.Ambient => true,
        RGBKeyboardBacklightEffect.BreathingColorCycle => true,
        RGBKeyboardBacklightEffect.Strobe => true,
        RGBKeyboardBacklightEffect.AudioVisualizer => true,
        _ => false
    };

    /// <summary>
    /// Determines if the effect is a hardware-driven (firmware) effect.
    /// </summary>
    public static bool IsHardwareEffect(this RGBKeyboardBacklightEffect effect) => !IsCustomEffect(effect);

    /// <summary>
    /// Determines if the effect requires an input signal provider (keyboard activity).
    /// </summary>
    public static bool RequiresInputProvider(this RGBKeyboardBacklightEffect effect) => effect switch
    {
        RGBKeyboardBacklightEffect.Fade => true,
        RGBKeyboardBacklightEffect.Ripple => true,
        _ => false
    };

    /// <summary>
    /// Determines if the effect requires a screen color provider.
    /// </summary>
    public static bool RequiresScreenProvider(this RGBKeyboardBacklightEffect effect) => effect switch
    {
        RGBKeyboardBacklightEffect.Ambient => true,
        _ => false
    };

    /// <summary>
    /// Determines if the effect supports zone colors.
    /// </summary>
    public static bool SupportsZoneColors(this RGBKeyboardBacklightEffect effect) => effect switch
    {
        RGBKeyboardBacklightEffect.Static => true,
        RGBKeyboardBacklightEffect.Breath => true,
        RGBKeyboardBacklightEffect.Swipe => true,
        RGBKeyboardBacklightEffect.SwipeFill => true,
        RGBKeyboardBacklightEffect.SwipeCleanWithBlack => true,
        RGBKeyboardBacklightEffect.Lightning => true,
        RGBKeyboardBacklightEffect.Fade => true,
        RGBKeyboardBacklightEffect.Ripple => true,
        RGBKeyboardBacklightEffect.Strobe => true,
        RGBKeyboardBacklightEffect.AudioVisualizer => true,
        _ => false
    };

    /// <summary>
    /// Determines if the effect supports speed adjustment.
    /// </summary>
    public static bool SupportsSpeed(this RGBKeyboardBacklightEffect effect) => effect switch
    {
        RGBKeyboardBacklightEffect.Breath => true,
        RGBKeyboardBacklightEffect.Smooth => true,
        RGBKeyboardBacklightEffect.WaveRTL => true,
        RGBKeyboardBacklightEffect.WaveLTR => true,
        RGBKeyboardBacklightEffect.Disco => true,
        RGBKeyboardBacklightEffect.Swipe => true,
        RGBKeyboardBacklightEffect.SwipeFill => true,
        RGBKeyboardBacklightEffect.SwipeCleanWithBlack => true,
        RGBKeyboardBacklightEffect.Lightning => true,
        RGBKeyboardBacklightEffect.RainbowWave => true,
        RGBKeyboardBacklightEffect.Fade => true,
        RGBKeyboardBacklightEffect.Ripple => true,
        RGBKeyboardBacklightEffect.BreathingColorCycle => true,
        RGBKeyboardBacklightEffect.Strobe => true,
        RGBKeyboardBacklightEffect.AudioVisualizer => true,
        _ => false
    };

    /// <summary>
    /// Converts RGBKeyboardBacklightEffect to CustomRGBEffectType.
    /// Returns null if the effect is not a custom effect.
    /// </summary>
    public static CustomRGBEffectType? ToCustomEffectType(this RGBKeyboardBacklightEffect effect) => effect switch
    {
        RGBKeyboardBacklightEffect.Disco => CustomRGBEffectType.Disco,
        RGBKeyboardBacklightEffect.Swipe => CustomRGBEffectType.Swipe,
        RGBKeyboardBacklightEffect.SwipeFill => CustomRGBEffectType.SwipeFill,
        RGBKeyboardBacklightEffect.SwipeCleanWithBlack => CustomRGBEffectType.SwipeCleanWithBlack,
        RGBKeyboardBacklightEffect.Lightning => CustomRGBEffectType.Lightning,
        RGBKeyboardBacklightEffect.Christmas => CustomRGBEffectType.Christmas,
        RGBKeyboardBacklightEffect.Temperature => CustomRGBEffectType.Temperature,
        RGBKeyboardBacklightEffect.RainbowWave => CustomRGBEffectType.RainbowWave,
        RGBKeyboardBacklightEffect.Fade => CustomRGBEffectType.Fade,
        RGBKeyboardBacklightEffect.Ripple => CustomRGBEffectType.Ripple,
        RGBKeyboardBacklightEffect.Ambient => CustomRGBEffectType.Ambient,
        RGBKeyboardBacklightEffect.BreathingColorCycle => CustomRGBEffectType.BreathingColorCycle,
        RGBKeyboardBacklightEffect.Strobe => CustomRGBEffectType.Strobe,
        RGBKeyboardBacklightEffect.AudioVisualizer => CustomRGBEffectType.AudioVisualizer,
        _ => null
    };
}
