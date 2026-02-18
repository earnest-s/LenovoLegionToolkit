using System.Collections.Generic;

namespace LenovoLegionToolkit.WPF.Controls.KeyboardBacklight.RGB;

/// <summary>
/// Key definition with dual legends: primary label (centered / bottom)
/// and optional secondary label (top area, smaller — shifted symbol or Fn function).
/// X/Y/W/H are absolute canvas pixels computed from unit-based widths.
/// </summary>
public readonly record struct KeyDef(
    string Primary,
    string? Secondary,
    double X, double Y,
    double W, double H,
    int Zone);

/// <summary>
/// Pixel-accurate LOQ 15IRX9 full-size keyboard layout — 101 keys.
///
/// Unit system:
///   U  = 48 px   standard 1U key
///   G  =  4 px   gap between keys
///   S  = 52 px   stride (U + G)
///   Px(units) = units × S − G
///
/// Standard key widths (units → pixels):
///   1U    =  48    Tab     1.5U =  74    Caps  1.75U =  87
///   Bksp  2U = 100    LShift 2.25U = 113    Enter 2.25U = 113
///   RShift 2.75U = 139    Mod(Ctrl/Fn/Win/Alt) 1.25U = 61
///   Spacebar 7.5U = 386    Numpad 0  2U = 100
///
/// Row heights:
///   FH = 34   function-key row
///   H  = 46   all other rows
///   TH = 96   tall numpad keys (+ and Enter, spanning 2 rows)
///
/// Row Y offsets:
///   Y0 =   0   F-row          Y3 = 146  Home row
///   Y1 =  46   Number row     Y4 = 196  Shift row
///   Y2 =  96   QWERTY row     Y5 = 246  Bottom row
///
/// Canvas: 1172 × 292 px.  Sections:
///   Main keyboard  :   0 … 776     (15U, all rows verified)
///   Arrow cluster  : 800 … 904     (inverted-T)
///   Numpad         : 968 … 1172    (4 columns)
///
/// Zone mapping (LOQ 15IRX9 four-zone firmware):
///   0 – left       Esc F1-F4 | `1234 | Tab Q-R | Caps A-F | LShift Z-X | Ctrl Fn Win
///   1 – mid-left   F5-F8 | 5-8 | T-I | G-K | C-M | Alt Spacebar
///   2 – mid-right  F9-F12 | 9 0 - = Bksp | O P [ ] \ | L ; ' Enter | , . / RShift | RAlt RCtrl | Arrows
///   3 – right      Ins PrtSc Del Home End PgUp PgDn | full Numpad
/// </summary>
public static class LoqKeyboardLayout
{
    private const double U  = 48;
    private const double G  =  4;
    private const double S  = U + G;   // 52

    private const double FH = 34;      // F-key row height
    private const double H  = 46;      // normal row height
    private const double TH = H * 2 + G; // 96: tall key (2 rows)

    private const double Y0 =   0;
    private const double Y1 =  46;
    private const double Y2 =  96;
    private const double Y3 = 146;
    private const double Y4 = 196;
    private const double Y5 = 246;

    private const double AX = 800;     // arrow cluster left X
    private const double NX = 968;     // numpad left X

    public const double CanvasWidth  = 1172;
    public const double CanvasHeight =  292;

    /// <summary>Width in pixels for a key spanning <paramref name="units"/> standard units.</summary>
    private static double Px(double units) => units * S - G;

