using Avalonia;
using Avalonia.Media;
using SavefileManager.Models;

namespace SavefileManager.Services;

/// <summary>
/// Installed fonts plus fonts the user adds from .ttf / .otf files. An added font file is COPIED to
/// config/fonts (so it survives the download being moved or deleted and travels with the config
/// folder to your other computer) and made available to this user's programs. The system only
/// picks up a new font when the program starts, so a font added now is usable after a restart.
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
        // common on Linux
        "Inter", "Roboto", "Noto Sans", "Ubuntu", "Cantarell", "DejaVu Sans", "DejaVu Sans Mono",
        "Liberation Sans", "Liberation Serif", "Liberation Mono",
    };

    private static string _dir = "";
    private static readonly List<string> UserFamilies = new();

    public static void Init(string fontsDirectory)
    {
        _dir = fontsDirectory;
        Directory.CreateDirectory(_dir);
        try
        {
            int installed = 0;
            foreach (var file in Directory.EnumerateFiles(_dir, "*.*", SearchOption.AllDirectories))
            {
                var ext = Path.GetExtension(file).ToLowerInvariant();
                if (ext is not (".ttf" or ".otf" or ".ttc")) continue;
                var family = FontNameReader.ReadFamilyName(file);
                if (family == null) continue;
                if (!UserFamilies.Contains(family, StringComparer.OrdinalIgnoreCase)) UserFamilies.Add(family);
                // a font that came from your other computer's config folder gets installed here too
                if (PlatformHelper.InstallUserFont(file, family, UniqueName(file))) installed++;
            }
            if (installed > 0) Log.Info($"{installed} font(s) installed for this user; they are available after a restart");
        }
        catch (Exception ex)
        {
            Log.Warn("Couldn't load added fonts: " + ex.Message);
        }
    }

    private static string UniqueName(string file) =>
        new DirectoryInfo(Path.GetDirectoryName(file)!).Name + "_" + Path.GetFileName(file);

    /// <summary>
    /// Copies a font file into config/fonts and installs it for this user. Returns its family name,
    /// or null if it isn't a usable font. <paramref name="needsRestart"/> is true when the font
    /// isn't visible to the program yet.
    /// </summary>
    public static string? AddFontFile(string sourcePath, out bool needsRestart)
    {
        needsRestart = false;
        var family = FontNameReader.ReadFamilyName(sourcePath);
        if (family == null) return null;

        string folder = Path.Combine(_dir, Guid.NewGuid().ToString("N"));
        try
        {
            Directory.CreateDirectory(folder);
            var dest = Path.Combine(folder, Path.GetFileName(sourcePath));
            File.Copy(sourcePath, dest, true);
            PlatformHelper.InstallUserFont(dest, family, UniqueName(dest));
            if (!UserFamilies.Contains(family, StringComparer.OrdinalIgnoreCase)) UserFamilies.Add(family);
            needsRestart = !SystemFontNames().Contains(family, StringComparer.OrdinalIgnoreCase);
            return family;
        }
        catch (Exception ex)
        {
            Log.Warn($"Couldn't add font {sourcePath}: {ex.Message}");
            TryDeleteOwnFolder(folder);
            return null;
        }
    }

    // Only ever used on the folder this method just created for a font that couldn't be added.
    private static void TryDeleteOwnFolder(string folder)
    {
        try { if (Directory.Exists(folder)) Directory.Delete(folder, true); } catch { /* ignore */ }
    }

    private static IEnumerable<string> SystemFontNames()
    {
        try { return FontManager.Current.SystemFonts.Select(f => f.Name).ToList(); }
        catch { return Array.Empty<string>(); }
    }

    /// <summary>Fonts you added first, then common fonts, then everything else installed.</summary>
    public static List<string> AllFontNames()
    {
        var names = new List<string>();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        void Add(string n) { if (seen.Add(n)) names.Add(n); }

        foreach (var n in UserFamilies.OrderBy(x => x, StringComparer.CurrentCultureIgnoreCase)) Add(n);

        var installed = new HashSet<string>(SystemFontNames(), StringComparer.OrdinalIgnoreCase);
        foreach (var c in Curated) if (installed.Contains(c)) Add(c);
        foreach (var n in installed.OrderBy(x => x, StringComparer.CurrentCultureIgnoreCase)) Add(n);
        return names;
    }

    public static FontFamily Resolve(string? name)
    {
        // A name that isn't installed here (e.g. "Segoe UI" on Linux) simply falls back to the system font.
        if (string.IsNullOrWhiteSpace(name)) name = "Segoe UI";
        return new FontFamily(name);
    }

    private static string NameFor(FontSettings s, string key)
    {
        if (s.PerElement && s.Elements.TryGetValue(key, out var n) && !string.IsNullOrWhiteSpace(n)) return n;
        return s.GlobalFont;
    }

    /// <summary>Publishes the fonts as "FontGeneral", "FontTitle", ... application resources.</summary>
    public static void Apply(FontSettings s)
    {
        var res = Application.Current?.Resources;
        if (res == null) return;
        foreach (var (key, _) in Elements)
            res["Font" + key] = Resolve(NameFor(s, key));
    }
}
