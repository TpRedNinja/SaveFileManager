using System.IO;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Threading;
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
        InitWatcherTimer();
        RestoreWindowBounds();
    }

    // ------------------------------------------------------------------ start / stop

    private void Window_Loaded(object sender, RoutedEventArgs e)
    {
        if (Store.LoadWarning != null)
            Dialogs.Info(this, "Settings File", Store.LoadWarning);

        ReloadProfileList(null);
        var state = Store.State;
        var start = Store.Data.Profiles.FirstOrDefault(p => p.Id == state.LastProfileId)
                    ?? Store.Data.Profiles.OrderBy(p => p.Name, FileOps.NaturalComparer.Instance).FirstOrDefault();
        ShowProfile(start, promptIfMissing: true);
        _started = true;
    }

    private void Window_Closing(object? sender, System.ComponentModel.CancelEventArgs e)
    {
        StopWatchers();
        SaveWindowBounds();
        Store.Save();
    }

    private void Window_Activated(object? sender, EventArgs e)
    {
        if (!_started) return;

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
            if (keep?.Id != _current?.Id) ShowProfile(keep, promptIfMissing: false);
        }

        // A drive may have been (re)connected or a folder moved while we were in the background.
        RefreshAvailability();
    }

    private void RestoreWindowBounds()
    {
        var s = Store.State;
        double vl = SystemParameters.VirtualScreenLeft, vt = SystemParameters.VirtualScreenTop;
        double vw = SystemParameters.VirtualScreenWidth, vh = SystemParameters.VirtualScreenHeight;

        if (s.Width is > 200 && s.Height is > 200)
        {
            Width = Math.Min(s.Width.Value, vw);
            Height = Math.Min(s.Height.Value, vh);
        }
        if (s.Left is { } l && s.Top is { } t &&
            l >= vl - 50 && l <= vl + vw - 100 && t >= vt - 10 && t <= vt + vh - 100)
        {
            WindowStartupLocation = WindowStartupLocation.Manual;
            Left = l;
            Top = t;
        }
        else
        {
            WindowStartupLocation = WindowStartupLocation.CenterScreen;
        }
    }

    private void SaveWindowBounds()
    {
        var b = WindowState == WindowState.Normal ? new Rect(Left, Top, Width, Height) : RestoreBounds;
        var s = Store.State;
        s.Left = b.Left;
        s.Top = b.Top;
        s.Width = b.Width;
        s.Height = b.Height;
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

    private void ProfileCombo_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_suppressProfileChange || ProfileCombo.SelectedItem is not ProfileItem item) return;

        if (item.IsAdd)
        {
            if (!AddProfile())
            {
                // cancelled: go back to what was selected before
                _suppressProfileChange = true;
                ProfileCombo.SelectedItem = _profileItems.FirstOrDefault(i => !i.IsAdd && i.Id == _current?.Id);
                _suppressProfileChange = false;
            }
            return;
        }

        var profile = Store.Data.Profiles.FirstOrDefault(p => p.Id == item.Id);
        ShowProfile(profile, promptIfMissing: true);
    }

    /// <summary>Makes <paramref name="profile"/> the active profile and loads its lists.</summary>
    private void ShowProfile(Profile? profile, bool promptIfMissing)
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
            PromptFixPaths(profile);

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

    private bool PromptFixPaths(Profile profile)
    {
        var dlg = new ProfileEditWindow(Store, profile, ProfileEditMode.FixPaths);
        if (IsLoaded) dlg.Owner = this;
        if (dlg.ShowDialog() != true)
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
            MissingBanner.Visibility = Visibility.Collapsed;
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
            MissingBanner.Visibility = Visibility.Collapsed;
        }
        else
        {
            MissingBanner.Visibility = Visibility.Visible;
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

    private bool AddProfile()
    {
        var dlg = new ProfileEditWindow(Store, null, ProfileEditMode.Add) { Owner = this };
        if (dlg.ShowDialog() != true) return false;

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
        ShowProfile(profile, promptIfMissing: false);
        return true;
    }

    private void AddProfile_Click(object sender, RoutedEventArgs e) => AddProfile();

    private void EditProfile_Click(object sender, RoutedEventArgs e)
    {
        if (_current == null)
        {
            Dialogs.Info(this, "No profiles", "No profiles exist. Add one first.");
            return;
        }
        var p = _current;
        var dlg = new ProfileEditWindow(Store, p, ProfileEditMode.Edit) { Owner = this };
        if (dlg.ShowDialog() != true) return;

        p.Name = dlg.ResultName;
        p.Extension = dlg.ResultExtension;
        p.Paths[SettingsService.Machine] = new FolderPair { Personal = dlg.ResultPersonal, Game = dlg.ResultGame };
        p.Touch();
        Log.Info($"Profile updated ({p.Name})");
        Store.Save();
        ReloadProfileList(p.Id);
        ShowProfile(p, promptIfMissing: false);
    }

    private void RemoveProfile_Click(object sender, RoutedEventArgs e)
    {
        if (_current == null)
        {
            Dialogs.Info(this, "No profiles", "No profiles to remove. Add one first.");
            return;
        }
        var p = _current;
        if (!Dialogs.Confirm(this, "Removing Profile",
                $"Remove the profile \"{p.Name}\"?\n\n" +
                "Only the profile entry is removed - your save files are not touched. " +
                "If you share this config folder between computers, it disappears there too."))
            return;

        Store.RemoveProfile(p);
        Log.Info($"Profile removed by user ({p.Name})");
        Store.Save();
        var next = Store.Data.Profiles.OrderBy(x => x.Name, FileOps.NaturalComparer.Instance).FirstOrDefault();
        ReloadProfileList(next?.Id);
        ShowProfile(next, promptIfMissing: false);
    }

    private void FixPaths_Click(object sender, RoutedEventArgs e)
    {
        if (_current == null) return;
        if (PromptFixPaths(_current))
        {
            Store.Save();
            ShowProfile(_current, promptIfMissing: false);
        }
    }

    // Converts the settings.json of the original (Python) Savefile Manager into profiles here.
    private void ImportLegacy_Click(object sender, RoutedEventArgs e)
    {
        var dlg = new Microsoft.Win32.OpenFileDialog
        {
            Title = "Choose the settings.json from Green-Bat's Savefile Manager",
            Filter = "settings.json|*.json|All files|*.*"
        };
        if (dlg.ShowDialog(this) != true) return;

        LegacyImportResult result;
        try
        {
            result = LegacyImporter.Import(dlg.FileName, Store);
        }
        catch (Exception ex)
        {
            Log.Error($"Legacy import failed: {ex.Message}");
            Dialogs.Info(this, "Import Failed", ex.Message);
            return;
        }

        if (result.Added.Count > 0)
        {
            Log.Info($"Imported {result.Added.Count} profile(s) from the original settings.json");
            Store.Save();
            ReloadProfileList(_current?.Id ?? result.Added[0].Id);
            if (_current == null) ShowProfile(result.Added[0], promptIfMissing: true);
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
        Dialogs.Info(this, "Import Green-Bat Settings", msg);
    }

    private static void OpenFolder(string? path)
    {
        if (string.IsNullOrEmpty(path) || !Directory.Exists(path)) return;
        try { Process.Start(new ProcessStartInfo(path) { UseShellExecute = true }); }
        catch (Exception ex) { Log.Error("Couldn't open folder: " + ex.Message); }
    }

    private void OpenBackups_Click(object sender, RoutedEventArgs e) => OpenFolder(Store.BackupsDir);

    // Backups are only ever removed when the user asks for it here (the original did it silently every week).
    private void CleanBackups_Click(object sender, RoutedEventArgs e)
    {
        if (!Dialogs.Confirm(this, "Clean Backups",
                "Permanently delete replace-backups and deleted-file backups that are older than 7 days?\n\n" +
                "Your profiles and save files are not affected."))
            return;

        var cutoff = DateTime.Now.AddDays(-7);
        int removed = 0;
        foreach (var root in new[] { Store.BackupsDir, Store.DeletedDir })
        {
            foreach (var entry in Directory.EnumerateFileSystemEntries(root).ToList())
            {
                if (entry.Equals(Store.DeletedDir, StringComparison.OrdinalIgnoreCase)) continue;
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
        Dialogs.Info(this, "Clean Backups", removed == 0 ? "No old backups found." : $"Removed {removed} old backup(s).");
    }

    private void PersonalPath_MouseDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ClickCount == 2) OpenFolder(_current?.PathsForThisPc?.Personal);
    }

    private void GamePath_MouseDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ClickCount == 2) OpenFolder(_current?.PathsForThisPc?.Game);
    }

    // ------------------------------------------------------------------ themes menu

    private void BuildThemeMenu()
    {
        ThemesMenu.Items.Clear();
        foreach (var name in BuiltInThemes.Names.Append(ThemeManager.CustomName))
        {
            var item = new MenuItem { Header = name, Tag = name };
            var n = name;
            item.Click += (_, _) => SetTheme(n);
            ThemesMenu.Items.Add(item);
        }
        ThemesMenu.Items.Add(new Separator());
        var edit = new MenuItem { Header = "Edit custom theme..." };
        edit.Click += (_, _) => EditCustomTheme();
        ThemesMenu.Items.Add(edit);
        var look = new MenuItem { Header = "Fonts & backgrounds..." };
        look.Click += (_, _) => EditAppearance();
        ThemesMenu.Items.Add(look);
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

    private void EditAppearance()
    {
        var dlg = new AppearanceWindow(Store.Data.Appearance) { Owner = this };
        if (dlg.ShowDialog() == true)
        {
            Store.Data.Appearance = dlg.Result;
            Store.Data.Appearance.UpdatedUtc = DateTime.UtcNow;
            AppearanceManager.Apply(Store.Data.Appearance);
            Store.Save();
        }
    }

    // ------------------------------------------------------------------ profile picture

    private void SetProfileIcon_Click(object sender, RoutedEventArgs e)
    {
        if (_current == null) return;
        var dlg = new Microsoft.Win32.OpenFileDialog
        {
            Title = $"Choose a picture for \"{_current.Name}\"",
            Filter = ImageService.FileFilter
        };
        if (dlg.ShowDialog(this) != true) return;
        try
        {
            // the picture is copied into config/images, so it still works if the original is deleted
            _current.IconFile = ImageService.ImportImage(dlg.FileName, "profiles");
        }
        catch (Exception ex)
        {
            Dialogs.Info(this, "Couldn't use that picture", ex.Message);
            return;
        }
        _current.Touch();
        Store.Save();
        ReloadProfileList(_current.Id);
        UpdateAvailabilityUi();
    }

    private void RemoveProfileIcon_Click(object sender, RoutedEventArgs e)
    {
        if (_current == null) return;
        _current.IconFile = null;
        _current.Touch();
        Store.Save();
        ReloadProfileList(_current.Id);
        UpdateAvailabilityUi();
    }

    private void EditCustomTheme()
    {
        var active = ThemeManager.Current ?? ThemeManager.Resolve(Store.Data);
        var dlg = new ThemeEditorWindow(Store.Data.CustomTheme, active) { Owner = this };
        if (dlg.ShowDialog() == true)
        {
            Store.Data.CustomTheme = dlg.Result;
            Store.Data.Theme = ThemeManager.CustomName;
            Store.Data.ThemeUpdatedUtc = DateTime.UtcNow;
            ThemeManager.Apply(ThemeManager.Resolve(Store.Data));
            UpdateThemeChecks();
            Store.Save();
        }
    }
}
