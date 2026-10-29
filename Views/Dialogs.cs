using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;

namespace SavefileManager.Views;

/// <summary>Small helpers so every message box and prompt follows the active theme.</summary>
public static class Dialogs
{
    private static Window? Pick(Window? owner)
    {
        if (owner != null) return owner;
        if (Application.Current?.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime d)
            return d.Windows.FirstOrDefault(w => w.IsActive) ?? d.MainWindow;
        return null;
    }

    /// <summary>Shows a window as a dialog on top of its owner and returns when it is closed.</summary>
    public static async Task ShowModal(Window dlg, Window? owner)
    {
        var o = Pick(owner);
        if (o != null && o.IsVisible && !ReferenceEquals(o, dlg))
        {
            await dlg.ShowDialog(o);
            return;
        }
        // no visible window to attach to (e.g. very early at startup): show it on its own
        var done = new TaskCompletionSource();
        dlg.Closed += (_, _) => done.TrySetResult();
        dlg.WindowStartupLocation = WindowStartupLocation.CenterScreen;
        dlg.Show();
        await done.Task;
    }

    public static async Task Info(Window? owner, string title, string message)
    {
        var dlg = new MessageDialog(title, message, "OK", null);
        await ShowModal(dlg, owner);
    }

    public static async Task<bool> Confirm(Window? owner, string title, string message,
        string yes = "Yes", string no = "No")
    {
        var dlg = new MessageDialog(title, message, yes, no);
        await ShowModal(dlg, owner);
        return dlg.Accepted;
    }

    /// <summary>Returns the entered text, or null when cancelled.</summary>
    public static async Task<string?> Prompt(Window? owner, string title, string prompt, string initial = "")
    {
        var dlg = new InputDialog(title, prompt, initial);
        await ShowModal(dlg, owner);
        return dlg.Accepted ? dlg.Result : null;
    }
}
