using System.Windows;

namespace SavefileManager.Views;

/// <summary>Small helpers so every message box and prompt follows the active theme.</summary>
public static class Dialogs
{
    private static Window? Pick(Window? owner) =>
        owner ?? Application.Current?.Windows.OfType<Window>().FirstOrDefault(w => w.IsActive)
              ?? Application.Current?.MainWindow;

    public static void Info(Window? owner, string title, string message)
    {
        var dlg = new MessageDialog(title, message, "OK", null);
        SetOwner(dlg, owner);
        dlg.ShowDialog();
    }

    public static bool Confirm(Window? owner, string title, string message,
        string yes = "Yes", string no = "No")
    {
        var dlg = new MessageDialog(title, message, yes, no);
        SetOwner(dlg, owner);
        return dlg.ShowDialog() == true;
    }

    /// <summary>Returns the entered text, or null when cancelled.</summary>
    public static string? Prompt(Window? owner, string title, string prompt, string initial = "")
    {
        var dlg = new InputDialog(title, prompt, initial);
        SetOwner(dlg, owner);
        return dlg.ShowDialog() == true ? dlg.Result : null;
    }

    private static void SetOwner(Window dlg, Window? owner)
    {
        var o = Pick(owner);
        if (o != null && o != dlg && o.IsLoaded) dlg.Owner = o;
        else dlg.WindowStartupLocation = WindowStartupLocation.CenterScreen;
    }
}
