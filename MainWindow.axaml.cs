using System.Collections.ObjectModel;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Interactivity;
using SavefileManager.Helpers;
using SavefileManager.Models;
using SavefileManager.Services;
using SavefileManager.Views;

namespace SavefileManager;

// Profiles, themes, window lifecycle. File operations are in MainWindow.Files.cs.
public partial class MainWindow : Window
{
    private const string AddItem = "Add...";
    private const string VersionText = "v1.0.0";

    private static SettingsService Store => App.Store;

    private readonly ObservableCollection<ProfileItem> _profileItems = new();
    private readonly ObservableCollection<FileNode> _personalNodes = new();
    private readonly ObservableCollection<FileNode> _gameNodes = new();

    private Profile? _current;
    private bool _available;
    private bool _suppressProfileChange;
    private bool _started;
    private bool _inPrompt;

    public MainWindow()
    {
        InitializeComponent();
        ThemeManager.Attach(this);

        TitleText.Text = $"Savefile Manager {VersionText}";
        ProfileCombo.ItemsSource = _profileItems;
        PersonalTree.ItemsSource = _personalNodes;
        GameTree.ItemsSource = _gameNodes;

        BuildThemeMenu();
        BuildTreeMenus();
        HookTreeMouse();
        InitWatcherTimer();
        RestoreWindowBounds();
        Closing += Window_Closing;
    }

    // ------------------------------------------------------------------ start / stop

    private async void Window_Opened(object? sender, EventArgs e)
    {
        if (_started) return;
        try
        {
            if (Store.LoadWarning != null)
                await Dialogs.Info(this, "Settings File", Store.LoadWarning);

            ReloadProfileList(null);
            var state = Store.State;
            var start = Store.Data.Profiles.FirstOrDefault(p => p.Id == state.LastProfileId)
                        ?? Store.Data.Profiles.OrderBy(p => p.Name, FileOps.NaturalComparer.Instance).FirstOrDefault();
            await ShowProfile(start, promptIfMissing: true);
        }
        catch (Exception ex)
        {
            Log.Error("Startup failed: " + ex);
        }
        _started = true;
    }

    private void Window_Closing(object? sender, WindowClosingEventArgs e)
    {
        StopWatchers();
        SaveWindowBounds();
        Store.Save();
    }

    private void Window_Activated(object? sender, EventArgs e)
    {
        if (!_started || _inPrompt) return;

        // Pick up changes made on another computer sharing this config folder.
        var (profilesChanged, themeChanged) = Store.MergeFromDisk();
        if (themeChanged)
        {
            AppearanceManager.Apply(Store.Data.Appearance);
            ThemeManager.Apply(ThemeManager.Resolve(Store.Data));
            UpdateThemeChecks();
        }
        if (profilesChanged)
        {
            var keep = Store.Data.Profiles.FirstOrDefault(p => p.Id == _current?.Id)
                       ?? Store.Data.Profiles.OrderBy(p => p.Name, FileOps.NaturalComparer.Instance).FirstOrDefault();
            ReloadProfileList(keep?.Id);
            if (keep?.Id != _current?.Id) _ = ShowProfile(keep, promptIfMissing: false);
        }

        // A drive may have been (re)connected or a folder moved while we were in the background.
        RefreshAvailability();
    }

    private void RestoreWindowBounds()
    {
        var s = Store.State;
        if (s.Width is > 200 && s.Height is > 200)
        {
            Width = s.Width.Value;
            Height = s.Height.Value;
        }

        bool placed = false;
        if (s.Left is { } l && s.Top is { } t)
        {
            try
            {
                // only restore the position when it is still on a connected screen
                foreach (var screen in Screens.All)
                {
                    var b = screen.Bounds;
                    if (l >= b.X - 50 && l <= b.Right - 100 && t >= b.Y - 10 && t <= b.Bottom - 100)
                    {
                        WindowStartupLocation = WindowStartupLocation.Manual;
                        Position = new PixelPoint((int)l, (int)t);
                        placed = true;
                        break;
                    }
                }
            }
            catch { /* fall back to centred */ }
        }
        if (!placed) WindowStartupLocation = WindowStartupLocation.CenterScreen;
    }

