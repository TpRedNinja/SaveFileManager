using System.IO;
using System.Text.Json;
using SavefileManager.Models;

namespace SavefileManager.Services;

/// <summary>
/// Loads/saves config/settings.json.
///
/// Profiles are NEVER removed automatically - not when a folder is missing, not when the
/// settings file is damaged, and not when another computer saves an older copy of the file.
/// Folder paths are stored per computer, and every save first merges in whatever another
/// computer wrote so two PCs sharing one (synced) config folder do not overwrite each other.
/// </summary>
public class SettingsService
{
    public static string Machine => Environment.MachineName;

    public string ConfigDir { get; }
    public string SettingsPath { get; }
    public string BackupsDir { get; }
    public string DeletedDir { get; }

    public AppSettings Data { get; private set; } = new();

    /// <summary>Set when settings.json could not be read normally; shown to the user once.</summary>
    public string? LoadWarning { get; private set; }

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNameCaseInsensitive = true
    };

    public SettingsService()
    {
        ConfigDir = ResolveConfigDir();
        SettingsPath = Path.Combine(ConfigDir, "settings.json");
        BackupsDir = Path.Combine(ConfigDir, "backups");
        DeletedDir = Path.Combine(BackupsDir, "Deleted");
        Directory.CreateDirectory(BackupsDir);
        Directory.CreateDirectory(DeletedDir);
        Log.Init(Path.Combine(ConfigDir, "log.log"));
        Load();
    }

    // Portable like the original: config lives next to the exe. If that folder is read-only
    // (e.g. Program Files) fall back to %AppData%.
    private static string ResolveConfigDir()
    {
        var primary = Path.Combine(AppContext.BaseDirectory, "config");
        if (CanWrite(primary)) return primary;
        var fallback = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "SavefileManager", "config");
        Directory.CreateDirectory(fallback);
        return fallback;
    }

    private static bool CanWrite(string dir)
    {
        try
        {
            Directory.CreateDirectory(dir);
            var probe = Path.Combine(dir, ".write-test");
            File.WriteAllText(probe, "x");
            File.Delete(probe);
            return true;
        }
        catch { return false; }
    }

    // ------------------------------------------------------------------ loading

    private void Load()
    {
        if (!File.Exists(SettingsPath))
        {
            Data = new AppSettings();
            Normalize();
            Save();
            return;
        }

        try
        {
            Data = ReadFile(SettingsPath) ?? new AppSettings();
            Log.Info("Settings file successfully loaded");
        }
        catch (Exception ex)
        {
            Log.Error($"Settings file unreadable: {ex.Message}");
            // Keep the damaged file so nothing is ever lost, then try the automatic backup copy.
            string aside = Path.Combine(ConfigDir, $"settings.corrupt-{DateTime.Now:yyyyMMdd-HHmmss}.json");
            try { File.Copy(SettingsPath, aside, true); } catch { /* ignore */ }

            var bak = SettingsPath + ".bak";
            try
            {
                Data = ReadFile(bak) ?? new AppSettings();
                LoadWarning = "settings.json could not be read, so the last backup copy (settings.json.bak) was loaded.\n\n" +
                              $"The damaged file was kept as:\n{aside}";
            }
            catch
            {
                Data = new AppSettings();
                LoadWarning = "settings.json could not be read and no usable backup copy exists. " +
                              "Starting with an empty list.\n\n" +
                              $"The damaged file was kept (nothing was deleted) as:\n{aside}";
            }
        }
        Normalize();
    }

    private static AppSettings? ReadFile(string path)
    {
        var text = File.ReadAllText(path);
        return JsonSerializer.Deserialize<AppSettings>(text, JsonOptions);
    }

    private void Normalize()
    {
        Data.Profiles ??= new();
        Data.Machines ??= new();
        Data.RemovedProfileIds ??= new();
        Data.Appearance ??= new AppearanceSettings();
        Data.Appearance.Fonts ??= new FontSettings();
        Data.Appearance.Fonts.Elements ??= new();
        Data.Appearance.PersonalBackground ??= new ImageSlot();
        Data.Appearance.GameBackground ??= new ImageSlot();
        Data.CustomTheme ??= BuiltInThemes.Create("Dark", "Custom");
        Data.CustomTheme.FillMissing(BuiltInThemes.Create("Dark"));

        Data.Profiles.RemoveAll(p => Data.RemovedProfileIds.Contains(p.Id));
        var names = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var p in Data.Profiles)
        {
            if (string.IsNullOrWhiteSpace(p.Id)) p.Id = Guid.NewGuid().ToString("N");
            p.Paths ??= new();
            p.FileIcons ??= new();
            if (string.IsNullOrWhiteSpace(p.Name)) p.Name = "Profile";
            var unique = p.Name;
            for (int i = 2; !names.Add(unique); i++) unique = $"{p.Name} ({i})";
            p.Name = unique;
        }
    }

    // ------------------------------------------------------------------ per-PC state

    public MachineState State
    {
        get
        {
            if (!Data.Machines.TryGetValue(Machine, out var s))
                Data.Machines[Machine] = s = new MachineState();
            return s;
        }
    }

    public string NextProfileName()
    {
        for (int i = Data.Profiles.Count + 1; ; i++)
        {
            var name = $"Profile {i}";
            if (!Data.Profiles.Any(p => p.Name.Equals(name, StringComparison.OrdinalIgnoreCase)))
                return name;
        }
    }

    public bool NameExists(string name, string? exceptId = null) =>
        Data.Profiles.Any(p => p.Id != exceptId && p.Name.Equals(name, StringComparison.OrdinalIgnoreCase));

    /// <summary>Only ever called after the user explicitly confirms "Remove profile".</summary>
    public void RemoveProfile(Profile profile)
    {
        Data.Profiles.Remove(profile);
        if (!Data.RemovedProfileIds.Contains(profile.Id))
            Data.RemovedProfileIds.Add(profile.Id);
    }

    // ------------------------------------------------------------------ saving / merging

    /// <summary>
    /// Pulls in changes written by another computer (new profiles, that PC's folder paths,
    /// newer theme). Our own computer's data always wins for our own entries.
    /// </summary>
    public (bool ProfilesChanged, bool ThemeChanged) MergeFromDisk()
    {
        bool profilesChanged = false, themeChanged = false;
        AppSettings? disk;
        try
        {
            if (!File.Exists(SettingsPath)) return (false, false);
            disk = ReadFile(SettingsPath);
        }
        catch { return (false, false); }
        if (disk == null) return (false, false);

        foreach (var id in disk.RemovedProfileIds ?? new())
        {
            if (!Data.RemovedProfileIds.Contains(id)) Data.RemovedProfileIds.Add(id);
            if (Data.Profiles.RemoveAll(p => p.Id == id) > 0) profilesChanged = true;
        }

        foreach (var dp in disk.Profiles ?? new())
        {
            if (string.IsNullOrWhiteSpace(dp.Id) || Data.RemovedProfileIds.Contains(dp.Id)) continue;
            dp.Paths ??= new();
            dp.FileIcons ??= new();
            var mine = Data.Profiles.FirstOrDefault(p => p.Id == dp.Id);
            if (mine == null)
            {
                var unique = dp.Name;
                for (int i = 2; NameExists(unique); i++) unique = $"{dp.Name} ({i})";
                dp.Name = unique;
                Data.Profiles.Add(dp);
                profilesChanged = true;
                continue;
            }

            foreach (var (machine, pair) in dp.Paths)
            {
                if (machine == Machine)
                {
                    if (!mine.Paths.ContainsKey(machine)) { mine.Paths[machine] = pair; profilesChanged = true; }
                    continue;
                }
                if (!mine.Paths.TryGetValue(machine, out var cur) ||
                    cur.Personal != pair.Personal || cur.Game != pair.Game)
                {
                    mine.Paths[machine] = pair;
                    profilesChanged = true;
                }
            }

            if (dp.UpdatedUtc > mine.UpdatedUtc)
            {
                if (!NameExists(dp.Name, mine.Id)) mine.Name = dp.Name;
                mine.Extension = dp.Extension;
                mine.IconFile = dp.IconFile;
                mine.FileIcons = dp.FileIcons;
                mine.UpdatedUtc = dp.UpdatedUtc;
                profilesChanged = true;
            }
        }

        foreach (var (machine, state) in disk.Machines ?? new())
            if (machine != Machine) Data.Machines[machine] = state;

        if (disk.ThemeUpdatedUtc > Data.ThemeUpdatedUtc)
        {
            Data.Theme = disk.Theme;
            if (disk.CustomTheme != null) Data.CustomTheme = disk.CustomTheme;
            Data.ThemeUpdatedUtc = disk.ThemeUpdatedUtc;
            Normalize();
            themeChanged = true;
        }
        // fonts / background pictures changed on another PC (reported as a "theme" change so they get re-applied)
        if (disk.Appearance != null && disk.Appearance.UpdatedUtc > Data.Appearance.UpdatedUtc)
        {
            Data.Appearance = disk.Appearance;
            Normalize();
            themeChanged = true;
        }
        return (profilesChanged, themeChanged);
    }

    public bool Save()
    {
        try
        {
            MergeFromDisk();
            var json = JsonSerializer.Serialize(Data, JsonOptions);
            var tmp = SettingsPath + ".tmp";
            File.WriteAllText(tmp, json);
            if (File.Exists(SettingsPath))
                File.Replace(tmp, SettingsPath, SettingsPath + ".bak");
            else
                File.Move(tmp, SettingsPath);
            return true;
        }
        catch (Exception ex)
        {
            Log.Error($"Couldn't save settings {ex.Message}");
            return false;
        }
    }
}
