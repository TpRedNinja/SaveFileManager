using System.IO;
using System.Text.Json.Serialization;

namespace SavefileManager.Models;

/// <summary>The two folders a profile uses on ONE computer.</summary>
public class FolderPair
{
    public string Personal { get; set; } = "";
    public string Game { get; set; } = "";

    public bool BothExist => Directory.Exists(Personal) && Directory.Exists(Game);
}

/// <summary>
/// A save profile. Folder paths are stored per computer (keyed by machine name) so that
/// fixing the paths on one PC never touches the paths used on another PC.
/// </summary>
public class Profile
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public string Name { get; set; } = "";

    /// <summary>File extension including the dot (".sgd"), or "" for any file / folders.</summary>
    public string Extension { get; set; } = "";

    public Dictionary<string, FolderPair> Paths { get; set; } = [];
    public DateTime UpdatedUtc { get; set; } = DateTime.UtcNow;

    /// <summary>Optional picture shown in the profile selector (a copy kept in config/images).</summary>
    public string? IconFile { get; set; }

    /// <summary>
    /// Custom pictures for files/folders. Key is "P|relative/path" (your list) or "G|relative/path"
    /// (game list), so the same icons work on every PC; value is a file in config/images.
    /// </summary>
    public Dictionary<string, string> FileIcons { get; set; } = [];

    [JsonIgnore]
    public FolderPair? PathsForThisPc =>
        Paths.TryGetValue(Environment.MachineName, out var p) ? p : null;

    /// <summary>True when this PC has folders saved for the profile AND they exist right now.</summary>
    [JsonIgnore]
    public bool IsAvailableHere => PathsForThisPc is { } p && p.BothExist;

    public void Touch() => UpdatedUtc = DateTime.UtcNow;
}