    public static List<KeyDef> CreateKeys()
    {
        var k = new List<KeyDef>(110);

        // ═══════════════════════════════════════════════════════════════════
        //  Row 0 — Function keys  (Y=0, H=34)
        //  Esc(48) + 44gap + [F1-F4: 204] + 36gap + [F5-F8: 204] + 36gap + [F9-F12: 204] = 776
        // ═══════════════════════════════════════════════════════════════════
        k.Add(K("Esc",  "FnLk",  0,   Y0, U, FH, 0));

        k.Add(K("F1",   "Mute",  92,  Y0, U, FH, 0));
        k.Add(K("F2",   "Vol-",  144, Y0, U, FH, 0));
        k.Add(K("F3",   "Vol+",  196, Y0, U, FH, 0));
        k.Add(K("F4",   "Mic",   248, Y0, U, FH, 0));

        k.Add(K("F5",   "Brt-",  332, Y0, U, FH, 1));
        k.Add(K("F6",   "Brt+",  384, Y0, U, FH, 1));
        k.Add(K("F7",   "Proj",  436, Y0, U, FH, 1));
        k.Add(K("F8",   "Air",   488, Y0, U, FH, 1));

        k.Add(K("F9",   "Pad",   572, Y0, U, FH, 2));
        k.Add(K("F10",  "Lock",  624, Y0, U, FH, 2));
        k.Add(K("F11",  "Snip",  676, Y0, U, FH, 2));
        k.Add(K("F12",  "Calc",  728, Y0, U, FH, 2));

        // Nav above arrows (zone 3)
        k.Add(K("Ins",   null, AX,        Y0, U, FH, 3));
        k.Add(K("PrtSc", null, AX + S,    Y0, U, FH, 3));
        k.Add(K("Del",   null, AX + S*2,  Y0, U, FH, 3));

        // Nav above numpad (zone 3)
        k.Add(K("Home", null, NX,        Y0, U, FH, 3));
        k.Add(K("End",  null, NX + S,    Y0, U, FH, 3));
        k.Add(K("PgUp", null, NX + S*2,  Y0, U, FH, 3));
        k.Add(K("PgDn", null, NX + S*3,  Y0, U, FH, 3));

        // ═══════════════════════════════════════════════════════════════════
        //  Row 1 — Number row  (Y=46, H=46)
        //  13×1U + Bksp(2U) = 15U → 776 px
        // ═══════════════════════════════════════════════════════════════════
        k.Add(K("`",    "~",  0*S,  Y1, U,      H, 0));
        k.Add(K("1",    "!",  1*S,  Y1, U,      H, 0));
        k.Add(K("2",    "@",  2*S,  Y1, U,      H, 0));
        k.Add(K("3",    "#",  3*S,  Y1, U,      H, 0));
        k.Add(K("4",    "$",  4*S,  Y1, U,      H, 0));
        k.Add(K("5",    "%",  5*S,  Y1, U,      H, 1));
        k.Add(K("6",    "^",  6*S,  Y1, U,      H, 1));
        k.Add(K("7",    "&",  7*S,  Y1, U,      H, 1));
        k.Add(K("8",    "*",  8*S,  Y1, U,      H, 1));
        k.Add(K("9",    "(",  9*S,  Y1, U,      H, 2));
        k.Add(K("0",    ")", 10*S,  Y1, U,      H, 2));
        k.Add(K("-",    "_", 11*S,  Y1, U,      H, 2));
        k.Add(K("=",    "+", 12*S,  Y1, U,      H, 2));
        k.Add(K("Backspace", null, 13*S, Y1, Px(2), H, 2));

        // Numpad top (zone 3)
        k.Add(K("Num",  "Lock", NX,       Y1, U, H, 3));
        k.Add(K("/",    null,   NX + S,   Y1, U, H, 3));
        k.Add(K("*",    null,   NX + S*2, Y1, U, H, 3));
        k.Add(K("-",    null,   NX + S*3, Y1, U, H, 3));

        // ═══════════════════════════════════════════════════════════════════
        //  Row 2 — QWERTY  (Y=96, H=46)
        //  Tab(1.5U) + 12×1U + \(1.5U) = 15U → 776 px
        // ═══════════════════════════════════════════════════════════════════
        double qx = Px(1.5) + G;  // 78
        k.Add(K("Tab",  null,  0,          Y2, Px(1.5), H, 0));
        k.Add(K("Q",    null,  qx + 0*S,  Y2, U,       H, 0));
        k.Add(K("W",    null,  qx + 1*S,  Y2, U,       H, 0));
        k.Add(K("E",    null,  qx + 2*S,  Y2, U,       H, 0));
        k.Add(K("R",    null,  qx + 3*S,  Y2, U,       H, 0));
        k.Add(K("T",    null,  qx + 4*S,  Y2, U,       H, 1));
        k.Add(K("Y",    null,  qx + 5*S,  Y2, U,       H, 1));
        k.Add(K("U",    null,  qx + 6*S,  Y2, U,       H, 1));
        k.Add(K("I",    null,  qx + 7*S,  Y2, U,       H, 1));
        k.Add(K("O",    null,  qx + 8*S,  Y2, U,       H, 2));
        k.Add(K("P",    null,  qx + 9*S,  Y2, U,       H, 2));
        k.Add(K("[",    "{",   qx + 10*S, Y2, U,       H, 2));
        k.Add(K("]",    "}",   qx + 11*S, Y2, U,       H, 2));
        k.Add(K("\\",   "|",   qx + 12*S, Y2, Px(1.5), H, 2));

        // Numpad 7/8/9 (zone 3) — + is tall (rows 2–3)
        k.Add(K("7",  "Home",  NX,        Y2, U,  H,  3));
        k.Add(K("8",  "\u2191", NX + S,   Y2, U,  H,  3));  // ↑
        k.Add(K("9",  "PgUp",  NX + S*2,  Y2, U,  H,  3));
        k.Add(K("+",  null,     NX + S*3,  Y2, U,  TH, 3));

        // ═══════════════════════════════════════════════════════════════════
        //  Row 3 — Home row  (Y=146, H=46)
        //  Caps(1.75U) + 11×1U + Enter(2.25U) = 15U → 776 px
        // ═══════════════════════════════════════════════════════════════════
        double ax = Px(1.75) + G;  // 91
        k.Add(K("CapsLk", null, 0,          Y3, Px(1.75), H, 0));
        k.Add(K("A",      null, ax + 0*S,   Y3, U,        H, 0));
        k.Add(K("S",      null, ax + 1*S,   Y3, U,        H, 0));
        k.Add(K("D",      null, ax + 2*S,   Y3, U,        H, 0));
        k.Add(K("F",      null, ax + 3*S,   Y3, U,        H, 0));
        k.Add(K("G",      null, ax + 4*S,   Y3, U,        H, 1));
        k.Add(K("H",      null, ax + 5*S,   Y3, U,        H, 1));
        k.Add(K("J",      null, ax + 6*S,   Y3, U,        H, 1));
        k.Add(K("K",      null, ax + 7*S,   Y3, U,        H, 1));
        k.Add(K("L",      null, ax + 8*S,   Y3, U,        H, 2));
        k.Add(K(";",      ":",  ax + 9*S,   Y3, U,        H, 2));
        k.Add(K("'",      "\"", ax + 10*S,  Y3, U,        H, 2));
        k.Add(K("Enter",  null, ax + 11*S,  Y3, Px(2.25), H, 2));

        // Numpad 4/5/6 (zone 3)
        k.Add(K("4",  "\u2190", NX,        Y3, U, H, 3));  // ←
        k.Add(K("5",  null,     NX + S,    Y3, U, H, 3));
        k.Add(K("6",  "\u2192", NX + S*2,  Y3, U, H, 3));  // →

        // ═══════════════════════════════════════════════════════════════════
        //  Row 4 — Shift row  (Y=196, H=46)
        //  LShift(2.25U) + 10×1U + RShift(2.75U) = 15U → 776 px
        // ═══════════════════════════════════════════════════════════════════
        double zx = Px(2.25) + G;  // 117
        k.Add(K("Shift", null, 0,          Y4, Px(2.25), H, 0));
        k.Add(K("Z",     null, zx + 0*S,  Y4, U,        H, 0));
        k.Add(K("X",     null, zx + 1*S,  Y4, U,        H, 0));
        k.Add(K("C",     null, zx + 2*S,  Y4, U,        H, 1));
        k.Add(K("V",     null, zx + 3*S,  Y4, U,        H, 1));
        k.Add(K("B",     null, zx + 4*S,  Y4, U,        H, 1));
        k.Add(K("N",     null, zx + 5*S,  Y4, U,        H, 1));
        k.Add(K("M",     null, zx + 6*S,  Y4, U,        H, 1));
        k.Add(K(",",     "<",  zx + 7*S,  Y4, U,        H, 2));
        k.Add(K(".",     ">",  zx + 8*S,  Y4, U,        H, 2));
        k.Add(K("/",     "?",  zx + 9*S,  Y4, U,        H, 2));
        k.Add(K("Shift", null, zx + 10*S, Y4, Px(2.75), H, 2));

        // ↑ arrow (zone 2) — above ↓ in inverted-T cluster
        k.Add(K("\u2191", null, AX + S, Y4, U, H, 2));

        // Numpad 1/2/3 (zone 3) — Enter is tall (rows 4–5)
        k.Add(K("1",     "End",     NX,        Y4, U,  H,  3));
        k.Add(K("2",     "\u2193",  NX + S,    Y4, U,  H,  3));  // ↓
        k.Add(K("3",     "PgDn",    NX + S*2,  Y4, U,  H,  3));
        k.Add(K("Enter", null,      NX + S*3,  Y4, U,  TH, 3));

        // ═══════════════════════════════════════════════════════════════════
        //  Row 5 — Bottom row  (Y=246, H=46)
        //  3×Mod(1.25U) + Alt(1.25U) + Space(7.5U) + Alt(1.25U) + Ctrl(1.25U) = 15U → 776 px
        // ═══════════════════════════════════════════════════════════════════
        double mw = Px(1.25);  // 61
        k.Add(K("Ctrl", null,  0,               Y5, mw,      H, 0));
        k.Add(K("Fn",   null,  mw + G,          Y5, mw,      H, 0));
        k.Add(K("Win",  null,  2*(mw + G),      Y5, mw,      H, 0));
        k.Add(K("Alt",  null,  3*(mw + G),      Y5, mw,      H, 1));
        k.Add(K("",     null,  4*(mw + G),      Y5, Px(7.5), H, 1));  // Spacebar — single key
        k.Add(K("Alt",  null,  776 - 2*mw - G,  Y5, mw,      H, 2));
        k.Add(K("Ctrl", null,  776 - mw,        Y5, mw,      H, 2));

        // Arrow cluster ← ↓ → (zone 2)
        k.Add(K("\u2190", null, AX,        Y5, U, H, 2));
        k.Add(K("\u2193", null, AX + S,    Y5, U, H, 2));
        k.Add(K("\u2192", null, AX + S*2,  Y5, U, H, 2));

        // Numpad bottom (zone 3) — 0 is 2U wide
        k.Add(K("0",  "Ins",  NX,        Y5, Px(2), H, 3));
        k.Add(K(".",  "Del",  NX + S*2,  Y5, U,     H, 3));

        return k;
    }

    private static KeyDef K(string primary, string? secondary,
                            double x, double y, double w, double h, int zone)
        => new(primary, secondary, x, y, w, h, zone);
}
