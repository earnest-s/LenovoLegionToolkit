using System.Collections.Generic;

namespace LenovoLegionToolkit.WPF.Controls.KeyboardBacklight.RGB;

/// <summary>
/// Physical LOQ keyboard key layout with accurate proportions.
/// Coordinates are in abstract units; renderer scales to fit.
/// Total canvas: 1520 × 500. Gap between keys: 4u. Standard key: 64×64.
///
/// Zone mapping matches Lenovo LOQ 15IRX9 4-zone backlight:
///   Zone 0 (leftmost ~25%) : Esc–F4, `–5, Tab–T, CapsLk–G, LShift–B, LCtrl–Space(left)
///   Zone 1 (~25%)          : F5–F8, 6–0, Y–P, H–', N–/, Space(right)–RCtrl
///   Zone 2 (~25%)          : F9–F12, -, =, Bksp, [, ], \, Enter, RShift, arrows
///   Zone 3 (rightmost ~25%): Insert–PgDn, Del–PgDn row, NumLk–Numpad
/// </summary>
public static class LoqKeyboardLayout
{
    // Layout constants
    private const double U = 64;   // 1 key unit
    private const double G = 4;    // gap between keys
    private const double S = U + G; // stride

    public const double CanvasWidth = 1520;
    public const double CanvasHeight = 500;

