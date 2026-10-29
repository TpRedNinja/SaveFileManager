using Avalonia.Media;
using SavefileManager.Helpers;
namespace SavefileManager.Themes;

/// <summary>
/// The four themes from the original Savefile Manager, using the same base colours
/// as its Theme.py: Dark, Dark (Alt), Classic and Solarized.
/// </summary>
public static class BuiltInThemes
{
    public static readonly string[] Names = ["Dark", "Dark (Alt)", "Classic", "Solarized"];

    public static ThemeDefinition Create(string name, string? newName = null)
    {
        var t = name switch
        {
            // bg1 (buttons/lists), bg2 (window), text, selection, selection text, path hover, border
            "Dark" => Make("#212020", "#1F1F1F", "#FFFFFF", "#4287F5", "#FFFFFF", "#838AEB", "#3C3C3C"),
            "Dark (Alt)" => Make("#212020", "#1F1F1F", "#FFFFFF", "#4287F5", "#FFFFFF", "#838AEB", "#6A6A6A"),
            "Classic" => MakeClassic(),
            "Solarized" => Make("#002B36", "#073642", "#FFFFFF", "#586E75", "#FFFFFF", "#8957FF", "#586E75"),
            _ => Make("#212020", "#1F1F1F", "#FFFFFF", "#4287F5", "#FFFFFF", "#838AEB", "#3C3C3C"),
        };
        t.Name = newName ?? name;
        return t;
    }

    private static ThemeDefinition Make(string bg1, string bg2, string fg, string sel,
        string selText, string pathHover, string border)
    {
        Color b1 = ColorUtil.FromHex(bg1, Colors.Black), b2 = ColorUtil.FromHex(bg2, Colors.Black);
        Color f = ColorUtil.FromHex(fg, Colors.White), s = ColorUtil.FromHex(sel, Colors.Blue);
        Color bd = ColorUtil.FromHex(border, Colors.Gray);

        var t = new ThemeDefinition();
        void Set(string key, Color c) => t.Set(key, c);

        Set("WindowBackground", b2);
        Set("PanelBackground", b2);
        Set("Text", f);
        Set("DisabledText", ColorUtil.Blend(f, b2, 0.55));
        Set("PathText", f);
        Set("PathHoverText", ColorUtil.FromHex(pathHover, Colors.CornflowerBlue));
        Set("UnavailableText", ColorUtil.FromHex("#FF6B6B", Colors.Red));
        Set("Accent", s);

        Set("TitleBarBackground", b1);
        Set("TitleBarText", f);
        Set("TitleBarBorder", bd);

        Set("ButtonBackground", b1);
        Set("ButtonText", f);
        Set("ButtonBorder", bd);
        Set("ButtonHoverBackground", ColorUtil.Shift(b1, 0.12));
        Set("ButtonPressedBackground", ColorUtil.Shift(b1, 0.22));
        Set("ButtonDisabledBackground", b2);

        Set("TreeBackground", b1);
        Set("TreeText", f);
        Set("TreeBorder", bd);
        Set("TreeHoverBackground", ColorUtil.Shift(b1, 0.10));
        Set("TreeSelectionBackground", s);
        Set("TreeSelectionText", ColorUtil.FromHex(selText, Colors.White));
        Set("TreeInactiveSelectionBackground", ColorUtil.Blend(s, b1, 0.5));
        Set("TreeInactiveSelectionText", f);
        Set("TreeExpander", f);

        Set("ScrollTrack", b2);
        Set("ScrollThumb", ColorUtil.Blend(f, b2, 0.70));
        Set("ScrollThumbHover", ColorUtil.Blend(f, b2, 0.55));
        Set("ScrollThumbPressed", ColorUtil.Blend(f, b2, 0.40));

        Set("InputBackground", b1);
        Set("InputText", f);
        Set("InputBorder", bd);
        Set("InputSelectionBackground", s);
        Set("InputSelectionText", ColorUtil.FromHex(selText, Colors.White));

        Set("ComboBackground", b1);
        Set("ComboText", f);
        Set("ComboHoverBackground", ColorUtil.Shift(b1, 0.12));
        Set("ComboArrow", f);
        Set("ComboPopupBackground", b1);
        Set("ComboItemHoverBackground", ColorUtil.Shift(b1, 0.14));
        Set("ComboItemSelectedBackground", s);
        Set("ComboItemSelectedText", ColorUtil.FromHex(selText, Colors.White));

        Set("MenuBarBackground", b1);
        Set("MenuBarText", f);
        Set("MenuBackground", b1);
        Set("MenuText", f);
        Set("MenuHoverBackground", s);
        Set("MenuHoverText", ColorUtil.FromHex(selText, Colors.White));
        Set("MenuBorder", bd);
        Set("MenuSeparator", bd);

        Set("TooltipBackground", b1);
        Set("TooltipText", f);
        Set("TooltipBorder", bd);
        return t;
    }

    private static ThemeDefinition MakeClassic()
    {
        // Windows "vista" look: grey window, white lists and text boxes, light grey buttons.
        var t = Make("#F0F0F0", "#F0F0F0", "#000000", "#0078D7", "#FFFFFF", "#57B6FF", "#ADADAD");
        void Set(string key, string hex) => t.Colors[key] = hex;

        Set("UnavailableText", "#C0392B");
        Set("TitleBarBackground", "#FFFFFF");
        Set("ButtonBackground", "#E1E1E1");
        Set("ButtonHoverBackground", "#E5F1FB");
        Set("ButtonPressedBackground", "#CCE4F7");
        Set("ButtonDisabledBackground", "#CCCCCC");
        Set("TreeBackground", "#FFFFFF");
        Set("TreeHoverBackground", "#E5F3FF");
        Set("TreeInactiveSelectionBackground", "#D9D9D9");
        Set("TreeInactiveSelectionText", "#000000");
        Set("ScrollTrack", "#F0F0F0");
        Set("ScrollThumb", "#CDCDCD");
        Set("ScrollThumbHover", "#A6A6A6");
        Set("ScrollThumbPressed", "#606060");
        Set("InputBackground", "#FFFFFF");
        Set("ComboBackground", "#E1E1E1");
        Set("ComboHoverBackground", "#E5F1FB");
        Set("ComboPopupBackground", "#FFFFFF");
        Set("ComboItemHoverBackground", "#E5F3FF");
        Set("MenuBarBackground", "#F0F0F0");
        Set("MenuBackground", "#F0F0F0");
        Set("TooltipBackground", "#FFFFE1");
        Set("TooltipBorder", "#767676");
        return t;
    }
}
