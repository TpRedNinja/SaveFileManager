using System.Diagnostics.CodeAnalysis;
using System.Runtime.InteropServices;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Media;
using Avalonia.Styling;
using SavefileManager.Helpers;
using SavefileManager.Models;
using SavefileManager.Themes;

namespace SavefileManager.Services;

/// <summary>
/// Applies a <see cref="ThemeDefinition"/> by replacing the "{Key}Brush" resources on the
/// application. Every control style uses DynamicResource, so the UI updates instantly.
/// The same colours are also written to the resource keys the Fluent theme itself uses, so
/// states our own styles don't cover (pressed, disabled, ...) still follow the theme.
/// </summary>
public static class ThemeManager
{
    public const string CustomName = "Custom";

    public static ThemeDefinition? Current { get; private set; }

    public static ThemeDefinition Resolve(AppSettings settings)
    {
        if (settings.Theme == CustomName)
        {
            var custom = settings.CustomTheme.Clone();
            custom.FillMissing(BuiltInThemes.Create("Dark"));
            return custom;
        }
        var name = BuiltInThemes.Names.Contains(settings.Theme) ? settings.Theme : "Dark";
        return BuiltInThemes.Create(name);
    }

    // Fluent resource key -> our theme key.
    private static readonly (string Fluent, string Ours)[] FluentMap =
    [
        // Button
        ("ButtonBackground", "ButtonBackground"), ("ButtonBackgroundPointerOver", "ButtonHoverBackground"),
        ("ButtonBackgroundPressed", "ButtonPressedBackground"), ("ButtonBackgroundDisabled", "ButtonDisabledBackground"),
        ("ButtonForeground", "ButtonText"), ("ButtonForegroundPointerOver", "ButtonText"),
        ("ButtonForegroundPressed", "ButtonText"), ("ButtonForegroundDisabled", "DisabledText"),
        ("ButtonBorderBrush", "ButtonBorder"), ("ButtonBorderBrushPointerOver", "ButtonBorder"),
        ("ButtonBorderBrushPressed", "ButtonBorder"), ("ButtonBorderBrushDisabled", "ButtonBorder"),

        // Text box
        ("TextControlBackground", "InputBackground"), ("TextControlBackgroundPointerOver", "InputBackground"),
        ("TextControlBackgroundFocused", "InputBackground"), ("TextControlBackgroundDisabled", "InputBackground"),
        ("TextControlForeground", "InputText"), ("TextControlForegroundPointerOver", "InputText"),
        ("TextControlForegroundFocused", "InputText"), ("TextControlForegroundDisabled", "DisabledText"),
        ("TextControlBorderBrush", "InputBorder"), ("TextControlBorderBrushPointerOver", "InputBorder"),
        ("TextControlBorderBrushFocused", "Accent"), ("TextControlBorderBrushDisabled", "InputBorder"),

        // Drop-down
        ("ComboBoxBackground", "ComboBackground"), ("ComboBoxBackgroundPointerOver", "ComboHoverBackground"),
        ("ComboBoxBackgroundPressed", "ComboHoverBackground"), ("ComboBoxBackgroundDisabled", "ComboBackground"),
        ("ComboBoxBackgroundFocused", "ComboBackground"), ("ComboBoxBackgroundUnfocused", "ComboBackground"),
        ("ComboBoxForeground", "ComboText"), ("ComboBoxForegroundPointerOver", "ComboText"),
        ("ComboBoxForegroundPressed", "ComboText"), ("ComboBoxForegroundFocused", "ComboText"),
        ("ComboBoxForegroundDisabled", "DisabledText"),
        ("ComboBoxBorderBrush", "InputBorder"), ("ComboBoxBorderBrushPointerOver", "InputBorder"),
        ("ComboBoxBorderBrushPressed", "Accent"), ("ComboBoxBorderBrushDisabled", "InputBorder"),
        ("ComboBoxDropDownBackground", "ComboPopupBackground"), ("ComboBoxDropDownBorderBrush", "InputBorder"),
        ("ComboBoxDropDownGlyphForeground", "ComboArrow"),
        ("ComboBoxItemBackground", "ComboPopupBackground"),
        ("ComboBoxItemBackgroundPointerOver", "ComboItemHoverBackground"),
        ("ComboBoxItemBackgroundPressed", "ComboItemHoverBackground"),
        ("ComboBoxItemBackgroundSelected", "ComboItemSelectedBackground"),
        ("ComboBoxItemBackgroundSelectedPointerOver", "ComboItemSelectedBackground"),
        ("ComboBoxItemBackgroundSelectedPressed", "ComboItemSelectedBackground"),
        ("ComboBoxItemForeground", "ComboText"), ("ComboBoxItemForegroundPointerOver", "ComboText"),
        ("ComboBoxItemForegroundPressed", "ComboText"),
        ("ComboBoxItemForegroundSelected", "ComboItemSelectedText"),
        ("ComboBoxItemForegroundSelectedPointerOver", "ComboItemSelectedText"),
        ("ComboBoxItemForegroundSelectedPressed", "ComboItemSelectedText"),

        // File lists
        ("TreeViewItemBackground", "TreeBackground"), ("TreeViewItemBackgroundPointerOver", "TreeHoverBackground"),
        ("TreeViewItemBackgroundPressed", "TreeHoverBackground"),
        ("TreeViewItemBackgroundSelected", "TreeSelectionBackground"),
        ("TreeViewItemBackgroundSelectedPointerOver", "TreeSelectionBackground"),
        ("TreeViewItemBackgroundSelectedPressed", "TreeSelectionBackground"),
        ("TreeViewItemForeground", "TreeText"), ("TreeViewItemForegroundPointerOver", "TreeText"),
        ("TreeViewItemForegroundPressed", "TreeText"),
        ("TreeViewItemForegroundSelected", "TreeSelectionText"),
        ("TreeViewItemForegroundSelectedPointerOver", "TreeSelectionText"),
        ("TreeViewItemForegroundSelectedPressed", "TreeSelectionText"),
        ("TreeViewItemForegroundDisabled", "DisabledText"),

        // Menus
        ("MenuFlyoutPresenterBackground", "MenuBackground"), ("MenuFlyoutPresenterBorderBrush", "MenuBorder"),
        ("MenuFlyoutItemBackground", "MenuBackground"), ("MenuFlyoutItemBackgroundPointerOver", "MenuHoverBackground"),
        ("MenuFlyoutItemBackgroundPressed", "MenuHoverBackground"),
        ("MenuFlyoutItemForeground", "MenuText"), ("MenuFlyoutItemForegroundPointerOver", "MenuHoverText"),
        ("MenuFlyoutItemForegroundPressed", "MenuHoverText"), ("MenuFlyoutItemForegroundDisabled", "DisabledText"),
        ("MenuFlyoutSeparatorBackground", "MenuSeparator"),
        ("MenuBarBackground", "MenuBarBackground"),
        ("MenuBarItemBackground", "MenuBarBackground"), ("MenuBarItemBackgroundPointerOver", "MenuHoverBackground"),
        ("MenuBarItemBackgroundPressed", "MenuHoverBackground"), ("MenuBarItemBackgroundSelected", "MenuHoverBackground"),
        ("MenuBarItemForeground", "MenuBarText"), ("MenuBarItemForegroundPointerOver", "MenuHoverText"),
        ("MenuBarItemForegroundSelected", "MenuHoverText"),

        // Scroll bars
        ("ScrollBarTrackFill", "ScrollTrack"), ("ScrollBarTrackFillPointerOver", "ScrollTrack"),
        ("ScrollBarTrackStroke", "ScrollTrack"), ("ScrollBarTrackStrokePointerOver", "ScrollTrack"),
        ("ScrollBarThumbFill", "ScrollThumb"), ("ScrollBarThumbFillPointerOver", "ScrollThumbHover"),
        ("ScrollBarThumbFillPressed", "ScrollThumbPressed"), ("ScrollBarThumbFillDisabled", "ScrollThumb"),
        ("ScrollBarBackground", "ScrollTrack"), ("ScrollBarBackgroundPointerOver", "ScrollTrack"),

        // Tooltips
        ("ToolTipBackground", "TooltipBackground"), ("ToolTipForeground", "TooltipText"),
        ("ToolTipBorderBrush", "TooltipBorder"),

        // Slider and radio buttons (used by the Fonts & backgrounds window)
        ("SliderTrackFill", "InputBorder"), ("SliderTrackFillPointerOver", "InputBorder"),
        ("SliderTrackValueFill", "Accent"), ("SliderTrackValueFillPointerOver", "Accent"),
        ("SliderThumbBackground", "Accent"), ("SliderThumbBackgroundPointerOver", "Accent"),
        ("SliderThumbBackgroundPressed", "Accent"),
        ("RadioButtonForeground", "Text"), ("RadioButtonForegroundPointerOver", "Text"),
        ("RadioButtonForegroundPressed", "Text"), ("RadioButtonForegroundDisabled", "DisabledText"),
        ("RadioButtonOuterEllipseStroke", "InputBorder"), ("RadioButtonOuterEllipseStrokePointerOver", "Accent"),
        ("RadioButtonOuterEllipseStrokePressed", "Accent"),
        ("RadioButtonOuterEllipseStrokeChecked", "Accent"), ("RadioButtonOuterEllipseStrokeCheckedPointerOver", "Accent"),
        ("RadioButtonOuterEllipseStrokeCheckedPressed", "Accent"),
        ("RadioButtonOuterEllipseFill", "InputBackground"), ("RadioButtonOuterEllipseFillPointerOver", "InputBackground"),
        ("RadioButtonOuterEllipseFillPressed", "InputBackground"),
        ("RadioButtonOuterEllipseCheckedFill", "InputBackground"),
        ("RadioButtonOuterEllipseCheckedFillPointerOver", "InputBackground"),
        ("RadioButtonOuterEllipseCheckedFillPressed", "InputBackground"),
        ("RadioButtonCheckGlyphFill", "Accent"), ("RadioButtonCheckGlyphFillPointerOver", "Accent"),
        ("RadioButtonCheckGlyphFillPressed", "Accent"),
    ];

