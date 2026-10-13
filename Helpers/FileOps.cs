using System.IO;
using System.Runtime.InteropServices;

namespace SavefileManager.Helpers;

public static class FileOps
{
    // Sorts like Windows Explorer (file2 before file10).
    [DllImport("shlwapi.dll", CharSet = CharSet.Unicode)]
    private static extern int StrCmpLogicalW(string x, string y);

    public sealed class NaturalComparer : IComparer<string>
    {
        public static readonly NaturalComparer Instance = new();
        public int Compare(string? x, string? y) => StrCmpLogicalW(x ?? "", y ?? "");
    }

    /// <summary>Most common file extension in a folder (or the extension of a file).</summary>
    public static string GetExt(string path)
    {
        if (File.Exists(path)) return Path.GetExtension(path);
        if (!Directory.Exists(path)) return "";
        try
        {
            return Directory.EnumerateFiles(path)
                .GroupBy(f => Path.GetExtension(f), StringComparer.OrdinalIgnoreCase)
                .OrderByDescending(g => g.Count())
                .Select(g => g.Key)
                .FirstOrDefault() ?? "";
        }
        catch { return ""; }
    }

    public static string NormalizeExtension(string? ext)
    {
        ext = (ext ?? "").Trim();
        if (ext == "*" || ext == "") return "";
        return ext.StartsWith('.') ? ext : "." + ext;
    }

    public static bool MatchesExtension(string fileName, string ext) =>
        ext.Length == 0 || fileName.EndsWith(ext, StringComparison.OrdinalIgnoreCase);

    public static void CopyDirectory(string src, string dst, bool overwrite)
    {
        Directory.CreateDirectory(dst);
        foreach (var file in Directory.GetFiles(src))
            File.Copy(file, Path.Combine(dst, Path.GetFileName(file)), overwrite);
        foreach (var dir in Directory.GetDirectories(src))
            CopyDirectory(dir, Path.Combine(dst, Path.GetFileName(dir)), overwrite);
    }

    public static string TimestampPrefix() => DateTime.Now.ToString("yyyy_MM_dd_HH'hr'_mm'min'_ss's_'");

    // ---------------- Batman: Arkham Knight special handling (from the original) ----------------
    // Arkham Knight keeps three slots per save (BAK1Save0x0/x1/x2.sgd).

    /// <summary>Replaces the chosen game save and every other slot of it that exists.</summary>
    public static void ArkhamReplace(string src, string dst)
    {
        var dir = Path.GetDirectoryName(dst)!;
        var stem = Path.GetFileNameWithoutExtension(dst);
        if (stem.Length < 2) { File.Copy(src, dst, true); return; }
        var prefix = stem[..^1];
        File.Copy(src, dst, true);
        for (int i = 0; i < 3; i++)
        {
            var slot = Path.Combine(dir, $"{prefix}{i}.sgd");
            if (File.Exists(slot)) File.Copy(src, slot, true);
        }
    }

    /// <summary>Backs up the most recently written slot of the chosen game save.</summary>
    public static void ArkhamBackup(string src, string dst)
    {
        var dir = Path.GetDirectoryName(src)!;
        var stem = Path.GetFileNameWithoutExtension(src);
        string best = src;
        if (stem.Length >= 2)
        {
            var prefix = stem[..^1];
            var newest = DateTime.MinValue;
            for (int i = 0; i < 3; i++)
            {
                var slot = Path.Combine(dir, $"{prefix}{i}.sgd");
                if (!File.Exists(slot)) continue;
                var t = File.GetLastWriteTime(slot);
                if (t > newest) { newest = t; best = slot; }
            }
        }
        File.Copy(best, dst, true);
    }
}