    public static List<KeyDefinition> CreateKeys()
    {
        var keys = new List<KeyDefinition>(100);

        // ── Row 0: Function keys ─────────────────────────────────────
        double y = 0;
        // Zone 0
        keys.Add(new("Esc", 0, y, U, U, 0));
        keys.Add(new("F1", S * 1.5, y, U, U, 0));
        keys.Add(new("F2", S * 2.5, y, U, U, 0));
        keys.Add(new("F3", S * 3.5, y, U, U, 0));
        keys.Add(new("F4", S * 4.5, y, U, U, 0));
        // Zone 1
        keys.Add(new("F5", S * 5.75, y, U, U, 1));
        keys.Add(new("F6", S * 6.75, y, U, U, 1));
        keys.Add(new("F7", S * 7.75, y, U, U, 1));
        keys.Add(new("F8", S * 8.75, y, U, U, 1));
        // Zone 2
        keys.Add(new("F9", S * 10, y, U, U, 2));
        keys.Add(new("F10", S * 11, y, U, U, 2));
        keys.Add(new("F11", S * 12, y, U, U, 2));
        keys.Add(new("F12", S * 13, y, U, U, 2));
        // Zone 3
        keys.Add(new("Ins", S * 14.25, y, U, U, 3));
        keys.Add(new("PrtSc", S * 15.25, y, U, U, 3));
        keys.Add(new("Del", S * 16.25, y, U, U, 3));
        keys.Add(new("Home", S * 17.25, y, U, U, 3));
        keys.Add(new("End", S * 18.25, y, U, U, 3));
        keys.Add(new("PgUp", S * 19.25, y, U, U, 3));
        keys.Add(new("PgDn", S * 20.25, y, U, U, 3));

        // ── Row 1: Number row ────────────────────────────────────────
        y = S;
        // Zone 0
        keys.Add(new("~", 0, y, U, U, 0));
        keys.Add(new("1", S, y, U, U, 0));
        keys.Add(new("2", S * 2, y, U, U, 0));
        keys.Add(new("3", S * 3, y, U, U, 0));
        keys.Add(new("4", S * 4, y, U, U, 0));
        keys.Add(new("5", S * 5, y, U, U, 0));
        // Zone 1
        keys.Add(new("6", S * 6, y, U, U, 1));
        keys.Add(new("7", S * 7, y, U, U, 1));
        keys.Add(new("8", S * 8, y, U, U, 1));
        keys.Add(new("9", S * 9, y, U, U, 1));
        keys.Add(new("0", S * 10, y, U, U, 1));
        // Zone 2
        keys.Add(new("-", S * 11, y, U, U, 2));
        keys.Add(new("=", S * 12, y, U, U, 2));
        keys.Add(new("Bksp", S * 13, y, U * 2 + G, U, 2)); // wide
        // Zone 3  (numpad top)
        keys.Add(new("Num", S * 15.25, y, U, U, 3));
        keys.Add(new("/", S * 16.25, y, U, U, 3));
        keys.Add(new("*", S * 17.25, y, U, U, 3));
        keys.Add(new("-", S * 18.25, y, U, U, 3));

        // ── Row 2: QWERTY ───────────────────────────────────────────
        y = S * 2;
        double tabW = U * 1.5 + G * 0.5;
        // Zone 0
        keys.Add(new("Tab", 0, y, tabW, U, 0));
        keys.Add(new("Q", tabW + G, y, U, U, 0));
        keys.Add(new("W", tabW + G + S, y, U, U, 0));
        keys.Add(new("E", tabW + G + S * 2, y, U, U, 0));
        keys.Add(new("R", tabW + G + S * 3, y, U, U, 0));
        keys.Add(new("T", tabW + G + S * 4, y, U, U, 0));
        // Zone 1
        keys.Add(new("Y", tabW + G + S * 5, y, U, U, 1));
        keys.Add(new("U", tabW + G + S * 6, y, U, U, 1));
        keys.Add(new("I", tabW + G + S * 7, y, U, U, 1));
        keys.Add(new("O", tabW + G + S * 8, y, U, U, 1));
        keys.Add(new("P", tabW + G + S * 9, y, U, U, 1));
        // Zone 2
        keys.Add(new("[", tabW + G + S * 10, y, U, U, 2));
        keys.Add(new("]", tabW + G + S * 11, y, U, U, 2));
        keys.Add(new("\\", tabW + G + S * 12, y, tabW, U, 2)); // wide
        // Zone 3 (numpad)
        keys.Add(new("7", S * 15.25, y, U, U, 3));
        keys.Add(new("8", S * 16.25, y, U, U, 3));
        keys.Add(new("9", S * 17.25, y, U, U, 3));
        keys.Add(new("+", S * 18.25, y, U, U * 2 + G, 3)); // tall

        // ── Row 3: Home row ─────────────────────────────────────────
        y = S * 3;
        double capsW = U * 1.75 + G * 0.75;
        // Zone 0
        keys.Add(new("Caps", 0, y, capsW, U, 0));
        keys.Add(new("A", capsW + G, y, U, U, 0));
        keys.Add(new("S", capsW + G + S, y, U, U, 0));
        keys.Add(new("D", capsW + G + S * 2, y, U, U, 0));
        keys.Add(new("F", capsW + G + S * 3, y, U, U, 0));
        keys.Add(new("G", capsW + G + S * 4, y, U, U, 0));
        // Zone 1
        keys.Add(new("H", capsW + G + S * 5, y, U, U, 1));
        keys.Add(new("J", capsW + G + S * 6, y, U, U, 1));
        keys.Add(new("K", capsW + G + S * 7, y, U, U, 1));
        keys.Add(new("L", capsW + G + S * 8, y, U, U, 1));
        keys.Add(new(";", capsW + G + S * 9, y, U, U, 1));
        keys.Add(new("'", capsW + G + S * 10, y, U, U, 1));
        // Zone 2
        double enterX = capsW + G + S * 11;
        keys.Add(new("Enter", enterX, y, S * 13 + U * 2 + G - enterX, U, 2));
        // Zone 3 (numpad)
        keys.Add(new("4", S * 15.25, y, U, U, 3));
        keys.Add(new("5", S * 16.25, y, U, U, 3));
        keys.Add(new("6", S * 17.25, y, U, U, 3));

        // ── Row 4: Shift row ────────────────────────────────────────
        y = S * 4;
        double lshiftW = U * 2.25 + G * 1.25;
        // Zone 0
        keys.Add(new("Shift", 0, y, lshiftW, U, 0));
        keys.Add(new("Z", lshiftW + G, y, U, U, 0));
        keys.Add(new("X", lshiftW + G + S, y, U, U, 0));
        keys.Add(new("C", lshiftW + G + S * 2, y, U, U, 0));
        keys.Add(new("V", lshiftW + G + S * 3, y, U, U, 0));
        keys.Add(new("B", lshiftW + G + S * 4, y, U, U, 0));
        // Zone 1
        keys.Add(new("N", lshiftW + G + S * 5, y, U, U, 1));
        keys.Add(new("M", lshiftW + G + S * 6, y, U, U, 1));
        keys.Add(new(",", lshiftW + G + S * 7, y, U, U, 1));
        keys.Add(new(".", lshiftW + G + S * 8, y, U, U, 1));
        keys.Add(new("/", lshiftW + G + S * 9, y, U, U, 1));
        // Zone 2
        double rshiftX = lshiftW + G + S * 10;
        keys.Add(new("Shift", rshiftX, y, S * 13 + U * 2 + G - rshiftX, U, 2));
        // Zone 2 (arrows - up)
        keys.Add(new("\u2191", S * 14.25, y, U, U, 2));
        // Zone 3 (numpad)
        keys.Add(new("1", S * 15.25, y, U, U, 3));
        keys.Add(new("2", S * 16.25, y, U, U, 3));
        keys.Add(new("3", S * 17.25, y, U, U, 3));
        keys.Add(new("Ent", S * 18.25, y, U, U * 2 + G, 3)); // tall

        // ── Row 5: Bottom row ───────────────────────────────────────
        y = S * 5;
        double modW = U * 1.25 + G * 0.25;
        // Zone 0
        keys.Add(new("Ctrl", 0, y, modW, U, 0));
        keys.Add(new("Fn", modW + G, y, modW, U, 0));
        keys.Add(new("Win", (modW + G) * 2, y, modW, U, 0));
        keys.Add(new("Alt", (modW + G) * 3, y, modW, U, 0));
        // Zone 0 + Zone 1: spacebar spans both
        double spaceX = (modW + G) * 4;
        double spaceW = S * 6.5;
        keys.Add(new("", spaceX, y, spaceW, U, 0)); // left half gets zone 0 color in renderer
        // Zone 1
        double altGrX = spaceX + spaceW + G;
        keys.Add(new("Alt", altGrX, y, U, U, 1));
        keys.Add(new("Ctrl", altGrX + S, y, U, U, 1));
        // Zone 2 (arrows)
        keys.Add(new("\u2190", S * 13.25, y, U, U, 2));
        keys.Add(new("\u2193", S * 14.25, y, U, U, 2));
        keys.Add(new("\u2192", S * 15.25, y, U, U, 2));
        // Zone 3 (numpad bottom)
        keys.Add(new("0", S * 16.25, y, U * 2 + G, U, 3)); // wide
        keys.Add(new(".", S * 18.25, y, U, U, 3));

        return keys;
    }
}
