using System.Collections.Generic;

namespace LenovoLegionToolkit.WPF.Controls.KeyboardBacklight.RGB;

/// <summary>
/// Key definition: label, absolute pixel position/size on canvas, zone index (0-3).
/// </summary>
public readonly record struct KeyDef(string Label, double X, double Y, double W, double H, int Zone);

/// <summary>
/// Pixel-accurate LOQ 15IRX9 full-size keyboard layout.
///
/// All positions are calculated from unit-based widths:
///   U  = 48 px  standard 1U key width
///   G  =  4 px  gap between keys
///   S  = 52 px  stride (U + G)
///
/// Key width formula:  Px(units) = units × S − G
///   1U    =  48    1.25U =  61    1.5U  =  74    1.75U =  87
///   2U    = 100    2.25U = 113    2.75U = 139    7.5U  = 386
///
/// Row heights:
///   FH = 34  function-key row (shorter)
///   H  = 46  all other rows
///   TH = 96  tall numpad key (2H + G)
///
/// Row Y offsets:
///   Y0 =   0  Function row
///   Y1 =  46  Number row    (12 px gap after F-row)
///   Y2 =  96  QWERTY row
///   Y3 = 146  Home row
///   Y4 = 196  Shift row
///   Y5 = 246  Bottom row
///
/// Section X anchors:
///   Main keyboard :   0 … 776  (15U per row, verified)
///   Arrow cluster : 800 … 952  (24 px gap after main)
///   Numpad        : 968 … 1172 (16 px gap after arrows)
///
/// Canvas: 1172 × 292 px
///
/// Zone mapping (LOQ 15IRX9 four-zone firmware):
///   Zone 0 (left):      Esc F1-F4, `1-4, Tab Q-R, Caps A-F, LShift Z-X, Ctrl/Fn/Win
///   Zone 1 (mid-left):  F5-F8, 5-8, T-I, G-K, C-M, Alt, Spacebar
///   Zone 2 (mid-right): F9-F12, 9-= Bksp, O-\, L-Enter, ,-RShift, RAlt/RCtrl, Arrows
///   Zone 3 (right):     Ins-PgDn (nav), full Numpad
/// </summary>
public static class LoqKeyboardLayout
{
    // ── pixel constants ───────────────────────────────────────────────────
    private const double U  = 48;
    private const double G  =  4;
    private const double S  = U + G;  // 52

    private const double FH = 34;     // F-key row height
    private const double H  = 46;     // normal row height
    private const double TH = H * 2 + G;  // 96  tall numpad key

    // row Y positions
    private const double Y0 =   0;
    private const double Y1 =  46;
    private const double Y2 =  96;
    private const double Y3 = 146;
    private const double Y4 = 196;
    private const double Y5 = 246;

    // section X anchors
    private const double AX = 800;    // arrow cluster
    private const double NX = 968;    // numpad

    public const double CanvasWidth  = 1172;
    public const double CanvasHeight =  292;

    /// <summary>Width in pixels for a key spanning <paramref name="units"/> standard units.</summary>
    private static double Px(double units) => units * S - G;

