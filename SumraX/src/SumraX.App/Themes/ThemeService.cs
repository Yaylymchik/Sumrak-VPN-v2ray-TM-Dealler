using System.Windows;
using System.Windows.Media;
using SumraX.Core.Enums;

namespace SumraX.App.Themes;

/// <summary>
/// Applies the "Obsidian / Amber" palette.
///
/// Every preset is a black-yellow variant — the product identity is a black
/// canvas with a single amber signal color, so there is no light or
/// multi-hue theme any more. Presets differ only in how warm/cool the black is
/// and how saturated the amber is.
/// </summary>
public static class ThemeService
{
    public static void Apply(AppTheme theme)
    {
        var p = GetPalette(theme);
        var r = Application.Current.Resources;

        SetColor(r, "BgPrimary", p.BgPrimary);
        SetColor(r, "BgSecondary", p.BgSecondary);
        SetColor(r, "BgCard", p.BgCard);
        SetColor(r, "BgElevated", p.BgElevated);
        SetColor(r, "BgGlass", p.BgGlass);
        SetColor(r, "NeonGreen", p.AccentSuccess);
        SetColor(r, "NeonBlue", p.AccentPrimary);
        SetColor(r, "CyberPurple", p.AccentSecondary);
        SetColor(r, "NeonRed", p.Danger);
        SetColor(r, "TextPrimary", p.TextPrimary);
        SetColor(r, "TextSecondary", p.TextSecondary);
        SetColor(r, "TextMuted", p.TextMuted);
        SetColor(r, "BorderSubtle", p.BorderSubtle);
        SetColor(r, "BorderGlow", p.AccentPrimary);
        SetColor(r, "Success", p.AccentSuccess);
        SetColor(r, "Danger", p.Danger);
        SetColor(r, "Warning", p.Warning);
        SetColor(r, "SurfaceInset", p.SurfaceInset);
        SetColor(r, "SurfaceHover", p.SurfaceHover);
        SetColor(r, "TerminalBg", p.TerminalBg);

        SetBrush(r, "BgPrimaryBrush", p.BgPrimary);
        SetBrush(r, "BgSecondaryBrush", p.BgSecondary);
        SetBrush(r, "BgCardBrush", p.BgCard);
        SetBrush(r, "BgElevatedBrush", p.BgElevated);
        SetBrush(r, "BgGlassBrush", p.BgGlass);
        SetBrush(r, "AccentCyanBrush", p.AccentPrimary);
        SetBrush(r, "AccentBlueBrush", p.AccentPrimary);
        SetBrush(r, "CyberPurpleBrush", p.AccentSecondary);
        SetBrush(r, "TextPrimaryBrush", p.TextPrimary);
        SetBrush(r, "TextSecondaryBrush", p.TextSecondary);
        SetBrush(r, "TextMutedBrush", p.TextMuted);
        SetBrush(r, "BorderSubtleBrush", p.BorderSubtle);
        SetBrush(r, "BorderGlowBrush", p.AccentPrimary);
        SetBrush(r, "SuccessBrush", p.AccentSuccess);
        SetBrush(r, "DangerBrush", p.Danger);
        SetBrush(r, "WarningBrush", p.Warning);
        SetBrush(r, "NeonGreenBrush", p.AccentSuccess);
        SetBrush(r, "NeonBlueBrush", p.AccentPrimary);
        SetBrush(r, "NeonRedBrush", p.Danger);
        SetBrush(r, "SurfaceInsetBrush", p.SurfaceInset);
        SetBrush(r, "SurfaceHoverBrush", p.SurfaceHover);
        SetBrush(r, "TerminalBgBrush", p.TerminalBg);

        // Action gradients: amber family only, so a button always reads as "yellow".
        r["ConnectGradient"] = MakeGradient(Lighten(p.AccentPrimary, 0.10), p.AccentSecondary, diagonal: false);
        r["AccentGradient"] = MakeGradient(p.AccentPrimary, p.AccentSecondary, diagonal: true);
        r["ProtectedGradient"] = MakeGradient(p.AccentSuccess, p.AccentPrimary, diagonal: false);
        r["AmberEdge"] = MakeVerticalGradient(p.AccentPrimary, p.AccentSecondary);
        r["WindowBackground"] = MakeTriGradient(p.BgPrimary, p.BgSecondary, p.BgPrimary);
        r["SidebarBackground"] = MakeVerticalGradient(p.BgSecondary, p.BgPrimary);
    }

    public static ThemePalette GetPalette(AppTheme theme) => theme switch
    {
        AppTheme.Midnight => Midnight,
        AppTheme.Emerald => Emerald,
        AppTheme.Ember => Ember,
        AppTheme.Arctic => Arctic,
        _ => Cyber
    };

    private static void SetColor(ResourceDictionary r, string key, Color color) =>
        r[key] = color;

    private static void SetBrush(ResourceDictionary r, string key, Color color) =>
        r[key] = new SolidColorBrush(color);

