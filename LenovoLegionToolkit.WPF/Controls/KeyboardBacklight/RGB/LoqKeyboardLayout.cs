using System.Collections.Generic;

namespace LenovoLegionToolkit.WPF.Controls.KeyboardBacklight.RGB;

/// <summary>
/// Physical key definition: label, pixel position/size, zone index.
/// </summary>
public readonly record struct KeyDef(string Label, double X, double Y, double W, double H, int Zone);

/// <summary>
/// Pixel-accurate LOQ 15IRX9 keyboard layout for WPF Canvas rendering.
///
/// Canvas: 1090 × 292 px  (ViewBox scales to fit control)
///
/// Pixel constants:
///   U  = 44   standard 1U key width
///   G  =  4   gap between keys
///   S  = 48   stride = U + G
///   FH = 36   F-key row height (slightly shorter)
///   H  = 42   normal row height
///   TH = 88   tall numpad key (2H + G, spans 2 rows)
///
/// Row Y anchors:
///   Y0 =   0   Function-key row
///   Y1 =  50   Number row      (10 px extra gap after F-row)
///   Y2 =  96   QWERTY row
///   Y3 = 142   Home row
///   Y4 = 188   Shift row
///   Y5 = 234   Bottom row
///
/// X section anchors:
///   Main keyboard : x =   0 … 716  (verified: all rows = 716 px wide)
///   Arrow cluster : x = 736 … 879
///   Numpad        : x = 884 … 1074
///
/// Zone mapping (LOQ 15IRX9 four-zone firmware):
///   0 – left      Esc F1-F4, `~1-4, Tab Q-R, CapsLk A-F, LShift Z-X, LCtrl/Fn/Win
///   1 – mid-left  F5-F8, 5-8, T-I, G-K, C-M+comma, LAlt, Spacebar
///   2 – mid-right F9-F12, 9 0 - = Bksp, O P [ ] \, L ; ' Enter, . / RShift, RAlt/RCtrl, Arrows
///   3 – right     Ins PrtSc Del Home End PgUp PgDn, full numpad
/// </summary>
public static class LoqKeyboardLayout
{
    // pixel constants
    private const double U  = 44;
    private const double G  =  4;
    private const double S  = U + G;  // 48

    private const double FH = 36;
    private const double H  = 42;
    private const double TH = H * 2 + G;  // 88

    // row Y anchors
    private const double Y0 =   0;
    private const double Y1 =  50;
    private const double Y2 =  96;
    private const double Y3 = 142;
    private const double Y4 = 188;
    private const double Y5 = 234;

    // section X anchors
    private const double ARR = 736;   // arrow cluster left X
    private const double NUM = 884;   // numpad left X

    // derived key widths
    private const double BkspW  =  92;   // Backspace: 716 − 13×48 = 92
    private const double TabW   =  66;   // Tab: 1.5U
    private const double BslshW =  70;   // Backslash fills QWERTY row to 716
    private const double CapsW  =  80;   // CapsLock: A starts at 84
    private const double EnterW = 104;   // Enter: 716 − 612
    private const double LShW   =  96;   // Left Shift: Z starts at 100
    private const double RShW   = 136;   // Right Shift: 716 − 580
    private const double ModW   =  54;   // bottom-row modifiers (~1.25U)
    private const double SpcW   = 368;   // Spacebar: 604 − 232 − 4

    public const double CanvasWidth  = 1090;
    public const double CanvasHeight =  292;

