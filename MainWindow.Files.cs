using System.Collections.ObjectModel;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Threading;
using Avalonia.VisualTree;
using SavefileManager.Helpers;
using SavefileManager.Models;
using SavefileManager.Services;
using SavefileManager.Views;

namespace SavefileManager;

// File lists, watching folders and the backup / replace / rename / delete operations.
public partial class MainWindow
{
    // Windows paths ignore case, Linux paths do not.
    private static readonly StringComparer PathCmp =
        OperatingSystem.IsWindows() ? StringComparer.OrdinalIgnoreCase : StringComparer.Ordinal;

    private FileSystemWatcher? _watchPersonal, _watchGame;
    private DispatcherTimer _refreshTimer = null!;

    // ------------------------------------------------------------------ folder watching

    private void InitWatcherTimer()
    {
        _refreshTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(450) };
        _refreshTimer.Tick += (_, _) =>
        {
            _refreshTimer.Stop();
            RefreshAvailability();   // also notices a folder that disappeared while running
        };
    }

    private void StartWatchers()
    {
        StopWatchers();
        var paths = _current?.PathsForThisPc;
        if (paths == null) return;
        _watchPersonal = MakeWatcher(paths.Personal);
        _watchGame = MakeWatcher(paths.Game);
    }

    private FileSystemWatcher? MakeWatcher(string path)
    {
        try
        {
            var w = new FileSystemWatcher(path)
            {
                IncludeSubdirectories = true,
                NotifyFilter = NotifyFilters.FileName | NotifyFilters.DirectoryName
            };
            w.Created += (_, _) => PokeRefresh();
            w.Deleted += (_, _) => PokeRefresh();
            w.Renamed += (_, _) => PokeRefresh();
            w.Error += (_, _) => PokeRefresh();
            w.EnableRaisingEvents = true;
            return w;
        }
        catch (Exception ex)
        {
            Log.Warn($"Couldn't watch {path}: {ex.Message}");
            return null;
        }
    }

    private void StopWatchers()
    {
        _watchPersonal?.Dispose();
        _watchGame?.Dispose();
        _watchPersonal = _watchGame = null;
    }

    // Watcher events arrive on a worker thread and in bursts: debounce on the UI thread.
    private void PokeRefresh()
    {
        Dispatcher.UIThread.Post(() =>
        {
            _refreshTimer.Stop();
            _refreshTimer.Start();
        });
    }

    // ------------------------------------------------------------------ list contents

    private void RefreshTrees()
    {
        var paths = _current?.PathsForThisPc;
        if (_current == null || paths == null) return;
        FillTree(_personalNodes, paths.Personal, _current.Extension, alwaysShowSubfolders: true, isGame: false);
        FillTree(_gameNodes, paths.Game, _current.Extension, alwaysShowSubfolders: false, isGame: true);
    }

    // ------------------------------------------------------------------ custom file icons

    // Icons are stored per profile, keyed by the path RELATIVE to the list's main folder,
    // so they work on every computer even when the folders live in different places.
    private string? IconKey(bool isGame, string fullPath)
    {
        var paths = _current?.PathsForThisPc;
        if (paths == null) return null;
        var root = isGame ? paths.Game : paths.Personal;
        return (isGame ? "G|" : "P|") + Path.GetRelativePath(root, fullPath).Replace('\\', '/');
    }

    private void ApplyIcons(IEnumerable<FileNode> built, bool isGame)
    {
        if (_current == null || _current.FileIcons.Count == 0) return;
        foreach (var n in FileNode.Flatten(built))
        {
            var key = IconKey(isGame, n.FullPath);
            if (key != null && _current.FileIcons.TryGetValue(key, out var rel))
                n.CustomIcon = ImageService.Load(rel, 48);
        }
    }

    private void MoveIconKeys(bool isGame, string oldPath, string newPath)
    {
        if (_current == null) return;
        var oldKey = IconKey(isGame, oldPath);
        var newKey = IconKey(isGame, newPath);
        if (oldKey == null || newKey == null) return;
        bool changed = false;
        foreach (var k in _current.FileIcons.Keys.ToList())
        {
            if (k == oldKey || k.StartsWith(oldKey + "/", StringComparison.Ordinal))
            {
                var val = _current.FileIcons[k];
                _current.FileIcons.Remove(k);
                _current.FileIcons[string.Concat(newKey, k.AsSpan(oldKey.Length))] = val;
                changed = true;
            }
        }
        if (changed) { _current.Touch(); Store.Save(); }
    }

    private void RemoveIconKeys(bool isGame, string path)
    {
        if (_current == null) return;
        var key = IconKey(isGame, path);
        if (key == null) return;
        int n = 0;
        foreach (var k in _current.FileIcons.Keys.ToList())
            if (k == key || k.StartsWith(key + "/", StringComparison.Ordinal)) { _current.FileIcons.Remove(k); n++; }
        if (n > 0) { _current.Touch(); Store.Save(); }
    }

    private async Task SetIconSelected(bool isGame)
    {
        var node = SelectedNode(isGame ? _gameNodes : _personalNodes);
        if (node == null || _current == null) return;
        var key = IconKey(isGame, node.FullPath);
        if (key == null) return;

        var path = await Pickers.OpenImage(this, $"Choose a picture for \"{node.Name}\"");
        if (path == null) return;
        try
        {
            // copied into config/images, so it keeps working if the original is deleted
            _current.FileIcons[key] = ImageService.ImportImage(path, "icons");
        }
        catch (Exception ex)
        {
            await Dialogs.Info(this, "Couldn't use that picture", ex.Message);
            return;
        }
        _current.Touch();
        Store.Save();
        RefreshTrees();
    }

    private void RemoveIconSelected(bool isGame)
    {
        var node = SelectedNode(isGame ? _gameNodes : _personalNodes);
        if (node == null || _current == null) return;
        var key = IconKey(isGame, node.FullPath);
        if (key == null || !_current.FileIcons.Remove(key)) return;
        _current.Touch();
        Store.Save();
        RefreshTrees();
    }

    private void FillTree(ObservableCollection<FileNode> target, string root, string ext, bool alwaysShowSubfolders, bool isGame)
    {
        // keep what was expanded / selected across refreshes
        var expanded = FileNode.Flatten(target).Where(n => n.IsExpanded)
            .Select(n => n.FullPath).ToHashSet(PathCmp);
        var selected = FileNode.Flatten(target).FirstOrDefault(n => n.IsSelected)?.FullPath;

        var built = FileNode.Build(root, ext, alwaysShowSubfolders);
        ApplyIcons(built, isGame);
        foreach (var n in FileNode.Flatten(built))
        {
            if (expanded.Contains(n.FullPath)) n.IsExpanded = true;
            if (selected != null && PathCmp.Equals(n.FullPath, selected)) n.IsSelected = true;
        }

        target.Clear();
        foreach (var n in built) target.Add(n);
    }

    private static FileNode? SelectedNode(IEnumerable<FileNode> nodes) =>
        FileNode.Flatten(nodes).FirstOrDefault(n => n.IsSelected);

    private static void DeselectAll(IEnumerable<FileNode> nodes)
    {
        foreach (var n in FileNode.Flatten(nodes)) n.IsSelected = false;
    }

    /// <summary>Selects the node with this path, expanding its parent folders.</summary>
    private static void SelectPath(IEnumerable<FileNode> nodes, string path)
    {
        bool Walk(IEnumerable<FileNode> level)
        {
            foreach (var n in level)
            {
                if (PathCmp.Equals(n.FullPath, path))
                {
                    DeselectAll(nodes);
                    n.IsSelected = true;
                    return true;
                }
                if (n.IsFolder && Walk(n.Children))
                {
                    n.IsExpanded = true;
                    return true;
                }
            }
            return false;
        }
        Walk(nodes);
    }

    // ------------------------------------------------------------------ mouse + context menus

    private ObservableCollection<FileNode> NodesOf(object? sender) =>
        ReferenceEquals(sender, GameTree) ? _gameNodes : _personalNodes;

    private void HookTreeMouse()
    {
        // tunnelling = we see the click before the tree handles it
        PersonalTree.AddHandler(PointerPressedEvent, Tree_PointerPressed, RoutingStrategies.Tunnel);
        GameTree.AddHandler(PointerPressedEvent, Tree_PointerPressed, RoutingStrategies.Tunnel);
    }

    private void Tree_PointerPressed(object? sender, PointerPressedEventArgs e)
    {
        var nodes = NodesOf(sender);
        var src = e.Source as Visual;
        var item = src?.FindAncestorOfType<TreeViewItem>(true);
        var props = e.GetCurrentPoint(sender as Visual).Properties;

        if (props.IsLeftButtonPressed)
        {
            // Clicking empty space in a list clears its selection (so "backup into the main folder" is possible).
            if (item != null || src?.FindAncestorOfType<ScrollBar>(true) != null) return;
            DeselectAll(nodes);
        }
        else if (props.IsRightButtonPressed)
        {
            DeselectAll(nodes);
            if (item?.DataContext is FileNode node) node.IsSelected = true;
        }
    }

    private void BuildTreeMenus()
    {
        PersonalTree.ContextMenu = MakeTreeMenu(isGame: false);
        GameTree.ContextMenu = MakeTreeMenu(isGame: true);
    }

    private ContextMenu MakeTreeMenu(bool isGame)
    {
        var menu = new ContextMenu();
        var rename = new MenuItem { Header = "Rename" };
        var delete = new MenuItem { Header = "Delete" };
        var folder = new MenuItem { Header = "Create Folder" };
        rename.Click += async (_, _) => await RenameSelected(isGame);
        delete.Click += async (_, _) => await DeleteSelected(isGame);
        folder.Click += async (_, _) => await CreateFolder(isGame);
        var setIcon = new MenuItem { Header = "Set icon..." };
        var clearIcon = new MenuItem { Header = "Remove icon" };
        setIcon.Click += async (_, _) => await SetIconSelected(isGame);
        clearIcon.Click += (_, _) => RemoveIconSelected(isGame);

        menu.Items.Add(rename);
        menu.Items.Add(delete);
        menu.Items.Add(folder);
        menu.Items.Add(new Separator());
        menu.Items.Add(setIcon);
        menu.Items.Add(clearIcon);

        menu.Opening += (_, _) =>
        {
            var sel = SelectedNode(isGame ? _gameNodes : _personalNodes);
            bool has = sel != null;
            rename.IsEnabled = has;
            delete.IsEnabled = has;
            folder.IsEnabled = _available;
            setIcon.IsEnabled = has;
            clearIcon.IsEnabled = has && sel!.HasCustomIcon;
        };
        return menu;
    }

    // ------------------------------------------------------------------ helpers

    private static bool IsValidName(string name) =>
        name.Length > 0 && name.IndexOfAny(Path.GetInvalidFileNameChars()) < 0 && name != "." && name != "..";

    private const string BadNameText = "That name contains characters that aren't allowed in file names.";

    private Task CopyFailed(Exception ex)
    {
        Log.Error($"Couldn't copy: {ex.Message}");
        return Dialogs.Info(this, "COPY ERROR", "Couldn't copy.\n\n" + ex.Message);
    }

    private static bool IsIoProblem(Exception ex) =>
        ex is IOException or UnauthorizedAccessException or System.Security.SecurityException;

    private void ReenableLater(Button button)
    {
        var t = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(250) };
        t.Tick += (_, _) => { t.Stop(); button.IsEnabled = _available; };
        t.Start();
    }

    // ------------------------------------------------------------------ => Replace
    private async void Replace_Click(object? sender, RoutedEventArgs e)
    {
        var sp = SelectedNode(_personalNodes);
        var sg = SelectedNode(_gameNodes);
        if (sp == null || sg == null)
        {
            await Dialogs.Info(this, "No selection",
                "Please choose a file/folder to copy from the left list\nand a file/folder to overwrite from the right list.");
            return;
        }
        if (sp.IsFolder != sg.IsFolder)
        {
            await Dialogs.Info(this, "Folder-to-File Warning", "Please make sure you're copying folder-to-folder or file-to-file.");
            return;
        }

        ReplaceButton.IsEnabled = false;   // stop it being spammed
        try
        {
            // Back up what is about to be overwritten FIRST (the original backed up after copying).
            var backup = Path.Combine(Store.BackupsDir, FileOps.TimestampPrefix() + sg.Name);
            if (sg.IsFolder) FileOps.CopyDirectory(sg.FullPath, backup, true);
            else File.Copy(sg.FullPath, backup, true);

            if (sg.IsFolder) FileOps.CopyDirectory(sp.FullPath, sg.FullPath, true);
            else if (sg.Name.Contains("BAK")) FileOps.ArkhamReplace(sp.FullPath, sg.FullPath);
            else File.Copy(sp.FullPath, sg.FullPath, true);
        }
        catch (Exception ex) when (IsIoProblem(ex))
        {
            await CopyFailed(ex);
            ReenableLater(ReplaceButton);
            return;
        }
        ReenableLater(ReplaceButton);

        // blink the game file to show it worked
        sg.IsSelected = false;
        var blink = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(250) };
        blink.Tick += (_, _) => { blink.Stop(); sg.IsSelected = true; };
        blink.Start();
    }

    // ------------------------------------------------------------------ <= Backup
    private async void Backup_Click(object? sender, RoutedEventArgs e)
    {
        var paths = _current?.PathsForThisPc;
        var sg = SelectedNode(_gameNodes);
        if (paths == null) return;
        if (sg == null)
        {
            await Dialogs.Info(this, "No selection", "Please choose a file/folder to back up from the right list.");
            return;
        }

        // Goes into the selected personal folder (or next to the selected file); nothing selected = main folder.
        var sp = SelectedNode(_personalNodes);
        string dstDir = paths.Personal;
        if (sp != null) dstDir = sp.IsFolder ? sp.FullPath : Path.GetDirectoryName(sp.FullPath)!;

        string src = sg.FullPath;
        bool srcIsFile = !sg.IsFolder;
        bool overwrite = false;
        string name, target;

        while (true)
        {
            var input = await Dialogs.Prompt(this, "Backup file", "Choose a name for the backup file/folder");
            if (input == null) return;
            name = input.Trim();
            if (name.Length == 0) { await Dialogs.Info(this, "No name", "Must choose a file name."); continue; }
            if (!IsValidName(name)) { await Dialogs.Info(this, "Invalid name", BadNameText); continue; }

            if (srcIsFile)
            {
                var ext = Path.GetExtension(src);
                if (!name.EndsWith(ext, StringComparison.OrdinalIgnoreCase)) name += ext;
            }
            target = Path.Combine(dstDir, name);
            bool fileExists = File.Exists(target), dirExists = Directory.Exists(target);
            if (!fileExists && !dirExists) break;

            if ((srcIsFile && dirExists) || (!srcIsFile && fileExists))
            {
                await Dialogs.Info(this, "Already exists", "Can't use this name, choose another one.");
                continue;
            }
            if (await Dialogs.Confirm(this, "Already exists", "File/folder already exists, would you like to overwrite it?"))
            {
                overwrite = true;
                break;
            }
        }

        try
        {
            if (srcIsFile)
            {
                if (sg.Name.Contains("BAK")) FileOps.ArkhamBackup(src, target);
                else File.Copy(src, target, overwrite);
            }
            else FileOps.CopyDirectory(src, target, overwrite);
        }
        catch (Exception ex) when (IsIoProblem(ex))
        {
            await CopyFailed(ex);
            return;
        }

        RefreshTrees();
        SelectPath(_personalNodes, target);
    }

    // ------------------------------------------------------------------ context menu actions
    private async Task RenameSelected(bool isGame)
    {
        var nodes = isGame ? _gameNodes : _personalNodes;
        var node = SelectedNode(nodes);
        if (node == null) return;

        if (isGame && !await Dialogs.Confirm(this, "Renaming Game file",
                "The game may not recognize the file if you rename it.\n\nDo you want to rename?"))
            return;

        var input = await Dialogs.Prompt(this, "New File Name", "Enter a new name for the file", node.Name);
        if (input == null) return;
        var newName = input.Trim();
        if (newName.Length == 0) return;
        if (!IsValidName(newName))
        {
            await Dialogs.Info(this, "Invalid name", BadNameText);
            return;
        }
        if (!node.IsFolder)
        {
            var ext = Path.GetExtension(node.FullPath);
            if (!newName.EndsWith(ext, StringComparison.OrdinalIgnoreCase)) newName += ext;
        }

        var oldPath = node.FullPath;
        var newPath = Path.Combine(Path.GetDirectoryName(oldPath)!, newName);
        if (string.Equals(oldPath, newPath, StringComparison.Ordinal)) return;

        try
        {
            if (OperatingSystem.IsWindows() && string.Equals(oldPath, newPath, StringComparison.OrdinalIgnoreCase))
            {
                // only the letter case changes - Windows needs a two-step move for that
                var temp = oldPath + ".rename-tmp";
                if (node.IsFolder) { Directory.Move(oldPath, temp); Directory.Move(temp, newPath); }
                else { File.Move(oldPath, temp); File.Move(temp, newPath); }
            }
            else if (File.Exists(newPath) || Directory.Exists(newPath))
            {
                if (!await Dialogs.Confirm(this, "Already Exists", "File/Folder already exists, would you like to overwrite?"))
                    return;
                if (node.IsFolder)
                {
                    if (!Directory.Exists(newPath))
                    {
                        await Dialogs.Info(this, "Can't Overwrite", "A file with that name exists; a folder can't replace it.");
                        return;
                    }
                    if (Directory.EnumerateFileSystemEntries(newPath).Any())
                    {
                        await Dialogs.Info(this, "Can't Overwrite",
                            "That folder isn't empty, so it won't be replaced. Please choose another name.");
                        return;
                    }
                    Directory.Delete(newPath);
                    Directory.Move(oldPath, newPath);
                }
                else
                {
                    if (!File.Exists(newPath))
                    {
                        await Dialogs.Info(this, "Can't Overwrite", "A folder with that name exists; a file can't replace it.");
                        return;
                    }
                    // keep a safety copy of the file being overwritten
                    File.Copy(newPath, Path.Combine(Store.DeletedDir, FileOps.TimestampPrefix() + Path.GetFileName(newPath)), true);
                    File.Move(oldPath, newPath, true);
                }
            }
            else if (node.IsFolder) Directory.Move(oldPath, newPath);
            else File.Move(oldPath, newPath);
        }
        catch (Exception ex) when (IsIoProblem(ex))
        {
            Log.Error($"Rename failed: {ex.Message}");
            await Dialogs.Info(this, "Rename Failed", ex.Message);
            return;
        }

        MoveIconKeys(isGame, oldPath, newPath);
        RefreshTrees();
        SelectPath(nodes, newPath);
    }

    private async Task DeleteSelected(bool isGame)
    {
        var nodes = isGame ? _gameNodes : _personalNodes;
        var node = SelectedNode(nodes);
        if (node == null) return;
        if (!await Dialogs.Confirm(this, "Deleting File",
                $"Are you sure you want to delete \"{node.Name}\"?\n\nA copy is kept in the backups folder (Options > Open backups folder)."))
            return;

        try
        {
            // always keep a copy in config/backups/Deleted before removing anything
            var backup = Path.Combine(Store.DeletedDir, FileOps.TimestampPrefix() + node.Name);
            if (node.IsFolder) FileOps.CopyDirectory(node.FullPath, backup, true);
            else File.Copy(node.FullPath, backup, true);
        }
        catch (Exception ex) when (IsIoProblem(ex))
        {
            Log.Error($"Couldn't back up before delete: {ex.Message}");
            await Dialogs.Info(this, "FAILED BACKUP ERROR", "Couldn't back up the file before deleting, so nothing was deleted.\n\n" + ex.Message);
            return;
        }

        try
        {
            if (node.IsFolder) Directory.Delete(node.FullPath, true);
            else File.Delete(node.FullPath);
        }
        catch (Exception ex) when (IsIoProblem(ex))
        {
            Log.Error($"Couldn't delete: {ex.Message}");
            await Dialogs.Info(this, "FAILED DELETE ERROR", "Couldn't delete.\n\n" + ex.Message);
            return;
        }
        RemoveIconKeys(isGame, node.FullPath);
        RefreshTrees();
    }

    private async Task CreateFolder(bool isGame)
    {
        var paths = _current?.PathsForThisPc;
        if (paths == null) return;
        var nodes = isGame ? _gameNodes : _personalNodes;

        string parent = isGame ? paths.Game : paths.Personal;
        var sel = SelectedNode(nodes);
        if (sel != null) parent = sel.IsFolder ? sel.FullPath : Path.GetDirectoryName(sel.FullPath)!;

        string newFolder;
        while (true)
        {
            var input = await Dialogs.Prompt(this, "New Folder Name", "Enter the name of the folder");
            if (input == null) return;
            var name = input.Trim();
            if (name.Length == 0) return;
            if (!IsValidName(name)) { await Dialogs.Info(this, "Invalid name", BadNameText); continue; }
            newFolder = Path.Combine(parent, name);
            if (Directory.Exists(newFolder) || File.Exists(newFolder))
            {
                await Dialogs.Info(this, "Already exists", "Folder already exists, choose a different name.");
                continue;
            }
            break;
        }

        try { Directory.CreateDirectory(newFolder); }
        catch (Exception ex) when (IsIoProblem(ex))
        {
            await Dialogs.Info(this, "Couldn't create folder", ex.Message);
            return;
        }
        RefreshTrees();
        SelectPath(nodes, newFolder);
    }
}
