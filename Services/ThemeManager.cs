using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;
using SavefileManager.Helpers;
using SavefileManager.Models;

namespace SavefileManager.Services;

/// <summary>
/// Applies a <see cref="ThemeDefinition"/> by replacing the "{Key}Brush" resources on the
/// application. Every control style uses DynamicResource, so the UI updates instantly.
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

    public static void Apply(ThemeDefinition def)
    {
        var res = Application.Current.Resources;
        foreach (var k in ThemeKeys.All)
        {
            var brush = new SolidColorBrush(def.Get(k.Key));
            brush.Freeze();
            res[k.Key + "Brush"] = brush;
        }
        // The little square where two scroll bars meet uses this system brush.
        res[SystemColors.ControlBrushKey] = res["PanelBackgroundBrush"];
        Current = def;

        foreach (Window w in Application.Current.Windows)
            ApplyTitleBar(w);
    }

    /// <summary>Call from a window's constructor so its title bar follows the theme.</summary>
    public static void Attach(Window window)
    {
        window.SourceInitialized += (_, _) => ApplyTitleBar(window);
    }

    // ---------- Windows 11 title bar colours (silently ignored on older systems) ----------
    private const int DWMWA_USE_IMMERSIVE_DARK_MODE = 20;
    private const int DWMWA_BORDER_COLOR = 34;
    private const int DWMWA_CAPTION_COLOR = 35;
    private const int DWMWA_TEXT_COLOR = 36;

    [DllImport("dwmapi.dll")]
    private static extern int DwmSetWindowAttribute(IntPtr hwnd, int attr, ref int value, int size);

    private static int ColorRef(Color c) => c.R | (c.G << 8) | (c.B << 16);

    private static void ApplyTitleBar(Window window)
    {
        if (Current == null) return;
        var hwnd = new WindowInteropHelper(window).Handle;
        if (hwnd == IntPtr.Zero) return;
        try
        {
            var bg = Current.Get("TitleBarBackground");
            int dark = ColorUtil.Luminance(bg) < 0.5 ? 1 : 0;
            int cap = ColorRef(bg);
            int txt = ColorRef(Current.Get("TitleBarText"));
            int border = ColorRef(Current.Get("TitleBarBorder"));
            DwmSetWindowAttribute(hwnd, DWMWA_USE_IMMERSIVE_DARK_MODE, ref dark, sizeof(int));
            DwmSetWindowAttribute(hwnd, DWMWA_CAPTION_COLOR, ref cap, sizeof(int));
            DwmSetWindowAttribute(hwnd, DWMWA_TEXT_COLOR, ref txt, sizeof(int));
            DwmSetWindowAttribute(hwnd, DWMWA_BORDER_COLOR, ref border, sizeof(int));
        }
        catch
        {
            // Not supported on this version of Windows - the normal title bar is used.
        }
    }
}
