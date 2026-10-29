using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.Media;
using SavefileManager.Models;
using SavefileManager.Services;

namespace SavefileManager.Views;

/// <summary>Fonts (one for everything, or one per part) and optional pictures behind the two file lists.</summary>
public partial class AppearanceWindow : Window
{
    private readonly AppearanceSettings _original = new();
    private readonly AppearanceSettings _work = new();
    private readonly Dictionary<string, ComboBox> _elementCombos = new();
    private bool _loading = true;
    private bool _closingHandled;

    public bool Accepted { get; private set; }
    public AppearanceSettings Result => _work;

    public AppearanceWindow()
    {
        InitializeComponent();
    }

    public AppearanceWindow(AppearanceSettings current) : this()
    {
        ThemeManager.Attach(this);
        _original = current.Clone();
        _work = current.Clone();

        FontNote.Text = "Added fonts are copied into the config/fonts folder and installed for your user account only " +
                        "(nothing system-wide). A newly added font shows up in the lists after you restart the program.";

        BuildFontControls();
        BuildBackgroundControls();
        RefreshFontLists();

        AllRadio.IsChecked = !_work.Fonts.PerElement;
        EachRadio.IsChecked = _work.Fonts.PerElement;
        AllRadio.IsCheckedChanged += Mode_Changed;
        EachRadio.IsCheckedChanged += Mode_Changed;
        GlobalCombo.SelectionChanged += GlobalCombo_Changed;
        UpdateModeUi();
        Closing += Window_Closing;
        _loading = false;
    }

    // ------------------------------------------------------------------ fonts

    private void BuildFontControls()
    {
        foreach (var (key, label) in FontService.Elements)
        {
            var row = new DockPanel { Margin = new Thickness(0, 0, 0, 6) };
            row.Children.Add(new TextBlock
            {
                Text = label, Width = 150, TextWrapping = TextWrapping.Wrap,
                VerticalAlignment = VerticalAlignment.Center
            });
            var combo = new ComboBox { Tag = key, HorizontalAlignment = HorizontalAlignment.Stretch };
            combo.SelectionChanged += ElementCombo_Changed;
            row.Children.Add(combo);
            EachPanel.Children.Add(row);
            _elementCombos[key] = combo;
        }
    }

    private void RefreshFontLists()
    {
        bool was = _loading;
        _loading = true;
        var names = FontService.AllFontNames();

        void Fill(ComboBox box, string? wanted)
        {
            box.ItemsSource = null;
            box.ItemsSource = names;
            box.SelectedItem = names.FirstOrDefault(n => n.Equals(wanted, StringComparison.OrdinalIgnoreCase))
                               ?? names.FirstOrDefault(n => n == "Segoe UI")
                               ?? names.FirstOrDefault();
        }

        Fill(GlobalCombo, _work.Fonts.GlobalFont);
        foreach (var (key, combo) in _elementCombos)
        {
            _work.Fonts.Elements.TryGetValue(key, out var f);
            Fill(combo, string.IsNullOrWhiteSpace(f) ? _work.Fonts.GlobalFont : f);
        }
        _loading = was;
    }

    private void UpdateModeUi()
    {
        GlobalCombo.IsEnabled = AllRadio.IsChecked == true;
        EachPanel.IsEnabled = EachRadio.IsChecked == true;
        EachPanel.Opacity = EachPanel.IsEnabled ? 1.0 : 0.5;
    }

    private void Mode_Changed(object? sender, RoutedEventArgs e)
    {
        if (_loading) return;
        _work.Fonts.PerElement = EachRadio.IsChecked == true;
        UpdateModeUi();
        Preview();
    }

    private void GlobalCombo_Changed(object? sender, SelectionChangedEventArgs e)
    {
        if (_loading || GlobalCombo.SelectedItem is not string name) return;
        _work.Fonts.GlobalFont = name;
        Preview();
    }

    private void ElementCombo_Changed(object? sender, SelectionChangedEventArgs e)
    {
        if (_loading || sender is not ComboBox box || box.Tag is not string key || box.SelectedItem is not string name) return;
        _work.Fonts.Elements[key] = name;
        Preview();
    }

    private async void AddFont_Click(object? sender, RoutedEventArgs e)
    {
        var files = await Pickers.OpenFiles(this, "Choose font file(s)", "Font files",
            new[] { "*.ttf", "*.otf", "*.ttc", "*.TTF", "*.OTF", "*.TTC" }, true);
        if (files.Count == 0) return;

        string? last = null;
        bool needsRestart = false;
        var failed = new List<string>();
        foreach (var file in files)
        {
            var name = FontService.AddFontFile(file, out bool restart);
            if (name == null) failed.Add(Path.GetFileName(file));
            else { last = name; needsRestart |= restart; }
        }

        if (last != null)
        {
            RefreshFontLists();
            if (AllRadio.IsChecked == true && !needsRestart)
            {
                _work.Fonts.GlobalFont = last;
                GlobalCombo.SelectedItem = last;
            }
            Preview();
        }
        if (failed.Count > 0)
            await Dialogs.Info(this, "Couldn't load font", "These files aren't usable fonts:\n\n" + string.Join("\n", failed));
        if (last != null && needsRestart)
            await Dialogs.Info(this, "Restart needed",
                "The font was added. Restart the program once and it will appear in the font lists.");
    }

