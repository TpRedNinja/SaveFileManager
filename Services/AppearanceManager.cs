using Avalonia;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using SavefileManager.Models;

namespace SavefileManager.Services;

/// <summary>Applies fonts and list background pictures to the running application.</summary>
public static class AppearanceManager
{
    public static void Apply(AppearanceSettings a)
    {
        FontService.Apply(a.Fonts);
        ApplyBackgrounds(a);
    }

    /// <summary>
    /// Fonts now, pictures later: decoding big pictures is the slow part, so at startup the lists get
    /// an empty background first and <see cref="ApplyBackgrounds"/> fills them in once the window is up.
    /// </summary>
    public static void ApplyFontsOnly(AppearanceSettings a)
    {
        FontService.Apply(a.Fonts);
        var res = Application.Current?.Resources;
        if (res == null) return;
        res["PersonalImageBrush"] = Brushes.Transparent;
        res["GameImageBrush"] = Brushes.Transparent;
    }

    public static void ApplyBackgrounds(AppearanceSettings a)
    {
        SetBackground("PersonalImageBrush", a.PersonalBackground);
        SetBackground("GameImageBrush", a.GameBackground);
    }

    private static void SetBackground(string resourceKey, ImageSlot slot)
    {
        var app = Application.Current;
        if (app == null) return;

        IBrush brush = Brushes.Transparent;
        Bitmap? image = ImageService.Load(slot.ImageFile, 1600);
        if (image != null)
        {
            brush = new ImageBrush
            {
                Source = image,
                Opacity = Math.Clamp(slot.Opacity, 0.05, 1.0),
                Stretch = slot.Stretch switch
                {
                    "Fill" => Stretch.Fill,
                    "Uniform" => Stretch.Uniform,
                    _ => Stretch.UniformToFill
                },
                AlignmentX = AlignmentX.Center,
                AlignmentY = AlignmentY.Center
            };
        }
        app.Resources[resourceKey] = brush;
    }
}
