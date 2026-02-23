// ============================================================================
// KeyboardZoneMapper.cs
// 
// Maps virtual key codes to keyboard zones (0-3).
// Ported from L5P-Keyboard-RGB zones.rs
// 
// Zone layout (left to right):
// - Zone 0: Left side (Esc, F1-F4, `~, 1-4, Tab, QWEASD, LShift, ZX, LCtrl, Win, LAlt)
// - Zone 1: Center-left (F5-F10, 5-9, RTYUI, FGHJK, CVBNM, Space, RAlt)
// - Zone 2: Center-right (F11-F12, Ins, Del, 0-=, Backspace, OP[], Enter, L;'\, ./?, RShift, RCtrl, Arrows)
// - Zone 3: Right side / Numpad (Home, End, PgUp, PgDn, NumLock, Numpad keys)
// ============================================================================

using System.Collections.Generic;

namespace LoqNova.Lib.Controllers.CustomRGBEffects;

/// <summary>
/// Maps virtual key codes to keyboard zones.
/// Zone indices are 0-3 (left to right).
/// </summary>
public static class KeyboardZoneMapper
{
    // Virtual key codes - Windows VK_ constants
    private static readonly HashSet<int> Zone0Keys = new()
    {
        0x1B,       // VK_ESCAPE
        0x70,       // VK_F1
        0x71,       // VK_F2
        0x72,       // VK_F3
        0x73,       // VK_F4
        0xC0,       // VK_OEM_3 (` ~)
        0x31,       // 1
        0x32,       // 2
        0x33,       // 3
        0x34,       // 4
        0x09,       // VK_TAB
        0x51,       // Q
        0x57,       // W
        0x45,       // E
        0x14,       // VK_CAPITAL (CapsLock)
        0x41,       // A
        0x53,       // S
        0x44,       // D
        0xA0,       // VK_LSHIFT
        0x5A,       // Z
        0x58,       // X
        0xA2,       // VK_LCONTROL
        0x5B,       // VK_LWIN
        0xA4,       // VK_LMENU (LAlt)
    };

    private static readonly HashSet<int> Zone1Keys = new()
    {
        0x74,       // VK_F5
        0x75,       // VK_F6
        0x76,       // VK_F7
        0x77,       // VK_F8
        0x78,       // VK_F9
        0x79,       // VK_F10
        0x35,       // 5
        0x36,       // 6
        0x37,       // 7
        0x38,       // 8
        0x39,       // 9
        0x52,       // R
        0x54,       // T
        0x59,       // Y
        0x55,       // U
        0x49,       // I
        0x46,       // F
        0x47,       // G
        0x48,       // H
        0x4A,       // J
        0x4B,       // K
        0x43,       // C
        0x56,       // V
        0x42,       // B
        0x4E,       // N
        0x4D,       // M
        0xBC,       // VK_OEM_COMMA
        0x20,       // VK_SPACE
        0xA5,       // VK_RMENU (RAlt)
    };

    private static readonly HashSet<int> Zone2Keys = new()
    {
        0x7A,       // VK_F11
        0x7B,       // VK_F12
        0x2D,       // VK_INSERT
        0x2E,       // VK_DELETE
        0x30,       // 0
        0xBD,       // VK_OEM_MINUS
        0xBB,       // VK_OEM_PLUS (=)
        0x08,       // VK_BACK (Backspace)
        0x4F,       // O
        0x50,       // P
        0xDB,       // VK_OEM_4 ([)
        0xDD,       // VK_OEM_6 (])
        0x0D,       // VK_RETURN (Enter)
        0x4C,       // L
        0xBA,       // VK_OEM_1 (;)
        0xDE,       // VK_OEM_7 (')
        0xDC,       // VK_OEM_5 (\)
        0xBE,       // VK_OEM_PERIOD (.)
        0xBF,       // VK_OEM_2 (/)
        0xA1,       // VK_RSHIFT
        0xA3,       // VK_RCONTROL
        0x26,       // VK_UP
        0x28,       // VK_DOWN
        0x25,       // VK_LEFT
        0x27,       // VK_RIGHT
    };

    private static readonly HashSet<int> Zone3Keys = new()
    {
        0x24,       // VK_HOME
        0x23,       // VK_END
        0x21,       // VK_PRIOR (PageUp)
        0x22,       // VK_NEXT (PageDown)
        0x90,       // VK_NUMLOCK
        0x6F,       // VK_DIVIDE
        0x6A,       // VK_MULTIPLY
        0x6D,       // VK_SUBTRACT
        0x67,       // VK_NUMPAD7
        0x68,       // VK_NUMPAD8
        0x69,       // VK_NUMPAD9
        0x6B,       // VK_ADD
        0x64,       // VK_NUMPAD4
        0x65,       // VK_NUMPAD5
        0x66,       // VK_NUMPAD6
        0x61,       // VK_NUMPAD1
        0x62,       // VK_NUMPAD2
        0x63,       // VK_NUMPAD3
        0x60,       // VK_NUMPAD0
        0x6E,       // VK_DECIMAL
        0x13,       // VK_PAUSE
        0x91,       // VK_SCROLL (ScrollLock)
        0x2C,       // VK_SNAPSHOT (PrintScreen)
    };

    /// <summary>
    /// Gets the zone index (0-3) for a virtual key code.
    /// Returns -1 if the key is not mapped to any zone.
    /// </summary>
    /// <param name="virtualKeyCode">The Windows virtual key code.</param>
    /// <returns>Zone index 0-3, or -1 if not mapped.</returns>
    public static int GetZone(int virtualKeyCode)
    {
        if (Zone0Keys.Contains(virtualKeyCode))
            return 0;
        if (Zone1Keys.Contains(virtualKeyCode))
            return 1;
        if (Zone2Keys.Contains(virtualKeyCode))
            return 2;
        if (Zone3Keys.Contains(virtualKeyCode))
            return 3;
        
        // Default to center zone for unmapped keys (better visual effect)
        return 1;
    }
}
