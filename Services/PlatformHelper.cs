using System.Diagnostics;
using System.Runtime.Versioning;
using Microsoft.Win32;

namespace SavefileManager.Services;

/// <summary>The few things that differ between Windows, Linux and macOS.</summary>
public static class PlatformHelper
{
    /// <summary>Opens a folder in the system file manager.</summary>
    public static void OpenFolder(string? path)
    {
        if (string.IsNullOrEmpty(path) || !Directory.Exists(path)) return;
        try
        {
            if (OperatingSystem.IsWindows())
            {
                Process.Start(new ProcessStartInfo(path) { UseShellExecute = true });
            }
            else
            {
                var psi = new ProcessStartInfo(OperatingSystem.IsMacOS() ? "open" : "xdg-open")
                {
                    UseShellExecute = false
                };
                psi.ArgumentList.Add(path);
                Process.Start(psi);
            }
        }
        catch (Exception ex)
        {
            Log.Error("Couldn't open folder: " + ex.Message);
        }
    }

    // ------------------------------------------------------------------ per-user font install

    private static string? UserFontDirectory()
    {
        if (OperatingSystem.IsWindows())
            return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "Microsoft", "Windows", "Fonts");
        if (OperatingSystem.IsMacOS())
            return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Library", "Fonts");

        var data = Environment.GetEnvironmentVariable("XDG_DATA_HOME");
        if (string.IsNullOrWhiteSpace(data))
            data = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".local", "share");
        return Path.Combine(data, "fonts", "SavefileManager");
    }

    /// <summary>
    /// Makes a font file available to this user's programs (no administrator rights needed). The font
    /// shows up the next time the program starts. Returns true when something new was installed.
    /// </summary>
    public static bool InstallUserFont(string sourceFile, string family, string uniqueFileName)
    {
        try
        {
            var dir = UserFontDirectory();
            if (dir == null) return false;
            Directory.CreateDirectory(dir);
            var dest = Path.Combine(dir, uniqueFileName);
            bool isNew = !File.Exists(dest);
            if (isNew) File.Copy(sourceFile, dest, false);
            if (OperatingSystem.IsWindows()) RegisterWindowsFont(family, dest);
            return isNew;
        }
        catch (Exception ex)
        {
            Log.Warn($"Couldn't install font {family}: {ex.Message}");
            return false;
        }
    }

    [SupportedOSPlatform("windows")]
    private static void RegisterWindowsFont(string family, string file)
    {
        var type = file.EndsWith(".otf", StringComparison.OrdinalIgnoreCase) ? "OpenType" : "TrueType";
        using var key = Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows NT\CurrentVersion\Fonts", true);
        key?.SetValue($"{family} ({type})", file);
    }
}
