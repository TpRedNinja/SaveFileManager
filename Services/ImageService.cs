using Avalonia.Media.Imaging;

namespace SavefileManager.Services;

/// <summary>
/// Every picture the user chooses (backgrounds, file icons, profile icons) is COPIED into
/// config/images, so deleting or moving the original file never breaks anything.
/// </summary>
public static class ImageService
{
    /// <summary>File patterns for the open-file dialog.</summary>
    public static readonly string[] Patterns =
    {
        "*.png", "*.jpg", "*.jpeg", "*.bmp", "*.gif", "*.ico", "*.tif", "*.tiff", "*.webp",
        "*.PNG", "*.JPG", "*.JPEG", "*.BMP", "*.GIF", "*.ICO", "*.TIF", "*.TIFF", "*.WEBP",
    };

    private static string _dir = "";
    private static readonly Dictionary<string, Bitmap> Cache = new();

    public static void Init(string configDir)
    {
        _dir = Path.Combine(configDir, "images");
        Directory.CreateDirectory(_dir);
    }

    private static Bitmap? Decode(string fullPath, int decodeWidth)
    {
        try
        {
            using var fs = File.OpenRead(fullPath);
            var bmp = new Bitmap(fs);
            if (decodeWidth > 0 && bmp.PixelSize.Width > decodeWidth)
            {
                // big picture: decode a smaller copy (saves memory); never enlarge small ones
                bmp.Dispose();
                fs.Position = 0;
                bmp = Bitmap.DecodeToWidth(fs, decodeWidth);
            }
            return bmp;
        }
        catch
        {
            return null;
        }
    }

    /// <summary>Copies the picture into config/images/&lt;subfolder&gt; and returns its relative name.</summary>
    public static string ImportImage(string sourcePath, string subfolder)
    {
        var probe = Decode(sourcePath, 64);
        if (probe == null)
            throw new InvalidDataException("That file isn't a picture this program can read (png, jpg, bmp, gif, ico, tiff or webp).");
        probe.Dispose();

        var dir = Path.Combine(_dir, subfolder);
        Directory.CreateDirectory(dir);
        var name = Guid.NewGuid().ToString("N") + Path.GetExtension(sourcePath).ToLowerInvariant();
        File.Copy(sourcePath, Path.Combine(dir, name), false);
        return subfolder + "/" + name;
    }

    /// <summary>Loads an image from config/images. Returns null if it is missing or unreadable.</summary>
    public static Bitmap? Load(string? relative, int decodeWidth = 0)
    {
        if (string.IsNullOrWhiteSpace(relative) || _dir.Length == 0) return null;
        var key = relative + "@" + decodeWidth;
        if (Cache.TryGetValue(key, out var hit)) return hit;

        var root = Path.GetFullPath(_dir);
        var full = Path.GetFullPath(Path.Combine(root, relative.Replace('/', Path.DirectorySeparatorChar)));
        // must stay inside config/images (no "..\.." tricks from an edited settings file)
        if (!full.StartsWith(root + Path.DirectorySeparatorChar, StringComparison.Ordinal) || !File.Exists(full))
            return null;

        var img = Decode(full, decodeWidth);
        if (img != null) Cache[key] = img;
        return img;
    }
}
