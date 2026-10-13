using System.IO;
using System.Windows;
using System.Windows.Controls;
using Microsoft.Win32;
using SavefileManager.Helpers;
using SavefileManager.Models;
using SavefileManager.Services;

namespace SavefileManager.Views;

public enum ProfileEditMode
{
    Add,
    Edit,
    /// <summary>Only the two folders for THIS computer can be changed.</summary>
    FixPaths
}

public partial class ProfileEditWindow : Window
{
    private readonly SettingsService _store;
    private readonly Profile? _profile;
    private readonly ProfileEditMode _mode;

    public string ResultName { get; private set; } = "";
    public string ResultExtension { get; private set; } = "";
    public string ResultPersonal { get; private set; } = "";
    public string ResultGame { get; private set; } = "";

    public ProfileEditWindow(SettingsService store, Profile? profile, ProfileEditMode mode)
    {
        InitializeComponent();
        ThemeManager.Attach(this);
        _store = store;
        _profile = profile;
        _mode = mode;

        Title = mode switch
        {
            ProfileEditMode.Add => "Add Profile",
            ProfileEditMode.Edit => "Edit Profile",
            _ => "Set Folders For This PC"
        };

        if (profile != null)
        {
            NameBox.Text = profile.Name;
            ExtBox.Text = profile.Extension.Length == 0 ? "*" : profile.Extension;
            if (profile.PathsForThisPc is { } mine)
            {
                PersonalBox.Text = mine.Personal;
                GameBox.Text = mine.Game;
            }
        }

        if (mode == ProfileEditMode.FixPaths)
        {
            NameBox.IsEnabled = false;
            ExtBox.IsEnabled = false;
            IntroText.Visibility = Visibility.Visible;
            IntroText.Text = $"The folders for \"{profile?.Name}\" can't be found on this PC. " +
                             "Choose them again, or press Cancel - the profile is kept either way.";
        }

        var others = profile?.Paths
            .Where(kv => !kv.Key.Equals(SettingsService.Machine, StringComparison.OrdinalIgnoreCase))
            .Select(kv => $"{kv.Key}:  {kv.Value.Personal}   |   {kv.Value.Game}")
            .ToList();
        NoteText.Text = $"Folders are saved for this PC ({SettingsService.Machine}) only, " +
                        "so other computers keep their own paths." +
                        (others is { Count: > 0 } ? "\n\nOther computers:\n" + string.Join("\n", others) : "");
    }

    private static string? Browse(Window owner, string title, string current)
    {
        var dlg = new OpenFolderDialog { Title = title };
        if (Directory.Exists(current)) dlg.InitialDirectory = current;
        return dlg.ShowDialog(owner) == true ? dlg.FolderName : null;
    }

    private void BrowsePersonal_Click(object sender, RoutedEventArgs e)
    {
        var f = Browse(this, "Choose your OWN personal saves folder", PersonalBox.Text);
        if (f == null) return;
        PersonalBox.Text = f;
        AutoExtension(f);
    }

    private void BrowseGame_Click(object sender, RoutedEventArgs e)
    {
        var f = Browse(this, "Choose the GAME'S saves folder", GameBox.Text);
        if (f == null) return;
        GameBox.Text = f;
        AutoExtension(f);
    }

    // Same as the original: guess the extension from the folder when none has been chosen yet.
    private void AutoExtension(string folder)
    {
        if (ExtBox.IsEnabled && string.IsNullOrWhiteSpace(ExtBox.Text))
        {
            var ext = FileOps.GetExt(folder);
            ExtBox.Text = ext.Length == 0 ? "*" : ext;
        }
    }

    private void Ok_Click(object sender, RoutedEventArgs e)
    {
        string personal = PersonalBox.Text.Trim().TrimEnd('\\', '/');
        string game = GameBox.Text.Trim().TrimEnd('\\', '/');
        string name = NameBox.Text.Trim();

        if (_mode != ProfileEditMode.FixPaths)
        {
            if (name.Length == 0)
            {
                if (_mode == ProfileEditMode.Add) name = _store.NextProfileName();
                else { Dialogs.Info(this, "No Name", "Please enter a profile name."); return; }
            }
            if (name.Equals("Add...", StringComparison.OrdinalIgnoreCase))
            {
                Dialogs.Info(this, "Invalid Name", "Please choose another profile name.");
                return;
            }
            if (_store.NameExists(name, _profile?.Id))
            {
                Dialogs.Info(this, "Profile Name Exists", "Profile name already exists, please choose another one.");
                return;
            }
        }
        else name = _profile?.Name ?? name;

        if (personal.Length == 0 || game.Length == 0)
        {
            Dialogs.Info(this, "Incomplete Profile",
                "Please make sure you fill in all the necessary profile information.");
            return;
        }
        if (!Directory.Exists(personal) || !Directory.Exists(game))
        {
            Dialogs.Info(this, "Folder Doesn't Exist",
                "The personal folder or the game folder doesn't exist. Please change it.");
            return;
        }
        if (string.Equals(Path.GetFullPath(personal), Path.GetFullPath(game), StringComparison.OrdinalIgnoreCase))
        {
            Dialogs.Info(this, "Same Folder",
                "The personal folder and the game folder must be two different folders.");
            return;
        }

        ResultName = name;
        ResultPersonal = personal;
        ResultGame = game;
        ResultExtension = _mode == ProfileEditMode.FixPaths
            ? _profile?.Extension ?? ""
            : FileOps.NormalizeExtension(ExtBox.Text);
        DialogResult = true;
    }

    private void Cancel_Click(object sender, RoutedEventArgs e) => DialogResult = false;
}