    private static Color Lighten(Color c, double amount)
    {
        byte L(byte v) => (byte)Math.Min(255, v + 255 * amount);
        return Color.FromRgb(L(c.R), L(c.G), L(c.B));
    }

    private static LinearGradientBrush MakeGradient(Color a, Color b, bool diagonal) => new()
    {
        StartPoint = diagonal ? new Point(0, 0) : new Point(0, 0.5),
        EndPoint = diagonal ? new Point(1, 1) : new Point(1, 0.5),
        GradientStops =
        [
            new GradientStop(a, 0),
            new GradientStop(b, 1)
        ]
    };

    private static LinearGradientBrush MakeTriGradient(Color a, Color b, Color c) => new()
    {
        StartPoint = new Point(0, 0),
        EndPoint = new Point(1, 1),
        GradientStops =
        [
            new GradientStop(a, 0),
            new GradientStop(b, 0.5),
            new GradientStop(c, 1)
        ]
    };

    private static LinearGradientBrush MakeVerticalGradient(Color a, Color b) => new()
    {
        StartPoint = new Point(0, 0),
        EndPoint = new Point(0, 1),
        GradientStops =
        [
            new GradientStop(a, 0),
            new GradientStop(b, 1)
        ]
    };

    // Default: neutral black canvas, warm amber signal.
    private static readonly ThemePalette Cyber = new(
        Hex("#0A0A0B"), Hex("#111112"), Hex("#161617"), Hex("#1E1E20"), Hex("#131314"),
        Hex("#FFD400"), Hex("#FFB800"), Hex("#FFC400"),
        Hex("#F2F2ED"), Hex("#B4B4AA"), Hex("#7A7A72"),
        Hex("#2A2A2C"), Hex("#FF5C5C"), Hex("#FFA726"),
        Hex("#080809"), Hex("#1C1C14"), Hex("#060607"));

    // Cooler black, pale lemon.
    private static readonly ThemePalette Midnight = new(
        Hex("#08090B"), Hex("#0E1013"), Hex("#14171B"), Hex("#1C2026"), Hex("#111318"),
        Hex("#FFE066"), Hex("#FFC400"), Hex("#B5E673"),
        Hex("#F5F5F0"), Hex("#B0B6BE"), Hex("#767C84"),
        Hex("#262A30"), Hex("#FF5C5C"), Hex("#FFA726"),
        Hex("#060709"), Hex("#1A1D14"), Hex("#040506"));

    // Black with a green-leaning yellow, success reads clearly as "on".
    private static readonly ThemePalette Emerald = new(
        Hex("#090B0A"), Hex("#0F1210"), Hex("#151815"), Hex("#1D211C"), Hex("#121512"),
        Hex("#D4E157"), Hex("#B8C93A"), Hex("#8FE05F"),
        Hex("#F4F6F0"), Hex("#B2BAAC"), Hex("#79806F"),
        Hex("#262C24"), Hex("#FF5C5C"), Hex("#FFA726"),
        Hex("#070908"), Hex("#1C2114"), Hex("#050706"));

    // Black with an orange-leaning amber.
    private static readonly ThemePalette Ember = new(
        Hex("#0B0A08"), Hex("#121009"), Hex("#19160E"), Hex("#211D15"), Hex("#15120B"),
        Hex("#FFB300"), Hex("#FF8A00"), Hex("#C8D96B"),
        Hex("#F6F3EC"), Hex("#BCB5A6"), Hex("#827A6B"),
        Hex("#2C2820"), Hex("#FF5C5C"), Hex("#FFB74D"),
        Hex("#090805"), Hex("#211B0E"), Hex("#070603"));

    // Purest black, brightest lemon.
    private static readonly ThemePalette Arctic = new(
        Hex("#000000"), Hex("#0A0A0A"), Hex("#121212"), Hex("#1A1A1A"), Hex("#0F0F0F"),
        Hex("#FFE81A"), Hex("#FFCC00"), Hex("#B8E05F"),
        Hex("#FFFFFF"), Hex("#BEBDBA"), Hex("#7E7D7A"),
        Hex("#242424"), Hex("#FF5C5C"), Hex("#FFA726"),
        Hex("#000000"), Hex("#1A1A00"), Hex("#000000"));

    private static Color Hex(string hex) =>
        (Color)ColorConverter.ConvertFromString(hex)!;
}

public readonly record struct ThemePalette(
    Color BgPrimary,
    Color BgSecondary,
    Color BgCard,
    Color BgElevated,
    Color BgGlass,
    Color AccentPrimary,
    Color AccentSecondary,
    Color AccentSuccess,
    Color TextPrimary,
    Color TextSecondary,
    Color TextMuted,
    Color BorderSubtle,
    Color Danger,
    Color Warning,
    Color SurfaceInset,
    Color SurfaceHover,
    Color TerminalBg);
