namespace SavefileManager.Models;

/// <summary>UI state that is remembered separately for each computer.</summary>
public class MachineState
{
    public string? LastProfileId { get; set; }
    public double? Left { get; set; }
    public double? Top { get; set; }
    public double? Width { get; set; }
    public double? Height { get; set; }
}

public class AppSettings
{
    public List<Profile> Profiles { get; set; } = new();

    /// <summary>
    /// Ids of profiles the user explicitly removed. Only used so that a removed profile does not
    /// come back when another computer saves an older copy of the settings file.
    /// </summary>
    public List<string> RemovedProfileIds { get; set; } = new();

    public Dictionary<string, MachineState> Machines { get; set; } = new();

    /// <summary>"Dark", "Dark (Alt)", "Classic", "Solarized" or "Custom".</summary>
    public string Theme { get; set; } = "Dark";
    public ThemeDefinition CustomTheme { get; set; } = BuiltInThemes.Create("Dark", "Custom");
    public DateTime ThemeUpdatedUtc { get; set; } = DateTime.MinValue;

    /// <summary>Fonts and list background pictures.</summary>
    public AppearanceSettings Appearance { get; set; } = new();
}
