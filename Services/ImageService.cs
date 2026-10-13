using System;
using System.Collections.Generic;
using System.IO;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace SavefileManager.Services;

/// <summary>
/// Every picture the user chooses (backgrounds, file icons, profile icons) is COPIED into
/// config/images, so deleting or moving the original file never breaks anything.
/// </summary>
public static class ImageService
{
    public const string FileFilter =
        "Images (*.png;*.jpg;*.jpeg;*.bmp;*.gif;*.ico;*.tif;*.tiff)|*.png;*.jpg;*.jpeg;*.bmp;*.gif;*.ico;*.tif;*.tiff|All files|*.*";

    private static string _dir = "";
    private static readonly Dictionary<string, ImageSource> Cache = new();

    public static void Init(string configDir)
    {
        _dir = Path.Combine(configDir, "images");
        Directory.CreateDirectory(_dir);
    }

    private static BitmapImage? Decode(string fullPath, int decodeWidth)
    {
        try
        {
            var bi = new BitmapImage();
            bi.BeginInit();
            bi.CacheOption = BitmapCacheOption.OnLoad;   // read it now, don't keep the file locked
            bi.CreateOptions = BitmapCreateOptions.IgnoreColorProfile;
            bi.UriSource = new Uri(fullPath, UriKind.Absolute);
            if (decodeWidth > 0) bi.DecodePixelWidth = decodeWidth;
            bi.EndInit();
            bi.Freeze();
            return bi;
        }
        catch
        {
            return null;
        }
    }

    /// <summary>Copies the picture into config/images/&lt;subfolder&gt; and returns its relative name.</summary>
    public static string ImportImage(string sourcePath, string subfolder)
    {
        if (Decode(sourcePath, 64) == null)
            throw new InvalidDataException("That file isn't a picture Windows can read (png, jpg, bmp, gif, ico or tiff).");

        var dir = Path.Combine(_dir, subfolder);
        Directory.CreateDirectory(dir);
        var name = Guid.NewGuid().ToString("N") + Path.GetExtension(sourcePath).ToLowerInvariant();
        File.Copy(sourcePath, Path.Combine(dir, name), false);
        return subfolder + "/" + name;
    }

    /// <summary>Loads an image from config/images. Returns null if it is missing or unreadable.</summary>
    public static ImageSource? Load(string? relative, int decodeWidth = 0)
    {
        if (string.IsNullOrWhiteSpace(relative) || _dir.Length == 0) return null;
        var key = relative + "@" + decodeWidth;
        if (Cache.TryGetValue(key, out var hit)) return hit;

        var root = Path.GetFullPath(_dir);
        var full = Path.GetFullPath(Path.Combine(root, relative.Replace('/', Path.DirectorySeparatorChar)));
        if (!full.StartsWith(root, StringComparison.OrdinalIgnoreCase) || !File.Exists(full)) return null;

        var img = Decode(full, decodeWidth);
        if (img != null) Cache[key] = img;
        return img;
    }
}
