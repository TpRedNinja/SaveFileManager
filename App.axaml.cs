using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using SavefileManager.Services;

namespace SavefileManager;

public partial class App : Application
{
    /// <summary>Settings, profiles and the config folder.</summary>
    public static SettingsService Store { get; private set; } = null!;

    public override void Initialize() => AvaloniaXamlLoader.Load(this);

    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            Store = new SettingsService();
            AppDomain.CurrentDomain.UnhandledException += (_, e) =>
                Log.Error("Unexpected error: " + e.ExceptionObject);
            TaskScheduler.UnobservedTaskException += (_, e) =>
                Log.Error("Unexpected error in a background task: " + e.Exception);

            ImageService.Init(Store.ConfigDir);
            FontService.Init(Path.Combine(Store.ConfigDir, "fonts"));
            AppearanceManager.Apply(Store.Data.Appearance);
            ThemeManager.Apply(ThemeManager.Resolve(Store.Data));

            desktop.MainWindow = new MainWindow();
        }
        base.OnFrameworkInitializationCompleted();
    }
}
