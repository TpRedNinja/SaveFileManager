using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Media;
using SavefileManager.Models;

namespace SavefileManager.Services;

/// <summary>
/// Installed fonts plus fonts the user adds from .ttf / .otf files. Added font files are
/// COPIED to config/fonts (each in its own folder), so they keep working if the download is moved or deleted,
/// and nothing has to be installed into Windows.
/// </summary>
public static class FontService
{
    /// <summary>The parts of the window that can have their own font.</summary>
    public static readonly (string Key, string Label)[] Elements =
    {
        ("General", "Everything else (dialogs, labels, text boxes)"),
        ("Title", "Title at the top"),
        ("Combo", "Profile selector"),
        ("Paths", "Folder paths"),
        ("Tree", "File lists"),
        ("Button", "Buttons"),
        ("Menu", "Menus and right-click menus"),
        ("Tooltip", "Tooltips"),
    };

    private static readonly string[] Curated =
    {
        "Segoe UI", "Segoe UI Variable Text", "Arial", "Calibri", "Cambria", "Candara", "Consolas",
        "Cascadia Code", "Cascadia Mono", "Comic Sans MS", "Courier New", "Franklin Gothic Medium",
        "Georgia", "Impact", "Lucida Console", "Palatino Linotype", "Segoe Print", "Segoe Script",
        "Tahoma", "Times New Roman", "Trebuchet MS", "Verdana",
    };

    private static string _dir = "";
    private static readonly Dictionary<string, FontFamily> UserFonts = new(StringComparer.OrdinalIgnoreCase);

    public static void Init(string fontsDirectory)
    {
        _dir = fontsDirectory;
        Directory.CreateDirectory(_dir);
        try
        {
            foreach (var file in Directory.EnumerateFiles(_dir, "*.*", SearchOption.AllDirectories))
            {
                var ext = Path.GetExtension(file).ToLowerInvariant();
                if (ext is ".ttf" or ".otf" or ".ttc") Register(file);
            }
        }
        catch (Exception ex)
        {
            Log.Warn("Couldn't load added fonts: " + ex.Message);
        }
    }

    private static string? Register(string file)
    {
        try
        {
            var glyph = new GlyphTypeface(new Uri(file, UriKind.Absolute));
            string family = glyph.FamilyNames.TryGetValue(CultureInfo.GetCultureInfo("en-US"), out var en)
                ? en
                : glyph.FamilyNames.Values.First();
            var folder = new Uri(Path.GetDirectoryName(file)! + Path.DirectorySeparatorChar, UriKind.Absolute);
            UserFonts[family] = new FontFamily(folder, "./#" + family);
            return family;
        }
        catch (Exception ex)
        {
            Log.Warn($"Couldn't read font {file}: {ex.Message}");
            return null;
        }
    }

    /// <summary>Copies a font file into config/fonts and makes it available. Returns its family name, or null.</summary>
    public static string? AddFontFile(string sourcePath)
    {
        string folder = Path.Combine(_dir, Guid.NewGuid().ToString("N"));
        try
        {
            Directory.CreateDirectory(folder);
            var dest = Path.Combine(folder, Path.GetFileName(sourcePath));
            File.Copy(sourcePath, dest, true);
            var name = Register(dest);
            if (name == null) TryDeleteOwnFolder(folder);
            return name;
        }
        catch (Exception ex)
        {
            Log.Warn($"Couldn't add font {sourcePath}: {ex.Message}");
            TryDeleteOwnFolder(folder);
            return null;
        }
    }

    // Only ever used on the folder this method just created for a font that turned out to be unusable.
    private static void TryDeleteOwnFolder(string folder)
    {
        try { if (Directory.Exists(folder)) Directory.Delete(folder, true); } catch { /* ignore */ }
    }

    /// <summary>User-added fonts first, then common fonts, then everything else installed.</summary>
    public static List<string> AllFontNames()
    {
        var names = new List<string>();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        void Add(string n) { if (seen.Add(n)) names.Add(n); }

        foreach (var n in UserFonts.Keys.OrderBy(x => x, StringComparer.CurrentCultureIgnoreCase)) Add(n);

        var installed = new HashSet<string>(Fonts.SystemFontFamilies.Select(f => f.Source), StringComparer.OrdinalIgnoreCase);
        foreach (var c in Curated) if (installed.Contains(c)) Add(c);
        foreach (var n in installed.OrderBy(x => x, StringComparer.CurrentCultureIgnoreCase)) Add(n);
        return names;
    }

    public static FontFamily Resolve(string? name)
    {
        if (string.IsNullOrWhiteSpace(name)) name = "Segoe UI";
        return UserFonts.TryGetValue(name, out var f) ? f : new FontFamily(name);
    }

    private static string NameFor(FontSettings s, string key)
    {
        if (s.PerElement && s.Elements.TryGetValue(key, out var n) && !string.IsNullOrWhiteSpace(n)) return n;
        return s.GlobalFont;
    }

    /// <summary>Publishes the fonts as "Font.General", "Font.Title", ... application resources.</summary>
    public static void Apply(FontSettings s)
    {
        var res = Application.Current.Resources;
        foreach (var (key, _) in Elements)
            res["Font." + key] = Resolve(NameFor(s, key));
    }
}