    public static List<KeyDef> CreateKeys()
    {
        var k = new List<KeyDef>(130);

        // ── Row 0: Function + navigation row ─────────────────────────────
        k.Add(K("Esc",   0,   Y0, U, FH, 0));

        // F1-F4 (zone 0) — small visual gap after Esc
        k.Add(K("F1",    80,  Y0, U, FH, 0));
        k.Add(K("F2",   124,  Y0, U, FH, 0));
        k.Add(K("F3",   168,  Y0, U, FH, 0));
        k.Add(K("F4",   212,  Y0, U, FH, 0));

        // F5-F8 (zone 1) — small gap after F4
        k.Add(K("F5",   264,  Y0, U, FH, 1));
        k.Add(K("F6",   308,  Y0, U, FH, 1));
        k.Add(K("F7",   352,  Y0, U, FH, 1));
        k.Add(K("F8",   396,  Y0, U, FH, 1));

        // F9-F12 (zone 2) — small gap after F8
        k.Add(K("F9",   448,  Y0, U, FH, 2));
        k.Add(K("F10",  492,  Y0, U, FH, 2));
        k.Add(K("F11",  536,  Y0, U, FH, 2));
        k.Add(K("F12",  580,  Y0, U, FH, 2));

        // Nav block: Ins/PrtSc/Del align over arrow columns (zone 2)
        k.Add(K("Ins",   ARR,        Y0, U, FH, 2));
        k.Add(K("PrtSc", ARR + S,    Y0, U, FH, 2));
        k.Add(K("Del",   ARR + S*2,  Y0, U, FH, 2));

        // Home/End/PgUp/PgDn align over numpad columns (zone 3)
        k.Add(K("Home",  NUM,        Y0, U, FH, 3));
        k.Add(K("End",   NUM + S,    Y0, U, FH, 3));
        k.Add(K("PgUp",  NUM + S*2,  Y0, U, FH, 3));
        k.Add(K("PgDn",  NUM + S*3,  Y0, U, FH, 3));

        // ── Row 1: Number row ─────────────────────────────────────────────
        // Main keyboard (x = 0..716)
        k.Add(K("~\n`",  0*S, Y1, U,     H, 0));
        k.Add(K("1",     1*S, Y1, U,     H, 0));
        k.Add(K("2",     2*S, Y1, U,     H, 0));
        k.Add(K("3",     3*S, Y1, U,     H, 0));
        k.Add(K("4",     4*S, Y1, U,     H, 0));
        k.Add(K("5",     5*S, Y1, U,     H, 1));
        k.Add(K("6",     6*S, Y1, U,     H, 1));
        k.Add(K("7",     7*S, Y1, U,     H, 1));
        k.Add(K("8",     8*S, Y1, U,     H, 1));
        k.Add(K("9",     9*S, Y1, U,     H, 2));
        k.Add(K("0",    10*S, Y1, U,     H, 2));
        k.Add(K("−",    11*S, Y1, U,     H, 2));
        k.Add(K("=",    12*S, Y1, U,     H, 2));
        k.Add(K("Bksp", 13*S, Y1, BkspW, H, 2));

        // Numpad top (zone 3)
        k.Add(K("Num\nLock", NUM,        Y1, U, H, 3));
        k.Add(K("/",          NUM + S,    Y1, U, H, 3));
        k.Add(K("*",          NUM + S*2,  Y1, U, H, 3));
        k.Add(K("−",          NUM + S*3,  Y1, U, H, 3));

        // ── Row 2: QWERTY ─────────────────────────────────────────────────
        double qx = TabW + G;  // = 70 (Q left edge)
        k.Add(K("Tab",  0,          Y2, TabW,   H, 0));
        k.Add(K("Q",    qx + 0*S,   Y2, U,      H, 0));
        k.Add(K("W",    qx + 1*S,   Y2, U,      H, 0));
        k.Add(K("E",    qx + 2*S,   Y2, U,      H, 0));
        k.Add(K("R",    qx + 3*S,   Y2, U,      H, 0));
        k.Add(K("T",    qx + 4*S,   Y2, U,      H, 1));
        k.Add(K("Y",    qx + 5*S,   Y2, U,      H, 1));
        k.Add(K("U",    qx + 6*S,   Y2, U,      H, 1));
        k.Add(K("I",    qx + 7*S,   Y2, U,      H, 1));
        k.Add(K("O",    qx + 8*S,   Y2, U,      H, 2));
        k.Add(K("P",    qx + 9*S,   Y2, U,      H, 2));
        k.Add(K("[",    qx + 10*S,  Y2, U,      H, 2));
        k.Add(K("]",    qx + 11*S,  Y2, U,      H, 2));
        k.Add(K("\\",   qx + 12*S,  Y2, BslshW, H, 2));   // fills row to 716

        // Numpad 7/8/9 + tall (+) spans rows 2–3
        k.Add(K("7",  NUM,        Y2, U, H,  3));
        k.Add(K("8",  NUM + S,    Y2, U, H,  3));
        k.Add(K("9",  NUM + S*2,  Y2, U, H,  3));
        k.Add(K("+",  NUM + S*3,  Y2, U, TH, 3));   // tall: rows 2-3

        // ── Row 3: Home row ───────────────────────────────────────────────
        double ax = CapsW + G;  // = 84 (A left edge)
        k.Add(K("Caps",  0,          Y3, CapsW,  H, 0));
        k.Add(K("A",     ax + 0*S,   Y3, U,      H, 0));
        k.Add(K("S",     ax + 1*S,   Y3, U,      H, 0));
        k.Add(K("D",     ax + 2*S,   Y3, U,      H, 0));
        k.Add(K("F",     ax + 3*S,   Y3, U,      H, 0));
        k.Add(K("G",     ax + 4*S,   Y3, U,      H, 1));
        k.Add(K("H",     ax + 5*S,   Y3, U,      H, 1));
        k.Add(K("J",     ax + 6*S,   Y3, U,      H, 1));
        k.Add(K("K",     ax + 7*S,   Y3, U,      H, 1));
        k.Add(K("L",     ax + 8*S,   Y3, U,      H, 2));
        k.Add(K(";",     ax + 9*S,   Y3, U,      H, 2));
        k.Add(K("'",     ax + 10*S,  Y3, U,      H, 2));
        k.Add(K("Enter", ax + 11*S,  Y3, EnterW, H, 2));  // x=612, fills to 716

        // Numpad 4/5/6 (zone 3)  — (+) continues from row 2
        k.Add(K("4", NUM,        Y3, U, H, 3));
        k.Add(K("5", NUM + S,    Y3, U, H, 3));
        k.Add(K("6", NUM + S*2,  Y3, U, H, 3));

        // ── Row 4: Shift row ──────────────────────────────────────────────
        double zx = LShW + G;  // = 100 (Z left edge)
        k.Add(K("Shift", 0,         Y4, LShW, H, 0));
        k.Add(K("Z",     zx + 0*S,  Y4, U,    H, 0));
        k.Add(K("X",     zx + 1*S,  Y4, U,    H, 0));
        k.Add(K("C",     zx + 2*S,  Y4, U,    H, 1));
        k.Add(K("V",     zx + 3*S,  Y4, U,    H, 1));
        k.Add(K("B",     zx + 4*S,  Y4, U,    H, 1));
        k.Add(K("N",     zx + 5*S,  Y4, U,    H, 1));
        k.Add(K("M",     zx + 6*S,  Y4, U,    H, 1));
        k.Add(K(",",     zx + 7*S,  Y4, U,    H, 2));
        k.Add(K(".",     zx + 8*S,  Y4, U,    H, 2));
        k.Add(K("/",     zx + 9*S,  Y4, U,    H, 2));
        k.Add(K("Shift", 580,        Y4, RShW,  H, 2));

        // ↑ arrow sits between RShift (ends at 716) and numpad (zone 2)
        k.Add(K("↑", ARR + S, Y4, U, H, 2));  // x = 784

        // Numpad 1/2/3 + tall Enter spans rows 4–5
        k.Add(K("1",     NUM,        Y4, U, H,  3));
        k.Add(K("2",     NUM + S,    Y4, U, H,  3));
        k.Add(K("3",     NUM + S*2,  Y4, U, H,  3));
        k.Add(K("Enter", NUM + S*3,  Y4, U, TH, 3));  // tall: rows 4-5

        // ── Row 5: Bottom row ─────────────────────────────────────────────
        k.Add(K("Ctrl", 0,   Y5, ModW, H, 0));
        k.Add(K("Fn",   58,  Y5, ModW, H, 0));
        k.Add(K("Win",  116, Y5, ModW, H, 0));
        k.Add(K("Alt",  174, Y5, ModW, H, 1));
        // Spacebar — single unified key, zone 1 (center ≈ x 416)
        k.Add(K("",     232, Y5, SpcW,  H, 1));
        k.Add(K("Alt",  604, Y5, ModW,  H, 2));
        k.Add(K("Ctrl", 662, Y5, ModW,  H, 2));

        // Arrow cluster ← ↓ → (zone 2)
        k.Add(K("←", ARR,        Y5, U, H, 2));  // x = 736
        k.Add(K("↓", ARR + S,    Y5, U, H, 2));  // x = 784
        k.Add(K("→", ARR + S*2,  Y5, U, H, 2));  // x = 832

        // Numpad bottom row (zone 3) — NumEnter continues from row 4
        k.Add(K("0", NUM,       Y5, U*2+G, H, 3));  // 0: 2U wide = 92 px
        k.Add(K(".", NUM + S*2, Y5, U,     H, 3));  // x = 980

        return k;
    }

    private static KeyDef K(string label, double x, double y, double w, double h, int zone)
        => new(label, x, y, w, h, zone);
}
