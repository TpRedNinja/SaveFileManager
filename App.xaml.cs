using System.Windows;
using System.Windows.Threading;
using SavefileManager.Services;

namespace SavefileManager;

public partial class App : Application
{
    /// <summary>Settings, profiles and the config folder.</summary>
    public static SettingsService Store { get; private set; } = null!;

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        DispatcherUnhandledException += OnUnhandled;

        Store = new SettingsService();
        ImageService.Init(Store.ConfigDir);
        FontService.Init(System.IO.Path.Combine(Store.ConfigDir, "fonts"));
        AppearanceManager.Apply(Store.Data.Appearance);
        ThemeManager.Apply(ThemeManager.Resolve(Store.Data));

        var window = new MainWindow();
        MainWindow = window;
        window.Show();
    }

    private static void OnUnhandled(object sender, DispatcherUnhandledExceptionEventArgs e)
    {
        Log.Error("Unexpected error: " + e.Exception);
        MessageBox.Show("Unexpected error, check config/log.log for more info.\n\n" + e.Exception.Message,
            "CRITICAL ERROR", MessageBoxButton.OK, MessageBoxImage.Error);
        e.Handled = true;
    }
}