    // ------------------------------------------------------------------ backgrounds

    private void BuildBackgroundControls()
    {
        AddBackgroundSection("Your list (left)", _work.PersonalBackground);
        AddBackgroundSection("Game list (right)", _work.GameBackground);
    }

    private void AddBackgroundSection(string title, ImageSlot slot)
    {
        var box = new StackPanel { Margin = new Thickness(0, 6, 0, 10) };
        box.Children.Add(new TextBlock { Text = title, FontWeight = FontWeight.SemiBold, Margin = new Thickness(0, 0, 0, 4) });

        var buttons = new StackPanel { Orientation = Orientation.Horizontal };
        var choose = new Button { Content = "Choose picture..." };
        var remove = new Button { Content = "Remove", Margin = new Thickness(8, 0, 0, 0) };
        var label = new TextBlock { Margin = new Thickness(10, 0, 0, 0), VerticalAlignment = VerticalAlignment.Center, Opacity = 0.7 };
        buttons.Children.Add(choose);
        buttons.Children.Add(remove);
        buttons.Children.Add(label);
        box.Children.Add(buttons);

        var opacityRow = new DockPanel { Margin = new Thickness(0, 6, 0, 0) };
        opacityRow.Children.Add(new TextBlock { Text = "Opacity:", Width = 70, VerticalAlignment = VerticalAlignment.Center });
        var pct = new TextBlock { Width = 44, TextAlignment = TextAlignment.Right, VerticalAlignment = VerticalAlignment.Center };
        DockPanel.SetDock(pct, Dock.Right);
        var slider = new Slider { Minimum = 0.05, Maximum = 1, Value = slot.Opacity, VerticalAlignment = VerticalAlignment.Center };
        opacityRow.Children.Add(pct);
        opacityRow.Children.Add(slider);
        box.Children.Add(opacityRow);

        var fitRow = new DockPanel { Margin = new Thickness(0, 6, 0, 0) };
        fitRow.Children.Add(new TextBlock { Text = "Fit:", Width = 70, VerticalAlignment = VerticalAlignment.Center });
        var fit = new ComboBox { Width = 220, HorizontalAlignment = HorizontalAlignment.Left };
        var fitOptions = new[]
        {
            ("UniformToFill", "Fill (crop edges)"),
            ("Fill", "Stretch"),
            ("Uniform", "Fit inside (keep shape)"),
        };
        foreach (var (_, text) in fitOptions) fit.Items.Add(text);
        fit.SelectedIndex = Math.Max(0, Array.FindIndex(fitOptions, o => o.Item1 == slot.Stretch));
        fitRow.Children.Add(fit);
        box.Children.Add(fitRow);

        void Update()
        {
            bool has = !string.IsNullOrEmpty(slot.ImageFile);
            label.Text = has ? "Picture set" : "No picture";
            remove.IsEnabled = has;
            slider.IsEnabled = has;
            fit.IsEnabled = has;
            pct.Text = $"{(int)Math.Round(slot.Opacity * 100)}%";
        }

        choose.Click += async (_, _) =>
        {
            var path = await Pickers.OpenImage(this, "Choose a background picture");
            if (path == null) return;
            try { slot.ImageFile = ImageService.ImportImage(path, "backgrounds"); }
            catch (Exception ex) { await Dialogs.Info(this, "Couldn't use that picture", ex.Message); return; }
            Update();
            Preview();
        };
        remove.Click += (_, _) => { slot.ImageFile = null; Update(); Preview(); };
        slider.PropertyChanged += (_, e) =>
        {
            if (e.Property != RangeBase.ValueProperty) return;
            slot.Opacity = slider.Value;
            Update();
            Preview();
        };
        fit.SelectionChanged += (_, _) =>
        {
            if (fit.SelectedIndex >= 0) slot.Stretch = fitOptions[fit.SelectedIndex].Item1;
            Preview();
        };

        Update();
        BackgroundsPanel.Children.Add(box);
    }

    // ------------------------------------------------------------------ preview / buttons

    // Everything previews live on the real window; Cancel puts the original back.
    private void Preview()
    {
        if (_loading) return;
        AppearanceManager.Apply(_work);
    }

    private void Save_Click(object? sender, RoutedEventArgs e)
    {
        _closingHandled = true;
        Accepted = true;
        Close();
    }

    private void Cancel_Click(object? sender, RoutedEventArgs e)
    {
        Accepted = false;
        Close();
    }

    private void Window_Closing(object? sender, WindowClosingEventArgs e)
    {
        if (_closingHandled) return;
        AppearanceManager.Apply(_original);
    }
}
