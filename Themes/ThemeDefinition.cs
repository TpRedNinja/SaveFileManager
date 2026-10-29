using System.Diagnostics.CodeAnalysis;
using System.IO;
using Avalonia.Media;
using SavefileManager.Helpers;
namespace SavefileManager.Themes;

/// <summary>One colour slot that can be changed in the custom theme editor.</summary>
public record ThemeKey(string Key, string Label, string Group);

public static class ThemeKeys
{
    public static readonly IReadOnlyList<ThemeKey> All =
    [
        // General
        new("WindowBackground", "Window background", "General"),
        new("PanelBackground", "Header / panel background", "General"),
        new("Text", "Main text", "General"),
        new("DisabledText", "Disabled text", "General"),
        new("PathText", "Folder path text", "General"),
        new("PathHoverText", "Folder path text (mouse over)", "General"),
        new("UnavailableText", "Warning / unavailable profile text", "General"),
        new("Accent", "Focus / accent border", "General"),

        // Title bar (Windows 11 only)
        new("TitleBarBackground", "Title bar background (Windows 11)", "Title bar"),
        new("TitleBarText", "Title bar text (Windows 11)", "Title bar"),
        new("TitleBarBorder", "Window border (Windows 11)", "Title bar"),

        // Buttons
        new("ButtonBackground", "Button background", "Buttons"),
        new("ButtonText", "Button text", "Buttons"),
        new("ButtonBorder", "Button border", "Buttons"),
        new("ButtonHoverBackground", "Button background (mouse over)", "Buttons"),
        new("ButtonPressedBackground", "Button background (pressed)", "Buttons"),
        new("ButtonDisabledBackground", "Button background (disabled)", "Buttons"),

        // File lists
        new("TreeBackground", "List background", "File lists"),
        new("TreeText", "List text", "File lists"),
        new("TreeBorder", "List border", "File lists"),
        new("TreeHoverBackground", "Item background (mouse over)", "File lists"),
        new("TreeSelectionBackground", "Selected item background", "File lists"),
        new("TreeSelectionText", "Selected item text", "File lists"),
        new("TreeInactiveSelectionBackground", "Selected item background (list not focused)", "File lists"),
        new("TreeInactiveSelectionText", "Selected item text (list not focused)", "File lists"),
        new("TreeExpander", "Folder expand arrow", "File lists"),

        // Scroll bars
        new("ScrollTrack", "Scroll bar track", "Scroll bars"),
        new("ScrollThumb", "Scroll bar thumb", "Scroll bars"),
        new("ScrollThumbHover", "Scroll bar thumb (mouse over)", "Scroll bars"),
        new("ScrollThumbPressed", "Scroll bar thumb (dragging)", "Scroll bars"),

        // Text boxes
        new("InputBackground", "Text box background", "Text boxes"),
        new("InputText", "Text box text", "Text boxes"),
        new("InputBorder", "Text box border", "Text boxes"),
        new("InputSelectionBackground", "Selected text background", "Text boxes"),
        new("InputSelectionText", "Selected text colour", "Text boxes"),

        // Drop-down
        new("ComboBackground", "Drop-down background", "Profile drop-down"),
        new("ComboText", "Drop-down text", "Profile drop-down"),
        new("ComboHoverBackground", "Drop-down background (mouse over)", "Profile drop-down"),
        new("ComboArrow", "Drop-down arrow", "Profile drop-down"),
        new("ComboPopupBackground", "Opened list background", "Profile drop-down"),
        new("ComboItemHoverBackground", "Opened list item (mouse over)", "Profile drop-down"),
        new("ComboItemSelectedBackground", "Opened list item (selected)", "Profile drop-down"),
        new("ComboItemSelectedText", "Opened list item text (selected)", "Profile drop-down"),

        // Menus
        new("MenuBarBackground", "Menu bar background", "Menus"),
        new("MenuBarText", "Menu bar text", "Menus"),
        new("MenuBackground", "Menu / context menu background", "Menus"),
        new("MenuText", "Menu / context menu text", "Menus"),
        new("MenuHoverBackground", "Menu item highlight", "Menus"),
        new("MenuHoverText", "Menu item highlight text", "Menus"),
        new("MenuBorder", "Menu border", "Menus"),
        new("MenuSeparator", "Menu separator line", "Menus"),

        // Tooltips
        new("TooltipBackground", "Tooltip background", "Tooltips"),
        new("TooltipText", "Tooltip text", "Tooltips"),
        new("TooltipBorder", "Tooltip border", "Tooltips"),
    ];
}

public class ThemeDefinition
{
    public string Name { get; set; } = "Custom";

    /// <summary>Key (see <see cref="ThemeKeys"/>) to "#RRGGBB".</summary>
    public Dictionary<string, string> Colors { get; set; } = [];

    public Color Get(string key) =>
        Colors.TryGetValue(key, out var hex)
            ? ColorUtil.FromHex(hex, Color.FromRgb(255, 0, 255))
            : Color.FromRgb(255, 0, 255);

    public void Set(string key, Color color) => Colors[key] = ColorUtil.ToHex(color);

    public ThemeDefinition Clone() => new()
    {
        Name = Name,
        Colors = new Dictionary<string, string>(Colors)
    };

    /// <summary>Copies colours from another theme, keeping this theme's name.</summary>
    public void CopyColorsFrom(ThemeDefinition other)
    {
        Colors = new Dictionary<string, string>(other.Colors);
    }

    /// <summary>Makes sure every slot has a value (used when loading older or hand-edited files).</summary>
    [SuppressMessage("Causes an error", "CA1854")]
    public void FillMissing(ThemeDefinition fallback)
    {
        foreach (var k in ThemeKeys.All)
            if (!Colors.ContainsKey(k.Key) || !ColorUtil.TryParseHex(Colors[k.Key], out _))
                Colors[k.Key] = fallback.Colors.TryGetValue(k.Key, out var v) ? v : "#FF00FF";
    }
}
