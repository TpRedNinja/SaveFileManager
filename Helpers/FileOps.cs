using System.IO;

namespace SavefileManager.Helpers;

public static class FileOps
{
    /// <summary>
    /// Windows file systems ignore letter case in paths; Linux file systems do not.
    /// Use this whenever two paths are compared.
    /// </summary>
    public static StringComparison PathComparison =>
        OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal;

    public static bool SamePath(string a, string b) => string.Equals(a, b, PathComparison);

    /// <summary>Sorts like a file manager (file2 before file10), on every platform.</summary>
    public sealed class NaturalComparer : IComparer<string>
    {
        public static readonly NaturalComparer Instance = new();

        public int Compare(string? x, string? y)
        {
            x ??= ""; y ??= "";
            int i = 0, j = 0;
            while (i < x.Length && j < y.Length)
            {
                if (char.IsDigit(x[i]) && char.IsDigit(y[j]))
                {
                    int si = i, sj = j;
                    while (i < x.Length && char.IsDigit(x[i])) i++;
                    while (j < y.Length && char.IsDigit(y[j])) j++;
                    var a = x.AsSpan(si, i - si).TrimStart('0');
                    var b = y.AsSpan(sj, j - sj).TrimStart('0');
                    if (a.Length != b.Length) return a.Length < b.Length ? -1 : 1;   // more digits = bigger number
                    int c = a.CompareTo(b, StringComparison.Ordinal);
                    if (c != 0) return c;
                }
                else
                {
                    int c = char.ToUpperInvariant(x[i]).CompareTo(char.ToUpperInvariant(y[j]));
                    if (c != 0) return c;
                    i++; j++;
                }
            }
            if (i < x.Length) return 1;
            if (j < y.Length) return -1;
            return string.Compare(x, y, StringComparison.Ordinal);   // identical except case/leading zeros: stable order
        }
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