    public static List<KeyDef> CreateKeys()
    {
        var k = new List<KeyDef>(110);

        // ── Row 0: Function + navigation row  (Y=0, H=34) ───────────────
        // F-row main area spans exactly 776 px:
        //   Esc(48) gap(44) [F1–F4: 4×48+3×4=204] gap(36) [F5–F8: 204] gap(36) [F9–F12: 204]
        //   = 48 + 44 + 204 + 36 + 204 + 36 + 204 = 776 ✓
        k.Add(K("Esc",  0,   Y0, U, FH, 0));

        k.Add(K("F1",   92,  Y0, U, FH, 0));
        k.Add(K("F2",  144,  Y0, U, FH, 0));
        k.Add(K("F3",  196,  Y0, U, FH, 0));
        k.Add(K("F4",  248,  Y0, U, FH, 0));

        k.Add(K("F5",  332,  Y0, U, FH, 1));
        k.Add(K("F6",  384,  Y0, U, FH, 1));
        k.Add(K("F7",  436,  Y0, U, FH, 1));
        k.Add(K("F8",  488,  Y0, U, FH, 1));

        k.Add(K("F9",  572,  Y0, U, FH, 2));
        k.Add(K("F10", 624,  Y0, U, FH, 2));
        k.Add(K("F11", 676,  Y0, U, FH, 2));
        k.Add(K("F12", 728,  Y0, U, FH, 2));

        // Nav keys above arrows (zone 3)
        k.Add(K("Ins",   AX,        Y0, U, FH, 3));
        k.Add(K("PrtSc", AX + S,    Y0, U, FH, 3));
        k.Add(K("Del",   AX + S*2,  Y0, U, FH, 3));

        // Nav keys above numpad (zone 3)
        k.Add(K("Home",  NX,        Y0, U, FH, 3));
        k.Add(K("End",   NX + S,    Y0, U, FH, 3));
        k.Add(K("PgUp",  NX + S*2,  Y0, U, FH, 3));
        k.Add(K("PgDn",  NX + S*3,  Y0, U, FH, 3));

        // ── Row 1: Number row  (Y=46, H=46) ─────────────────────────────
        // 13×1U + Bksp(2U) = 15U → 14×S + Px(2)−S+G = 776 ✓
        k.Add(K("`",   0*S,  Y1, U,        H, 0));
        k.Add(K("1",   1*S,  Y1, U,        H, 0));
        k.Add(K("2",   2*S,  Y1, U,        H, 0));
        k.Add(K("3",   3*S,  Y1, U,        H, 0));
        k.Add(K("4",   4*S,  Y1, U,        H, 0));
        k.Add(K("5",   5*S,  Y1, U,        H, 1));
        k.Add(K("6",   6*S,  Y1, U,        H, 1));
        k.Add(K("7",   7*S,  Y1, U,        H, 1));
        k.Add(K("8",   8*S,  Y1, U,        H, 1));
        k.Add(K("9",   9*S,  Y1, U,        H, 2));
        k.Add(K("0",  10*S,  Y1, U,        H, 2));
        k.Add(K("-",  11*S,  Y1, U,        H, 2));
        k.Add(K("=",  12*S,  Y1, U,        H, 2));
        k.Add(K("Bksp",13*S, Y1, Px(2),    H, 2));  // 100 px, right=776

        // Numpad top (zone 3)
        k.Add(K("Num",  NX,        Y1, U, H, 3));
        k.Add(K("/",    NX + S,    Y1, U, H, 3));
        k.Add(K("*",    NX + S*2,  Y1, U, H, 3));
        k.Add(K("-",    NX + S*3,  Y1, U, H, 3));

        // ── Row 2: QWERTY  (Y=96, H=46) ─────────────────────────────────
        // Tab(1.5U) + 12×1U + \(1.5U) = 15U → 776 ✓
        double qx = Px(1.5) + G;  // 78  (Q left edge)
        k.Add(K("Tab", 0,           Y2, Px(1.5), H, 0));
        k.Add(K("Q",   qx + 0*S,   Y2, U,       H, 0));
        k.Add(K("W",   qx + 1*S,   Y2, U,       H, 0));
        k.Add(K("E",   qx + 2*S,   Y2, U,       H, 0));
        k.Add(K("R",   qx + 3*S,   Y2, U,       H, 0));
        k.Add(K("T",   qx + 4*S,   Y2, U,       H, 1));
        k.Add(K("Y",   qx + 5*S,   Y2, U,       H, 1));
        k.Add(K("U",   qx + 6*S,   Y2, U,       H, 1));
        k.Add(K("I",   qx + 7*S,   Y2, U,       H, 1));
        k.Add(K("O",   qx + 8*S,   Y2, U,       H, 2));
        k.Add(K("P",   qx + 9*S,   Y2, U,       H, 2));
        k.Add(K("[",   qx + 10*S,  Y2, U,       H, 2));
        k.Add(K("]",   qx + 11*S,  Y2, U,       H, 2));
        k.Add(K("\\",  qx + 12*S,  Y2, Px(1.5), H, 2));  // right=776

        // Numpad 7/8/9 (zone 3), + is tall spanning rows 2–3
        k.Add(K("7",  NX,        Y2, U,  H,  3));
        k.Add(K("8",  NX + S,    Y2, U,  H,  3));
        k.Add(K("9",  NX + S*2,  Y2, U,  H,  3));
        k.Add(K("+",  NX + S*3,  Y2, U,  TH, 3));

        // ── Row 3: Home row  (Y=146, H=46) ──────────────────────────────
        // Caps(1.75U) + 11×1U + Enter(2.25U) = 15U → 776 ✓
        double ax = Px(1.75) + G;  // 91  (A left edge)
        k.Add(K("Caps",  0,          Y3, Px(1.75), H, 0));
        k.Add(K("A",     ax + 0*S,   Y3, U,        H, 0));
        k.Add(K("S",     ax + 1*S,   Y3, U,        H, 0));
        k.Add(K("D",     ax + 2*S,   Y3, U,        H, 0));
        k.Add(K("F",     ax + 3*S,   Y3, U,        H, 0));
        k.Add(K("G",     ax + 4*S,   Y3, U,        H, 1));
        k.Add(K("H",     ax + 5*S,   Y3, U,        H, 1));
        k.Add(K("J",     ax + 6*S,   Y3, U,        H, 1));
        k.Add(K("K",     ax + 7*S,   Y3, U,        H, 1));
        k.Add(K("L",     ax + 8*S,   Y3, U,        H, 2));
        k.Add(K(";",     ax + 9*S,   Y3, U,        H, 2));
        k.Add(K("'",     ax + 10*S,  Y3, U,        H, 2));
        k.Add(K("Enter", ax + 11*S,  Y3, Px(2.25), H, 2));  // right=776

        // Numpad 4/5/6 (zone 3)
        k.Add(K("4",  NX,        Y3, U, H, 3));
        k.Add(K("5",  NX + S,    Y3, U, H, 3));
        k.Add(K("6",  NX + S*2,  Y3, U, H, 3));

        // ── Row 4: Shift row  (Y=196, H=46) ─────────────────────────────
        // LShift(2.25U) + 10×1U + RShift(2.75U) = 15U → 776 ✓
        double zx = Px(2.25) + G;  // 117  (Z left edge)
        k.Add(K("Shift", 0,          Y4, Px(2.25), H, 0));
        k.Add(K("Z",     zx + 0*S,   Y4, U,        H, 0));
        k.Add(K("X",     zx + 1*S,   Y4, U,        H, 0));
        k.Add(K("C",     zx + 2*S,   Y4, U,        H, 1));
        k.Add(K("V",     zx + 3*S,   Y4, U,        H, 1));
        k.Add(K("B",     zx + 4*S,   Y4, U,        H, 1));
        k.Add(K("N",     zx + 5*S,   Y4, U,        H, 1));
        k.Add(K("M",     zx + 6*S,   Y4, U,        H, 1));
        k.Add(K(",",     zx + 7*S,   Y4, U,        H, 2));
        k.Add(K(".",     zx + 8*S,   Y4, U,        H, 2));
        k.Add(K("/",     zx + 9*S,   Y4, U,        H, 2));
        k.Add(K("Shift", zx + 10*S,  Y4, Px(2.75), H, 2));  // right=776

        // ↑ arrow above ↓  (zone 2)
        k.Add(K("↑",  AX + S, Y4, U, H, 2));

        // Numpad 1/2/3 (zone 3), Enter is tall spanning rows 4–5
        k.Add(K("1",     NX,        Y4, U,  H,  3));
        k.Add(K("2",     NX + S,    Y4, U,  H,  3));
        k.Add(K("3",     NX + S*2,  Y4, U,  H,  3));
        k.Add(K("Enter", NX + S*3,  Y4, U,  TH, 3));

        // ── Row 5: Bottom row  (Y=246, H=46) ────────────────────────────
        // Ctrl(1.25U) Fn(1.25U) Win(1.25U) Alt(1.25U) Space(7.5U) Alt(1.25U) Ctrl(1.25U) = 15U → 776 ✓
        double mw = Px(1.25);  // 61
        k.Add(K("Ctrl", 0*S,              Y5, mw, H, 0));
        k.Add(K("Fn",   mw + G,           Y5, mw, H, 0));
        k.Add(K("Win",  2*(mw + G),       Y5, mw, H, 0));
        k.Add(K("Alt",  3*(mw + G),       Y5, mw, H, 1));
        // Spacebar: single key, zone 1 (center ≈ x 453)
        k.Add(K("",     4*(mw + G),       Y5, Px(7.5), H, 1));
        // Right mods: Alt at 776 − 2×(mw+G) + G = 650, Ctrl at 776 − mw = 715
        k.Add(K("Alt",  776 - 2*mw - G,   Y5, mw, H, 2));
        k.Add(K("Ctrl", 776 - mw,         Y5, mw, H, 2));

        // Arrow cluster ← ↓ →  (zone 2)
        k.Add(K("←",  AX,        Y5, U, H, 2));
        k.Add(K("↓",  AX + S,    Y5, U, H, 2));
        k.Add(K("→",  AX + S*2,  Y5, U, H, 2));

        // Numpad 0(2U wide) and .  (zone 3)
        k.Add(K("0",  NX,        Y5, Px(2), H, 3));
        k.Add(K(".",  NX + S*2,  Y5, U,     H, 3));

        return k;
    }

    private static KeyDef K(string label, double x, double y, double w, double h, int zone)
        => new(label, x, y, w, h, zone);
}
