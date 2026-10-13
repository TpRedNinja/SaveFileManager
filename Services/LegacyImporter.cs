using System.IO;
using System.Text.Json;
using SavefileManager.Helpers;
using SavefileManager.Models;

namespace SavefileManager.Services;

public record LegacyImportResult(List<Profile> Added, List<string> SkippedExisting, int Unreadable);

/// <summary>
/// Converts the settings.json of Green-Bat's original Savefile Manager into profiles for this app.
/// The original file is only read, never modified.
/// Original layout:  "Profiles": { "Name": [ "Name", "personal folder", "game folder", ".ext" ] }
/// </summary>
public static class LegacyImporter
{
    public static LegacyImportResult Import(string path, SettingsService store)
    {
        using var doc = JsonDocument.Parse(File.ReadAllText(path));
        var root = doc.RootElement;

        if (root.ValueKind != JsonValueKind.Object ||
            !root.TryGetProperty("Profiles", out var profiles) ||
            profiles.ValueKind != JsonValueKind.Object)
        {
            throw new InvalidDataException(
                "This doesn't look like a Savefile Manager settings.json from the original program " +
                "(there is no \"Profiles\" section in the old format).");
        }

        var added = new List<Profile>();
        var skipped = new List<string>();
        int unreadable = 0;

        foreach (var entry in profiles.EnumerateObject())
        {
            var arr = entry.Value;
            if (arr.ValueKind != JsonValueKind.Array || arr.GetArrayLength() < 3) { unreadable++; continue; }

            string Item(int i) =>
                i < arr.GetArrayLength() && arr[i].ValueKind == JsonValueKind.String ? arr[i].GetString() ?? "" : "";

            var name = Item(0).Trim();
            if (name.Length == 0) name = entry.Name.Trim();
            var personal = Tidy(Item(1));
            var game = Tidy(Item(2));

            if (name.Length == 0 || name.Equals("Add...", StringComparison.OrdinalIgnoreCase) ||
                personal.Length == 0 || game.Length == 0)
            {
                unreadable++;
                continue;
            }

            if (store.NameExists(name))
            {
                skipped.Add(name);
                continue;
            }

            var profile = new Profile
            {
                Name = name,
                Extension = FileOps.NormalizeExtension(Item(3))
            };
            // The old file only had one set of folders, so they become this PC's folders.
            profile.Paths[SettingsService.Machine] = new FolderPair { Personal = personal, Game = game };
            store.Data.Profiles.Add(profile);
            added.Add(profile);
        }

        return new LegacyImportResult(added, skipped, unreadable);
    }

    // The original (Python) stored paths with forward slashes: C:/Users/...
    private static string Tidy(string p)
    {
        p = p.Trim();
        if (p.Length == 0) return p;
        try { return Path.GetFullPath(p).TrimEnd('\\', '/'); }
        catch { return p.Replace('/', '\\').TrimEnd('\\'); }
    }
}
