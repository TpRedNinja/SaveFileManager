using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Windows.Media;

namespace SavefileManager.Models;

public class FontSettings
{
    /// <summary>Font used for all text (and for every part not given its own font).</summary>
    public string GlobalFont { get; set; } = "Segoe UI";

    /// <summary>When true, <see cref="Elements"/> can give individual parts their own font.</summary>
    public bool PerElement { get; set; }

    /// <summary>Element key (see FontService.Elements) to font name.</summary>
    public Dictionary<string, string> Elements { get; set; } = new();
}

/// <summary>A background picture behind one of the two file lists.</summary>
public class ImageSlot
{
    /// <summary>File inside config/images (a copy made by the program), or null for no image.</summary>
    public string? ImageFile { get; set; }
    public double Opacity { get; set; } = 0.35;

    /// <summary>"UniformToFill", "Fill" or "Uniform".</summary>
    public string Stretch { get; set; } = "UniformToFill";
}

public class AppearanceSettings
{
    public FontSettings Fonts { get; set; } = new();
    public ImageSlot PersonalBackground { get; set; } = new();
    public ImageSlot GameBackground { get; set; } = new();
    public DateTime UpdatedUtc { get; set; } = DateTime.MinValue;

    public AppearanceSettings Clone() =>
        JsonSerializer.Deserialize<AppearanceSettings>(JsonSerializer.Serialize(this)) ?? new AppearanceSettings();
}

/// <summary>One entry of the profile selector (a profile, or the "Add..." entry).</summary>
public class ProfileItem
{
    public string Id { get; init; } = "";
    public string Name { get; init; } = "";
    public ImageSource? Icon { get; init; }
    public bool IsAdd { get; init; }
    public bool HasIcon => Icon != null;
}