    public static void Apply(ThemeDefinition def)
    {
        var app = Application.Current;
        if (app == null) return;
        var res = app.Resources;

        foreach (var k in ThemeKeys.All)
            res[k.Key + "Brush"] = new SolidColorBrush(def.Get(k.Key));
        foreach (var (fluent, ours) in FluentMap)
            res[fluent] = new SolidColorBrush(def.Get(ours));

        // Light themes (Classic) use Fluent's light base, dark themes its dark base, so anything we
        // don't style explicitly still looks right.
        var variant = ColorUtil.Luminance(def.Get("WindowBackground")) < 0.5
            ? ThemeVariant.Dark
            : ThemeVariant.Light;
        if (app.RequestedThemeVariant != variant)   // switching variant restyles everything: only when needed
            app.RequestedThemeVariant = variant;

        Current = def;

        if (app.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
            foreach (var w in desktop.Windows.ToList())
                ApplyTitleBar(w);
    }

    /// <summary>Call from a window's constructor so its title bar follows the theme.</summary>
    public static void Attach(Window window)
    {
        window.Opened += (_, _) => ApplyTitleBar(window);
    }

    // ---------- Windows 11 title bar colours (ignored on Linux, macOS and older Windows) ----------
    private const int DWMWA_USE_IMMERSIVE_DARK_MODE = 20;
    private const int DWMWA_BORDER_COLOR = 34;
    private const int DWMWA_CAPTION_COLOR = 35;
    private const int DWMWA_TEXT_COLOR = 36;

    [SuppressMessage("remove unnessaceary suppression", "IDE0079")]
    [SuppressMessage("Interoperability", "SYSLIB1054")]
    [DllImport("dwmapi.dll")]
    private static extern int DwmSetWindowAttribute(IntPtr hwnd, int attr, ref int value, int size);

    private static int ColorRef(Color c) => c.R | (c.G << 8) | (c.B << 16);

    private static void ApplyTitleBar(Window window)
    {
        if (!OperatingSystem.IsWindows() || Current == null) return;
        try
        {
            var hwnd = window.TryGetPlatformHandle()?.Handle ?? IntPtr.Zero;
            if (hwnd == IntPtr.Zero) return;

            var bg = Current.Get("TitleBarBackground");
            int dark = ColorUtil.Luminance(bg) < 0.5 ? 1 : 0;
            int cap = ColorRef(bg);
            int txt = ColorRef(Current.Get("TitleBarText"));
            int border = ColorRef(Current.Get("TitleBarBorder"));
            _ = DwmSetWindowAttribute(hwnd, DWMWA_USE_IMMERSIVE_DARK_MODE, ref dark, sizeof(int));
            _ = DwmSetWindowAttribute(hwnd, DWMWA_CAPTION_COLOR, ref cap, sizeof(int));
            _ = DwmSetWindowAttribute(hwnd, DWMWA_TEXT_COLOR, ref txt, sizeof(int));
            _ = DwmSetWindowAttribute(hwnd, DWMWA_BORDER_COLOR, ref border, sizeof(int));
        }
        catch
        {
            // Not supported on this version of Windows - the normal title bar is used.
        }
    }
}