    private void SaveWindowBounds()
    {
        // a maximised / minimised window has no useful "normal" size here, so keep the last saved one
        if (WindowState != WindowState.Normal) return;
        var s = Store.State;
        s.Left = Position.X;
        s.Top = Position.Y;
        s.Width = Width;
        s.Height = Height;
    }

    // ------------------------------------------------------------------ profile list

    private void ReloadProfileList(string? selectId)
    {
        _suppressProfileChange = true;
        _profileItems.Clear();
        foreach (var p in Store.Data.Profiles.OrderBy(p => p.Name, FileOps.NaturalComparer.Instance))
            _profileItems.Add(new ProfileItem { Id = p.Id, Name = p.Name, Icon = ImageService.Load(p.IconFile, 40) });
        _profileItems.Add(new ProfileItem { Name = AddItem, IsAdd = true });
        ProfileCombo.SelectedItem = _profileItems.FirstOrDefault(i => !i.IsAdd && i.Id == selectId);
        _suppressProfileChange = false;
    }

    private async void ProfileCombo_SelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (_suppressProfileChange || ProfileCombo.SelectedItem is not ProfileItem item) return;

        if (item.IsAdd)
        {
            if (!await AddProfile())
            {
                // cancelled: go back to what was selected before
                _suppressProfileChange = true;
                ProfileCombo.SelectedItem = _profileItems.FirstOrDefault(i => !i.IsAdd && i.Id == _current?.Id);
                _suppressProfileChange = false;
            }
            return;
        }

