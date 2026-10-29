using Avalonia.Controls;
using Avalonia.Platform.Storage;
using SavefileManager.Services;

namespace SavefileManager.Views;

/// <summary>Open / save dialogs that work the same on Windows and Linux.</summary>
public static class Pickers
{
    public static async Task<string?> PickFolder(Window owner, string title, string? current)
    {
        IStorageFolder? start = null;
        if (!string.IsNullOrWhiteSpace(current) && Directory.Exists(current))
        {
            try { start = await owner.StorageProvider.TryGetFolderFromPathAsync(current); }
            catch { /* just open at the default place */ }
        }
        var result = await owner.StorageProvider.OpenFolderPickerAsync(new FolderPickerOpenOptions
        {
            Title = title,
            AllowMultiple = false,
            SuggestedStartLocation = start
        });
        return result.Count > 0 ? result[0].TryGetLocalPath() : null;
    }

    public static async Task<List<string>> OpenFiles(Window owner, string title, string typeName,
        IReadOnlyList<string> patterns, bool multiple)
    {
        var result = await owner.StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = title,
            AllowMultiple = multiple,
            FileTypeFilter =
            [
                new FilePickerFileType(typeName) { Patterns = patterns },
                FilePickerFileTypes.All
            ]
        });
        return [.. result.Select(f => f.TryGetLocalPath()).Where(p => p != null).Select(p => p!)];
    }

    public static async Task<string?> OpenFile(Window owner, string title, string typeName, IReadOnlyList<string> patterns)
    {
        var files = await OpenFiles(owner, title, typeName, patterns, false);
        return files.Count > 0 ? files[0] : null;
    }

    public static Task<string?> OpenImage(Window owner, string title) =>
        OpenFile(owner, title, "Pictures", ImageService.Patterns);

    public static async Task<string?> SaveFile(Window owner, string title, string suggestedName,
        string typeName, IReadOnlyList<string> patterns)
    {
        var file = await owner.StorageProvider.SaveFilePickerAsync(new FilePickerSaveOptions
        {
            Title = title,
            SuggestedFileName = suggestedName,
            FileTypeChoices = [new FilePickerFileType(typeName) { Patterns = patterns }]
        });
        return file?.TryGetLocalPath();
    }
}
