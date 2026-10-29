using Avalonia.Controls;
using Avalonia.Interactivity;
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
    private readonly SettingsService _store = null!;
    private readonly Profile? _profile;
    private readonly ProfileEditMode _mode;

    public bool Accepted { get; private set; }
    public string ResultName { get; private set; } = "";
    public string ResultExtension { get; private set; } = "";
    public string ResultPersonal { get; private set; } = "";
    public string ResultGame { get; private set; } = "";

    public ProfileEditWindow()
    {
        InitializeComponent();
    }

    public ProfileEditWindow(SettingsService store, Profile? profile, ProfileEditMode mode) : this()
    {
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
            IntroText.IsVisible = true;
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

    private async void BrowsePersonal_Click(object? sender, RoutedEventArgs e)
    {
        var f = await Pickers.PickFolder(this, "Choose your OWN personal saves folder", PersonalBox.Text);
        if (f == null) return;
        PersonalBox.Text = f;
        AutoExtension(f);
    }

    private async void BrowseGame_Click(object? sender, RoutedEventArgs e)
    {
        var f = await Pickers.PickFolder(this, "Choose the GAME'S saves folder", GameBox.Text);
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

    private async void Ok_Click(object? sender, RoutedEventArgs e)
    {
        string personal = (PersonalBox.Text ?? "").Trim().TrimEnd('\\', '/');
        string game = (GameBox.Text ?? "").Trim().TrimEnd('\\', '/');
        string name = (NameBox.Text ?? "").Trim();
        // a bare drive or root ("C:\" or "/") must keep its slash
        if (personal.Length == 0 && (PersonalBox.Text ?? "").Trim().Length > 0) personal = (PersonalBox.Text ?? "").Trim();
        if (game.Length == 0 && (GameBox.Text ?? "").Trim().Length > 0) game = (GameBox.Text ?? "").Trim();

        if (_mode != ProfileEditMode.FixPaths)
        {
            if (name.Length == 0)
            {
                if (_mode == ProfileEditMode.Add) name = _store.NextProfileName();
                else { await Dialogs.Info(this, "No Name", "Please enter a profile name."); return; }
            }
            if (name.Equals("Add...", StringComparison.OrdinalIgnoreCase))
            {
                await Dialogs.Info(this, "Invalid Name", "Please choose another profile name.");
                return;
            }
            if (_store.NameExists(name, _profile?.Id))
            {
                await Dialogs.Info(this, "Profile Name Exists", "Profile name already exists, please choose another one.");
                return;
            }
        }
        else name = _profile?.Name ?? name;

        if (personal.Length == 0 || game.Length == 0)
        {
            await Dialogs.Info(this, "Incomplete Profile",
                "Please make sure you fill in all the necessary profile information.");
            return;
        }
        if (!Directory.Exists(personal) || !Directory.Exists(game))
        {
            await Dialogs.Info(this, "Folder Doesn't Exist",
                "The personal folder or the game folder doesn't exist. Please change it.");
            return;
        }
        if (FileOps.SamePath(Path.GetFullPath(personal), Path.GetFullPath(game)))
        {
            await Dialogs.Info(this, "Same Folder",
                "The personal folder and the game folder must be two different folders.");
            return;
        }

        ResultName = name;
        ResultPersonal = personal;
        ResultGame = game;
        ResultExtension = _mode == ProfileEditMode.FixPaths
            ? _profile?.Extension ?? ""
            : FileOps.NormalizeExtension(ExtBox.Text);
        Accepted = true;
        Close();
    }

    private void Cancel_Click(object? sender, RoutedEventArgs e)
    {
        Accepted = false;
        Close();
    }
}