        var profile = Store.Data.Profiles.FirstOrDefault(p => p.Id == item.Id);
        await ShowProfile(profile, promptIfMissing: true);
    }

    /// <summary>Makes <paramref name="profile"/> the active profile and loads its lists.</summary>
    private async Task ShowProfile(Profile? profile, bool promptIfMissing)
    {
        StopWatchers();
        _current = profile;
        Store.State.LastProfileId = profile?.Id;

        _suppressProfileChange = true;
        ProfileCombo.SelectedItem = _profileItems.FirstOrDefault(i => !i.IsAdd && i.Id == profile?.Id);
        _suppressProfileChange = false;

        // A missing folder is NOT a reason to throw the profile away. Ask once; if the user
        // cancels, the profile simply stays inaccessible on this PC until they set the folders.
        if (profile != null && !profile.IsAvailableHere && promptIfMissing)
            await PromptFixPaths(profile);

        // the user may have switched profile while the dialog was open
        if (!ReferenceEquals(_current, profile)) return;

        _personalNodes.Clear();
        _gameNodes.Clear();
        UpdateAvailabilityUi();
        if (_available)
        {
            RefreshTrees();
            StartWatchers();
        }
        Store.Save();
    }

    private async Task<bool> PromptFixPaths(Profile profile)
    {
        var dlg = new ProfileEditWindow(Store, profile, ProfileEditMode.FixPaths);
        _inPrompt = true;
        try { await Dialogs.ShowModal(dlg, this); }
        finally { _inPrompt = false; }

        if (!dlg.Accepted)
        {
            Log.Info($"Folders for profile '{profile.Name}' not set on this PC; profile kept");
            return false;
        }
        profile.Paths[SettingsService.Machine] = new FolderPair
        {
            Personal = dlg.ResultPersonal,
            Game = dlg.ResultGame
        };
        profile.Touch();
        Log.Info($"Folders updated for profile '{profile.Name}' on {SettingsService.Machine}");
        return true;
    }

    private void UpdateAvailabilityUi()
    {
        var p = _current;
        var paths = p?.PathsForThisPc;
        _available = p != null && p.IsAvailableHere;

        EditMenu.IsEnabled = p != null;
        RemoveMenu.IsEnabled = p != null;
        FixPathsMenu.IsEnabled = p != null;
        SetProfileIconMenu.IsEnabled = p != null;
        RemoveProfileIconMenu.IsEnabled = p != null && !string.IsNullOrEmpty(p.IconFile);
        ReplaceButton.IsEnabled = _available;
        BackupButton.IsEnabled = _available;

        if (p == null)
        {
            PersonalPathText.Text = "No profiles yet - use Options > Add (or pick \"Add...\" in the Profiles list).";
            GamePathText.Text = "";
            MissingBanner.IsVisible = false;
            return;
        }

        string Describe(string label, string? path) =>
            string.IsNullOrEmpty(path)
                ? $"{label}: (not set on this PC)"
                : Directory.Exists(path) ? $"{label}: {path}" : $"{label}: {path}   (not found)";

        PersonalPathText.Text = Describe("Current personal directory", paths?.Personal);
        GamePathText.Text = Describe("Current game directory", paths?.Game);

        if (_available)
        {
            MissingBanner.IsVisible = false;
        }
        else
        {
            MissingBanner.IsVisible = true;
            MissingBannerText.Text = paths == null
                ? $"\"{p.Name}\" has no folders set for this PC ({SettingsService.Machine}) yet. " +
                  "Your profile is safe - set the folders to use it here."
                : "One of this profile's folders can't be found on this PC (moved, renamed or drive not connected?). " +
                  "Nothing was deleted - the profile is just inaccessible until the folders exist again.";
        }
    }

    /// <summary>Re-checks the current profile's folders (called on window activation and by the watcher).</summary>
    private void RefreshAvailability()
    {
        if (_current == null) return;
        bool now = _current.IsAvailableHere;
        if (now != _available)
        {
            StopWatchers();
            _personalNodes.Clear();
            _gameNodes.Clear();
            UpdateAvailabilityUi();
            if (_available)
            {
                RefreshTrees();
                StartWatchers();
            }
        }
        else if (_available)
        {
            RefreshTrees();
        }
    }

    // ------------------------------------------------------------------ Options menu

    private async Task<bool> AddProfile()
    {
        var dlg = new ProfileEditWindow(Store, null, ProfileEditMode.Add);
        await Dialogs.ShowModal(dlg, this);
        if (!dlg.Accepted) return false;

        var profile = new Profile { Name = dlg.ResultName, Extension = dlg.ResultExtension };
        profile.Paths[SettingsService.Machine] = new FolderPair
        {
            Personal = dlg.ResultPersonal,
            Game = dlg.ResultGame
        };
        Store.Data.Profiles.Add(profile);
        Log.Info($"New profile added ({profile.Name})");
        Store.Save();
        ReloadProfileList(profile.Id);
        await ShowProfile(profile, promptIfMissing: false);
        return true;
    }

    private async void AddProfile_Click(object? sender, RoutedEventArgs e) => await AddProfile();

    private async void EditProfile_Click(object? sender, RoutedEventArgs e)
    {
        if (_current == null)
        {
            await Dialogs.Info(this, "No profiles", "No profiles exist. Add one first.");
            return;
        }
        var p = _current;
        var dlg = new ProfileEditWindow(Store, p, ProfileEditMode.Edit);
        await Dialogs.ShowModal(dlg, this);
        if (!dlg.Accepted) return;

        p.Name = dlg.ResultName;
        p.Extension = dlg.ResultExtension;
        p.Paths[SettingsService.Machine] = new FolderPair { Personal = dlg.ResultPersonal, Game = dlg.ResultGame };
        p.Touch();
        Log.Info($"Profile updated ({p.Name})");
        Store.Save();
        ReloadProfileList(p.Id);
        await ShowProfile(p, promptIfMissing: false);
    }

    private async void RemoveProfile_Click(object? sender, RoutedEventArgs e)
    {
        if (_current == null)
        {
            await Dialogs.Info(this, "No profiles", "No profiles to remove. Add one first.");
            return;
        }
        var p = _current;
        if (!await Dialogs.Confirm(this, "Removing Profile",
                $"Remove the profile \"{p.Name}\"?\n\n" +
                "Only the profile entry is removed - your save files are not touched. " +
                "If you share this config folder between computers, it disappears there too."))
            return;

        Store.RemoveProfile(p);
        Log.Info($"Profile removed by user ({p.Name})");
        Store.Save();
        var next = Store.Data.Profiles.OrderBy(x => x.Name, FileOps.NaturalComparer.Instance).FirstOrDefault();
        ReloadProfileList(next?.Id);
        await ShowProfile(next, promptIfMissing: false);
    }

    private async void FixPaths_Click(object? sender, RoutedEventArgs e)
    {
        if (_current == null) return;
        if (await PromptFixPaths(_current))
        {
            Store.Save();
            await ShowProfile(_current, promptIfMissing: false);
        }
    }

    // Converts the settings.json of the original (Python) Savefile Manager into profiles here.
    private async void ImportLegacy_Click(object? sender, RoutedEventArgs e)
    {
        var path = await Pickers.OpenFile(this, "Choose the settings.json from Green-Bat's Savefile Manager",
            "settings.json", new[] { "*.json" });
        if (path == null) return;

        LegacyImportResult result;
        try
        {
            result = LegacyImporter.Import(path, Store);
        }
        catch (Exception ex)
        {
            Log.Error($"Legacy import failed: {ex.Message}");
            await Dialogs.Info(this, "Import Failed", ex.Message);
            return;
        }

        if (result.Added.Count > 0)
        {
            Log.Info($"Imported {result.Added.Count} profile(s) from the original settings.json");
            Store.Save();
            ReloadProfileList(_current?.Id ?? result.Added[0].Id);
            if (_current == null) await ShowProfile(result.Added[0], promptIfMissing: true);
        }

        var msg = result.Added.Count == 0
            ? "No new profiles were imported."
            : $"Imported {result.Added.Count} profile(s):\n  " + string.Join("\n  ", result.Added.Select(p => p.Name));
        if (result.SkippedExisting.Count > 0)
            msg += $"\n\nSkipped (a profile with that name already exists):\n  " + string.Join("\n  ", result.SkippedExisting);
        if (result.Unreadable > 0)
            msg += $"\n\n{result.Unreadable} entr{(result.Unreadable == 1 ? "y was" : "ies were")} incomplete and couldn't be imported.";
        if (result.Added.Count > 0)
            msg += $"\n\nThe folders from the old file were saved for this PC ({SettingsService.Machine}). " +
                   "If one doesn't exist here you'll be asked to set it. The original file was not changed.";
        await Dialogs.Info(this, "Import Green-Bat Settings", msg);
    }

    private void OpenBackups_Click(object? sender, RoutedEventArgs e) => PlatformHelper.OpenFolder(Store.BackupsDir);

    // Backups are only ever removed when the user asks for it here (the original did it silently every week).
    private async void CleanBackups_Click(object? sender, RoutedEventArgs e)
    {
        if (!await Dialogs.Confirm(this, "Clean Backups",
                "Permanently delete replace-backups and deleted-file backups that are older than 7 days?\n\n" +
                "Your profiles and save files are not affected."))
            return;

        var cutoff = DateTime.Now.AddDays(-7);
        int removed = 0;
        foreach (var root in new[] { Store.BackupsDir, Store.DeletedDir })
        {
            if (!Directory.Exists(root)) continue;
            foreach (var entry in Directory.EnumerateFileSystemEntries(root).ToList())
            {
                if (FileOps.SamePath(entry, Store.DeletedDir)) continue;
                try
                {
                    if (Directory.Exists(entry))
                    {
                        if (Directory.GetCreationTime(entry) < cutoff) { Directory.Delete(entry, true); removed++; }
                    }
                    else if (File.GetCreationTime(entry) < cutoff) { File.Delete(entry); removed++; }
                }
                catch (Exception ex) { Log.Warn($"Couldn't clean backup {entry}: {ex.Message}"); }
            }
        }
        await Dialogs.Info(this, "Clean Backups", removed == 0 ? "No old backups found." : $"Removed {removed} old backup(s).");
    }

    private void PersonalPath_DoubleTapped(object? sender, TappedEventArgs e) =>
        PlatformHelper.OpenFolder(_current?.PathsForThisPc?.Personal);

    private void GamePath_DoubleTapped(object? sender, TappedEventArgs e) =>
        PlatformHelper.OpenFolder(_current?.PathsForThisPc?.Game);

    // ------------------------------------------------------------------ themes menu

    private void BuildThemeMenu()
    {
        var items = new List<object>();
        foreach (var name in BuiltInThemes.Names.Append(ThemeManager.CustomName))
        {
            var item = new MenuItem { Header = name, Tag = name, ToggleType = MenuItemToggleType.CheckBox };
            var n = name;
            item.Click += (_, _) => SetTheme(n);
            items.Add(item);
        }
        items.Add(new Separator());
        var edit = new MenuItem { Header = "Edit custom theme..." };
        edit.Click += (_, _) => EditCustomTheme();
        items.Add(edit);
        var look = new MenuItem { Header = "Fonts & backgrounds..." };
        look.Click += (_, _) => EditAppearance();
        items.Add(look);
        ThemesMenu.ItemsSource = items;
        UpdateThemeChecks();
    }

    private void UpdateThemeChecks()
    {
        foreach (var item in ThemesMenu.Items.OfType<MenuItem>())
            if (item.Tag is string name) item.IsChecked = name == Store.Data.Theme;
    }

    private void SetTheme(string name)
    {
        Store.Data.Theme = name;
        Store.Data.ThemeUpdatedUtc = DateTime.UtcNow;
        ThemeManager.Apply(ThemeManager.Resolve(Store.Data));
        UpdateThemeChecks();
        Store.Save();
    }

    private async void EditAppearance()
    {
        var dlg = new AppearanceWindow(Store.Data.Appearance);
        await Dialogs.ShowModal(dlg, this);
        if (dlg.Accepted)
        {
            Store.Data.Appearance = dlg.Result;
            Store.Data.Appearance.UpdatedUtc = DateTime.UtcNow;
            AppearanceManager.Apply(Store.Data.Appearance);
            Store.Save();
        }
    }

    private async void EditCustomTheme()
    {
        var active = ThemeManager.Current ?? ThemeManager.Resolve(Store.Data);
        var dlg = new ThemeEditorWindow(Store.Data.CustomTheme, active);
        await Dialogs.ShowModal(dlg, this);
        if (dlg.Accepted)
        {
            Store.Data.CustomTheme = dlg.Result;
            Store.Data.Theme = ThemeManager.CustomName;
            Store.Data.ThemeUpdatedUtc = DateTime.UtcNow;
            ThemeManager.Apply(ThemeManager.Resolve(Store.Data));
            UpdateThemeChecks();
            Store.Save();
        }
    }

    // ------------------------------------------------------------------ profile picture

    private async void SetProfileIcon_Click(object? sender, RoutedEventArgs e)
    {
        if (_current == null) return;
        var path = await Pickers.OpenImage(this, $"Choose a picture for \"{_current.Name}\"");
        if (path == null) return;
        try
        {
            // the picture is copied into config/images, so it still works if the original is deleted
            _current.IconFile = ImageService.ImportImage(path, "profiles");
        }
        catch (Exception ex)
        {
            await Dialogs.Info(this, "Couldn't use that picture", ex.Message);
            return;
        }
        _current.Touch();
        Store.Save();
        ReloadProfileList(_current.Id);
        UpdateAvailabilityUi();
    }

    private void RemoveProfileIcon_Click(object? sender, RoutedEventArgs e)
    {
        if (_current == null) return;
        _current.IconFile = null;
        _current.Touch();
        Store.Save();
        ReloadProfileList(_current.Id);
        UpdateAvailabilityUi();
    }
}
