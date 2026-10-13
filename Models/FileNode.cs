using System.IO;
using System.Collections.ObjectModel;
using System.ComponentModel;
using SavefileManager.Helpers;

namespace SavefileManager.Models;

/// <summary>One file or folder shown in a save list.</summary>
public class FileNode : INotifyPropertyChanged
{
    private bool _isExpanded;
    private bool _isSelected;

    public string Name { get; init; } = "";
    public string FullPath { get; init; } = "";
    public bool IsFolder { get; init; }

    /// <summary>Picture chosen by the user for this file/folder; null shows the default icon.</summary>
    public System.Windows.Media.ImageSource? CustomIcon { get; set; }
    public bool HasCustomIcon => CustomIcon != null;
    public string Icon => IsFolder ? "\U0001F4C1" : "\U0001F4C4";
    public ObservableCollection<FileNode> Children { get; } = new();

    public bool IsExpanded
    {
        get => _isExpanded;
        set { if (_isExpanded != value) { _isExpanded = value; Raise(nameof(IsExpanded)); } }
    }

    public bool IsSelected
    {
        get => _isSelected;
        set { if (_isSelected != value) { _isSelected = value; Raise(nameof(IsSelected)); } }
    }

    public event PropertyChangedEventHandler? PropertyChanged;
    private void Raise(string name) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));

    // ------------------------------------------------------------------ building

    /// <summary>
    /// Builds the list for a folder. The personal list always shows sub-folders; the game list
    /// only does when matching files actually exist below the top folder (same as the original).
    /// </summary>
    public static List<FileNode> Build(string root, string ext, bool alwaysShowSubfolders)
    {
        var result = new List<FileNode>();
        if (!Directory.Exists(root)) return result;

        bool showSub = alwaysShowSubfolders || AnyMatchBelow(root, ext);
        if (showSub) AddSubfolders(root, result, ext);
        AddFiles(root, result, ext);
        return result;
    }

    private static bool AnyMatchBelow(string root, string ext)
    {
        try
        {
            return Directory.EnumerateFiles(root, "*", SearchOption.AllDirectories)
                .Any(f => FileOps.MatchesExtension(Path.GetFileName(f), ext));
        }
        catch { return false; }
    }

    private static void AddSubfolders(string path, List<FileNode> into, string ext)
    {
        string[] dirs;
        try { dirs = Directory.GetDirectories(path); } catch { return; }
        foreach (var dir in dirs.OrderBy(d => Path.GetFileName(d), FileOps.NaturalComparer.Instance))
        {
            var node = new FileNode { Name = Path.GetFileName(dir), FullPath = dir, IsFolder = true };
            var kids = new List<FileNode>();
            AddSubfolders(dir, kids, ext);
            AddFiles(dir, kids, ext);
            foreach (var k in kids) node.Children.Add(k);
            into.Add(node);
        }
    }

    private static void AddFiles(string path, List<FileNode> into, string ext)
    {
        string[] files;
        try { files = Directory.GetFiles(path); } catch { return; }
        foreach (var file in files
                     .Where(f => FileOps.MatchesExtension(Path.GetFileName(f), ext))
                     .OrderBy(f => Path.GetFileName(f), FileOps.NaturalComparer.Instance))
        {
            into.Add(new FileNode { Name = Path.GetFileName(file), FullPath = file, IsFolder = false });
        }
    }

    public static IEnumerable<FileNode> Flatten(IEnumerable<FileNode> nodes)
    {
        foreach (var n in nodes)
        {
            yield return n;
            foreach (var c in Flatten(n.Children)) yield return c;
        }
    }
}
